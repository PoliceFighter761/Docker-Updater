using DockerUpdater.Shared;

namespace DockerUpdater.Notifications;

public interface INotifier
{
    Task NotifySessionAsync(UpdateSessionResult sessionResult, CancellationToken cancellationToken);
}