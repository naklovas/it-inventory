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
            .Include(r => r.Scenarios)
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

            column.Item().PaddingTop(4).Text("Planlanan Runbook").SemiBold().FontSize(14);
            column.Item().Element(element => ComposePlannedRunbook(element, runbook));

            column.Item().PageBreak();
            column.Item().Text("Gerceklesen Runbook").SemiBold().FontSize(14);
            column.Item().PaddingBottom(2).Text(
                "Adimlar, gercek baslama zamanlarina gore hangi sirayla yapildigini gosterecek sekilde siralanmistir.")
                .FontSize(8).FontColor("#7B8794");
            column.Item().Element(element => ComposeActualRunbook(element, runbook));

            column.Item().PageBreak();
            column.Item().Element(element => ComposeFlowchart(element, runbook));
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

    /// <summary>
    /// Runbook'un PLANLANAN halini gosterir: ana gorevler, her senaryo plani ve
    /// geri donus plani gorevleri ayri alt basliklar altinda gruplanir. Onceden
    /// hepsi tek bir listede (Order alanina gore) siralaniyordu - ama Order her
    /// track (ana akis/geri donus/her senaryo) icin AYRI AYRI 1'den basladigindan
    /// bu, uc turun gorevlerini birbirine karistiriyordu.
    /// </summary>
    private static void ComposePlannedRunbook(IContainer container, Runbook runbook)
    {
        var allTasks = runbook.Tasks.ToList();
        var mainTasks = allTasks.Where(t => !t.IsRollbackStep && string.IsNullOrEmpty(t.ScenarioGroup))
            .OrderBy(t => t.Order).ToList();
        var rollbackSteps = allTasks.Where(t => t.IsRollbackStep).OrderBy(t => t.Order).ToList();
        var scenarioGroupNames = allTasks
            .Where(t => !t.IsRollbackStep && !string.IsNullOrEmpty(t.ScenarioGroup))
            .Select(t => t.ScenarioGroup!)
            .Distinct()
            .OrderBy(name => allTasks.Where(t => t.ScenarioGroup == name).Min(t => t.Order))
            .ToList();

        container.Column(column =>
        {
            column.Spacing(6);

            column.Item().Text($"Ana Gorevler ({mainTasks.Count})").SemiBold().FontSize(12);
            foreach (var task in mainTasks)
            {
                column.Item().Element(element => ComposeTask(element, task));
            }

            foreach (var groupName in scenarioGroupNames)
            {
                var steps = allTasks.Where(t => !t.IsRollbackStep && t.ScenarioGroup == groupName)
                    .OrderBy(t => t.Order).ToList();
                column.Item().PaddingTop(6).Text($"Senaryo Plani: {groupName} ({steps.Count})")
                    .SemiBold().FontSize(12).FontColor("#8BC34A");
                foreach (var task in steps)
                {
                    column.Item().Element(element => ComposeTask(element, task));
                }
            }

            if (rollbackSteps.Count > 0)
            {
                column.Item().PaddingTop(6).Text($"Geri Donus Plani Gorevleri ({rollbackSteps.Count})")
                    .SemiBold().FontSize(12).FontColor("#9C6ADE");
                foreach (var task in rollbackSteps)
                {
                    column.Item().Element(element => ComposeTask(element, task));
                }
            }
        });
    }

    /// <summary>
    /// Runbook'un GERCEKLESEN halini gosterir: yalnizca fiilen baslatilmis
    /// adimlar, gercek baslama zamanina gore tek bir kronolojik sirada -
    /// ana akis/senaryo/geri donus ayrimi yapilmadan, calisma sirasinda
    /// gercekte hangi adimin hangisinden once/sonra yapildigini gosterir.
    /// </summary>
    private static void ComposeActualRunbook(IContainer container, Runbook runbook)
    {
        var executedTasks = runbook.Tasks
            .Where(t => t.ActualStart.HasValue)
            .OrderBy(t => t.ActualStart!.Value)
            .ThenBy(t => t.ActualEnd ?? DateTimeOffset.MaxValue)
            .ToList();

        container.Column(column =>
        {
            column.Spacing(6);

            if (executedTasks.Count == 0)
            {
                column.Item().Text("Henuz hicbir adim baslatilmadi.").FontColor("#7B8794").Italic();
                return;
            }

            for (var i = 0; i < executedTasks.Count; i++)
            {
                column.Item().Element(element => ComposeActualTask(element, executedTasks[i], i + 1));
            }
        });
    }

    /// <summary>Bir gorevin hangi track'e (ana akis/senaryo/geri donus) ait oldugunu kisa bir etiket olarak dondurur.</summary>
    private static string TrackLabel(RunbookTask task)
        => task.IsRollbackStep ? "Geri Donus Plani"
            : string.IsNullOrEmpty(task.ScenarioGroup) ? "Ana Akis"
            : $"Senaryo: {task.ScenarioGroup}";

    private static void ComposeActualTask(IContainer container, RunbookTask task, int sequenceNumber)
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
                        text.Span($"{sequenceNumber}. ").SemiBold().FontColor(task.ColorHex);
                        text.Span(task.Title).SemiBold().FontSize(11);
                        text.Span($"  [{TrackLabel(task)}]").FontSize(8).FontColor("#7B8794");
                    });

                    row.ConstantItem(120).AlignRight().Text(DisplayText.Status(task.Status)).FontColor("#334E68");
                });

                var (assigneeNames, _, _) = AssigneeSummary(task);

                column.Item().PaddingTop(2).Text(text =>
                {
                    text.Span("Atanan: ").FontColor("#7B8794").FontSize(8);
                    text.Span(assigneeNames).FontSize(8);
                    text.Span("   Gerceklesen: ").FontColor("#7B8794").FontSize(8);
                    text.Span($"{FormatDate(task.ActualStart)} - {FormatDate(task.ActualEnd)}").FontSize(8);
                });
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

    /// <summary>
    /// Ana akisi, senaryo dallanmalarini ve geri donus planini sirali kutular
    /// halinde cizer - arayuzdeki "Akis Semasi" butonunun (mermaid tabanli)
    /// PDF karsiligi. Ayni grafigi cizmek yerine (PDF'te QuestPDF Canvas API'si
    /// dusuk seviyeli oldugundan) okunmasi kolay, dikey siralanmis kutu+ok
    /// deseni kullanilir.
    /// </summary>
    private static void ComposeFlowchart(IContainer container, Runbook runbook)
    {
        var allTasks = runbook.Tasks.ToList();
        var mainTasks = allTasks.Where(t => !t.IsRollbackStep && string.IsNullOrEmpty(t.ScenarioGroup))
            .OrderBy(t => t.Order).ToList();
        var rollbackSteps = allTasks.Where(t => t.IsRollbackStep).OrderBy(t => t.Order).ToList();

        var scenarioGroupNames = allTasks
            .Where(t => !t.IsRollbackStep && !string.IsNullOrEmpty(t.ScenarioGroup))
            .Select(t => t.ScenarioGroup!)
            .Distinct()
            .OrderBy(name => allTasks.Where(t => t.ScenarioGroup == name).Min(t => t.Order))
            .ToList();

        container.Column(column =>
        {
            column.Item().Text("Akis Semasi").SemiBold().FontSize(14);
            column.Item().PaddingBottom(4).Text(
                "Ana akis, senaryo dallanmalari ve geri donus plani asagida sirayla gosterilir.")
                .FontSize(8).FontColor("#7B8794");

            if (mainTasks.Count > 0)
            {
                ComposeFlowChain(column, "Ana Akis", "#4F86F7", mainTasks, null, allTasks);
            }

            foreach (var groupName in scenarioGroupNames)
            {
                var steps = allTasks.Where(t => !t.IsRollbackStep && t.ScenarioGroup == groupName)
                    .OrderBy(t => t.Order).ToList();
                // Rejoin hedefi oncelikle ayri Scenario varligindan (yeni "Yeni
                // Senaryo Olustur" akisi) okunur; orada yoksa (eski/gecis donemi
                // runbook'lari) gorev uzerindeki ScenarioRejoinTaskId'ye geri duser.
                var rejoinTaskId = runbook.Scenarios.FirstOrDefault(s => s.Name == groupName)?.RejoinTaskId
                    ?? steps.Select(t => t.ScenarioRejoinTaskId).FirstOrDefault(id => id.HasValue);
                var rejoinTask = rejoinTaskId.HasValue ? allTasks.FirstOrDefault(t => t.Id == rejoinTaskId.Value) : null;
                var title = "Senaryo: " + groupName + (runbook.ActiveScenarioGroup == groupName ? " (AKTIF)" : "");
                var footNote = rejoinTask is not null
                    ? $"Tamamlaninca ana akista '{rejoinTask.Title}' gorevinden devam eder."
                    : "Tek yonlu: tum adimlari tamamlaninca calisma burada sona erer.";
                // Senaryonun TUMU basarisiz sayilma durumu: bir senaryo adiminin
                // kendi FailureAction'i yoktur, yalnizca senaryonun kendisi boyle
                // bir eylem tasiyabilir (bkz. Scenario.FailureAction).
                if (runbook.Scenarios.FirstOrDefault(s => s.Name == groupName)?.FailureAction == TaskFailureAction.StartRollback)
                {
                    footNote += " Herhangi bir adimi basarisiz olursa geri donus plani otomatik baslar.";
                }
                ComposeFlowChain(column, title, "#8BC34A", steps, footNote, allTasks);
            }

            if (rollbackSteps.Count > 0)
            {
                var title = "Geri Donus Plani" + (runbook.IsRollbackActive ? " (AKTIF)" : "");
                ComposeFlowChain(column, title, "#9C6ADE", rollbackSteps, null, allTasks);
            }
        });
    }

    private static void ComposeFlowChain(
        ColumnDescriptor column, string title, string accentColor, List<RunbookTask> steps, string? footNote,
        List<RunbookTask> allTasks)
    {
        column.Item().PaddingTop(10).Row(row =>
        {
            row.ConstantItem(10).Height(10).AlignMiddle().Background(accentColor).CornerRadius(5);
            row.RelativeItem().PaddingLeft(6).Text(title).SemiBold().FontSize(11).FontColor(accentColor);
        });

        for (var i = 0; i < steps.Count; i++)
        {
            column.Item().Element(element => ComposeFlowNode(element, steps[i], accentColor, allTasks));
            if (i < steps.Count - 1)
            {
                ComposeFlowArrow(column, accentColor);
            }
        }

        if (!string.IsNullOrEmpty(footNote))
        {
            column.Item().PaddingTop(4).Element(element => ComposeFlowLabel(element, footNote, "#7B8794", "#F5F7FA"));
        }
    }

    /// <summary>Iki kutu arasindaki bagi, dikey bir cizgi + ok ucu olarak cizer (mermaid'deki dikey oklarin PDF karsiligi).</summary>
    private static void ComposeFlowArrow(ColumnDescriptor column, string accentColor)
    {
        column.Item().AlignCenter().Column(arrow =>
        {
            arrow.Item().AlignCenter().Width(2).Height(10).Background(accentColor);
            // Not: "▼" (U+25BC) bazi font/ortamlarda eksik glif (bos kutu)
            // olarak cizildi - "↓" (U+2193) daha genis desteklendigi icin
            // tercih edildi.
            arrow.Item().AlignCenter().Text("↓").FontSize(13).FontColor(accentColor);
        });
    }

    /// <summary>Bir dallanma/tetikleme notunu, mermaid'deki ok etiketlerine benzer kucuk bir rozet olarak cizer.</summary>
    private static void ComposeFlowLabel(IContainer container, string text, string textColor, string backgroundColor)
    {
        container.Background(backgroundColor).CornerRadius(4).PaddingVertical(3).PaddingHorizontal(6)
            .Text(text).FontSize(8).Italic().FontColor(textColor);
    }

    private static void ComposeFlowNode(IContainer container, RunbookTask task, string accentColor, List<RunbookTask> allTasks)
    {
        var (name, photo, _) = AssigneeSummary(task);
        var statusColor = StatusColor(task.Status);

        container.Border(1).BorderColor("#D9E2EC").Background("#FFFFFF").CornerRadius(6).Padding(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(22).Height(22).Background(accentColor).CornerRadius(11).AlignMiddle().AlignCenter()
                    .Text(task.Order.ToString()).FontSize(10).SemiBold().FontColor(Colors.White);

                row.RelativeItem().PaddingLeft(8).AlignMiddle().Text(task.Title).SemiBold().FontSize(10.5f);

                row.ConstantItem(85).AlignMiddle().AlignRight()
                    .Background(WithOpacity(statusColor, 0.15f)).CornerRadius(3).PaddingVertical(2).PaddingHorizontal(5)
                    .Text(DisplayText.Status(task.Status)).FontSize(7.5f).SemiBold().FontColor(statusColor);
            });

            column.Item().PaddingTop(4).PaddingLeft(30).Row(row =>
            {
                if (photo is { Length: > 0 })
                {
                    row.ConstantItem(16).Height(16).Image(photo).FitArea();
                    row.ConstantItem(4);
                }
                row.RelativeItem().Text(name).FontSize(8).FontColor("#334E68");
            });

            var notes = TriggerNotes(task, allTasks).ToList();
            if (notes.Count > 0)
            {
                column.Item().PaddingTop(5).PaddingLeft(30).Column(notesColumn =>
                {
                    notesColumn.Spacing(3);
                    foreach (var note in notes)
                    {
                        notesColumn.Item().Element(element => ComposeFlowLabel(element, note.Text, note.Color, WithOpacity(note.Color, 0.1f)));
                    }
                });
            }
        });
    }

    /// <summary>Bir hex rengi, verilen opaklikta acik bir arka plan tonuna cevirir (rozet/etiket arka planlari icin).</summary>
    private static string WithOpacity(string hexColor, float opacity)
    {
        var alpha = (byte)Math.Round(opacity * 255);
        return $"#{alpha:X2}{hexColor.TrimStart('#')}";
    }

    /// <summary>
    /// Bir gorevin TUM aktif atananlarinin adlarini virgulle birlestirir (yalnizca
    /// ilki degil - birden fazla kisi/grup atanmis olabilir). Fotograf olarak
    /// yalnizca ilk atananin fotografi kullanilir (kutu duzeni tek kucuk resme
    /// gore tasarlandigi icin).
    /// </summary>
    private static (string Names, byte[]? Photo, bool IsGroup) AssigneeSummary(RunbookTask task)
    {
        var assignments = task.Assignments.Where(a => a.IsActive).ToList();
        if (assignments.Count == 0)
        {
            return ("Atanmamis", null, false);
        }

        var names = string.Join(", ", assignments.Select(a => a.AssigneeType == AssigneeType.User
            ? a.User?.DisplayName ?? "-"
            : $"{a.Group?.Name ?? "-"} (grup)"));

        var first = assignments[0];
        return first.AssigneeType == AssigneeType.User
            ? (names, first.User?.Photo, false)
            : (names, null, true);
    }

    private static IEnumerable<(string Text, string Color)> TriggerNotes(RunbookTask task, List<RunbookTask> allTasks)
    {
        if (task.FailureAction == TaskFailureAction.StartRollback)
        {
            yield return ("Basarisiz olursa -> Geri Donus Plani", "#C0504D");
        }
        else if (task.FailureAction == TaskFailureAction.SwitchToScenario && !string.IsNullOrEmpty(task.FailureScenarioGroup))
        {
            yield return ($"Basarisiz olursa -> Senaryo: {task.FailureScenarioGroup}", "#C0504D");
        }
        else if (task.FailureAction == TaskFailureAction.SwitchToTask && task.FailureTargetTaskId.HasValue)
        {
            var target = allTasks.FirstOrDefault(t => t.Id == task.FailureTargetTaskId.Value);
            if (target is not null)
            {
                yield return ($"Basarisiz olursa -> Gorev {target.Order}. {target.Title}", "#C0504D");
            }
        }

        if (!string.IsNullOrEmpty(task.SuccessScenarioGroup))
        {
            yield return ($"Basarili olursa -> Senaryo: {task.SuccessScenarioGroup}", "#2E7D32");
        }
        else if (task.SuccessTargetTaskId.HasValue)
        {
            var target = allTasks.FirstOrDefault(t => t.Id == task.SuccessTargetTaskId.Value);
            if (target is not null)
            {
                yield return ($"Basarili olursa -> Gorev {target.Order}. {target.Title}", "#2E7D32");
            }
        }
    }

    private static string StatusColor(RunbookTaskStatus status) => status switch
    {
        RunbookTaskStatus.NotStarted => "#7B8794",
        RunbookTaskStatus.InProgress => "#2F80ED",
        RunbookTaskStatus.Completed => "#27AE60",
        RunbookTaskStatus.Failed => "#EB5757",
        RunbookTaskStatus.Blocked => "#F2994A",
        RunbookTaskStatus.Skipped => "#9AA5B1",
        RunbookTaskStatus.NotApplicable => "#9AA5B1",
        _ => "#7B8794"
    };

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
