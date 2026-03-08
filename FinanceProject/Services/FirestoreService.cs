using System.Text.Json;
using Microsoft.JSInterop;

namespace FinanceProject.Services;

public class FirestoreService(IJSRuntime js)
{
    public async Task SaveAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        await js.InvokeVoidAsync("firebaseInterop.firestoreSave", key, json);
    }

    public async Task<T?> LoadAsync<T>(string key)
    {
        var json = await js.InvokeAsync<string?>("firebaseInterop.firestoreLoad", key);
        if (string.IsNullOrEmpty(json)) return default;
        return JsonSerializer.Deserialize<T>(json);
    }
}
