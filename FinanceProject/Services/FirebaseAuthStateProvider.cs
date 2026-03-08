using System.Security.Claims;
using FinanceProject.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace FinanceProject.Services;

public class FirebaseAuthStateProvider(IJSRuntime js) : AuthenticationStateProvider, IAsyncDisposable
{
    private DotNetObjectReference<FirebaseAuthStateProvider>? _dotNetRef;
    private readonly TaskCompletionSource<bool> _firstAuthStateReady = new();
    private FirebaseUser? _currentUser;
    private bool _initialized;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            _initialized = true;
            _dotNetRef = DotNetObjectReference.Create(this);
            await js.InvokeVoidAsync("firebaseInterop.onAuthStateChanged", _dotNetRef);
        }

        await _firstAuthStateReady.Task;
        return BuildAuthState(_currentUser);
    }

    [JSInvokable]
    public void OnAuthStateChanged(FirebaseUser? user)
    {
        _currentUser = user;
        _firstAuthStateReady.TrySetResult(true);
        NotifyAuthenticationStateChanged(Task.FromResult(BuildAuthState(user)));
    }

    private static AuthenticationState BuildAuthState(FirebaseUser? user)
    {
        if (user is null)
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Uid),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email ?? "")
        };

        var identity = new ClaimsIdentity(claims, "Firebase");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
