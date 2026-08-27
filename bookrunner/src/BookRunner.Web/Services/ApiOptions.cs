namespace BookRunner.Web.Services;

/// <summary>Frontend'in cagirdigi REST API ayarlari (appsettings: "Api").</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>BookRunner.Api'nin taban adresi.</summary>
    public string BaseUrl { get; set; } = "https://localhost:7443";

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>SignalR hub adresi; bos ise BaseUrl uzerinden turetilir.</summary>
    public string? HubUrl { get; set; }
}
