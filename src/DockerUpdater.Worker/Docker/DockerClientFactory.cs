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

            ConfigureTlsEnvironment();
            DockerClientConfiguration configuration = new(endpoint);
            return configuration.CreateClient();
        }

        private void ConfigureTlsEnvironment()
        {
            if (options.DockerTlsVerify)
            {
                Environment.SetEnvironmentVariable("DOCKER_TLS_VERIFY", "1");
                logger.LogInformation("DOCKER_TLS_VERIFY enabled");

                if (!string.IsNullOrWhiteSpace(options.DockerCertPath))
                {
                    Environment.SetEnvironmentVariable("DOCKER_CERT_PATH", options.DockerCertPath);
                    logger.LogInformation("Using TLS certificates from {CertPath}", options.DockerCertPath);
                }
                else
                {
                    logger.LogWarning("DOCKER_TLS_VERIFY is enabled but DOCKER_CERT_PATH is not set. Will use default certificate location.");
                }
            }
            else
            {
                // Ensure DOCKER_TLS_VERIFY is not set if disabled
                Environment.SetEnvironmentVariable("DOCKER_TLS_VERIFY", null);
            }
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