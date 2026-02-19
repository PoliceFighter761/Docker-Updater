using Docker.DotNet;
using Docker.DotNet.Models;
using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Docker
{
    public sealed class ContainerRecreator(ILogger<ContainerRecreator> logger, RecoveryJournal journal)
    {
        internal const string BackupSuffix = "__pre_update";

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

            string backupName = originalName + BackupSuffix;
            await dockerClient.Containers.RenameContainerAsync(
                containerId,
                new ContainerRenameParameters { NewName = backupName },
                cancellationToken);

            journal.BeginRecreation(new RecoveryEntry
            {
                ContainerName = originalName,
                ImageName = imageName,
                BackupContainerId = containerId,
                WasRunning = wasRunning,
                ReviveStopped = reviveStopped,
                Phase = RecoveryPhase.Renamed
            });

            logger.LogInformation("Renamed container {Name} → {BackupName} before recreation", originalName, backupName);

            CreateContainerResponse? created = null;
            try
            {
                CreateContainerParameters createParams = BuildCreateParameters(inspect, originalName, imageName);
                created = await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);
                journal.RecordCreated(originalName, created.ID);

                if (wasRunning || reviveStopped)
                {
                    bool started = await dockerClient.Containers.StartContainerAsync(
                        created.ID,
                        new ContainerStartParameters(),
                        cancellationToken);

                    if (!started)
                    {
                        throw new InvalidOperationException($"Docker engine reported that container '{originalName}' did not start.");
                    }

                    journal.RecordStarted(originalName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to create/start replacement for {Name}. Rolling back", originalName);
                await RollbackAsync(dockerClient, containerId, created?.ID, originalName, backupName, wasRunning);
                journal.Complete(originalName);
                throw;
            }

            await TryRemoveBackupAsync(dockerClient, containerId, backupName);
            journal.Complete(originalName);

            logger.LogInformation("Recreated container {Name} using image {Image}", originalName, imageName);
        }

        private async Task RollbackAsync(
            DockerClient dockerClient,
            string oldContainerId,
            string? newContainerId,
            string originalName,
            string backupName,
            bool wasRunning)
        {
            if (newContainerId is not null)
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        newContainerId,
                        new ContainerRemoveParameters { Force = true, RemoveVolumes = false },
                        CancellationToken.None);
                    logger.LogInformation("Removed partially-created container {Id} during rollback", newContainerId[..12]);
                }
                catch (Exception removeEx)
                {
                    logger.LogWarning(removeEx, "Rollback: could not remove partial container {Id}", newContainerId[..12]);
                }
            }

            try
            {
                await dockerClient.Containers.RenameContainerAsync(
                    oldContainerId,
                    new ContainerRenameParameters { NewName = originalName },
                    CancellationToken.None);
                logger.LogInformation("Rollback: renamed {BackupName} → {Name}", backupName, originalName);
            }
            catch (Exception renameEx)
            {
                logger.LogError(renameEx,
                    "Rollback: could not rename {BackupName} back to {Name}. "
                    + "The original container still exists as '{BackupName}' and can be restored manually",
                    backupName, originalName, backupName);
                return; // Can't restart if rename failed
            }

            if (wasRunning)
            {
                try
                {
                    await dockerClient.Containers.StartContainerAsync(
                        oldContainerId,
                        new ContainerStartParameters(),
                        CancellationToken.None);
                    logger.LogInformation("Rollback: restarted original container {Name}", originalName);
                }
                catch (Exception startEx)
                {
                    logger.LogError(startEx,
                        "Rollback: could not restart container {Name}. It exists but is stopped", originalName);
                }
            }
        }

        private async Task TryRemoveBackupAsync(DockerClient dockerClient, string oldContainerId, string backupName)
        {
            try
            {
                await dockerClient.Containers.RemoveContainerAsync(
                    oldContainerId,
                    new ContainerRemoveParameters { Force = true, RemoveVolumes = false },
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not remove backup container '{BackupName}'. It is safe to remove manually",
                    backupName);
            }
        }

        internal static CreateContainerParameters BuildCreateParameters(
            ContainerInspectResponse inspect,
            string containerName,
            string imageName)
        {
            return new CreateContainerParameters
            {
                Name = containerName,
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
        }
    }
}