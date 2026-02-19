using Docker.DotNet.Models;
using DockerUpdater.Worker.Docker;

namespace DockerUpdater.Worker.Tests
{
    public sealed class ContainerRecreatorTests
    {
        [Fact]
        public void BackupSuffix_IsConsistent()
        {
            Assert.Equal("__pre_update", ContainerRecreator.BackupSuffix);
        }

        [Fact]
        public void BuildCreateParameters_CopiesIdentityFields()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "my-app", "my-app:v2");

            Assert.Equal("my-app", result.Name);
            Assert.Equal("my-app:v2", result.Image);
        }

        [Fact]
        public void BuildCreateParameters_CopiesConfigFields()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();
            inspect.Config = new Config
            {
                Env = ["FOO=bar", "BAZ=qux"],
                Cmd = ["--flag"],
                Entrypoint = ["/entrypoint.sh"],
                WorkingDir = "/app",
                Labels = new Dictionary<string, string> { ["tier"] = "frontend" },
                ExposedPorts = new Dictionary<string, EmptyStruct> { ["8080/tcp"] = default }
            };

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "web", "web:latest");

            Assert.Equal(inspect.Config.Env, result.Env);
            Assert.Equal(inspect.Config.Cmd, result.Cmd);
            Assert.Equal(inspect.Config.Entrypoint, result.Entrypoint);
            Assert.Equal("/app", result.WorkingDir);
            Assert.Equal("frontend", result.Labels["tier"]);
            Assert.True(result.ExposedPorts.ContainsKey("8080/tcp"));
        }

        [Fact]
        public void BuildCreateParameters_CopiesHostConfig()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();
            inspect.HostConfig = new HostConfig
            {
                Memory = 512 * 1024 * 1024,
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.Always }
            };

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "api", "api:v3");

            Assert.NotNull(result.HostConfig);
            Assert.Equal(512 * 1024 * 1024, result.HostConfig.Memory);
            Assert.Equal(RestartPolicyKind.Always, result.HostConfig.RestartPolicy.Name);
        }

        [Fact]
        public void BuildCreateParameters_CopiesNetworkingConfig()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();
            inspect.NetworkSettings = new NetworkSettings
            {
                Networks = new Dictionary<string, EndpointSettings>
                {
                    ["bridge"] = new()
                    {
                        NetworkID = "net-1",
                        IPAddress = "172.17.0.5",
                        Aliases = ["my-alias"]
                    }
                }
            };

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "svc", "svc:1");

            Assert.NotNull(result.NetworkingConfig);
            Assert.True(result.NetworkingConfig.EndpointsConfig.ContainsKey("bridge"));
            EndpointSettings ep = result.NetworkingConfig.EndpointsConfig["bridge"];
            Assert.Equal("net-1", ep.NetworkID);
            Assert.Equal("172.17.0.5", ep.IPAddress);
            Assert.Contains("my-alias", ep.Aliases);
        }

        [Fact]
        public void BuildCreateParameters_HandlesNullNetworkSettings()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();
            inspect.NetworkSettings = null;

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "svc", "svc:1");

            Assert.Null(result.NetworkingConfig);
        }

        [Fact]
        public void BuildCreateParameters_HandlesNullNetworks()
        {
            ContainerInspectResponse inspect = CreateInspectResponse();
            inspect.NetworkSettings = new NetworkSettings { Networks = null };

            CreateContainerParameters result = ContainerRecreator.BuildCreateParameters(inspect, "svc", "svc:1");

            Assert.Null(result.NetworkingConfig);
        }

        private static ContainerInspectResponse CreateInspectResponse()
        {
            return new ContainerInspectResponse
            {
                Name = "/my-app",
                Config = new Config(),
                HostConfig = new HostConfig(),
                State = new ContainerState { Running = true },
                NetworkSettings = new NetworkSettings()
            };
        }
    }
}
