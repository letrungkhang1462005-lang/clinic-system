using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Clinic.Web.Services;

public static class JwtParser
{
    // Chỉ ĐỌC nội dung token để hiển thị giao diện.
    // Việc kiểm tra chữ ký là của API ở mỗi request, trình duyệt không (và không thể) làm việc đó.
    public static ClaimsPrincipal? TryCreatePrincipal(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = doc.RootElement;

            // Token hết hạn thì coi như chưa đăng nhập
            if (root.TryGetProperty("exp", out var exp) &&
                DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) <= DateTimeOffset.UtcNow)
                return null;

            var claims = new List<Claim>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    claims.AddRange(prop.Value.EnumerateArray().Select(v => new Claim(prop.Name, v.ToString())));
                else
                    claims.Add(new Claim(prop.Name, prop.Value.ToString()));
            }

            // "name" và "role" là tên claim mà backend đã ghi vào token
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role"));
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}