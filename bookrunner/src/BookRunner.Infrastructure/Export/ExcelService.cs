using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookRunner.Infrastructure.Export;

/// <summary>
/// Runbook'lari Excel'e aktarir ve Excel'den gorev ice aktarimi yapar.
/// Ice aktarim once dogrulama modunda calistirilabilir (<c>commit = false</c>).
/// </summary>
public sealed class ExcelService(
    BookRunnerDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    ILogger<ExcelService> logger) : IExcelService
{
    /// <summary>Ice aktarim sablonundaki sutun basliklari (sira onemlidir).</summary>
    private static readonly string[] ImportHeaders =
    [
        "Sira", "Baslik", "Aciklama", "Oncelik", "Tahmini Sure (dk)",
        "Planlanan Baslangic", "Planlanan Bitis", "Renk (#RRGGBB)", "Geri Alma Notu"
    ];

    public async Task<byte[]> ExportRunbookAsync(Guid runbookId, CancellationToken ct = default)
    {
        var runbook = await db.Runbooks
            .AsNoTracking()
            .Include(r => r.Owner)
            .Include(r => r.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.User)
            .Include(r => r.Tasks).ThenInclude(t => t.Assignments).ThenInclude(a => a.Group)
            .Include(r => r.Tasks).ThenInclude(t => t.Comments).ThenInclude(c => c.Author)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == runbookId, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        using var workbook = new XLWorkbook();

        BuildOverviewSheet(workbook, runbook);
        BuildTaskSheet(workbook, runbook);
        BuildCommentSheet(workbook, runbook);

        var bytes = ToBytes(workbook);

        await audit.LogAsync(AuditAction.Export, nameof(Runbook), runbookId.ToString(),
            $"{runbook.Code} runbook'u Excel olarak disa aktarildi.", runbookId, ct: ct);

        logger.LogInformation("{Code} runbook'u Excel'e aktarildi ({User}).", runbook.Code, currentUser.UserName);

        return bytes;
    }

    public async Task<byte[]> ExportRunbookListAsync(RunbookFilter filter, CancellationToken ct = default)
    {
        var query = db.Runbooks.AsNoTracking().Include(r => r.Owner).AsQueryable();

        if (filter.IsTemplate.HasValue)
        {
            query = query.Where(r => r.IsTemplate == filter.IsTemplate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(r => EF.Functions.Like(r.Title, term) || EF.Functions.Like(r.Code, term));
        }

        if (filter.Statuses is { Length: > 0 })
        {
            query = query.Where(r => filter.Statuses.Contains(r.Status));
        }

        var runbooks = await query
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .Take(5000)
            .Select(r => new
            {
                r.Code,
                r.Title,
                r.Status,
                r.IsTemplate,
                r.TemplateCategory,
                r.PlannedStart,
                r.PlannedEnd,
                Owner = r.Owner.DisplayName,
                r.ServiceManagerWorkItemId,
                r.Tags,
                TaskCount = r.Tasks.Count,
                DoneCount = r.Tasks.Count(t => t.Status == RunbookTaskStatus.Completed),
                r.CreatedAt,
                r.CreatedBy
            })
            .ToListAsync(ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Runbook Listesi");

        string[] headers =
        [
            "Kod", "Baslik", "Durum", "Tur", "Kategori", "Planlanan Baslangic", "Planlanan Bitis",
            "Sahip", "SCSM Kayit", "Etiketler", "Gorev", "Tamamlanan", "Olusturma", "Olusturan"
        ];

        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var item in runbooks)
        {
            sheet.Cell(row, 1).Value = item.Code;
            sheet.Cell(row, 2).Value = item.Title;
            sheet.Cell(row, 3).Value = DisplayText.Status(item.Status);
            sheet.Cell(row, 4).Value = item.IsTemplate ? "Sablon" : "Runbook";
            sheet.Cell(row, 5).Value = item.TemplateCategory ?? "-";
            sheet.Cell(row, 6).Value = FormatDate(item.PlannedStart);
            sheet.Cell(row, 7).Value = FormatDate(item.PlannedEnd);
            sheet.Cell(row, 8).Value = item.Owner;
            sheet.Cell(row, 9).Value = item.ServiceManagerWorkItemId ?? "-";
            sheet.Cell(row, 10).Value = item.Tags ?? "-";
            sheet.Cell(row, 11).Value = item.TaskCount;
            sheet.Cell(row, 12).Value = item.DoneCount;
            sheet.Cell(row, 13).Value = FormatDate(item.CreatedAt);
            sheet.Cell(row, 14).Value = item.CreatedBy;
            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();

        await audit.LogAsync(AuditAction.Export, nameof(Runbook), null,
            $"Runbook listesi Excel'e aktarildi ({runbooks.Count} kayit).", ct: ct);

        return ToBytes(workbook);
    }

    public byte[] CreateImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Gorevler");

        WriteHeader(sheet, ImportHeaders);

        // Kullaniciya bicimi gosteren ornek satir.
        sheet.Cell(2, 1).Value = 1;
        sheet.Cell(2, 2).Value = "Veritabani yedegi al";
        sheet.Cell(2, 3).Value = "Gecis oncesi tam yedek alinir ve dogrulanir.";
        sheet.Cell(2, 4).Value = "Yuksek";
        sheet.Cell(2, 5).Value = 45;
        sheet.Cell(2, 6).Value = "01.01.2026 22:00";
        sheet.Cell(2, 7).Value = "01.01.2026 22:45";
        sheet.Cell(2, 8).Value = "#4F86F7";
        sheet.Cell(2, 9).Value = "Yedek dosyasi silinir.";
        sheet.Row(2).Style.Font.Italic = true;
        sheet.Row(2).Style.Font.FontColor = XLColor.Gray;

        var help = workbook.AddWorksheet("Aciklama");
        help.Cell(1, 1).Value = "Ice aktarim kurallari";
        help.Cell(1, 1).Style.Font.Bold = true;
        help.Cell(2, 1).Value = "- 'Gorevler' sayfasindaki ornek satiri silip kendi satirlarinizi girin.";
        help.Cell(3, 1).Value = "- Baslik zorunludur; bos baslikli satirlar atlanir.";
        help.Cell(4, 1).Value = "- Oncelik: Dusuk / Normal / Yuksek / Kritik";
        help.Cell(5, 1).Value = "- Tarih bicimi: gg.aa.yyyy SS:dd";
        help.Cell(6, 1).Value = "- Renk bos birakilirsa sira numarasina gore otomatik atanir.";
        help.Columns().AdjustToContents();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public async Task<ImportResult> ImportTasksAsync(Guid runbookId, Stream excelStream, bool commit, CancellationToken ct = default)
    {
        var runbook = await db.Runbooks.FirstOrDefaultAsync(r => r.Id == runbookId, ct)
            ?? throw new NotFoundException("Runbook", runbookId);

        using var workbook = new XLWorkbook(excelStream);
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Gorevler", StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Worksheets.First();

        var errors = new List<ImportError>();
        var parsed = new List<RunbookTask>();

        var maxOrder = await db.Tasks.Where(t => t.RunbookId == runbookId).MaxAsync(t => (int?)t.Order, ct) ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        var totalRows = Math.Max(0, lastRow - 1);

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = sheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var title = row.Cell(2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new ImportError(rowNumber, "Baslik", "Baslik bos olamaz."));
                continue;
            }

            if (title.Length > 250)
            {
                errors.Add(new ImportError(rowNumber, "Baslik", "Baslik en fazla 250 karakter olabilir."));
                continue;
            }

            var priorityText = row.Cell(4).GetString().Trim();
            if (!TryParsePriority(priorityText, out var priority))
            {
                errors.Add(new ImportError(rowNumber, "Oncelik",
                    $"'{priorityText}' gecerli bir oncelik degil (Dusuk/Normal/Yuksek/Kritik)."));
                continue;
            }

            int? estimated = null;
            var estimatedText = row.Cell(5).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(estimatedText))
            {
                if (!int.TryParse(estimatedText, out var minutes) || minutes < 0)
                {
                    errors.Add(new ImportError(rowNumber, "Tahmini Sure", "Sure pozitif bir tam sayi olmalidir."));
                    continue;
                }

                estimated = minutes;
            }

            if (!TryParseDate(row.Cell(6), out var plannedStart))
            {
                errors.Add(new ImportError(rowNumber, "Planlanan Baslangic", "Tarih okunamadi (gg.aa.yyyy SS:dd)."));
                continue;
            }

            if (!TryParseDate(row.Cell(7), out var plannedEnd))
            {
                errors.Add(new ImportError(rowNumber, "Planlanan Bitis", "Tarih okunamadi (gg.aa.yyyy SS:dd)."));
                continue;
            }

            if (plannedStart.HasValue && plannedEnd.HasValue && plannedEnd < plannedStart)
            {
                errors.Add(new ImportError(rowNumber, "Planlanan Bitis", "Bitis, baslangictan once olamaz."));
                continue;
            }

            // Gorev tarihi runbook'un planlanan araligini asamaz (bkz.
            // TaskService.ValidateTaskPlannedRange - ayni kural burada da uygulanir).
            if (plannedStart.HasValue || plannedEnd.HasValue)
            {
                if (runbook.PlannedStart is null || runbook.PlannedEnd is null)
                {
                    errors.Add(new ImportError(rowNumber, "Planlanan Baslangic",
                        "Gorev tarihi girebilmek icin once runbook'un planlanan tarihini girmelisiniz."));
                    continue;
                }

                if (plannedStart.HasValue && plannedStart.Value < runbook.PlannedStart.Value)
                {
                    errors.Add(new ImportError(rowNumber, "Planlanan Baslangic",
                        "Gorev baslangici runbook'un planlanan baslangicindan once olamaz."));
                    continue;
                }

                if (plannedEnd.HasValue && plannedEnd.Value > runbook.PlannedEnd.Value)
                {
                    errors.Add(new ImportError(rowNumber, "Planlanan Bitis",
                        "Gorev bitisi runbook'un planlanan bitisini asamaz."));
                    continue;
                }
            }

            var order = maxOrder + parsed.Count + 1;
            var color = row.Cell(8).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(color) && !IsHexColor(color))
            {
                errors.Add(new ImportError(rowNumber, "Renk", "Renk #RRGGBB formatinda olmalidir."));
                continue;
            }

            parsed.Add(new RunbookTask
            {
                RunbookId = runbookId,
                Order = order,
                Title = title,
                Description = NullIfEmpty(row.Cell(3).GetString()),
                Priority = priority,
                EstimatedMinutes = estimated,
                PlannedStart = plannedStart,
                PlannedEnd = plannedEnd,
                ColorHex = string.IsNullOrWhiteSpace(color) ? AvatarHelper.TaskColor(order) : color,
                RollbackNotes = NullIfEmpty(row.Cell(9).GetString())
            });
        }

        // Dogrulama modunda ya da hatali satir varsa hicbir sey yazilmaz: ya hep ya hic.
        var willCommit = commit && errors.Count == 0 && parsed.Count > 0;

        if (willCommit)
        {
            db.Tasks.AddRange(parsed);
            await db.SaveChangesAsync(ct);

            foreach (var task in parsed)
            {
                db.Activities.Add(new TaskActivity
                {
                    TaskId = task.Id,
                    Type = TaskActivityType.Created,
                    ActorUserId = currentUser.UserId,
                    ActorDisplayName = currentUser.DisplayName,
                    Summary = "Gorev Excel ice aktarimi ile olusturuldu."
                });
            }

            await db.SaveChangesAsync(ct);

            await audit.LogAsync(AuditAction.Import, nameof(Runbook), runbookId.ToString(),
                $"{runbook.Code} runbook'una Excel'den {parsed.Count} gorev aktarildi.", runbookId, ct: ct);
        }

        return new ImportResult
        {
            TotalRows = totalRows,
            ImportedRows = willCommit ? parsed.Count : 0,
            SkippedRows = totalRows - parsed.Count,
            Committed = willCommit,
            Errors = errors
        };
    }

    private static void BuildOverviewSheet(XLWorkbook workbook, Runbook runbook)
    {
        var sheet = workbook.AddWorksheet("Ozet");

        sheet.Cell(1, 1).Value = runbook.Code;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 16;

        sheet.Cell(2, 1).Value = runbook.Title;
        sheet.Cell(2, 1).Style.Font.FontSize = 13;

        var rows = new (string Label, string Value)[]
        {
            ("Durum", DisplayText.Status(runbook.Status)),
            ("Tur", runbook.IsTemplate ? "Sablon" : "Runbook"),
            ("Kategori", runbook.TemplateCategory ?? "-"),
            ("Sahip", runbook.Owner?.DisplayName ?? "-"),
            ("Planlanan Baslangic", FormatDate(runbook.PlannedStart)),
            ("Planlanan Bitis", FormatDate(runbook.PlannedEnd)),
            ("Gerceklesen Baslangic", FormatDate(runbook.ActualStart)),
            ("Gerceklesen Bitis", FormatDate(runbook.ActualEnd)),
            ("SCSM Kayit", runbook.ServiceManagerWorkItemId ?? "-"),
            ("Etiketler", runbook.Tags ?? "-"),
            ("Olusturan", runbook.CreatedBy),
            ("Olusturma", FormatDate(runbook.CreatedAt)),
            ("Toplam Gorev", runbook.Tasks.Count.ToString()),
            ("Tamamlanan", runbook.Tasks.Count(t => t.Status == RunbookTaskStatus.Completed).ToString())
        };

        var row = 4;
        foreach (var (label, value) in rows)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        sheet.Cell(row + 1, 1).Value = "Aciklama";
        sheet.Cell(row + 1, 1).Style.Font.Bold = true;
        sheet.Cell(row + 2, 1).Value = runbook.Description ?? "-";
        sheet.Cell(row + 2, 1).Style.Alignment.WrapText = true;

        sheet.Column(1).Width = 26;
        sheet.Column(2).Width = 80;
    }

    private static void BuildTaskSheet(XLWorkbook workbook, Runbook runbook)
    {
        var sheet = workbook.AddWorksheet("Gorevler");

        string[] headers =
        [
            "Sira", "Baslik", "Aciklama", "Durum", "Oncelik", "Atananlar", "Tahmini Sure (dk)",
            "Planlanan Baslangic", "Planlanan Bitis", "Gerceklesen Baslangic", "Gerceklesen Bitis",
            "Geri Alma Notu", "Yorum Sayisi"
        ];

        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var task in runbook.Tasks.OrderBy(t => t.Order))
        {
            var assignees = string.Join(", ", task.Assignments
                .Where(a => a.IsActive)
                .Select(a => a.AssigneeType == AssigneeType.User
                    ? a.User?.DisplayName ?? "-"
                    : $"{a.Group?.Name} (grup)"));

            sheet.Cell(row, 1).Value = task.Order;
            sheet.Cell(row, 2).Value = task.Title;
            sheet.Cell(row, 3).Value = task.Description ?? "-";
            sheet.Cell(row, 4).Value = DisplayText.Status(task.Status);
            sheet.Cell(row, 5).Value = DisplayText.Priority(task.Priority);
            sheet.Cell(row, 6).Value = string.IsNullOrWhiteSpace(assignees) ? "-" : assignees;
            sheet.Cell(row, 7).Value = task.EstimatedMinutes?.ToString() ?? "-";
            sheet.Cell(row, 8).Value = FormatDate(task.PlannedStart);
            sheet.Cell(row, 9).Value = FormatDate(task.PlannedEnd);
            sheet.Cell(row, 10).Value = FormatDate(task.ActualStart);
            sheet.Cell(row, 11).Value = FormatDate(task.ActualEnd);
            sheet.Cell(row, 12).Value = task.RollbackNotes ?? "-";
            sheet.Cell(row, 13).Value = task.Comments.Count(c => !c.IsDeleted);

            // Gorev rengi arayuzdeki bari temsil eder; Excel'de de ilk sutunda gosterilir.
            if (TryParseColor(task.ColorHex, out var color))
            {
                sheet.Cell(row, 1).Style.Fill.BackgroundColor = color;
                sheet.Cell(row, 1).Style.Font.FontColor = XLColor.White;
            }

            row++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.RangeUsed()?.SetAutoFilter();
        sheet.Columns().AdjustToContents();
        sheet.Column(3).Width = 60;
        sheet.Column(3).Style.Alignment.WrapText = true;
    }

    private static void BuildCommentSheet(XLWorkbook workbook, Runbook runbook)
    {
        var sheet = workbook.AddWorksheet("Yorumlar");
        WriteHeader(sheet, ["Gorev Sirasi", "Gorev", "Yazan", "Tarih", "Yorum"]);

        var row = 2;
        foreach (var task in runbook.Tasks.OrderBy(t => t.Order))
        {
            foreach (var comment in task.Comments.Where(c => !c.IsDeleted).OrderBy(c => c.CreatedAt))
            {
                sheet.Cell(row, 1).Value = task.Order;
                sheet.Cell(row, 2).Value = task.Title;
                sheet.Cell(row, 3).Value = comment.Author.DisplayName;
                sheet.Cell(row, 4).Value = FormatDate(comment.CreatedAt);
                sheet.Cell(row, 5).Value = comment.Body;
                row++;
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
        sheet.Column(5).Width = 80;
        sheet.Column(5).Style.Alignment.WrapText = true;
    }

    private static void WriteHeader(IXLWorksheet sheet, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2F5BD7");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static bool TryParsePriority(string? text, out TaskPriority priority)
    {
        priority = TaskPriority.Normal;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        switch (text.Trim().ToLowerInvariant())
        {
            case "dusuk":
            case "düşük":
            case "low":
                priority = TaskPriority.Low;
                return true;
            case "normal":
            case "orta":
                priority = TaskPriority.Normal;
                return true;
            case "yuksek":
            case "yüksek":
            case "high":
                priority = TaskPriority.High;
                return true;
            case "kritik":
            case "critical":
                priority = TaskPriority.Critical;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseDate(IXLCell cell, out DateTimeOffset? value)
    {
        value = null;

        if (cell.IsEmpty())
        {
            return true;
        }

        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dateValue))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(dateValue, DateTimeKind.Local));
            return true;
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string[] formats = ["dd.MM.yyyy HH:mm", "dd.MM.yyyy", "yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd"];
        if (DateTime.TryParseExact(text, formats, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
            return true;
        }

        return false;
    }

    private static bool IsHexColor(string value)
        => System.Text.RegularExpressions.Regex.IsMatch(value, "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$");

    private static bool TryParseColor(string? hex, out XLColor color)
    {
        color = XLColor.NoColor;
        if (string.IsNullOrWhiteSpace(hex) || !IsHexColor(hex))
        {
            return false;
        }

        try
        {
            color = XLColor.FromHtml(hex);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-";

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
