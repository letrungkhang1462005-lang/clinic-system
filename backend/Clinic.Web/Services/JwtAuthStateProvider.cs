using System.Security.Claims;
using Clinic.Core.DTOs;
using Microsoft.AspNetCore.Components.Authorization;

namespace Clinic.Web.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly TokenStorage _storage;

    public JwtAuthStateProvider(TokenStorage storage)
    {
        _storage = storage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _storage.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) return Anonymous;

        var principal = JwtParser.TryCreatePrincipal(token);
        if (principal is null)
        {
            await _storage.RemoveTokenAsync(); // token hỏng hoặc hết hạn
            return Anonymous;
        }

        return new AuthenticationState(principal);
    }

    public async Task SignInAsync(AuthResponse response)
    {
        await _storage.SetTokenAsync(response.Token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await _storage.RemoveTokenAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
    }
}