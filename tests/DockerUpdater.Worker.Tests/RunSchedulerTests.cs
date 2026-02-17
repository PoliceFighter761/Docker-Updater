using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Scheduling;

namespace DockerUpdater.Worker.Tests
{
    public class RunSchedulerTests
    {
        [Fact]
        public async Task IntervalWaitsAtLeastConfiguredDelay()
        {
            UpdaterOptions options = new() { PollIntervalSeconds = 1 };
            RunScheduler scheduler = new(options, TimeProvider.System);

            DateTimeOffset started = DateTimeOffset.UtcNow;
            await scheduler.WaitForNextRunAsync(CancellationToken.None);
            TimeSpan elapsed = DateTimeOffset.UtcNow - started;

            Assert.True(elapsed >= TimeSpan.FromMilliseconds(900));
        }

        [Fact]
        public async Task CronScheduleComputesNextRun()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int nextSecond = (now.Second + 2) % 60;
            string schedule = $"{nextSecond} * * * * *";

            UpdaterOptions options = new()
            {
                Schedule = schedule,
                PollIntervalSeconds = 60
            };

            RunScheduler scheduler = new(options, TimeProvider.System);

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            await scheduler.WaitForNextRunAsync(cts.Token);
        }

        [Fact]
        public async Task InvalidTimeZoneFallsBackToUtc()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            int nextSecond = (now.Second + 2) % 60;
            string schedule = $"{nextSecond} * * * * *";

            UpdaterOptions options = new()
            {
                Schedule = schedule,
                TimeZone = "Invalid/Timezone",
                PollIntervalSeconds = 60
            };

            RunScheduler scheduler = new(options, TimeProvider.System);

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
            await scheduler.WaitForNextRunAsync(cts.Token);
        }
    }
}