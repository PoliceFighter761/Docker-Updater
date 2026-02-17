using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Tests
{
    [Collection("Environment Variables")]
    public sealed class UpdaterOptionsTests : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void LoadFromEnvironment_ParsesExpectedValues()
        {
            Set(EnvNames.DockerHost, "unix:///var/run/docker.sock");
            Set(EnvNames.DockerTlsVerify, "true");
            Set(EnvNames.PollInterval, "120");
            Set(EnvNames.Schedule, "");
            Set(EnvNames.LabelEnable, "1");
            Set(EnvNames.DisableContainers, "db, cache api");
            Set(EnvNames.Cleanup, "yes");
            Set(EnvNames.Timeout, "30s");
            Set(EnvNames.RunOnce, "on");
            Set(EnvNames.IncludeStopped, "true");
            Set(EnvNames.ReviveStopped, "true");
            Set(EnvNames.Containers, " /web  api,worker ");
            Set(EnvNames.NotificationUrl, " https://example.test/webhook ");
            Set(EnvNames.DiscordWebhookUrl, " https://discord.com/api/webhooks/x/y ");
            Set(EnvNames.TimeZone, "UTC");

            UpdaterOptions options = UpdaterOptions.LoadFromEnvironment();

            Assert.NotNull(options.DockerHost);
            Assert.Equal("unix:///var/run/docker.sock", options.DockerHost!.ToString());
            Assert.True(options.DockerTlsVerify);
            Assert.True(options.PollIntervalExplicitlySet);
            Assert.Equal(120, options.PollIntervalSeconds);
            Assert.True(options.LabelEnable);
            Assert.True(options.DisableContainers.SetEquals(["db", "cache", "api"]));
            Assert.True(options.Cleanup);
            Assert.Equal(TimeSpan.FromSeconds(30), options.StopTimeout);
            Assert.True(options.RunOnce);
            Assert.True(options.IncludeStopped);
            Assert.True(options.ReviveStopped);
            Assert.True(options.TargetContainers.SetEquals(["web", "api", "worker"]));
            Assert.Equal("https://example.test/webhook", options.NotificationUrl);
            Assert.Equal("https://discord.com/api/webhooks/x/y", options.DiscordWebhookUrl);
            Assert.Equal("UTC", options.TimeZone);
        }

        [Theory]
        [InlineData("2m", 120)]
        [InlineData("1h", 3600)]
        [InlineData("00:00:45", 45)]
        public void LoadFromEnvironment_ParsesTimeoutFormats(string timeoutValue, int expectedSeconds)
        {
            Set(EnvNames.Timeout, timeoutValue);

            UpdaterOptions options = UpdaterOptions.LoadFromEnvironment();

            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), options.StopTimeout);
        }

        [Fact]
        public void LoadFromEnvironment_UsesDefaultsForInvalidValues()
        {
            Set(EnvNames.PollInterval, "invalid");
            Set(EnvNames.Timeout, "invalid");
            Set(EnvNames.DockerHost, "not-a-uri");
            Set(EnvNames.LabelEnable, "invalid");

            UpdaterOptions options = UpdaterOptions.LoadFromEnvironment();

            Assert.Null(options.DockerHost);
            Assert.Equal(86400, options.PollIntervalSeconds);
            Assert.Equal(TimeSpan.FromSeconds(10), options.StopTimeout);
            Assert.False(options.LabelEnable);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("   ", "")]
        [InlineData("/api", "api")]
        [InlineData("  /api  ", "api")]
        [InlineData("service", "service")]
        public void NormalizeContainerName_WorksAsExpected(string? input, string expected)
        {
            string normalized = UpdaterOptions.NormalizeContainerName(input);
            Assert.Equal(expected, normalized);
        }

        public void Dispose()
        {
            foreach ((string? key, string? value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private void Set(string key, string? value)
        {
            if (!_originalValues.ContainsKey(key))
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}