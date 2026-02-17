using Docker.DotNet;

namespace DockerUpdater.Worker.Docker
{
    public interface IDockerClientFactory
    {
        DockerClient CreateClient();
    }
}