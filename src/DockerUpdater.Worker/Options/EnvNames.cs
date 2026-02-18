namespace DockerUpdater.Worker.Options
{
    public static class EnvNames
    {
        public const string DockerHost = "DOCKER_HOST";
        public const string DockerTlsVerify = "DOCKER_TLS_VERIFY";
        public const string DockerCertPath = "DOCKER_CERT_PATH";
        public const string PollInterval = "DOCKER_UPDATER_POLL_INTERVAL";
        public const string Schedule = "DOCKER_UPDATER_SCHEDULE";
        public const string LabelEnable = "DOCKER_UPDATER_LABEL_ENABLE";
        public const string DisableContainers = "DOCKER_UPDATER_DISABLE_CONTAINERS";
        public const string Cleanup = "DOCKER_UPDATER_CLEANUP";
        public const string Timeout = "DOCKER_UPDATER_TIMEOUT";
        public const string RunOnce = "DOCKER_UPDATER_RUN_ONCE";
        public const string SelfUpdate = "DOCKER_UPDATER_SELF_UPDATE";
        public const string IncludeStopped = "DOCKER_UPDATER_INCLUDE_STOPPED";
        public const string ReviveStopped = "DOCKER_UPDATER_REVIVE_STOPPED";
        public const string Containers = "DOCKER_UPDATER_CONTAINERS";
        public const string NotificationUrl = "DOCKER_UPDATER_NOTIFICATION_URL";
        public const string DiscordWebhookUrl = "DOCKER_UPDATER_DISCORD_WEBHOOK_URL";
        public const string DiscordMessageTemplate = "DOCKER_UPDATER_DISCORD_MESSAGE_TEMPLATE";
        public const string TimeZone = "TZ";
    }

    public static class LabelNames
    {
        public const string Enable = "com.dockerupdater.enable";
    }
}