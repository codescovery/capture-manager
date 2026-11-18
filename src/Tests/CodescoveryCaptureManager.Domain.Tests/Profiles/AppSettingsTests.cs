using CodescoveryCaptureManager.Domain.Profiles;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Profiles
{
    public sealed class AppSettingsTests
    {
        [Fact]
        public void Default_ExitOnClose_IsFalse_SoClosingHidesToTray()
        {
            Assert.False(new AppSettings().ExitOnClose);
        }
    }
}
