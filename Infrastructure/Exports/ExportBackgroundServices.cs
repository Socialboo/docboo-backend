using System.Threading.Channels;
using Application.Exports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Exports;

/// <summary>In-process queue of export job ids (bounded Channel).</summary>
public sealed class ChannelExportQueue : IExportQueue
{
    private readonly Channel<Guid> _channel;

    public ChannelExportQueue(IOptions<ExportOptions> options) =>
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Math.Max(1, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
        });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct) => _channel.Writer.WriteAsync(jobId, ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Runs export jobs with limited concurrency. On start it re-queues jobs left queued/running by a
/// previous process, so a restart never loses a request.
/// </summary>
public sealed class ExportWorker(
    ChannelExportQueue queue,
    IServiceScopeFactory scopes,
    IOptions<ExportOptions> options,
    ILogger<ExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverUnfinishedAsync(stoppingToken);

        await Parallel.ForEachAsync(
            queue.ReadAllAsync(stoppingToken),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, options.Value.MaxConcurrency), CancellationToken = stoppingToken },
            async (jobId, ct) =>
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().ProcessAsync(jobId, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    // ProcessAsync records failures itself; this only guards the loop.
                    logger.LogError(ex, "Export worker failed while processing {JobId}", jobId);
                }
            }).ConfigureAwait(false);
    }

    private async Task RecoverUnfinishedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var jobs = scope.ServiceProvider.GetRequiredService<IExportJobRepository>();
            var unfinished = await jobs.ListUnfinishedAsync(ct);
            foreach (var job in unfinished) job.Requeue();
            await jobs.SaveChangesAsync(ct);
            foreach (var job in unfinished) await queue.EnqueueAsync(job.Id, ct);
            if (unfinished.Count > 0) logger.LogInformation("Re-queued {Count} unfinished export job(s)", unfinished.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not recover unfinished export jobs");
        }
    }
}

/// <summary>Periodically removes expired export files.</summary>
public sealed class ExportCleanupService(
    IServiceScopeFactory scopes,
    IOptions<ExportOptions> options,
    ILogger<ExportCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.CleanupIntervalMinutes)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var removed = await scope.ServiceProvider.GetRequiredService<ExportJobProcessor>().CleanupExpiredAsync(stoppingToken);
                if (removed > 0) logger.LogInformation("Removed {Count} expired export file(s)", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Export cleanup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
