using System.Net;
using System.Text;
using Clinic.Core.DTOs;
using Clinic.Core.Exceptions;
using Clinic.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.Tests;

public class AiChatServiceTests
{
    // Máy chủ giả: ghi lại nội dung đã gửi và trả về phản hồi do test quy định
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return respond(request);
        }
    }

    private static AiChatService Create(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") }, NullLogger<AiChatService>.Instance);

    private static ChatRequest Question(string text = "hi") => new() { Message = text, SessionId = "abc" };

    [Fact]
    public async Task Maps_the_python_response_and_sends_snake_case_session_id()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"answer\":\"Xin chào\",\"mode\":\"greeting\"," +
                "\"sources\":[{\"title\":\"Giờ làm việc\",\"file\":\"gio.md\",\"score\":0.5}]}",
                Encoding.UTF8, "application/json")
        });

        var result = await Create(handler).AskAsync(Question("  hi  "));

        Assert.Equal("Xin chào", result.Answer);
        Assert.Equal("greeting", result.Mode);
        Assert.Equal("Giờ làm việc", Assert.Single(result.Sources).Title);
        Assert.Contains("\"session_id\":\"abc\"", handler.LastBody);
        Assert.Contains("\"message\":\"hi\"", handler.LastBody); // đã cắt khoảng trắng thừa
    }

    [Fact]
    public async Task A_failing_ai_service_becomes_a_friendly_503()
    {
        var service = Create(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var ex = await Assert.ThrowsAsync<AppException>(() => service.AskAsync(Question()));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal("AI_UNAVAILABLE", ex.Code);
    }

    [Fact]
    public async Task A_network_failure_becomes_a_friendly_503()
    {
        var service = Create(new StubHandler(_ => throw new HttpRequestException("down")));

        var ex = await Assert.ThrowsAsync<AppException>(() => service.AskAsync(Question()));

        Assert.Equal(503, ex.StatusCode);
    }
}