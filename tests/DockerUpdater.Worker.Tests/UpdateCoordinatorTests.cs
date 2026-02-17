using DockerUpdater.Notifications;
using DockerUpdater.Shared;

namespace DockerUpdater.Worker.Tests
{
    public class UpdateCoordinatorTests
    {
        [Fact]
        public void NotificationComposer_IncludesSummaryAndImportantEntries()
        {
            UpdateSessionResult session = new(
                DateTimeOffset.UtcNow.AddSeconds(-5),
                DateTimeOffset.UtcNow,
                [
                    new ContainerUpdateResult("api", "my/api:latest", ContainerUpdateState.Updated),
                    new ContainerUpdateResult("web", "my/web:latest", ContainerUpdateState.Fresh),
                    new ContainerUpdateResult("db", "postgres:16", ContainerUpdateState.Failed, "pull failed")
                ]);

            string message = NotificationComposer.Compose(session);

            Assert.Contains("Scanned: 3, Updated: 1, Failed: 1", message, StringComparison.Ordinal);
            Assert.Contains("api: Updated", message, StringComparison.Ordinal);
            Assert.Contains("db: Failed", message, StringComparison.Ordinal);
            Assert.DoesNotContain("web: Fresh", message, StringComparison.Ordinal);
        }
    }
}