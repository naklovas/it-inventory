namespace PromptBuilder.Models;

public class WizardModel
{
    public UiLanguage Language { get; set; } = UiLanguage.Tr;

    public string ProjectName { get; set; } = "";

    public Dictionary<string, string> SingleValues { get; set; } = new();
    public Dictionary<string, List<string>> MultiValues { get; set; } = new();
    public Dictionary<string, string> OtherValues { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> ItemNotes { get; set; } = new();

    public string ExtraNotes { get; set; } = "";
}
