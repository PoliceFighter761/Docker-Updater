using Cronos;

namespace DockerUpdater.Worker.Options
{
    public static class OptionsValidator
    {
        public static IReadOnlyList<string> Validate(UpdaterOptions options)
        {
            List<string> errors = [];

            if (!string.IsNullOrWhiteSpace(options.Schedule) && options.PollIntervalExplicitlySet)
            {
                errors.Add($"{EnvNames.Schedule} cannot be used with {EnvNames.PollInterval}.");
            }

            if (!string.IsNullOrWhiteSpace(options.Schedule))
            {
                try
                {
                    _ = CronExpression.Parse(options.Schedule, CronFormat.IncludeSeconds);
                }
                catch
                {
                    errors.Add($"{EnvNames.Schedule} is not a valid six-field cron expression.");
                }
            }

            if (options.ReviveStopped && !options.IncludeStopped)
            {
                errors.Add($"{EnvNames.ReviveStopped} requires {EnvNames.IncludeStopped}=true.");
            }

            if (options.PollIntervalSeconds <= 0)
            {
                errors.Add($"{EnvNames.PollInterval} must be greater than zero.");
            }

            if (options.StopTimeout <= TimeSpan.Zero)
            {
                errors.Add($"{EnvNames.Timeout} must be greater than zero.");
            }

            return errors;
        }
    }
}