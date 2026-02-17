namespace DockerUpdater.Worker.Update
{
    public sealed record ContainerRef(
        string Id,
        string Name,
        string Image,
        string ImageId,
        IReadOnlyDictionary<string, string> Labels,
        string State
    );
}