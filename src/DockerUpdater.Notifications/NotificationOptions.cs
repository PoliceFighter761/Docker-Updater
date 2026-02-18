namespace DockerUpdater.Notifications
{
    public sealed record NotificationOptions(
        string? NotificationUrl,
        string? DiscordWebhookUrl,
        string? DiscordMessageTemplate,
        string? SmtpHost = null,
        int SmtpPort = 587,
        bool SmtpUseTls = true,
        string? SmtpUsername = null,
        string? SmtpPassword = null,
        string? SmtpFrom = null,
        string? SmtpTo = null,
        string? SmtpSubject = null,
        string? SmtpMessageTemplate = null
    );
}