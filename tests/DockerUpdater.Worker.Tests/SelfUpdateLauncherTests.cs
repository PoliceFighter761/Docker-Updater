using DockerUpdater.Worker.Options;
using DockerUpdater.Worker.Update;

namespace DockerUpdater.Worker.Tests
{
    [Collection("Environment Variables")]
    public sealed class SelfUpdateLauncherTests : IDisposable
    {
        private readonly string? _originalHostname;

        public SelfUpdateLauncherTests()
        {
            _originalHostname = Environment.GetEnvironmentVariable("HOSTNAME");
        }

        [Fact]
        public void IsSelf_ReturnsFalse_WhenHostnameNotSet()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);

            Assert.False(SelfUpdateLauncher.IsSelf("abc123def456"));
        }

        [Fact]
        public void IsSelf_ReturnsTrue_WhenContainerIdStartsWithHostname()
        {
            // Docker sets HOSTNAME to the short (12-char) container ID
            Environment.SetEnvironmentVariable("HOSTNAME", "abc123def456");

            Assert.True(SelfUpdateLauncher.IsSelf("abc123def456789abcdef0123456789abcdef012345678"));
        }

        [Fact]
        public void IsSelf_ReturnsTrue_WhenHostnameStartsWithContainerId()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "abc123def456");

            Assert.True(SelfUpdateLauncher.IsSelf("abc123def456"));
        }

        [Fact]
        public void IsSelf_ReturnsFalse_WhenIdDoesNotMatch()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "abc123def456");

            Assert.False(SelfUpdateLauncher.IsSelf("zzz999aaa111"));
        }

        [Fact]
        public void IsSelf_IsCaseInsensitive()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "ABC123DEF456");

            Assert.True(SelfUpdateLauncher.IsSelf("abc123def456789abcdef0123456789abcdef012345678"));
        }

        [Fact]
        public void GetOwnContainerId_ReturnsHostname()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", "deadbeef1234");

            Assert.Equal("deadbeef1234", SelfUpdateLauncher.GetOwnContainerId());
        }

        [Fact]
        public void GetOwnContainerId_ReturnsNull_WhenNotSet()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", null);

            Assert.Null(SelfUpdateLauncher.GetOwnContainerId());
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("HOSTNAME", _originalHostname);
        }
    }
}
