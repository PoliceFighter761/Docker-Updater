using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Update;

namespace DockerUpdater.Worker.Tests
{
    public class ContainerSelectionPolicyTests
    {
        [Fact]
        public void ExcludesByDisableContainerList()
        {
            UpdaterOptions options = new()
            {
                DisableContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "db" }
            };
            ContainerSelectionPolicy policy = new(options);

            bool shouldMonitor = policy.ShouldMonitor(new ContainerRef(
                "1",
                "db",
                "postgres:16",
                "img1",
                new Dictionary<string, string>(),
                "running"));

            Assert.False(shouldMonitor);
        }

        [Fact]
        public void IncludesOnlyEnabledLabelWhenLabelEnableIsTrue()
        {
            UpdaterOptions options = new() { LabelEnable = true };
            ContainerSelectionPolicy policy = new(options);

            bool enabled = policy.ShouldMonitor(new ContainerRef(
                "1",
                "api",
                "my/api:latest",
                "img1",
                new Dictionary<string, string> { [LabelNames.Enable] = "true" },
                "running"));

            bool disabled = policy.ShouldMonitor(new ContainerRef(
                "2",
                "web",
                "my/web:latest",
                "img2",
                new Dictionary<string, string>(),
                "running"));

            Assert.True(enabled);
            Assert.False(disabled);
        }

        [Fact]
        public void ExcludesDisabledByLabelWhenLabelEnableIsFalse()
        {
            UpdaterOptions options = new() { LabelEnable = false };
            ContainerSelectionPolicy policy = new(options);

            bool shouldMonitor = policy.ShouldMonitor(new ContainerRef(
                "1",
                "api",
                "my/api:latest",
                "img1",
                new Dictionary<string, string> { [LabelNames.Enable] = "false" },
                "running"));

            Assert.False(shouldMonitor);
        }

        [Fact]
        public void ExcludesStoppedWhenIncludeStoppedIsFalse()
        {
            UpdaterOptions options = new() { IncludeStopped = false };
            ContainerSelectionPolicy policy = new(options);

            bool shouldMonitor = policy.ShouldMonitor(new ContainerRef(
                "1",
                "api",
                "my/api:latest",
                "img1",
                new Dictionary<string, string>(),
                "exited"));

            Assert.False(shouldMonitor);
        }

        [Fact]
        public void IncludesStoppedWhenIncludeStoppedIsTrue()
        {
            UpdaterOptions options = new() { IncludeStopped = true };
            ContainerSelectionPolicy policy = new(options);

            bool shouldMonitor = policy.ShouldMonitor(new ContainerRef(
                "1",
                "api",
                "my/api:latest",
                "img1",
                new Dictionary<string, string>(),
                "created"));

            Assert.True(shouldMonitor);
        }

        [Fact]
        public void ExcludesWhenTargetContainerListDoesNotContainName()
        {
            UpdaterOptions options = new()
            {
                TargetContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "web" }
            };

            ContainerSelectionPolicy policy = new(options);

            bool shouldMonitor = policy.ShouldMonitor(new ContainerRef(
                "1",
                "api",
                "my/api:latest",
                "img1",
                new Dictionary<string, string>(),
                "running"));

            Assert.False(shouldMonitor);
        }
    }
}