using System.Text;
using PromptBuilder.Models;

namespace PromptBuilder.Services;

public class PromptGeneratorService
{
    public string Generate(WizardModel model, List<WizardFieldDefinition> fields)
    {
        var ui = UiStrings.For(model.Language);
        var sb = new StringBuilder();

        sb.AppendLine(ui.PromptIntro);
        sb.AppendLine();

        AppendLine(sb, ui.ProjectNameLabel, model.ProjectName);

        foreach (var field in fields)
        {
            if (IsHidden(field, model)) continue;

            var value = field.FieldType == WizardFieldType.MultiSelect
                ? ResolveMulti(model, field)
                : ResolveSingle(model, field);

            AppendLine(sb, field.Label(model.Language), value);
        }

        if (!string.IsNullOrWhiteSpace(model.ExtraNotes))
        {
            sb.AppendLine();
            sb.AppendLine(ui.ExtraNotesHeading);
            sb.AppendLine(model.ExtraNotes.Trim());
        }

        sb.AppendLine();
        sb.AppendLine(ui.PromptOutro);

        return sb.ToString();
    }

    private static bool IsHidden(WizardFieldDefinition field, WizardModel model)
    {
        if (field.ConditionalOnFieldKey is null) return false;
        var parentValue = model.SingleValues.GetValueOrDefault(field.ConditionalOnFieldKey, "");
        return parentValue == field.ConditionalHiddenValue;
    }

    private static string ResolveSingle(WizardModel model, WizardFieldDefinition field)
    {
        var value = model.SingleValues.GetValueOrDefault(field.FieldKey, "");
        if (value == WizardConstants.OtherSentinel)
        {
            return model.OtherValues.GetValueOrDefault(field.FieldKey, "");
        }

        var option = field.Options.FirstOrDefault(o => o.Tr == value);
        return option?.For(model.Language) ?? "";
    }

    private static string ResolveMulti(WizardModel model, WizardFieldDefinition field)
    {
        var selected = model.MultiValues.GetValueOrDefault(field.FieldKey, []);
        var notes = model.ItemNotes.GetValueOrDefault(field.FieldKey, new Dictionary<string, string>());
        var items = new List<string>();

        foreach (var value in selected)
        {
            var option = field.Options.FirstOrDefault(o => o.Tr == value);
            if (option is null) continue;

            var text = option.For(model.Language);
            if (notes.TryGetValue(value, out var note) && !string.IsNullOrWhiteSpace(note))
            {
                text += $" ({note.Trim()})";
            }
            items.Add(text);
        }

        var other = model.OtherValues.GetValueOrDefault(field.FieldKey, "");
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
