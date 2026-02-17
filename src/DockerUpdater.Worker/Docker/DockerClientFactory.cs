using Docker.DotNet;
using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Docker
{
    public sealed class DockerClientFactory(UpdaterOptions options, ILogger<DockerClientFactory> logger) : IDockerClientFactory
    {
        public DockerClient CreateClient()
        {
            Uri endpoint = ResolveEndpoint();
            logger.LogInformation("Connecting to Docker host: {Endpoint}", endpoint);

            DockerClientConfiguration configuration = new(endpoint);
            return configuration.CreateClient();
        }

        private Uri ResolveEndpoint()
        {
            if (options.DockerHost is not null)
            {
                return options.DockerHost;
            }

            if (OperatingSystem.IsWindows())
            {
                return new Uri("npipe://./pipe/docker_engine");
            }

            return new Uri("unix:///var/run/docker.sock");
        }
    }
}