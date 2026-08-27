using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Application.Dtos;
using BookRunner.Application.Security;
using BookRunner.Domain.Entities;
using BookRunner.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookRunner.Application.Services;

/// <summary>
/// Runbook adimlarina baglanan Roslyn (CSX) script'lerini yonetir ve calistirir.
/// Script yazma yetkisi yalnizca yoneticilerdedir; her calistirma audit'e yazilir.
/// </summary>
public sealed class ScriptService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IScriptRunner runner,
    IAuditService audit,
    ILogger<ScriptService> logger) : IScriptService
{
    public async Task<IReadOnlyList<ScriptDto>> ListAsync(Guid? runbookId, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ScriptExecute);

        var query = db.Scripts.AsNoTracking().AsQueryable();
        query = runbookId.HasValue
            ? query.Where(s => s.RunbookId == runbookId.Value || s.RunbookId == null)
            : query.Where(s => s.RunbookId == null);

        var scripts = await query.OrderBy(s => s.Name).ToListAsync(ct);
        return scripts.Select(s => s.ToDto()).ToList();
    }

    public async Task<ScriptDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ScriptExecute);

        var script = await db.Scripts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Script", id);

        return script.ToDto();
    }

    public async Task<ScriptDto> SaveAsync(Guid? id, SaveScriptRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ScriptManage);

        if (request.RunbookId.HasValue && !await db.Runbooks.AnyAsync(r => r.Id == request.RunbookId.Value, ct))
        {
            throw new NotFoundException("Runbook", request.RunbookId.Value);
        }

        RunbookScript script;
        if (id.HasValue)
        {
            script = await db.Scripts.FirstOrDefaultAsync(s => s.Id == id.Value, ct)
                ?? throw new NotFoundException("Script", id.Value);
        }
        else
        {
            script = new RunbookScript();
            db.Scripts.Add(script);
        }

        script.RunbookId = request.RunbookId;
        script.Name = request.Name.Trim();
        script.Description = request.Description;
        script.Code = request.Code;
        script.TimeoutSeconds = Math.Clamp(request.TimeoutSeconds, 1, 900);
        script.IsEnabled = request.IsEnabled;

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(id.HasValue ? AuditAction.Update : AuditAction.Create, nameof(RunbookScript),
            script.Id.ToString(), $"'{script.Name}' script'i kaydedildi.", script.RunbookId, ct: ct);

        return script.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ScriptManage);

        var script = await db.Scripts.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Script", id);

        if (await db.Tasks.AnyAsync(t => t.ScriptId == id, ct))
        {
            throw new BusinessRuleException("Bu script bir goreve bagli oldugu icin silinemez.");
        }

        db.Scripts.Remove(script);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Delete, nameof(RunbookScript), id.ToString(),
            $"'{script.Name}' script'i silindi.", script.RunbookId, ct: ct);
    }

    public async Task<ScriptRunResult> RunAsync(Guid id, RunScriptRequest request, CancellationToken ct = default)
    {
        RequirePermission(Permissions.ScriptExecute);

        var script = await db.Scripts
            .Include(s => s.Runbook)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Script", id);

        if (!script.IsEnabled)
        {
            throw new BusinessRuleException("Bu script devre disi birakilmis.");
        }

        RunbookTask? task = null;
        if (request.TaskId.HasValue)
        {
            task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == request.TaskId.Value, ct)
                ?? throw new NotFoundException("Gorev", request.TaskId.Value);
        }

        var execution = new ScriptExecution
        {
            ScriptId = script.Id,
            TaskId = task?.Id,
            ExecutedByUserId = currentUser.UserId,
            Status = ScriptExecutionStatus.Running
        };

        db.ScriptExecutions.Add(execution);
        await db.SaveChangesAsync(ct);

        var context = new ScriptContext
        {
            RunbookId = script.RunbookId ?? task?.RunbookId,
            RunbookCode = script.Runbook?.Code,
            RunbookTitle = script.Runbook?.Title,
            TaskId = task?.Id,
            TaskTitle = task?.Title,
            ExecutedBy = currentUser.UserName,
            Parameters = request.Parameters
        };

        var result = await runner.RunAsync(script.Code, context, script.TimeoutSeconds, ct);

        execution.Status = result.Status;
        execution.FinishedAt = DateTimeOffset.UtcNow;
        execution.DurationMs = result.DurationMs;
        execution.Result = result.Result;
        execution.Output = result.Output.Count == 0 ? null : string.Join(Environment.NewLine, result.Output);
        execution.Error = result.Error;

        if (task is not null)
        {
            db.Activities.Add(new TaskActivity
            {
                TaskId = task.Id,
                Type = TaskActivityType.ScriptExecuted,
                ActorUserId = currentUser.UserId,
                ActorDisplayName = currentUser.DisplayName,
                Summary = $"'{script.Name}' script'i calistirildi: {result.Status}",
                NewValue = result.Status.ToString()
            });
        }

        await db.SaveChangesAsync(ct);

        await audit.LogAsync(AuditAction.Execute, nameof(RunbookScript), script.Id.ToString(),
            $"'{script.Name}' script'i calistirildi ({result.Status}, {result.DurationMs} ms).",
            context.RunbookId, new { result.Status, result.Error }, ct);

        logger.LogInformation("Script {Script} {Status} olarak tamamlandi ({Duration} ms).",
            script.Name, result.Status, result.DurationMs);

        return result;
    }

    private void RequirePermission(string permission)
    {
        if (!Permissions.Has(currentUser.Role, permission))
        {
            throw new ForbiddenException($"Bu islem icin '{permission}' yetkisi gerekiyor.");
        }
    }
}
