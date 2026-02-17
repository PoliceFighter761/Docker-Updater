using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Tests
{
    public class OptionsValidatorTests
    {
        [Fact]
        public void ReturnsErrorWhenScheduleAndIntervalBothSet()
        {
            UpdaterOptions options = new()
            {
                Schedule = "0 0 * * * *",
                PollIntervalSeconds = 60,
                PollIntervalExplicitlySet = true
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.Contains(errors, error => error.Contains(EnvNames.Schedule, StringComparison.Ordinal));
        }

        [Fact]
        public void DoesNotErrorWhenOnlyScheduleIsSet()
        {
            UpdaterOptions options = new()
            {
                Schedule = "0 0 * * * *",
                PollIntervalSeconds = 86400,
                PollIntervalExplicitlySet = false
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.DoesNotContain(errors, error => error.Contains(EnvNames.Schedule, StringComparison.Ordinal));
        }

        [Fact]
        public void ReturnsErrorWhenReviveStoppedWithoutIncludeStopped()
        {
            UpdaterOptions options = new()
            {
                ReviveStopped = true,
                IncludeStopped = false,
                PollIntervalSeconds = 30
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.Contains(errors, error => error.Contains(EnvNames.ReviveStopped, StringComparison.Ordinal));
        }

        [Fact]
        public void ReturnsErrorForInvalidCron()
        {
            UpdaterOptions options = new()
            {
                Schedule = "invalid-cron"
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.Contains(errors, error => error.Contains("valid six-field cron", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ReturnsErrorWhenPollIntervalIsZero()
        {
            UpdaterOptions options = new()
            {
                PollIntervalSeconds = 0
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.Contains(errors, error => error.Contains(EnvNames.PollInterval, StringComparison.Ordinal));
        }

        [Fact]
        public void ReturnsErrorWhenTimeoutIsZero()
        {
            UpdaterOptions options = new()
            {
                StopTimeout = TimeSpan.Zero
            };

            IReadOnlyList<string> errors = OptionsValidator.Validate(options);

            Assert.Contains(errors, error => error.Contains(EnvNames.Timeout, StringComparison.Ordinal));
        }
    }
}