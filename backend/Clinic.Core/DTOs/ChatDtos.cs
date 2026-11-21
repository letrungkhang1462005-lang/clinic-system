using System.ComponentModel.DataAnnotations;

namespace Clinic.Core.DTOs;

public class ChatRequest
{
    [Required, MinLength(1), MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    // Mã phiên chat, dùng ở bước sau để nhớ ngữ cảnh hội thoại
    [MaxLength(64)]
    public string? SessionId { get; set; }
}

public record ChatSourceDto(string Title, string File, double Score);

// Mode: retrieval, no_answer, greeting, thanks, emergency, self_harm, medical_advice, triage
public record ChatResponse(string Answer, string Mode, List<ChatSourceDto> Sources);