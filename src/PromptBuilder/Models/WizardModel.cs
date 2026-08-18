namespace PromptBuilder.Models;

public class WizardModel
{
    public string ProjectName { get; set; } = "";

    public string AppType { get; set; } = "";
    public string AppTypeOther { get; set; } = "";

    public string Domain { get; set; } = "";
    public string DomainOther { get; set; } = "";

    public string Scale { get; set; } = "";

    public string DataLayer { get; set; } = "";

    public string AccessMethod { get; set; } = "";

    public string Auth { get; set; } = "";

    public string Architecture { get; set; } = "";

    public List<string> Features { get; set; } = [];
    public string FeaturesOther { get; set; } = "";

    public string UiStyle { get; set; } = "";

    public string DotnetVersion { get; set; } = "";

    public string TestExpectation { get; set; } = "";

    public string Deployment { get; set; } = "";

    public string BackendArchitecture { get; set; } = "";

    public string ApiDocs { get; set; } = "";

    public string Logging { get; set; } = "";

    public List<string> ExtraLibraries { get; set; } = [];
    public string ExtraLibrariesOther { get; set; } = "";

    public List<string> Languages { get; set; } = [];
    public string LanguagesOther { get; set; } = "";

    public string ScriptInterpreter { get; set; } = "";

    public string ExtraNotes { get; set; } = "";
}
