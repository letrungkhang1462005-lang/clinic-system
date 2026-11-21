using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;

    public ChatController(IChatService chat)
    {
        _chat = chat;
    }

    [Authorize(Roles = RoleNames.Patient)]
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Ask(ChatRequest request)
        => Ok(await _chat.AskAsync(request));
}