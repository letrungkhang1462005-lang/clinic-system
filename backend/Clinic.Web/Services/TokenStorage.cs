using Microsoft.JSInterop;

namespace Clinic.Web.Services;

// Lưu JWT trong localStorage của trình duyệt để F5 không mất đăng nhập
public class TokenStorage
{
    private const string Key = "clinic_token";
    private readonly IJSRuntime _js;

    public TokenStorage(IJSRuntime js)
    {
        _js = js;
    }

    public ValueTask<string?> GetTokenAsync() => _js.InvokeAsync<string?>("localStorage.getItem", Key);

    public ValueTask SetTokenAsync(string token) => _js.InvokeVoidAsync("localStorage.setItem", Key, token);

    public ValueTask RemoveTokenAsync() => _js.InvokeVoidAsync("localStorage.removeItem", Key);
}