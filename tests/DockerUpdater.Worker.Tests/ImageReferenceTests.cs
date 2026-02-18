using DockerUpdater.Worker.Docker;

namespace DockerUpdater.Worker.Tests
{
    public sealed class ImageReferenceTests
    {
        [Theory]
        [InlineData("nginx", "index.docker.io", "nginx", "latest")]
        [InlineData("nginx:latest", "index.docker.io", "nginx", "latest")]
        [InlineData("library/redis:7", "index.docker.io", "library/redis", "7")]
        [InlineData("ghcr.io/org/app:v1", "ghcr.io", "ghcr.io/org/app", "v1")]
        [InlineData("my-registry.de:5000/image:v2", "my-registry.de:5000", "my-registry.de:5000/image", "v2")]
        [InlineData("localhost:5000/team/app:2", "localhost:5000", "localhost:5000/team/app", "2")]
        [InlineData("myuser/myimage", "index.docker.io", "myuser/myimage", "latest")]
        [InlineData("registry.example.com/app@sha256:abc123", "registry.example.com", "registry.example.com/app", "latest")]
        [InlineData("registry.example.com/app:v3@sha256:abc123", "registry.example.com", "registry.example.com/app", "v3")]
        public void Parse_ReturnsExpectedParts(string image, string expectedRegistry, string expectedRepository, string expectedTag)
        {
            ImageReference result = ImageReference.Parse(image);

            Assert.Equal(expectedRegistry, result.Registry);
            Assert.Equal(expectedRepository, result.Repository);
            Assert.Equal(expectedTag, result.Tag);
        }
    }
}
