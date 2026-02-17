using Cronos;
using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Scheduling
{
    public sealed class RunScheduler(UpdaterOptions options, TimeProvider timeProvider) : IRunScheduler
    {
        private readonly CronExpression? _schedule = string.IsNullOrWhiteSpace(options.Schedule)
            ? null
            : CronExpression.Parse(options.Schedule, CronFormat.IncludeSeconds);
        private readonly TimeZoneInfo _timeZone = ResolveTimeZone(options.TimeZone);

        public async ValueTask WaitForNextRunAsync(CancellationToken cancellationToken)
        {
            if (_schedule is null)
            {
                TimeSpan intervalDelay = TimeSpan.FromSeconds(options.PollIntervalSeconds);
                await Task.Delay(intervalDelay, cancellationToken);
                return;
            }

            DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            DateTime? next = _schedule.GetNextOccurrence(nowUtc, _timeZone, inclusive: false);
            if (next is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), cancellationToken);
                return;
            }

            TimeSpan delay = next.Value - nowUtc;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            await Task.Delay(delay, cancellationToken);
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZone)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}