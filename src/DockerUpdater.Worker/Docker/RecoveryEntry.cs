using System.Text.Json.Serialization;

namespace DockerUpdater.Worker.Docker
{
    [JsonConverter(typeof(JsonStringEnumConverter<RecoveryPhase>))]
    public enum RecoveryPhase
    {
        Renamed,

        Created,

       Started
    }

    public sealed class RecoveryEntry
    {
        public required string ContainerName { get; init; }
        public required string ImageName { get; init; }
        public required string BackupContainerId { get; init; }
        public string? NewContainerId { get; set; }
        public required bool WasRunning { get; init; }
        public required bool ReviveStopped { get; init; }
        public RecoveryPhase Phase { get; set; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }
}
