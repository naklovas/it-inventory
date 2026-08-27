using System.Diagnostics;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Dtos;
using BookRunner.Domain.Enums;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BookRunner.Infrastructure.Scripting;

/// <summary>
/// Runbook adimlarina baglanan C# script'lerini (CSX) Roslyn ile calistirir.
/// Script'ler uygulama sureci icinde tam guvenle calisir; bu nedenle yazma
/// yetkisi yalnizca yoneticilerde olmali ve her calistirma audit'e yazilmalidir.
/// </summary>
public sealed class RoslynScriptRunner(
    IOptions<ScriptingOptions> options,
    ILogger<RoslynScriptRunner> logger) : IScriptRunner
{
    private readonly ScriptingOptions _options = options.Value;

    public async Task<ScriptRunResult> RunAsync(
        string code, ScriptContext context, int timeoutSeconds, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.Failed,
                Error = "Script calistirma yapilandirmada kapali."
            };
        }

        var blocked = _options.BlockedPatterns
            .FirstOrDefault(pattern => code.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (blocked is not null)
        {
            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.Failed,
                Error = $"Script yasakli bir ifade iceriyor: {blocked}"
            };
        }

        var effectiveTimeout = timeoutSeconds > 0 ? timeoutSeconds : _options.DefaultTimeoutSeconds;
        var globals = new ScriptGlobals(context, _options.MaxOutputLines);

        var scriptOptions = ScriptOptions.Default
            .WithImports(_options.Imports)
            .WithReferences(
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(ScriptGlobals).Assembly);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(effectiveTimeout));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var state = await CSharpScript.RunAsync(
                code, scriptOptions, globals, typeof(ScriptGlobals), timeoutSource.Token);

            stopwatch.Stop();

            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.Succeeded,
                Result = state.ReturnValue?.ToString(),
                Output = globals.Output,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (CompilationErrorException ex)
        {
            stopwatch.Stop();
            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.Failed,
                Output = globals.Output,
                Error = "Derleme hatasi: " + string.Join(Environment.NewLine, ex.Diagnostics),
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            logger.LogWarning("Script {Timeout} saniyelik zaman asimina ugradi ({User}).",
                effectiveTimeout, context.ExecutedBy);

            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.TimedOut,
                Output = globals.Output,
                Error = $"Script {effectiveTimeout} saniye icinde tamamlanmadi.",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Script calistirilirken hata olustu ({User}).", context.ExecutedBy);

            return new ScriptRunResult
            {
                Status = ScriptExecutionStatus.Failed,
                Output = globals.Output,
                Error = ex.Message,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
