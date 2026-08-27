using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BookRunner.Application.Services;

/// <summary>Gorev yorumlari. Her yorum ayni zamanda bir tarihce kaydi uretir.</summary>
public sealed class CommentService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IRunbookAccess access,
    IAuditService audit,
    INotificationService notifications,
    IRealtimeNotifier realtime) : ICommentService
{
    public async Task<IReadOnlyList<TaskCommentDto>> ListAsync(Guid taskId, CancellationToken ct = default)
    {
        var comments = await db.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.TaskId == taskId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var lookup = await BuildMentionLookupAsync(comments, ct);
        return comments.Select(c => c.ToDto(lookup)).ToList();
    }

    public async Task<TaskCommentDto> AddAsync(Guid taskId, CreateCommentRequest request, CancellationToken ct = default)
    {
        // Yorum yazmak icin rol izni ya da runbook sahipligi yeterlidir.
        await access.EnsureForTaskAsync(taskId, Permissions.TaskComment, ct);

        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new NotFoundException("Gorev", taskId);

        var authorId = currentUser.UserId
            ?? throw new BusinessRuleException("Yorum yazabilmek icin kullanicinin Active Directory'den cozulmesi gerekir.");

        if (request.ParentCommentId.HasValue &&
            !await db.Comments.AnyAsync(c => c.Id == request.ParentCommentId.Value && c.TaskId == taskId, ct))
        {
            throw ValidationException.Single(nameof(request.ParentCommentId), "Yanit verilen yorum bu goreve ait degil.");
        }

        var mentionIds = request.MentionedUserIds?.Distinct().ToList() ?? [];
        if (mentionIds.Count > 0)
        {
            var existing = await db.Users.Where(u => mentionIds.Contains(u.Id)).Select(u => u.Id).ToListAsync(ct);
            mentionIds = existing;
        }

        var comment = new TaskComment
        {
            TaskId = taskId,
            AuthorUserId = authorId,
            Body = request.Body.Trim(),
            ParentCommentId = request.ParentCommentId,
            MentionedUserIds = mentionIds.Count == 0 ? null : string.Join(',', mentionIds)
        };

        db.Comments.Add(comment);

        db.Activities.Add(new TaskActivity
        {
            TaskId = taskId,
            Type = TaskActivityType.Commented,
            ActorUserId = authorId,
            ActorDisplayName = currentUser.DisplayName,
            Summary = Summarize(comment.Body)
        });

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Create, nameof(TaskComment), comment.Id.ToString(),
            $"'{task.Title}' gorevine yorum eklendi.", task.RunbookId, ct: ct);

        await notifications.NotifyTaskCommentedAsync(taskId, comment.Id, ct);

        var dto = await LoadDtoAsync(comment.Id, ct);
        await realtime.CommentAddedAsync(task.RunbookId, taskId, dto, ct);

        return dto;
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        var comment = await db.Comments
            .Include(c => c.Task)
            .FirstOrDefaultAsync(c => c.Id == commentId, ct)
            ?? throw new NotFoundException("Yorum", commentId);

        var isAuthor = currentUser.UserId.HasValue && comment.AuthorUserId == currentUser.UserId.Value;
        if (!isAuthor && !Permissions.Has(currentUser.Role, Permissions.AdminManage))
        {
            throw new ForbiddenException("Yalnizca kendi yorumunuzu silebilirsiniz.");
        }

        comment.IsDeleted = true;
        comment.DeletedAt = DateTimeOffset.UtcNow;
        comment.DeletedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Delete, nameof(TaskComment), comment.Id.ToString(),
            "Yorum silindi.", comment.Task.RunbookId, ct: ct);

        await realtime.TaskChangedAsync(comment.Task.RunbookId, comment.TaskId, "comment-deleted", ct);
    }

    private async Task<TaskCommentDto> LoadDtoAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments
            .AsNoTracking()
            .Include(c => c.Author)
            .FirstAsync(c => c.Id == commentId, ct);

        var lookup = await BuildMentionLookupAsync([comment], ct);
        return comment.ToDto(lookup);
    }

    private async Task<IReadOnlyDictionary<Guid, AppUser>> BuildMentionLookupAsync(
        IReadOnlyCollection<TaskComment> comments, CancellationToken ct)
    {
        var ids = comments
            .Where(c => !string.IsNullOrWhiteSpace(c.MentionedUserIds))
            .SelectMany(c => c.MentionedUserIds!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(raw => Guid.TryParse(raw, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, AppUser>();
        }

        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }

    private static string Summarize(string body)
        => body.Length <= 120 ? body : body[..120] + "...";
}
