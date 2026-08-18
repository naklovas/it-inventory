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
    public string ItemNotePlaceholder { get; init; } = "";

    public string PromptIntro { get; init; } = "";
    public string ExtraNotesHeading { get; init; } = "";
    public string PromptOutro { get; init; } = "";

    // Tabs
    public string TabGeneral { get; init; } = "";
    public string TabScreens { get; init; } = "";
    public string TabProcesses { get; init; } = "";

    // Screens tab
    public string ScreensIntro { get; init; } = "";
    public string ScreenNameLabel { get; init; } = "";
    public string ScreenNamePlaceholder { get; init; } = "";
    public string ScreenPurposeLabel { get; init; } = "";
    public string ScreenPurposePlaceholder { get; init; } = "";
    public string ScreenFieldsLabel { get; init; } = "";
    public string ScreenFieldsPlaceholder { get; init; } = "";
    public string ScreenActionsLabel { get; init; } = "";
    public string ScreenActionsPlaceholder { get; init; } = "";
    public string AddScreenButton { get; init; } = "";
    public string ScreensEmptyHint { get; init; } = "";

    // Processes tab
    public string ProcessesIntro { get; init; } = "";
    public string ProcessNameLabel { get; init; } = "";
    public string ProcessNamePlaceholder { get; init; } = "";
    public string ProcessDescriptionLabel { get; init; } = "";
    public string ProcessDescriptionPlaceholder { get; init; } = "";
    public string AddProcessButton { get; init; } = "";
    public string ProcessesEmptyHint { get; init; } = "";
    public string StepsLabel { get; init; } = "";
    public string StepDescriptionPlaceholder { get; init; } = "";
    public string StepActorPlaceholder { get; init; } = "";
    public string StepOutcomePlaceholder { get; init; } = "";
    public string AddStepButton { get; init; } = "";
    public string RemoveButton { get; init; } = "";

    // Prompt output headings for screens/processes
    public string ScreensHeading { get; init; } = "";
    public string ProcessesHeading { get; init; } = "";
    public string FieldsHeading { get; init; } = "";
    public string ActionsHeading { get; init; } = "";
    public string StepsHeading { get; init; } = "";
    public string OwnerLabel { get; init; } = "";
    public string OutcomeLabel { get; init; } = "";

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
        ItemNotePlaceholder = "Not ekleyin (opsiyonel)...",
        PromptIntro = "Aşağıdaki gereksinimlere uygun bir C# uygulaması geliştirmeni istiyorum:",
        ExtraNotesHeading = "Ek notlar:",
        PromptOutro = "Lütfen bu gereksinimlere uygun, iyi yapılandırılmış, best practice'lere uyan " +
                       "ve derlenebilir bir C# proje iskeleti oluştur. Varsayımların varsa belirt.",

        TabGeneral = "Genel",
        TabScreens = "Ekranlar",
        TabProcesses = "Süreçler",

        ScreensIntro = "Uygulamada olmasını istediğiniz her ekranı/sayfayı ayrı ayrı tanımlayın: adı, amacı, " +
                        "üzerinde hangi bilgilerin/alanların olacağı ve hangi aksiyonların (kaydet, sil, " +
                        "dışa aktar vb.) yapılabileceği.",
        ScreenNameLabel = "Ekran adı",
        ScreenNamePlaceholder = "Örn: Ürün Listesi",
        ScreenPurposeLabel = "Amaç",
        ScreenPurposePlaceholder = "Bu ekran ne için kullanılacak?",
        ScreenFieldsLabel = "Bu ekranda hangi bilgiler/alanlar olacak",
        ScreenFieldsPlaceholder = "Örn: Ürün adı, stok miktarı, fiyat, kategori",
        ScreenActionsLabel = "Aksiyonlar",
        ScreenActionsPlaceholder = "Örn: Yeni ekle, düzenle, sil, Excel'e aktar",
        AddScreenButton = "+ Ekran Ekle",
        ScreensEmptyHint = "Henüz ekran eklenmedi. Yukarıdaki butonla ekleyebilirsiniz.",

        ProcessesIntro = "Uygulamadaki iş süreçlerini adım adım tanımlayın: süreç kimlerden geçiyor " +
                          "(örn. onaycı var mı), her adımda ne oluyor ve süreç nasıl ilerliyor " +
                          "(onaylanırsa/reddedilirse ne olur).",
        ProcessNameLabel = "Süreç adı",
        ProcessNamePlaceholder = "Örn: Satın Alma Onay Süreci",
        ProcessDescriptionLabel = "Açıklama",
        ProcessDescriptionPlaceholder = "Bu süreç ne için var, kısaca özetleyin",
        AddProcessButton = "+ Süreç Ekle",
        ProcessesEmptyHint = "Henüz süreç eklenmedi. Yukarıdaki butonla ekleyebilirsiniz.",
        StepsLabel = "Adımlar",
        StepDescriptionPlaceholder = "Adımda ne oluyor? (örn: Yönetici talebi inceler)",
        StepActorPlaceholder = "Sorumlu/onaycı (örn: Departman Yöneticisi)",
        StepOutcomePlaceholder = "Sonuç/sonraki adım (örn: Onaylanırsa 3. adıma geç, reddedilirse talep sahibine bildirim)",
        AddStepButton = "+ Adım Ekle",
        RemoveButton = "Kaldır",

        ScreensHeading = "Ekranlar:",
        ProcessesHeading = "Süreçler:",
        FieldsHeading = "Alanlar",
        ActionsHeading = "Aksiyonlar",
        StepsHeading = "Adımlar",
        OwnerLabel = "Sorumlu",
        OutcomeLabel = "Sonuç",
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
        ItemNotePlaceholder = "Add a note (optional)...",
        PromptIntro = "I want you to build a C# application that meets the following requirements:",
        ExtraNotesHeading = "Additional notes:",
        PromptOutro = "Please produce a well-structured, best-practice C# project skeleton that meets " +
                       "these requirements and compiles. State any assumptions you make.",

        TabGeneral = "General",
        TabScreens = "Screens",
        TabProcesses = "Processes",

        ScreensIntro = "Define each screen/page you want in the app: its name, purpose, what information/" +
                        "fields it shows, and which actions (save, delete, export, etc.) are available on it.",
        ScreenNameLabel = "Screen name",
        ScreenNamePlaceholder = "e.g. Product List",
        ScreenPurposeLabel = "Purpose",
        ScreenPurposePlaceholder = "What is this screen for?",
        ScreenFieldsLabel = "What information/fields will be on this screen",
        ScreenFieldsPlaceholder = "e.g. Product name, stock quantity, price, category",
        ScreenActionsLabel = "Actions",
        ScreenActionsPlaceholder = "e.g. Add new, edit, delete, export to Excel",
        AddScreenButton = "+ Add Screen",
        ScreensEmptyHint = "No screens added yet. Use the button above to add one.",

        ProcessesIntro = "Define the app's business processes step by step: who the process goes through " +
                          "(e.g. is there an approver), what happens at each step, and how the process " +
                          "moves forward (what happens if approved/rejected).",
        ProcessNameLabel = "Process name",
        ProcessNamePlaceholder = "e.g. Purchase Approval Process",
        ProcessDescriptionLabel = "Description",
        ProcessDescriptionPlaceholder = "Briefly summarize what this process is for",
        AddProcessButton = "+ Add Process",
        ProcessesEmptyHint = "No processes added yet. Use the button above to add one.",
        StepsLabel = "Steps",
        StepDescriptionPlaceholder = "What happens in this step? (e.g. Manager reviews the request)",
        StepActorPlaceholder = "Owner/approver (e.g. Department Manager)",
        StepOutcomePlaceholder = "Outcome/next step (e.g. If approved go to step 3, if rejected notify the requester)",
        AddStepButton = "+ Add Step",
        RemoveButton = "Remove",

        ScreensHeading = "Screens:",
        ProcessesHeading = "Processes:",
        FieldsHeading = "Fields",
        ActionsHeading = "Actions",
        StepsHeading = "Steps",
        OwnerLabel = "Owner",
        OutcomeLabel = "Outcome",
    };

    public static UiStrings For(UiLanguage lang) => lang == UiLanguage.En ? En : Tr;
}
