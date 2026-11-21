using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clinic.Core.DTOs;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Clinic.Infrastructure.Services;

// Gọi dịch vụ AI (Python FastAPI). Mọi lỗi kết nối đều đổi thành một thông báo thân thiện,
// không để lộ chi tiết nội bộ ra người dùng.
public class AiChatService : IChatService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Định dạng dữ liệu trao đổi với dịch vụ Python (khớp ai-service/app/main.py)
    private sealed record AiRequest(
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("session_id")] string? SessionId);

    private sealed record AiSource(string Title, string File, double Score);

    private sealed record AiResponse(string Answer, string Mode, List<AiSource>? Sources);

    private readonly HttpClient _http;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(HttpClient http, ILogger<AiChatService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ChatResponse> AskAsync(ChatRequest request)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync(
                "chat", new AiRequest(request.Message.Trim(), request.SessionId), Json);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dịch vụ AI trả về mã {Status}", (int)response.StatusCode);
                throw Unavailable();
            }

            var body = await response.Content.ReadFromJsonAsync<AiResponse>(Json) ?? throw Unavailable();

            return new ChatResponse(
                body.Answer,
                body.Mode,
                (body.Sources ?? new List<AiSource>())
                    .Select(s => new ChatSourceDto(s.Title, s.File, s.Score))
                    .ToList());
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Không kết nối được dịch vụ AI");
            throw Unavailable();
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Dịch vụ AI phản hồi quá chậm");
            throw Unavailable();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Dịch vụ AI trả về dữ liệu không đúng định dạng");
            throw Unavailable();
        }
    }

    private static AppException Unavailable() => new(
        503, "AI_UNAVAILABLE", "Trợ lý ảo tạm thời chưa sẵn sàng. Vui lòng thử lại sau hoặc liên hệ lễ tân.");
}