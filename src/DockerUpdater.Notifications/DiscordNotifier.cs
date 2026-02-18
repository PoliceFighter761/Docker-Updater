using System.Net.Http.Json;
using DockerUpdater.Shared;
using Microsoft.Extensions.Logging;

namespace DockerUpdater.Notifications
{
    public sealed class DiscordNotifier(HttpClient httpClient, NotificationOptions options, ILogger<DiscordNotifier> logger) : INotifier
    {
        public async Task NotifySessionAsync(UpdateSessionResult sessionResult, CancellationToken cancellationToken)
        {
            string? webhookUrl = ResolveWebhookUrl(options);
            if (string.IsNullOrWhiteSpace(webhookUrl))
            {
                return;
            }

            DiscordPayload payload = new(NotificationComposer.Compose(sessionResult, options.DiscordMessageTemplate));

            try
            {
                using HttpResponseMessage response = await httpClient.PostAsJsonAsync(webhookUrl, payload, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("Discord notification failed with status code {Code}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Discord notification failed.");
            }
        }

        private static string? ResolveWebhookUrl(NotificationOptions currentOptions)
        {
            if (!string.IsNullOrWhiteSpace(currentOptions.DiscordWebhookUrl))
            {
                return currentOptions.DiscordWebhookUrl;
            }

            if (string.IsNullOrWhiteSpace(currentOptions.NotificationUrl))
            {
                return null;
            }

            if (currentOptions.NotificationUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return currentOptions.NotificationUrl;
            }

            return null;
        }

        private sealed class DiscordPayload
        {
            public DiscordPayload(string content)
            {
                Content = content;
            }

            public string Content { get; }
        }
    }
}