namespace BookRunner.Infrastructure.Scripting;

/// <summary>Roslyn (CSX) calistirici ayarlari (appsettings: "Scripting").</summary>
public sealed class ScriptingOptions
{
    public const string SectionName = "Scripting";

    /// <summary>false ise script calistirma girisimleri reddedilir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Varsayilan zaman asimi (saniye).</summary>
    public int DefaultTimeoutSeconds { get; set; } = 60;

    /// <summary>Script'lere acilan ad alanlari.</summary>
    public string[] Imports { get; set; } =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Threading.Tasks"
    ];

    /// <summary>
    /// Script kaynak kodunda yasakli anahtar sozcukler. Roslyn script'leri tam
    /// guven altinda calistigi icin bu liste tek basina bir guvenlik siniri
    /// degildir; script yazma yetkisi yalnizca yoneticilerde olmalidir.
    /// </summary>
    public string[] BlockedPatterns { get; set; } =
    [
        "System.IO.File.Delete",
        "System.Diagnostics.Process",
        "System.Reflection.Assembly.Load",
        "Environment.Exit"
    ];

    /// <summary>Script ciktisinda saklanacak en fazla satir.</summary>
    public int MaxOutputLines { get; set; } = 500;
}
