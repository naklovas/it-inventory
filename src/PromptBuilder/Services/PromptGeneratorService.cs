using System.Text;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class PromptGeneratorService
{
    public string Generate(WizardModel m)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:");
        sb.AppendLine();

        AppendLine(sb, "Proje adı", m.ProjectName);
        AppendLine(sb, "Uygulama tipi", Resolve(m.AppType, m.AppTypeOther));
        AppendLine(sb, "Amaç/domain", Resolve(m.Domain, m.DomainOther));
        AppendLine(sb, "Ölçek", m.Scale);
        AppendLine(sb, "Veri katmanı", m.DataLayer);
        if (m.DataLayer != "Yok (bellek içi)")
        {
            AppendLine(sb, "Veri erişim yöntemi", m.AccessMethod);
        }
        AppendLine(sb, "Kimlik doğrulama", m.Auth);
        AppendLine(sb, "Mimari", m.Architecture);
        AppendLine(sb, "Backend mimarisi", m.BackendArchitecture);
        if (m.BackendArchitecture != "Monolit (arayüzle tek proje)")
        {
            AppendLine(sb, "API dokümantasyonu", m.ApiDocs);
        }
        AppendLine(sb, "Temel özellikler", Resolve(m.Features, m.FeaturesOther));
        AppendLine(sb, "UI stili", m.UiStyle);
        AppendLine(sb, ".NET sürümü", m.DotnetVersion);
        AppendLine(sb, "Loglama", m.Logging);
        AppendLine(sb, "Test beklentisi", m.TestExpectation);
        AppendLine(sb, "Deployment", m.Deployment);
        AppendLine(sb, "Ek kütüphaneler", Resolve(m.ExtraLibraries, m.ExtraLibrariesOther));
        AppendLine(sb, "Kullanılacak diller", Resolve(m.Languages, m.LanguagesOther));
        if (m.ScriptInterpreter != "Yok")
        {
            AppendLine(sb, "Script/otomasyon interpreter'ı", m.ScriptInterpreter);
        }

        if (!string.IsNullOrWhiteSpace(m.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine("Ek notlar:");
            sb.AppendLine(m.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.");

        return sb.ToString();
    }

    private static string Resolve(string value, string other) =>
        value == "Diğer" && !string.IsNullOrWhiteSpace(other) ? other : value;

    private static string Resolve(List<string> values, string other)
    {
        var items = new List<string>(values);
        if (!string.IsNullOrWhiteSpace(other))
        {
            items.AddRange(other.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        return items.Count > 0 ? string.Join(", ", items) : "";
    }

    private static void AppendLine(StringBuilder sb, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.AppendLine($"- {label}: {value}");
    }
}
