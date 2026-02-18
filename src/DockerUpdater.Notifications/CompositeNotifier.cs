using DockerUpdater.Shared;
using Microsoft.Extensions.Logging;

namespace DockerUpdater.Notifications
{
    public sealed class CompositeNotifier : INotifier
    {
        private readonly IReadOnlyList<INotifier> _notifiers;

        public CompositeNotifier(NotificationOptions options, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            List<INotifier> notifiers = [];

            if (IsDiscordConfigured(options))
            {
                notifiers.Add(new DiscordNotifier(
                    httpClientFactory,
                    options,
                    loggerFactory.CreateLogger<DiscordNotifier>()));
            }

            if (IsSmtpConfigured(options))
            {
                notifiers.Add(new SmtpNotifier(
                    options,
                    loggerFactory.CreateLogger<SmtpNotifier>()));
            }

            _notifiers = notifiers;
        }

        public async Task NotifySessionAsync(UpdateSessionResult sessionResult, CancellationToken cancellationToken)
        {
            foreach (INotifier notifier in _notifiers)
            {
                await notifier.NotifySessionAsync(sessionResult, cancellationToken);
            }
        }

        private static bool IsDiscordConfigured(NotificationOptions options)
        {
            return !string.IsNullOrWhiteSpace(options.DiscordWebhookUrl)
                || !string.IsNullOrWhiteSpace(options.NotificationUrl);
        }

        private static bool IsSmtpConfigured(NotificationOptions options)
        {
            return !string.IsNullOrWhiteSpace(options.SmtpHost)
                && !string.IsNullOrWhiteSpace(options.SmtpTo);
        }
    }
}
