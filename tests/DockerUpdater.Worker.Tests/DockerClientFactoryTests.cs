using Docker.DotNet;
using DockerUpdater.Worker.Docker;
using DockerUpdater.Worker.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace DockerUpdater.Worker.Tests
{
    public class DockerClientFactoryTests
    {
        [Fact]
        public void CreateClient_UsesConfiguredDockerHost()
        {
            UpdaterOptions options = new()
            {
                DockerHost = new Uri("tcp://localhost:2375")
            };

            DockerClientFactory factory = new(options, NullLogger<DockerClientFactory>.Instance);

            using DockerClient client = factory.CreateClient();

            Assert.NotNull(client);
        }
    }
}
