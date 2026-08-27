namespace BookRunner.Infrastructure.ServiceManager;

/// <summary>
/// System Center Service Manager veritabanina salt-okunur erisim ayarlari
/// (appsettings: "ServiceManager").
/// </summary>
public sealed class ServiceManagerOptions
{
    public const string SectionName = "ServiceManager";

    /// <summary>false ise SCSM entegrasyonu devre disidir ve sorgular bos doner.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// SCSM Data Warehouse baglanti dizesi. Salt-okunur bir hesap kullanin
    /// (Integrated Security onerilir; bkz. sql/03_ServiceManager_ReadOnly.sql).
    /// Bos birakilirsa <c>ConnectionStrings:ServiceManager</c> degeri kullanilir.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Sorgu zaman asimi (saniye).</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Arama sorgusu. SCSM surumune/ozellestirmesine gore degistirilebilir.
    /// Beklenen sutunlar: Id, Title, Description, Status, Category, AssignedTo,
    /// CreatedBy, CreatedDate, ScheduledStartDate, ScheduledEndDate, WorkItemType.
    /// Parametreler: @term, @take
    /// </summary>
    public string SearchQuery { get; set; } = DefaultSearchQuery;

    /// <summary>Tek kayit sorgusu. Parametre: @id</summary>
    public string GetByIdQuery { get; set; } = DefaultGetByIdQuery;

    /// <summary>
    /// SCSM DW'deki degisiklik kayitlarini okuyan varsayilan sorgu.
    /// Ortaminizdaki gorunum adlari farkliysa yapilandirmadan degistirin.
    /// </summary>
    public const string DefaultSearchQuery = """
        SELECT TOP (@take)
            cr.Id                       AS Id,
            cr.Title                    AS Title,
            cr.Description              AS Description,
            status.DisplayName          AS Status,
            area.DisplayName            AS Category,
            NULL                        AS AssignedTo,
            cr.CreatedBy                AS CreatedBy,
            cr.CreatedDate              AS CreatedDate,
            cr.ScheduledStartDate       AS ScheduledStartDate,
            cr.ScheduledEndDate         AS ScheduledEndDate,
            'ChangeRequest'             AS WorkItemType
        FROM dbo.ChangeRequestDimvw AS cr
        LEFT JOIN dbo.ChangeStatusvw   AS status ON status.ChangeStatusId = cr.Status_ChangeStatusId
        LEFT JOIN dbo.ChangeAreavw     AS area   ON area.ChangeAreaId = cr.Area_ChangeAreaId
        WHERE cr.Id LIKE '%' + @term + '%' OR cr.Title LIKE '%' + @term + '%'
        ORDER BY cr.CreatedDate DESC;
        """;

    public const string DefaultGetByIdQuery = """
        SELECT TOP (1)
            cr.Id                       AS Id,
            cr.Title                    AS Title,
            cr.Description              AS Description,
            status.DisplayName          AS Status,
            area.DisplayName            AS Category,
            NULL                        AS AssignedTo,
            cr.CreatedBy                AS CreatedBy,
            cr.CreatedDate              AS CreatedDate,
            cr.ScheduledStartDate       AS ScheduledStartDate,
            cr.ScheduledEndDate         AS ScheduledEndDate,
            'ChangeRequest'             AS WorkItemType
        FROM dbo.ChangeRequestDimvw AS cr
        LEFT JOIN dbo.ChangeStatusvw   AS status ON status.ChangeStatusId = cr.Status_ChangeStatusId
        LEFT JOIN dbo.ChangeAreavw     AS area   ON area.ChangeAreaId = cr.Area_ChangeAreaId
        WHERE cr.Id = @id;
        """;
}
