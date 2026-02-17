using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Scheduling;
using DockerUpdater.Worker.Update;
using DockerUpdater.Shared;

namespace DockerUpdater.Worker
{
    public class Worker(
        ILogger<Worker> logger,
        UpdateCoordinator coordinator,
        IRunScheduler scheduler,
        UpdaterOptions options) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int runNumber = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                runNumber++;

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Starting update session {RunNumber}", runNumber);
                }

                UpdateSessionResult result = await coordinator.RunSessionAsync(stoppingToken);

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
                    logger.LogInformation("Run-once mode enabled, exiting worker.");
                    return;
                }

                await scheduler.WaitForNextRunAsync(stoppingToken);
            }
        }
    }
}
