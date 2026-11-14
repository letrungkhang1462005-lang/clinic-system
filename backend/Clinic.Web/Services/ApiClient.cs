using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Clinic.Web.Services;

public sealed class Empty { }

public sealed class ApiResult<T>
{
    public bool Success { get; private init; }
    public T? Data { get; private init; }
    public int Status { get; private init; }
    public string Code { get; private init; } = string.Empty;
    public string Error { get; private init; } = string.Empty;

    public static ApiResult<T> Ok(T? data, int status = 200) =>
        new() { Success = true, Data = data, Status = status };

    public static ApiResult<T> Fail(int status, string code, string error) =>
        new() { Status = status, Code = code, Error = error };
}

public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TokenStorage _storage;
    private readonly JwtAuthStateProvider _auth;

    public ApiClient(HttpClient http, TokenStorage storage, JwtAuthStateProvider auth)
    {
        _http = http;
        _storage = storage;
        _auth = auth;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url, null);
    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<T>> PutAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Put, url, body);
    public Task<ApiResult<Empty>> DeleteAsync(string url) => SendAsync<Empty>(HttpMethod.Delete, url, null);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);

        var token = await _storage.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), null, Json);

        try
        {
            using var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var data = string.IsNullOrWhiteSpace(content)
                    ? default
                    : JsonSerializer.Deserialize<T>(content, Json);
                return ApiResult<T>.Ok(data, status);
            }

            // Đang có token mà bị 401 nghĩa là token hết hạn hoặc không hợp lệ: đăng xuất
            if (response.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(token))
                await _auth.SignOutAsync();

            var (code, message) = ParseError(status, content);
            return ApiResult<T>.Fail(status, code, message);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(0, "NETWORK",
                "Không kết nối được máy chủ. Hãy kiểm tra API đã chạy chưa.");
        }
    }

    // Backend trả lỗi nghiệp vụ dạng { status, code, message },
    // còn lỗi kiểm tra dữ liệu của ASP.NET trả dạng { errors: { Field: [..] } }
    private static (string Code, string Message) ParseError(int status, string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                var code = root.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                return (code, message.GetString()!);
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(x => x.GetString()))
                    .Where(s => !string.IsNullOrEmpty(s));
                return ("VALIDATION", string.Join(" ", messages));
            }
        }
        catch (JsonException)
        {
            // Phản hồi không phải JSON (ví dụ 401/403 rỗng): dùng thông báo mặc định bên dưới
        }

        return ("", status switch
        {
            401 => "Bạn cần đăng nhập để tiếp tục.",
            403 => "Bạn không có quyền thực hiện thao tác này.",
            404 => "Không tìm thấy dữ liệu.",
            _ => "Đã xảy ra lỗi. Vui lòng thử lại."
        });
    }
}