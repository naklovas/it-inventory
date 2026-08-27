using BookRunner.Application.Abstractions;
using BookRunner.Application.Security;
using BookRunner.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BookRunner.Application;

/// <summary>Is katmani servislerinin kaydi.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IRunbookAccess, RunbookAccess>();
        services.AddScoped<IDirectorySyncService, DirectorySyncService>();
        services.AddScoped<IRunbookService, RunbookService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IScriptService, ScriptService>();
        return services;
    }
}
