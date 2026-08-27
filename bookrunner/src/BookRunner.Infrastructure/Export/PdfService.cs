using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookRunner.Infrastructure.Export;

/// <summary>
/// Runbook'u yazdirmaya uygun PDF'e cevirir. Gorevler arayuzdeki gibi kendi
/// renkli barlariyla, atananlar ve yorumlariyla birlikte basilir.
/// </summary>
public sealed class PdfService(BookRunnerDbContext db, IAuditService audit) : IPdfService
{
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

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Calibri).FontColor("#1F2933"));

                page.Header().Element(header => ComposeHeader(header, runbook));
                page.Content().Element(content => ComposeContent(content, runbook));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();

        await audit.LogAsync(AuditAction.Export, nameof(Runbook), runbookId.ToString(),
            $"{runbook.Code} runbook'u PDF olarak disa aktarildi.", runbookId, ct: ct);

        return bytes;
    }

    private static void ComposeHeader(IContainer container, Runbook runbook)
    {
        container.PaddingBottom(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(runbook.Code).FontSize(10).FontColor("#7B8794");
                    left.Item().Text(runbook.Title).FontSize(18).SemiBold();
                });

                row.ConstantItem(150).AlignRight().Column(right =>
                {
                    right.Item().Text(DisplayText.Status(runbook.Status)).FontSize(11).SemiBold().FontColor("#2F5BD7");
                    right.Item().Text(runbook.IsTemplate ? "Sablon" : "Runbook").FontColor("#7B8794");
                    right.Item().Text($"Yazdirma: {DateTimeOffset.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#7B8794");
                });
            });

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor("#D9E2EC");
        });
    }

    private static void ComposeContent(IContainer container, Runbook runbook)
    {
        container.PaddingVertical(8).Column(column =>
        {
            column.Spacing(10);

            column.Item().Element(element => ComposeSummary(element, runbook));

            if (!string.IsNullOrWhiteSpace(runbook.Description))
            {
                column.Item().Column(description =>
                {
                    description.Item().Text("Aciklama").SemiBold().FontSize(11);
                    description.Item().PaddingTop(2).Text(runbook.Description);
                });
            }

            column.Item().PaddingTop(4).Text($"Gorevler ({runbook.Tasks.Count})").SemiBold().FontSize(12);

            foreach (var task in runbook.Tasks.OrderBy(t => t.Order))
            {
                column.Item().Element(element => ComposeTask(element, task));
            }
        });
    }

    private static void ComposeSummary(IContainer container, Runbook runbook)
    {
        container.Background("#F5F7FA").Padding(8).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(110);
                columns.RelativeColumn();
                columns.ConstantColumn(110);
                columns.RelativeColumn();
            });

            void Row(string label1, string value1, string label2, string value2)
            {
                table.Cell().Text(label1).FontColor("#7B8794");
                table.Cell().Text(value1);
                table.Cell().Text(label2).FontColor("#7B8794");
                table.Cell().Text(value2);
            }

            Row("Sahip", runbook.Owner?.DisplayName ?? "-", "SCSM Kayit", runbook.ServiceManagerWorkItemId ?? "-");
            Row("Planlanan Baslangic", FormatDate(runbook.PlannedStart), "Planlanan Bitis", FormatDate(runbook.PlannedEnd));
            Row("Gerceklesen Baslangic", FormatDate(runbook.ActualStart), "Gerceklesen Bitis", FormatDate(runbook.ActualEnd));
            Row("Etiketler", runbook.Tags ?? "-", "Olusturan", runbook.CreatedBy);
        });
    }

    private static void ComposeTask(IContainer container, RunbookTask task)
    {
        container
            .BorderLeft(4)
            .BorderColor(task.ColorHex)
            .Background("#FFFFFF")
            .PaddingLeft(8)
            .PaddingVertical(6)
            .Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(text =>
                    {
                        text.Span($"{task.Order}. ").SemiBold().FontColor(task.ColorHex);
                        text.Span(task.Title).SemiBold().FontSize(11);
                    });

                    row.ConstantItem(120).AlignRight().Text(DisplayText.Status(task.Status)).FontColor("#334E68");
                });

                var assignees = string.Join(", ", task.Assignments
                    .Where(a => a.IsActive)
                    .Select(a => a.AssigneeType == AssigneeType.User
                        ? a.User?.DisplayName ?? "-"
                        : $"{a.Group?.Name} (grup)"));

                column.Item().PaddingTop(2).Text(text =>
                {
                    text.Span("Atanan: ").FontColor("#7B8794").FontSize(8);
                    text.Span(string.IsNullOrWhiteSpace(assignees) ? "-" : assignees).FontSize(8);
                    text.Span("   Oncelik: ").FontColor("#7B8794").FontSize(8);
                    text.Span(DisplayText.Priority(task.Priority)).FontSize(8);
                    text.Span("   Sure: ").FontColor("#7B8794").FontSize(8);
                    text.Span(task.EstimatedMinutes.HasValue ? $"{task.EstimatedMinutes} dk" : "-").FontSize(8);
                    text.Span("   Plan: ").FontColor("#7B8794").FontSize(8);
                    text.Span($"{FormatDate(task.PlannedStart)} - {FormatDate(task.PlannedEnd)}").FontSize(8);
                });

                if (!string.IsNullOrWhiteSpace(task.Description))
                {
                    column.Item().PaddingTop(3).Text(task.Description).FontSize(9);
                }

                if (!string.IsNullOrWhiteSpace(task.RollbackNotes))
                {
                    column.Item().PaddingTop(3).Text(text =>
                    {
                        text.Span("Geri alma: ").SemiBold().FontSize(8).FontColor("#C0504D");
                        text.Span(task.RollbackNotes).FontSize(8);
                    });
                }

                var comments = task.Comments.Where(c => !c.IsDeleted).OrderBy(c => c.CreatedAt).ToList();
                if (comments.Count > 0)
                {
                    column.Item().PaddingTop(4).PaddingLeft(6).Column(commentColumn =>
                    {
                        commentColumn.Item().Text($"Yorumlar ({comments.Count})").FontSize(8).SemiBold().FontColor("#7B8794");

                        foreach (var comment in comments)
                        {
                            commentColumn.Item().PaddingTop(2).Text(text =>
                            {
                                text.Span($"{comment.Author.DisplayName} ").SemiBold().FontSize(8);
                                text.Span($"({FormatDate(comment.CreatedAt)}): ").FontSize(7).FontColor("#7B8794");
                                text.Span(comment.Body).FontSize(8);
                            });
                        }
                    });
                }
            });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(8).FontColor("#7B8794"));
            text.Span("BookRunner | Sayfa ");
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-";
}
