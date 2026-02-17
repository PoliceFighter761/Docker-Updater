using Docker.DotNet;
using Docker.DotNet.Models;
using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Docker
{
    public sealed class ContainerRecreator(ILogger<ContainerRecreator> logger)
    {
        public async Task RecreateAsync(
            DockerClient dockerClient,
            string containerId,
            string imageName,
            TimeSpan stopTimeout,
            bool reviveStopped,
            CancellationToken cancellationToken)
        {
            ContainerInspectResponse inspect = await dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);
            string originalName = UpdaterOptions.NormalizeContainerName(inspect.Name);
            bool wasRunning = inspect.State?.Running ?? false;

            if (wasRunning)
            {
                await dockerClient.Containers.StopContainerAsync(
                    containerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = (uint)Math.Ceiling(stopTimeout.TotalSeconds) },
                    cancellationToken);
            }

            await dockerClient.Containers.RemoveContainerAsync(
                containerId,
                new ContainerRemoveParameters
                {
                    Force = true,
                    RemoveVolumes = false
                },
                cancellationToken);

            CreateContainerParameters createParams = new()
            {
                Name = originalName,
                Image = imageName,
                Env = inspect.Config?.Env,
                Cmd = inspect.Config?.Cmd,
                Entrypoint = inspect.Config?.Entrypoint,
                WorkingDir = inspect.Config?.WorkingDir,
                Labels = inspect.Config?.Labels,
                ExposedPorts = inspect.Config?.ExposedPorts,
                HostConfig = inspect.HostConfig,
                NetworkingConfig = inspect.NetworkSettings?.Networks is null
                    ? null
                    : new NetworkingConfig
                    {
                        EndpointsConfig = inspect.NetworkSettings.Networks.ToDictionary(
                            network => network.Key,
                            network => new EndpointSettings
                            {
                                Aliases = network.Value.Aliases,
                                NetworkID = network.Value.NetworkID,
                                IPAddress = network.Value.IPAddress,
                                IPAMConfig = network.Value.IPAMConfig,
                                Gateway = network.Value.Gateway,
                                GlobalIPv6Address = network.Value.GlobalIPv6Address,
                                IPv6Gateway = network.Value.IPv6Gateway,
                                MacAddress = network.Value.MacAddress
                            })
                    }
            };

            CreateContainerResponse created = await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);

            if (wasRunning || reviveStopped)
            {
                bool started = await dockerClient.Containers.StartContainerAsync(
                    created.ID,
                    new ContainerStartParameters(),
                    cancellationToken);

                if (!started)
                {
                    throw new InvalidOperationException($"Failed to start recreated container '{originalName}'.");
                }
            }

            logger.LogInformation("Recreated container {Name} using image {Image}", originalName, imageName);
        }
    }
}