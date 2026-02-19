using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerUpdater.Worker.Docker
{
    public sealed class RecoveryProcessor(
        RecoveryJournal journal,
        ILogger<RecoveryProcessor> logger)
    {
        public async Task RecoverAsync(DockerClient dockerClient, CancellationToken cancellationToken)
        {
            IReadOnlyList<RecoveryEntry> entries = journal.GetPendingEntries();
            if (entries.Count == 0)
            {
                return;
            }

            logger.LogWarning(
                "Found {Count} interrupted recreation(s) from a previous session. Attempting recovery",
                entries.Count);

            foreach (RecoveryEntry entry in entries)
            {
                try
                {
                    await RecoverEntryAsync(dockerClient, entry, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Recovery failed for container {Name}. Manual intervention may be needed. "
                        + "The backup container may still exist as '{BackupName}'",
                        entry.ContainerName,
                        entry.ContainerName + ContainerRecreator.BackupSuffix);
                    journal.Complete(entry.ContainerName);
                }
            }
        }

        private async Task RecoverEntryAsync(
            DockerClient dockerClient,
            RecoveryEntry entry,
            CancellationToken cancellationToken)
        {
            logger.LogInformation(
                "Recovering container {Name} (phase={Phase}, backup={BackupId}, new={NewId})",
                entry.ContainerName,
                entry.Phase,
                entry.BackupContainerId[..Math.Min(12, entry.BackupContainerId.Length)],
                entry.NewContainerId?[..Math.Min(12, entry.NewContainerId.Length)] ?? "none");

            bool backupExists = await ContainerExistsAsync(dockerClient, entry.BackupContainerId, cancellationToken);
            (bool newExists, bool newRunning) = await InspectNewContainerStateAsync(dockerClient, entry.NewContainerId, cancellationToken);

            if (!backupExists && !newExists)
            {
                logger.LogWarning(
                    "Recovery: neither backup nor replacement container found for {Name}. Clearing journal entry",
                    entry.ContainerName);
                journal.Complete(entry.ContainerName);
                return;
            }

            if (newRunning)
            {
                logger.LogInformation("Recovery: replacement for {Name} is already running. Cleaning up backup", entry.ContainerName);
                await CleanupBackupAsync(dockerClient, entry, cancellationToken);
                return;
            }

            if (newExists)
            {
                bool shouldStart = entry.WasRunning || entry.ReviveStopped;
                if (shouldStart)
                {
                    bool started = await TryStartNewContainerAsync(dockerClient, entry, cancellationToken);
                    if (started)
                    {
                        await CleanupBackupAsync(dockerClient, entry, cancellationToken);
                        return;
                    }

                    logger.LogWarning("Recovery: could not start replacement for {Name}. Rolling back", entry.ContainerName);
                }
                else
                {
                    logger.LogInformation("Recovery: replacement for {Name} exists and wasn't running before. Cleaning up", entry.ContainerName);
                    await CleanupBackupAsync(dockerClient, entry, cancellationToken);
                    return;
                }
            }

            await RollbackAsync(dockerClient, entry, backupExists, newExists, cancellationToken);
        }

        private async Task<bool> TryStartNewContainerAsync(
            DockerClient dockerClient,
            RecoveryEntry entry,
            CancellationToken cancellationToken)
        {
            try
            {
                bool started = await dockerClient.Containers.StartContainerAsync(
                    entry.NewContainerId!,
                    new ContainerStartParameters(),
                    cancellationToken);

                if (started)
                {
                    journal.RecordStarted(entry.ContainerName);
                    logger.LogInformation("Recovery: started replacement container for {Name}", entry.ContainerName);
                }

                return started;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Recovery: failed to start replacement for {Name}", entry.ContainerName);
                return false;
            }
        }

        private async Task CleanupBackupAsync(
            DockerClient dockerClient,
            RecoveryEntry entry,
            CancellationToken cancellationToken)
        {
            bool backupExists = await ContainerExistsAsync(dockerClient, entry.BackupContainerId, cancellationToken);
            if (backupExists)
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        entry.BackupContainerId,
                        new ContainerRemoveParameters { Force = true, RemoveVolumes = false },
                        cancellationToken);
                    logger.LogInformation("Recovery: removed backup container for {Name}", entry.ContainerName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Recovery: could not remove backup for {Name}. It is safe to remove the '{BackupName}' container manually",
                        entry.ContainerName,
                        entry.ContainerName + ContainerRecreator.BackupSuffix);
                }
            }

            journal.Complete(entry.ContainerName);
            logger.LogInformation("Recovery: completed for container {Name}", entry.ContainerName);
        }

        private async Task RollbackAsync(
            DockerClient dockerClient,
            RecoveryEntry entry,
            bool backupExists,
            bool newExists,
            CancellationToken cancellationToken)
        {
            if (newExists && entry.NewContainerId is not null)
            {
                try
                {
                    await dockerClient.Containers.RemoveContainerAsync(
                        entry.NewContainerId,
                        new ContainerRemoveParameters { Force = true, RemoveVolumes = false },
                        cancellationToken);
                    logger.LogInformation("Recovery: removed failed replacement for {Name}", entry.ContainerName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Recovery: could not remove failed replacement for {Name}", entry.ContainerName);
                }
            }

            if (backupExists)
            {
                try
                {
                    await dockerClient.Containers.RenameContainerAsync(
                        entry.BackupContainerId,
                        new ContainerRenameParameters { NewName = entry.ContainerName },
                        cancellationToken);
                    logger.LogInformation("Recovery: renamed backup → {Name}", entry.ContainerName);

                    if (entry.WasRunning)
                    {
                        await dockerClient.Containers.StartContainerAsync(
                            entry.BackupContainerId,
                            new ContainerStartParameters(),
                            cancellationToken);
                        logger.LogInformation("Recovery: restarted original container {Name}", entry.ContainerName);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Recovery: rollback failed for {Name}. The backup container '{BackupName}' may need manual restoration",
                        entry.ContainerName,
                        entry.ContainerName + ContainerRecreator.BackupSuffix);
                }
            }

            journal.Complete(entry.ContainerName);
            logger.LogInformation("Recovery: rolled back container {Name} to previous state", entry.ContainerName);
        }

        private static async Task<bool> ContainerExistsAsync(
            DockerClient dockerClient,
            string containerId,
            CancellationToken cancellationToken)
        {
            try
            {
                await dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);
                return true;
            }
            catch (DockerContainerNotFoundException)
            {
                return false;
            }
        }

        private static async Task<(bool Exists, bool Running)> InspectNewContainerStateAsync(
            DockerClient dockerClient,
            string? containerId,
            CancellationToken cancellationToken)
        {
            if (containerId is null)
            {
                return (false, false);
            }

            try
            {
                ContainerInspectResponse inspect = await dockerClient.Containers.InspectContainerAsync(containerId, cancellationToken);
                bool running = inspect.State?.Running ?? false;
                return (true, running);
            }
            catch (DockerContainerNotFoundException)
            {
                return (false, false);
            }
        }
    }
}
