using System.Text.Json.Serialization;

namespace FinanceProject.Models;

public class FirebaseUser
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
