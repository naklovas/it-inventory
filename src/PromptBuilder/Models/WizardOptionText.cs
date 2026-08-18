namespace PromptBuilder.Models;

public class WizardOptionText
{
    public string Tr { get; set; } = "";
    public string En { get; set; } = "";

    public string For(UiLanguage lang) =>
        lang == UiLanguage.En && !string.IsNullOrWhiteSpace(En) ? En : Tr;
}
