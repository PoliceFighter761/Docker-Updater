namespace DockerUpdater.Shared
{
    public enum ContainerUpdateState
    {
        Fresh,
        Updated,
        Skipped,
        Failed
    }

    public sealed record ContainerUpdateResult(
        string Name,
        string ImageName,
        ContainerUpdateState State,
        string? Error = null
    );

    public sealed record UpdateSessionResult(
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        IReadOnlyList<ContainerUpdateResult> Results
    )
    {
        public int Scanned => Results.Count;
        public int Updated => Results.Count(static result => result.State == ContainerUpdateState.Updated);
        public int Failed => Results.Count(static result => result.State == ContainerUpdateState.Failed);
    }
}