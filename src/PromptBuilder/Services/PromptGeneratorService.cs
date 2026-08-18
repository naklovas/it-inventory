using System.Text;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class PromptGeneratorService
{
    public string Generate(WizardModel model, List<WizardFieldDefinition> fields)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:");
        sb.AppendLine();

        AppendLine(sb, "Proje adı", model.ProjectName);

        foreach (var field in fields)
        {
            if (IsHidden(field, model)) continue;

            var value = field.FieldType == WizardFieldType.MultiSelect
                ? ResolveMulti(model, field.FieldKey)
                : ResolveSingle(model, field.FieldKey);

            AppendLine(sb, field.Label, value);
        }

        if (!string.IsNullOrWhiteSpace(model.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine("Ek notlar:");
            sb.AppendLine(model.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.");

        return sb.ToString();
    }

    private static bool IsHidden(WizardFieldDefinition field, WizardModel model)
    {
        if (field.ConditionalOnFieldKey is null) return false;
        var parentValue = model.SingleValues.GetValueOrDefault(field.ConditionalOnFieldKey, "");
        return parentValue == field.ConditionalHiddenValue;
    }

    private static string ResolveSingle(WizardModel model, string fieldKey)
    {
        var value = model.SingleValues.GetValueOrDefault(fieldKey, "");
        var other = model.OtherValues.GetValueOrDefault(fieldKey, "");
        return value == "Diğer" && !string.IsNullOrWhiteSpace(other) ? other : value;
    }

    private static string ResolveMulti(WizardModel model, string fieldKey)
    {
        var items = new List<string>(model.MultiValues.GetValueOrDefault(fieldKey, []));
        var other = model.OtherValues.GetValueOrDefault(fieldKey, "");
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
