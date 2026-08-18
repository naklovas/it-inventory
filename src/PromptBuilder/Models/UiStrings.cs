namespace PromptBuilder.Models;

public class UiStrings
{
    public string PageTitle { get; init; } = "";
    public string Intro { get; init; } = "";
    public string ProjectNameLabel { get; init; } = "";
    public string ProjectNamePlaceholder { get; init; } = "";
    public string ExtraNotesLabel { get; init; } = "";
    public string ExtraNotesPlaceholder { get; init; } = "";
    public string GenerateButton { get; init; } = "";
    public string LoadingText { get; init; } = "";
    public string LoadErrorPrefix { get; init; } = "";
    public string OutputHeader { get; init; } = "";
    public string CopyButton { get; init; } = "";
    public string OtherLabel { get; init; } = "";
    public string OtherPlaceholder { get; init; } = "";
    public string OtherPlaceholderMulti { get; init; } = "";

    public string PromptIntro { get; init; } = "";
    public string ExtraNotesHeading { get; init; } = "";
    public string PromptOutro { get; init; } = "";

    public static readonly UiStrings Tr = new()
    {
        PageTitle = "C# Uygulama Prompt Builder",
        Intro = "Alanları seçin, en altta hazır bir prompt oluşturulacak. Sorular SQL Server'daki " +
                "dbo.WizardField / dbo.WizardOption tablolarından geliyor.",
        ProjectNameLabel = "Proje adı",
        ProjectNamePlaceholder = "Örn: StokTakip",
        ExtraNotesLabel = "Ek notlar (opsiyonel)",
        ExtraNotesPlaceholder = "Yukarıdaki alanlara sığmayan özel istekler...",
        GenerateButton = "Prompt Oluştur",
        LoadingText = "Yükleniyor...",
        LoadErrorPrefix = "Alanlar veritabanından yüklenemedi:",
        OutputHeader = "Oluşan Prompt",
        CopyButton = "Kopyala",
        OtherLabel = "Diğer",
        OtherPlaceholder = "Belirtin...",
        OtherPlaceholderMulti = "Diğer (virgülle ayırın)...",
        PromptIntro = "Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:",
        ExtraNotesHeading = "Ek notlar:",
        PromptOutro = "Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.",
    };

    public static readonly UiStrings En = new()
    {
        PageTitle = "C# App Prompt Builder",
        Intro = "Pick the fields below; a ready-to-use prompt will be generated at the bottom. Questions " +
                "come from the dbo.WizardField / dbo.WizardOption tables in SQL Server.",
        ProjectNameLabel = "Project name",
        ProjectNamePlaceholder = "e.g. StockTracker",
        ExtraNotesLabel = "Additional notes (optional)",
        ExtraNotesPlaceholder = "Any special requests not covered above...",
        GenerateButton = "Generate Prompt",
        LoadingText = "Loading...",
        LoadErrorPrefix = "Failed to load fields from the database:",
        OutputHeader = "Generated Prompt",
        CopyButton = "Copy",
        OtherLabel = "Other",
        OtherPlaceholder = "Please specify...",
        OtherPlaceholderMulti = "Other (comma-separated)...",
        PromptIntro = "I want you to build a C# application that meets the following requirements:",
        ExtraNotesHeading = "Additional notes:",
        PromptOutro = "Please produce a well-structured, best-practice C# project skeleton that meets " +
                       "these requirements and compiles. State any assumptions you make.",
    };

    public static UiStrings For(UiLanguage lang) => lang == UiLanguage.En ? En : Tr;
}
