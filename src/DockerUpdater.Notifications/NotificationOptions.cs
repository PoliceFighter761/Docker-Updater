namespace DockerUpdater.Notifications
{
    public sealed record NotificationOptions(
        string? NotificationUrl,
        string? DiscordWebhookUrl,
        string? DiscordMessageTemplate
    );
}