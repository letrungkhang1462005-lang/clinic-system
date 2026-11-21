using Clinic.Core.DTOs;

namespace Clinic.Core.Interfaces;

public interface IChatService
{
    Task<ChatResponse> AskAsync(ChatRequest request);
}