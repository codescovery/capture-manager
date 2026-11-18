using CodescoveryCaptureManager.Domain.Streaming;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Streaming
{
    public sealed class AnnexBTests
    {
        [Fact]
        public void ContainsKeyFrame_IdrSliceWithLongStartCode_ReturnsTrue()
        {
            var accessUnit = new byte[] { 0, 0, 0, 1, 0x09, 0x10, 0, 0, 0, 1, 0x67, 0x4D, 0, 0, 1, 0x65, 0x88 };

            Assert.True(AnnexB.ContainsKeyFrame(accessUnit));
        }

        [Fact]
        public void ContainsKeyFrame_NonIdrSlice_ReturnsFalse()
        {
            var accessUnit = new byte[] { 0, 0, 0, 1, 0x09, 0x30, 0, 0, 0, 1, 0x61, 0xE0 };

            Assert.False(AnnexB.ContainsKeyFrame(accessUnit));
        }

        [Fact]
        public void ContainsKeyFrame_EmptyData_ReturnsFalse()
        {
            Assert.False(AnnexB.ContainsKeyFrame(new byte[0]));
        }
    }
}
