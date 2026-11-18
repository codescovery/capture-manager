using CodescoveryCaptureManager.Domain.Diagnostics;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Diagnostics
{
    public sealed class AppVersionTests
    {
        [Theory]
        [InlineData("1.4.0+3f2a9c1", "1.4.0")]
        [InlineData("1.4.0", "1.4.0")]
        [InlineData("2.0.0-beta.1+abc", "2.0.0-beta.1")]
        public void Trim_InformationalVersion_DropsBuildMetadata(string informationalVersion, string expected)
        {
            Assert.Equal(expected, AppVersion.Trim(informationalVersion));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Trim_NoVersion_ReturnsUnknown(string informationalVersion)
        {
            Assert.Equal(AppVersion.Unknown, AppVersion.Trim(informationalVersion));
        }

        [Fact]
        public void Read_NoAssembly_ReturnsUnknown()
        {
            Assert.Equal(AppVersion.Unknown, AppVersion.Read(null));
        }
    }
}
