namespace DockerUpdater.Worker.Scheduling
{
    public interface IRunScheduler
    {
        ValueTask WaitForNextRunAsync(CancellationToken cancellationToken);
    }
}