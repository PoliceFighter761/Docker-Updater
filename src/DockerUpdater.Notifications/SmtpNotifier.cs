using System.Net;
using System.Net.Mail;
using DockerUpdater.Shared;
using Microsoft.Extensions.Logging;

namespace DockerUpdater.Notifications
{
    public sealed class SmtpNotifier(NotificationOptions options, ILogger<SmtpNotifier> logger) : INotifier
    {
        public async Task NotifySessionAsync(UpdateSessionResult sessionResult, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(options.SmtpHost) || string.IsNullOrWhiteSpace(options.SmtpTo))
            {
                return;
            }

            string from = !string.IsNullOrWhiteSpace(options.SmtpFrom)
                ? options.SmtpFrom
                : "docker-updater@localhost";

            string subject = !string.IsNullOrWhiteSpace(options.SmtpSubject)
                ? ReplaceSubjectTokens(options.SmtpSubject, sessionResult)
                : $"Docker Updater: {sessionResult.Updated} updated, {sessionResult.Failed} failed";

            string body = NotificationComposer.Compose(sessionResult, options.SmtpMessageTemplate);

            try
            {
                using SmtpClient client = CreateSmtpClient();
                using MailMessage message = new(from, options.SmtpTo, subject, body);
                await client.SendMailAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "SMTP notification failed.");
            }
        }

        private SmtpClient CreateSmtpClient()
        {
            SmtpClient client = new(options.SmtpHost, options.SmtpPort)
            {
                EnableSsl = options.SmtpUseTls
            };

            if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
            {
                client.Credentials = new NetworkCredential(options.SmtpUsername, options.SmtpPassword);
            }

            return client;
        }

        private static string ReplaceSubjectTokens(string subject, UpdateSessionResult session)
        {
            return subject
                .Replace("{{scanned}}", session.Scanned.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{{updated}}", session.Updated.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{{failed}}", session.Failed.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
