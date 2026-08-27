using BookRunner.Application.Dtos;

namespace BookRunner.Infrastructure.Scripting;

/// <summary>
/// CSX script'lerinin dogrudan erisebildigi degiskenler ve yardimci metotlar.
/// Script icinde <c>Runbook</c>, <c>Task</c>, <c>Log("...")</c> gibi isimler
/// bu sinifin uyeleri oldugu icin niteliksiz kullanilabilir.
/// </summary>
public sealed class ScriptGlobals(ScriptContext context, int maxOutputLines)
{
    private readonly List<string> _output = [];

    /// <summary>Script'in calistigi runbook/gorev baglami.</summary>
    public ScriptContext Context { get; } = context;

    /// <summary>Runbook kodu, orn. "RB-2026-0042".</summary>
    public string? RunbookCode => Context.RunbookCode;

    /// <summary>Runbook basligi.</summary>
    public string? RunbookTitle => Context.RunbookTitle;

    /// <summary>Baglamdaki gorevin basligi (varsa).</summary>
    public string? TaskTitle => Context.TaskTitle;

    /// <summary>Script'i calistiran Windows hesabi.</summary>
    public string ExecutedBy => Context.ExecutedBy;

    /// <summary>Calistirma sirasinda gecilen parametreler.</summary>
    public IReadOnlyDictionary<string, string> Parameters => Context.Parameters;

    /// <summary>Script ciktisina bir satir yazar; sonuc ekraninda ve audit'te gorunur.</summary>
    public void Log(string message)
    {
        if (_output.Count >= maxOutputLines)
        {
            return;
        }

        _output.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }

    /// <summary>Parametreyi okur; yoksa varsayilani doner.</summary>
    public string Param(string name, string defaultValue = "")
        => Parameters.TryGetValue(name, out var value) ? value : defaultValue;

    /// <summary>Toplanan cikti satirlari.</summary>
    public IReadOnlyList<string> Output => _output;
}
