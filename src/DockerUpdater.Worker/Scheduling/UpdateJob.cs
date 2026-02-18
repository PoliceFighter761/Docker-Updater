using DockerUpdater.Shared;
using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Update;
using Quartz;

namespace DockerUpdater.Worker.Scheduling;

[DisallowConcurrentExecution]
public sealed class UpdateJob(
    ILogger<UpdateJob> logger,
    UpdateCoordinator coordinator,
    UpdaterOptions options,
    IHostApplicationLifetime lifetime) : IJob
{
    private static int s_runNumber;

    public async Task Execute(IJobExecutionContext context)
    {
        int runNumber = Interlocked.Increment(ref s_runNumber);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Starting update session {RunNumber}", runNumber);
        }

        UpdateSessionResult result = await coordinator.RunSessionAsync(context.CancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Finished session {RunNumber}. Scanned={Scanned}, Updated={Updated}, Failed={Failed}",
                runNumber,
                result.Scanned,
                result.Updated,
                result.Failed);
        }

        if (options.RunOnce)
        {
            logger.LogInformation("Run-once mode enabled, shutting down.");
            lifetime.StopApplication();
        }
    }
}
