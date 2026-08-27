using System.Net;
using System.Text;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using BookRunner.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Email;

/// <summary>
/// Runbook olaylari icin bildirim metinlerini uretir ve alicilari belirler.
/// Grup atamalarinda AD'deki grup uyeleri cozulur; grubun kendi e-posta adresi
/// varsa dogrudan o adres kullanilir.
/// </summary>
public sealed class NotificationService(
    BookRunnerDbContext db,
    IEmailSender emailSender,
    IDirectoryService directory,
    IOptions<EmailOptions> options,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly EmailOptions _options = options.Value;

    public async Task NotifyTaskAssignedAsync(Guid taskId, Guid assignmentId, CancellationToken ct = default)
    {
        var context = await LoadContextAsync(taskId, ct);
        if (context is null)
        {
            return;
        }

        var assignment = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

        if (assignment is null)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(assignment, ct);
        if (recipients.Count == 0)
        {
            logger.LogDebug("Atama bildirimi icin e-posta adresi bulunamadi (atama {Assignment}).", assignmentId);
            return;
        }

        var target = assignment.AssigneeType == AssigneeType.User
            ? assignment.User?.DisplayName ?? "-"
            : $"{assignment.Group?.Name} grubu";

        var body = BuildBody(
            title: "Size yeni bir runbook gorevi atandi",
            intro: $"<strong>{Encode(target)}</strong> uzerine <strong>{Encode(context.Task.Title)}</strong> gorevi atandi.",
            context: context,
            extraRows:
            [
                ("Oncelik", Application.Common.DisplayText.Priority(context.Task.Priority)),
                ("Planlanan baslangic", Format(context.Task.PlannedStart)),
                ("Not", assignment.HandoverNote ?? "-")
            ]);

        await emailSender.SendAsync(new EmailMessage
        {
            To = recipients,
            Subject = $"[BookRunner] {context.Runbook.Code} - '{context.Task.Title}' gorevi size atandi",
            HtmlBody = body,
            Reason = "TaskAssigned",
            RunbookId = context.Runbook.Id,
            TaskId = taskId
        }, ct);

        assignment.NotifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyTaskHandedOverAsync(Guid taskId, Guid newAssignmentId, string? note, CancellationToken ct = default)
    {
        var context = await LoadContextAsync(taskId, ct);
        if (context is null)
        {
            return;
        }

        var target = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .FirstOrDefaultAsync(a => a.Id == newAssignmentId, ct);

        if (target is null)
        {
            return;
        }

        var recipients = await ResolveRecipientsAsync(target, ct);

        // Devreden taraf da bilgilendirilir.
        if (target.HandedOverFromAssignmentId.HasValue)
        {
            var source = await db.Assignments
                .Include(a => a.User)
                .Include(a => a.Group)
                .FirstOrDefaultAsync(a => a.Id == target.HandedOverFromAssignmentId.Value, ct);

            if (source is not null)
            {
                recipients = recipients.Concat(await ResolveRecipientsAsync(source, ct))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var body = BuildBody(
            title: "Bir gorev devredildi",
            intro: $"<strong>{Encode(context.Task.Title)}</strong> gorevi devredildi.",
            context: context,
            extraRows: [("Devir notu", note ?? "-")]);

        await emailSender.SendAsync(new EmailMessage
        {
            To = recipients,
            Subject = $"[BookRunner] {context.Runbook.Code} - '{context.Task.Title}' gorevi devredildi",
            HtmlBody = body,
            Reason = "TaskHandedOver",
            RunbookId = context.Runbook.Id,
            TaskId = taskId
        }, ct);
    }

    public async Task NotifyTaskCommentedAsync(Guid taskId, Guid commentId, CancellationToken ct = default)
    {
        var context = await LoadContextAsync(taskId, ct);
        if (context is null)
        {
            return;
        }

        var comment = await db.Comments
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct);

        if (comment is null)
        {
            return;
        }

        var recipients = new List<string>();

        // Anilan kisiler
        if (!string.IsNullOrWhiteSpace(comment.MentionedUserIds))
        {
            var ids = comment.MentionedUserIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            recipients.AddRange(await db.Users
                .Where(u => ids.Contains(u.Id) && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(ct));
        }

        // Gorevin mevcut sahipleri
        var assignments = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .Where(a => a.TaskId == taskId && a.IsActive)
            .ToListAsync(ct);

        foreach (var assignment in assignments)
        {
            recipients.AddRange(await ResolveRecipientsAsync(assignment, ct));
        }

        // Yorumu yazan kisiye kendi yorumu gonderilmez.
        var authorEmail = comment.Author.Email;
        recipients = recipients
            .Where(r => !string.IsNullOrWhiteSpace(r) && !string.Equals(r, authorEmail, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        var body = BuildBody(
            title: "Goreve yeni yorum eklendi",
            intro: $"<strong>{Encode(comment.Author.DisplayName)}</strong> <strong>{Encode(context.Task.Title)}</strong> gorevine yorum yazdi:",
            context: context,
            extraRows: [("Yorum", Encode(comment.Body))]);

        await emailSender.SendAsync(new EmailMessage
        {
            To = recipients,
            Subject = $"[BookRunner] {context.Runbook.Code} - '{context.Task.Title}' gorevine yorum",
            HtmlBody = body,
            Reason = "TaskCommented",
            RunbookId = context.Runbook.Id,
            TaskId = taskId
        }, ct);
    }

    public async Task NotifyTaskStatusChangedAsync(Guid taskId, string oldStatus, string newStatus, CancellationToken ct = default)
    {
        var context = await LoadContextAsync(taskId, ct);
        if (context is null)
        {
            return;
        }

        var recipients = new List<string>();
        if (context.Runbook.Owner?.Email is { Length: > 0 } ownerEmail)
        {
            recipients.Add(ownerEmail);
        }

        var assignments = await db.Assignments
            .Include(a => a.User)
            .Include(a => a.Group)
            .Where(a => a.TaskId == taskId && a.IsActive)
            .ToListAsync(ct);

        foreach (var assignment in assignments)
        {
            recipients.AddRange(await ResolveRecipientsAsync(assignment, ct));
        }

        recipients = recipients.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        var body = BuildBody(
            title: "Gorev durumu degisti",
            intro: $"<strong>{Encode(context.Task.Title)}</strong> gorevinin durumu <strong>{Encode(oldStatus)}</strong> -> <strong>{Encode(newStatus)}</strong> olarak guncellendi.",
            context: context,
            extraRows: []);

        await emailSender.SendAsync(new EmailMessage
        {
            To = recipients,
            Subject = $"[BookRunner] {context.Runbook.Code} - '{context.Task.Title}' durumu: {newStatus}",
            HtmlBody = body,
            Reason = "TaskStatusChanged",
            RunbookId = context.Runbook.Id,
            TaskId = taskId
        }, ct);
    }

    /// <summary>Atamanin e-posta alicilarini cozer (kisi adresi ya da grup uyeleri).</summary>
    private async Task<List<string>> ResolveRecipientsAsync(TaskAssignment assignment, CancellationToken ct)
    {
        if (assignment.AssigneeType == AssigneeType.User)
        {
            return assignment.User?.Email is { Length: > 0 } email ? [email] : [];
        }

        if (assignment.Group is null)
        {
            return [];
        }

        // Grubun kendi posta adresi varsa dagitim listesi olarak kullanilir.
        if (!string.IsNullOrWhiteSpace(assignment.Group.Email))
        {
            return [assignment.Group.Email!];
        }

        try
        {
            var members = await directory.GetGroupMembersAsync(assignment.Group.Sid, ct);
            return members
                .Where(m => !string.IsNullOrWhiteSpace(m.Email))
                .Select(m => m.Email!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Group} grubunun uyeleri AD'den okunamadi; yerel uyelik kullaniliyor.", assignment.Group.Name);

            return await db.UserGroups
                .Where(ug => ug.GroupId == assignment.GroupId && ug.User.Email != null)
                .Select(ug => ug.User.Email!)
                .Distinct()
                .ToListAsync(ct);
        }
    }

    private async Task<NotificationContext?> LoadContextAsync(Guid taskId, CancellationToken ct)
    {
        var task = await db.Tasks
            .Include(t => t.Runbook).ThenInclude(r => r.Owner)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct);

        return task is null ? null : new NotificationContext(task, task.Runbook);
    }

    /// <summary>Basit, e-posta istemcilerinde guvenli goruntulenen HTML sablon.</summary>
    private string BuildBody(string title, string intro, NotificationContext context, (string Label, string Value)[] extraRows)
    {
        var url = $"{_options.WebBaseUrl.TrimEnd('/')}/Runbooks/Details/{context.Runbook.Id}#task-{context.Task.Id}";

        var rows = new StringBuilder();
        rows.Append(Row("Runbook", $"{Encode(context.Runbook.Code)} - {Encode(context.Runbook.Title)}"));
        rows.Append(Row("Gorev", Encode(context.Task.Title)));
        rows.Append(Row("Durum", Application.Common.DisplayText.Status(context.Task.Status)));

        foreach (var (label, value) in extraRows)
        {
            rows.Append(Row(label, value));
        }

        return $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2933;">
          <div style="border-left:4px solid {context.Task.ColorHex};padding:12px 16px;background:#f5f7fa;">
            <h2 style="margin:0 0 8px;font-size:18px;">{Encode(title)}</h2>
            <p style="margin:0;">{intro}</p>
          </div>
          <table style="margin:16px 0;border-collapse:collapse;">{rows}</table>
          <p>
            <a href="{url}" style="background:#2f5bd7;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;">
              Gorevi ac
            </a>
          </p>
          <p style="color:#7b8794;font-size:12px;margin-top:24px;">
            Bu e-posta BookRunner tarafindan otomatik olarak gonderilmistir.
          </p>
        </div>
        """;
    }

    private static string Row(string label, string value)
        => $"""<tr><td style="padding:4px 12px 4px 0;color:#7b8794;">{Encode(label)}</td><td style="padding:4px 0;">{value}</td></tr>""";

    private static string Format(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-";

    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private sealed record NotificationContext(RunbookTask Task, Runbook Runbook);
}
