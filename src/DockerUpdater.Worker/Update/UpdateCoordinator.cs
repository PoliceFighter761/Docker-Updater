using Docker.DotNet;
using Docker.DotNet.Models;
using DockerUpdater.Notifications;
using DockerUpdater.Shared;
using DockerUpdater.Worker.Docker;
using DockerUpdater.Worker.Options;

using static DockerUpdater.Worker.Docker.ImageReference;

namespace DockerUpdater.Worker.Update
{
    public sealed class UpdateCoordinator(
        IDockerClientFactory dockerClientFactory,
        RegistryAuthResolver registryAuthResolver,
        ContainerSelectionPolicy selectionPolicy,
        ContainerRecreator recreator,
        SelfUpdateLauncher selfUpdateLauncher,
        INotifier notifier,
        UpdaterOptions options,
        ILogger<UpdateCoordinator> logger)
    {
        public async Task<UpdateSessionResult> RunSessionAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;
            List<ContainerUpdateResult> results = [];

            using DockerClient dockerClient = dockerClientFactory.CreateClient();

            IList<ContainerListResponse> containers = await dockerClient.Containers.ListContainersAsync(
                new ContainersListParameters { All = options.IncludeStopped },
                cancellationToken);

            foreach (ContainerListResponse? container in containers)
            {
                ContainerRef containerRef = ToContainerRef(container);
                if (!selectionPolicy.ShouldMonitor(containerRef))
                {
                    results.Add(new ContainerUpdateResult(containerRef.Name, containerRef.Image, ContainerUpdateState.Skipped, "Excluded by selection policy"));
                    continue;
                }

                try
                {
                    ImageInspectResponse imageBefore = await dockerClient.Images.InspectImageAsync(containerRef.Image, cancellationToken);
                    await PullImageAsync(dockerClient, containerRef.Image, cancellationToken);
                    ImageInspectResponse imageAfter = await dockerClient.Images.InspectImageAsync(containerRef.Image, cancellationToken);

                    if (string.Equals(imageBefore.ID, imageAfter.ID, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(new ContainerUpdateResult(containerRef.Name, containerRef.Image, ContainerUpdateState.Fresh));
                        continue;
                    }

                    if (options.SelfUpdate && SelfUpdateLauncher.IsSelf(containerRef.Id))
                    {
                        await selfUpdateLauncher.LaunchHelperAsync(containerRef, containerRef.Image, cancellationToken);
                        results.Add(new ContainerUpdateResult(containerRef.Name, containerRef.Image, ContainerUpdateState.Updated, "Self-update delegated to helper container"));
                        continue;
                    }

                    await recreator.RecreateAsync(
                        dockerClient,
                        containerRef.Id,
                        containerRef.Image,
                        options.StopTimeout,
                        options.ReviveStopped,
                        cancellationToken);

                    if (options.Cleanup)
                    {
                        await TryDeleteImageAsync(dockerClient, imageBefore.ID, cancellationToken);
                    }

                    results.Add(new ContainerUpdateResult(containerRef.Name, containerRef.Image, ContainerUpdateState.Updated));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to update container {Container}", containerRef.Name);
                    results.Add(new ContainerUpdateResult(containerRef.Name, containerRef.Image, ContainerUpdateState.Failed, ex.Message));
                }
            }

            UpdateSessionResult session = new(startedAt, DateTimeOffset.UtcNow, results);
            await notifier.NotifySessionAsync(session, cancellationToken);
            return session;
        }

        private static ContainerRef ToContainerRef(ContainerListResponse container)
        {
            string name = container.Names?.FirstOrDefault() ?? container.ID[..12];

            return new ContainerRef(
                container.ID,
                UpdaterOptions.NormalizeContainerName(name),
                container.Image,
                container.ImageID,
                container.Labels is null
                    ? []
                    : new Dictionary<string, string>(container.Labels, StringComparer.OrdinalIgnoreCase),
                container.State ?? string.Empty);
        }

        private async Task PullImageAsync(DockerClient client, string imageName, CancellationToken cancellationToken)
        {
            ImageReference image = Parse(imageName);
            AuthConfig? authConfig = registryAuthResolver.ResolveForImage(imageName);

            await client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = image.Repository,
                    Tag = image.Tag
                },
                authConfig,
                new Progress<JSONMessage>(),
                cancellationToken);
        }

        private async Task TryDeleteImageAsync(DockerClient client, string imageId, CancellationToken cancellationToken)
        {
            try
            {
                await client.Images.DeleteImageAsync(
                    imageId,
                    new ImageDeleteParameters
                    {
                        Force = false
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Image cleanup failed for image {ImageId}", imageId);
            }
        }

    }
}