using System.Globalization;

namespace DockerUpdater.Worker.Options
{
    public sealed class UpdaterOptions
    {
        public Uri? DockerHost { get; init; }
        public bool DockerTlsVerify { get; init; }
        public int PollIntervalSeconds { get; init; } = 86400;
        public bool PollIntervalExplicitlySet { get; init; }
        public string? Schedule { get; init; }
        public bool LabelEnable { get; init; }
        public HashSet<string> DisableContainers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Cleanup { get; init; }
        public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public bool RunOnce { get; init; }
        public bool IncludeStopped { get; init; }
        public bool ReviveStopped { get; init; }
        public HashSet<string> TargetContainers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public string? NotificationUrl { get; init; }
        public string? DiscordWebhookUrl { get; init; }
        public string? DiscordMessageTemplate { get; init; }
        public string TimeZone { get; init; } = "UTC";

        public static UpdaterOptions LoadFromEnvironment()
        {
            return new UpdaterOptions
            {
                PollIntervalExplicitlySet = !string.IsNullOrWhiteSpace(Get(EnvNames.PollInterval)),
                DockerHost = ParseUri(Get(EnvNames.DockerHost)),
                DockerTlsVerify = ParseBool(Get(EnvNames.DockerTlsVerify), defaultValue: false),
                PollIntervalSeconds = ParseInt(Get(EnvNames.PollInterval), 86400),
                Schedule = NullIfWhiteSpace(Get(EnvNames.Schedule)),
                LabelEnable = ParseBool(Get(EnvNames.LabelEnable), defaultValue: false),
                DisableContainers = ParseSet(Get(EnvNames.DisableContainers)),
                Cleanup = ParseBool(Get(EnvNames.Cleanup), defaultValue: false),
                StopTimeout = ParseDuration(Get(EnvNames.Timeout), TimeSpan.FromSeconds(10)),
                RunOnce = ParseBool(Get(EnvNames.RunOnce), defaultValue: false),
                IncludeStopped = ParseBool(Get(EnvNames.IncludeStopped), defaultValue: false),
                ReviveStopped = ParseBool(Get(EnvNames.ReviveStopped), defaultValue: false),
                TargetContainers = ParseSet(Get(EnvNames.Containers)),
                NotificationUrl = NullIfWhiteSpace(Get(EnvNames.NotificationUrl)),
                DiscordWebhookUrl = NullIfWhiteSpace(Get(EnvNames.DiscordWebhookUrl)),
                DiscordMessageTemplate = NullIfWhiteSpace(Get(EnvNames.DiscordMessageTemplate)),
                TimeZone = NullIfWhiteSpace(Get(EnvNames.TimeZone)) ?? "UTC"
            };
        }

        private static string? Get(string key) => Environment.GetEnvironmentVariable(key);

        private static Uri? ParseUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;
        }

        private static bool ParseBool(string? value, bool defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "yes" or "on" => true,
                "0" or "false" or "no" or "off" => false,
                _ => defaultValue
            };
        }

        private static int ParseInt(string? value, int defaultValue)
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0)
            {
                return parsed;
            }

            return defaultValue;
        }

        private static TimeSpan ParseDuration(string? value, TimeSpan defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            string trimmed = value.Trim();
            if (trimmed.EndsWith('s') && int.TryParse(trimmed[..^1], out int seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            if (trimmed.EndsWith('m') && int.TryParse(trimmed[..^1], out int minutes))
            {
                return TimeSpan.FromMinutes(minutes);
            }

            if (trimmed.EndsWith('h') && int.TryParse(trimmed[..^1], out int hours))
            {
                return TimeSpan.FromHours(hours);
            }

            return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out TimeSpan parsed)
                ? parsed
                : defaultValue;
        }

        private static HashSet<string> ParseSet(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return value
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeContainerName)
                .Where(static entry => !string.IsNullOrWhiteSpace(entry))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public static string NormalizeContainerName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name.Trim().TrimStart('/');
        }

        private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}