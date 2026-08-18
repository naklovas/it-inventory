namespace PromptBuilder.Models;

public enum WizardFieldType
{
    SingleSelect,
    MultiSelect
}

public class WizardFieldDefinition
{
    public string FieldKey { get; set; } = "";
    public string LabelTr { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public WizardFieldType FieldType { get; set; }
    public bool AllowOther { get; set; }
    public int SortOrder { get; set; }
    public string? ConditionalOnFieldKey { get; set; }
    public string? ConditionalHiddenValue { get; set; }
    public List<WizardOptionText> Options { get; set; } = [];

    public string Label(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(LabelEn) ? LabelEn : LabelTr;
}
