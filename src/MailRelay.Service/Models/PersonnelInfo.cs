using System.Text.Json.Serialization;

namespace MailRelay.Service.Models;

// GET {BaseUrl}{LookupPathTemplate} cevabi. "thumbnail" alani (base64 foto) bilerek
// modele alinmadi - ne loglara ne de bellek onbellegine tasinir, gereksiz yuk olusturur.
public sealed class PersonnelInfo
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("teamName")]
    public string? TeamName { get; set; }
}

// GET {BaseUrl}{TeamsPath} dizisindeki her eleman.
public sealed class TeamInfo
{
    [JsonPropertyName("ekipAdi")]
    public string EkipAdi { get; set; } = "";

    [JsonPropertyName("yoneticiler")]
    public List<string> Yoneticiler { get; set; } = new();

    [JsonPropertyName("kadrolu")]
    public List<string> Kadrolu { get; set; } = new();

    [JsonPropertyName("danisman")]
    public List<string> Danisman { get; set; } = new();
}
