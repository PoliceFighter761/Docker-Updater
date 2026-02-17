using System.Text;
using DockerUpdater.Worker.Docker;
using Microsoft.Extensions.Logging.Abstractions;

namespace DockerUpdater.Worker.Tests
{
  [Collection("Environment Variables")]
    public sealed class RegistryAuthResolverTests : IDisposable
    {
        private readonly string? _originalDockerConfig;

        public RegistryAuthResolverTests()
        {
            _originalDockerConfig = Environment.GetEnvironmentVariable("DOCKER_CONFIG");
        }

        [Fact]
        public void ResolveForImage_ReturnsNull_WhenHostConfigMissing()
        {
            Environment.SetEnvironmentVariable("DOCKER_CONFIG", Path.Combine(Path.GetTempPath(), $"docker-updater-missing-{Guid.NewGuid():N}"));

            RegistryAuthResolver resolver = new(NullLogger<RegistryAuthResolver>.Instance);

            global::Docker.DotNet.Models.AuthConfig? config = resolver.ResolveForImage("registry.example.com/team/app:latest");

            Assert.Null(config);
        }

        [Fact]
        public void ResolveForImage_UsesConfigFile_WhenInlineConfigMissing()
        {
            string dockerConfigDirectory = Path.Combine(Path.GetTempPath(), $"docker-updater-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dockerConfigDirectory);

            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes("bob:token"));
            File.WriteAllText(
                Path.Combine(dockerConfigDirectory, "config.json"),
                $$"""
                {
                  "auths": {
                    "ghcr.io": {
                      "auth": "{{auth}}"
                    }
                  }
                }
                """);

            Environment.SetEnvironmentVariable("DOCKER_CONFIG", dockerConfigDirectory);

            RegistryAuthResolver resolver = new(NullLogger<RegistryAuthResolver>.Instance);

            global::Docker.DotNet.Models.AuthConfig? config = resolver.ResolveForImage("ghcr.io/org/service:latest");

            Assert.NotNull(config);
            Assert.Equal("ghcr.io", config!.ServerAddress);
            Assert.Equal("bob", config.Username);
            Assert.Equal("token", config.Password);

            Directory.Delete(dockerConfigDirectory, recursive: true);
        }

        [Theory]
        [InlineData("nginx:latest", "index.docker.io")]
        [InlineData("library/redis:7", "index.docker.io")]
        [InlineData("ghcr.io/org/app:1", "ghcr.io")]
        [InlineData("localhost:5000/team/app:2", "localhost:5000")]
        public void ExtractRegistry_ParsesExpectedRegistry(string image, string expectedRegistry)
        {
            string registry = RegistryAuthResolver.ExtractRegistry(image);

            Assert.Equal(expectedRegistry, registry);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("DOCKER_CONFIG", _originalDockerConfig);
        }
    }
}