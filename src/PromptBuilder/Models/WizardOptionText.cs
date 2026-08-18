namespace PromptBuilder.Models;

public class WizardOptionText
{
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";
    public string HelpTr { get; set; } = "";
    public string HelpEn { get; set; } = "";

    public string For(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(En) ? En : Tr;

    public string HelpFor(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(HelpEn) ? HelpEn : HelpTr;
}
