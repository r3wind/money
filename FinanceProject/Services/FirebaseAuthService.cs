using Microsoft.JSInterop;

namespace FinanceProject.Services;

public class FirebaseAuthService(IJSRuntime js)
{
    public async Task<string?> SignInWithEmailAsync(string email, string password)
        => await js.InvokeAsync<string?>("firebaseInterop.signInWithEmail", email, password);

    public async Task<string?> RegisterWithEmailAsync(string email, string password)
        => await js.InvokeAsync<string?>("firebaseInterop.registerWithEmail", email, password);

    public async Task<string?> SignInWithGoogleAsync()
        => await js.InvokeAsync<string?>("firebaseInterop.signInWithGoogle");

    public async Task SignOutAsync()
        => await js.InvokeVoidAsync("firebaseInterop.signOut");
}
