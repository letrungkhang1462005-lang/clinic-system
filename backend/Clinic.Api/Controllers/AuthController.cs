using System.Security.Claims;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        => Ok(await _auth.RegisterAsync(request));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        => Ok(await _auth.LoginAsync(request));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> Me()
    {
        var userId = int.Parse(User.FindFirstValue("sub")!);
        return Ok(await _auth.GetMeAsync(userId));
    }
}