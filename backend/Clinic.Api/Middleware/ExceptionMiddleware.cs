using Clinic.Core.Exceptions;

namespace Clinic.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xử lý được");
            await WriteAsync(context, 500, "SERVER_ERROR", "Đã xảy ra lỗi hệ thống.");
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { status, code, message });
    }
}