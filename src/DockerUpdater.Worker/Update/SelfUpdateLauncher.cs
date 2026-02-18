using Docker.DotNet;
using Docker.DotNet.Models;
using DockerUpdater.Worker.Docker;
using DockerUpdater.Worker.Options;

namespace DockerUpdater.Worker.Update
{
    public sealed class SelfUpdateLauncher(
        IDockerClientFactory dockerClientFactory,
        UpdaterOptions options,
        ILogger<SelfUpdateLauncher> logger)
    {
        private const string HelperPrefix = "docker-updater-self-update-";

        public static string? GetOwnContainerId()
        {
            return Environment.GetEnvironmentVariable("HOSTNAME");
        }

        public static bool IsSelf(string containerId)
        {
            string? ownId = GetOwnContainerId();
            if (string.IsNullOrEmpty(ownId))
            {
                return false;
            }

            return containerId.StartsWith(ownId, StringComparison.OrdinalIgnoreCase)
                || ownId.StartsWith(containerId, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> LaunchHelperAsync(
            ContainerRef selfContainer,
            string newImage,
            CancellationToken cancellationToken)
        {
            using DockerClient dockerClient = dockerClientFactory.CreateClient();

            ContainerInspectResponse inspect =
                await dockerClient.Containers.InspectContainerAsync(selfContainer.Id, cancellationToken);

            string helperName = HelperPrefix + Guid.NewGuid().ToString("N")[..8];
            string updaterContainerName = UpdaterOptions.NormalizeContainerName(inspect.Name);

            List<string> env =
            [
                $"{EnvNames.RunOnce}=true",
                $"{EnvNames.Containers}={updaterContainerName}",
                $"{EnvNames.LabelEnable}=false",
                // Propagate cleanup preference
                $"{EnvNames.Cleanup}={options.Cleanup}",
                $"{EnvNames.Timeout}={options.StopTimeout.TotalSeconds}s",
            ];

            PropagateEnv(env, inspect, EnvNames.DockerHost);
            PropagateEnv(env, inspect, EnvNames.DockerTlsVerify);
            PropagateEnv(env, inspect, EnvNames.DockerCertPath);
            PropagateEnv(env, inspect, "DOCKER_CONFIG");

            PropagateEnv(env, inspect, EnvNames.NotificationUrl);
            PropagateEnv(env, inspect, EnvNames.DiscordWebhookUrl);
            PropagateEnv(env, inspect, EnvNames.DiscordMessageTemplate);

            CreateContainerParameters createParams = new()
            {
                Name = helperName,
                Image = newImage,
                Env = env,
                HostConfig = new HostConfig
                {
                    AutoRemove = true,
                    Binds = inspect.HostConfig?.Binds,
                    NetworkMode = inspect.HostConfig?.NetworkMode,
                },
                Labels = new Dictionary<string, string>
                {
                    [LabelNames.Enable] = "false"
                }
            };

            CreateContainerResponse created =
                await dockerClient.Containers.CreateContainerAsync(createParams, cancellationToken);

            bool started = await dockerClient.Containers.StartContainerAsync(
                created.ID,
                new ContainerStartParameters(),
                cancellationToken);

            if (!started)
            {
                throw new InvalidOperationException(
                    $"Failed to start self-update helper container '{helperName}'.");
            }

            logger.LogInformation(
                "Launched self-update helper container {HelperName} ({HelperId}) to update {UpdaterContainer}",
                helperName, created.ID[..12], updaterContainerName);

            return created.ID;
        }

        private static void PropagateEnv(List<string> target, ContainerInspectResponse inspect, string key)
        {
            string prefix = key + "=";
            string? existing = inspect.Config?.Env?.FirstOrDefault(
                e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                target.Add(existing);
            }
        }
    }
}
