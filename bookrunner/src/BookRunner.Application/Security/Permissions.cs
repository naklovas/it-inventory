using BookRunner.Domain.Enums;

namespace BookRunner.Application.Security;

/// <summary>
/// Uygulama izinleri. Kullanicilar AD gruplari uzerinden bir <see cref="AppRole"/>
/// alir, roller de burada tanimli izinlere sahiptir.
/// </summary>
public static class Permissions
{
    public const string RunbookRead = "runbook.read";
    /// <summary>
    /// Yeni bir runbook acabilme (bos veya sablondan). Mevcut bir runbook'u
    /// duzenlemek icin gereken RunbookWrite'tan bilerek ayridir: bir kisi
    /// kendi actigi runbook'u zaten sahiplik yoluyla (bkz. IRunbookAccess)
    /// duzenleyebilir/gorev ekleyebilir/atama yapabilir - RunbookWrite yalnizca
    /// BASKASININ runbook'unu da duzenleyebilme (yonetici/yazar) yetkisidir.
    /// </summary>
    public const string RunbookCreate = "runbook.create";
    public const string RunbookWrite = "runbook.write";
    public const string RunbookDelete = "runbook.delete";
    public const string RunbookPublishTemplate = "runbook.template.publish";
    public const string TaskWrite = "task.write";
    /// <summary>Gorev silme. Yalnizca yonetici rolunde; runbook sahibi de kendi gorevlerini silebilir.</summary>
    public const string TaskDelete = "task.delete";
    public const string TaskAssign = "task.assign";
    public const string TaskExecute = "task.execute";
    public const string TaskComment = "task.comment";
    public const string ExportData = "data.export";
    public const string ImportData = "data.import";
    public const string ScriptManage = "script.manage";
    public const string ScriptExecute = "script.execute";
    public const string AuditRead = "audit.read";
    public const string AdminManage = "admin.manage";

    private static readonly IReadOnlyDictionary<AppRole, string[]> RolePermissions =
        new Dictionary<AppRole, string[]>
        {
            [AppRole.Viewer] =
            [
                RunbookRead
            ],
            [AppRole.Contributor] =
            [
                RunbookRead, RunbookCreate, TaskExecute, TaskComment, ExportData
            ],
            [AppRole.RunbookAuthor] =
            [
                RunbookRead, RunbookCreate, RunbookWrite, RunbookPublishTemplate, TaskWrite, TaskAssign,
                TaskExecute, TaskComment, ExportData, ImportData, ScriptExecute
            ],
            [AppRole.Administrator] =
            [
                RunbookRead, RunbookCreate, RunbookWrite, RunbookDelete, RunbookPublishTemplate, TaskWrite,
                TaskDelete, TaskAssign, TaskExecute, TaskComment, ExportData, ImportData,
                ScriptManage, ScriptExecute, AuditRead, AdminManage
            ]
        };

    /// <summary>Verilen rolun sahip oldugu izinler.</summary>
    public static IReadOnlyList<string> ForRole(AppRole role)
        => RolePermissions.TryGetValue(role, out var permissions) ? permissions : Array.Empty<string>();

    public static bool Has(AppRole role, string permission)
        => ForRole(role).Contains(permission, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tum izin adlari (yetkilendirme politikalarini kaydetmek icin).</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        RunbookRead, RunbookCreate, RunbookWrite, RunbookDelete, RunbookPublishTemplate, TaskWrite, TaskDelete,
        TaskAssign, TaskExecute, TaskComment, ExportData, ImportData, ScriptManage, ScriptExecute,
        AuditRead, AdminManage
    ];

    /// <summary>Yetkilendirme sirasinda kullanilan ozel claim turu.</summary>
    public const string ClaimType = "bookrunner:permission";
}
