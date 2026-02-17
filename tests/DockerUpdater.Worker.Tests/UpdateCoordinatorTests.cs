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

        [Fact]
        public void NotificationComposer_ReplacesKnownTemplateVariables()
        {
            UpdateSessionResult session = new(
                DateTimeOffset.Parse("2026-02-17T10:00:00Z"),
                DateTimeOffset.Parse("2026-02-17T10:00:30Z"),
                [
                    new ContainerUpdateResult("api", "my/api:latest", ContainerUpdateState.Updated),
                    new ContainerUpdateResult("web", "my/web:latest", ContainerUpdateState.Fresh),
                    new ContainerUpdateResult("db", "postgres:16", ContainerUpdateState.Failed, "pull failed")
                ]);

            string message = NotificationComposer.Compose(
                session,
                "Run scanned={{scanned}} updated={{updated}} failed={{failed}}\\nupdated={{updated_list}}\\nfailed={{failed_list}}\\nresults:\\n{{results}}");

            Assert.Contains("scanned=3", message, StringComparison.Ordinal);
            Assert.Contains("updated=1", message, StringComparison.Ordinal);
            Assert.Contains("failed=1", message, StringComparison.Ordinal);
            Assert.Contains("updated=api", message, StringComparison.Ordinal);
            Assert.Contains("failed=db (pull failed)", message, StringComparison.Ordinal);
            Assert.Contains("- api: Updated", message, StringComparison.Ordinal);
            Assert.Contains("- web: Fresh", message, StringComparison.Ordinal);
            Assert.Contains("- db: Failed (pull failed)", message, StringComparison.Ordinal);
        }

        [Fact]
        public void NotificationComposer_KeepsUnknownTemplateVariablesUntouched()
        {
            UpdateSessionResult session = new(
                DateTimeOffset.UtcNow.AddSeconds(-1),
                DateTimeOffset.UtcNow,
                []);

            string message = NotificationComposer.Compose(session, "value={{unknown_token}}");

            Assert.Equal("value={{unknown_token}}", message);
        }

        [Fact]
        public void NotificationComposer_ConditionalBlocks_WorkForUpdatedFailedAndBoth()
        {
            UpdateSessionResult session = new(
                DateTimeOffset.UtcNow.AddSeconds(-2),
                DateTimeOffset.UtcNow,
                [
                    new ContainerUpdateResult("api", "my/api:latest", ContainerUpdateState.Updated),
                    new ContainerUpdateResult("db", "postgres:16", ContainerUpdateState.Failed, "pull failed")
                ]);

            string template = ""
                + "{{#if updated_only}}ONLY_UPDATED{{/if}}\n"
                + "{{#if failed_only}}ONLY_FAILED{{/if}}\n"
                + "{{#if updated_and_failed}}BOTH{{/if}}\n"
                + "{{#if updated}}HAS_UPDATED{{/if}}\n"
                + "{{#if failed}}HAS_FAILED{{/if}}";

            string message = NotificationComposer.Compose(session, template);

            Assert.DoesNotContain("ONLY_UPDATED", message, StringComparison.Ordinal);
            Assert.DoesNotContain("ONLY_FAILED", message, StringComparison.Ordinal);
            Assert.Contains("BOTH", message, StringComparison.Ordinal);
            Assert.Contains("HAS_UPDATED", message, StringComparison.Ordinal);
            Assert.Contains("HAS_FAILED", message, StringComparison.Ordinal);
        }

        [Fact]
        public void NotificationComposer_ConditionalBlocks_WorkForUpdatedOnly()
        {
            UpdateSessionResult session = new(
                DateTimeOffset.UtcNow.AddSeconds(-2),
                DateTimeOffset.UtcNow,
                [
                    new ContainerUpdateResult("api", "my/api:latest", ContainerUpdateState.Updated)
                ]);

            string template = "{{#if updated_only}}ONLY_UPDATED{{/if}}|{{#if failed}}HAS_FAILED{{/if}}|{{#if updated_and_failed}}BOTH{{/if}}";
            string message = NotificationComposer.Compose(session, template);

            Assert.Contains("ONLY_UPDATED", message, StringComparison.Ordinal);
            Assert.DoesNotContain("HAS_FAILED", message, StringComparison.Ordinal);
            Assert.DoesNotContain("BOTH", message, StringComparison.Ordinal);
        }
    }
}