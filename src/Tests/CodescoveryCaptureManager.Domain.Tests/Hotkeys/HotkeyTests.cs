using CodescoveryCaptureManager.Domain.Hotkeys;
using Xunit;

namespace CodescoveryCaptureManager.Domain.Tests.Hotkeys
{
    public sealed class HotkeyTests
    {
        [Theory]
        [InlineData("Ctrl+Alt+P", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x50)]
        [InlineData("ctrl + shift + f9", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x78)]
        [InlineData("Win+Alt+0", HotkeyModifiers.Windows | HotkeyModifiers.Alt, 0x30)]
        [InlineData("Pause", HotkeyModifiers.None, 0x13)]
        [InlineData("F13", HotkeyModifiers.None, 0x7C)]
        public void TryParse_ValidText_ReturnsModifiersAndKey(string text, HotkeyModifiers modifiers, int virtualKey)
        {
            var parsed = Hotkey.TryParse(text, out var hotkey);

            Assert.True(parsed);
            Assert.Equal(new Hotkey(modifiers, virtualKey), hotkey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("P")]
        [InlineData("Shift+P")]
        [InlineData("F5")]
        [InlineData("Ctrl+Alt")]
        [InlineData("Ctrl+Ctrl+P")]
        [InlineData("Hyper+P")]
        [InlineData("Ctrl+Enter")]
        public void TryParse_UnusableText_ReturnsFalse(string text)
        {
            Assert.False(Hotkey.TryParse(text, out _));
        }

        [Fact]
        public void ToString_ModifiersInFixedOrder_RoundTripsThroughTryParse()
        {
            var hotkey = new Hotkey(HotkeyModifiers.Shift | HotkeyModifiers.Windows | HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x7B);

            var text = hotkey.ToString();

            Assert.Equal("Ctrl+Alt+Shift+Win+F12", text);
            Assert.True(Hotkey.TryParse(text, out var parsed));
            Assert.Equal(hotkey, parsed);
        }

        [Fact]
        public void IsValid_KeyOutsideSupportedSet_IsFalse()
        {
            const int virtualKeyEnter = 0x0D;

            Assert.False(new Hotkey(HotkeyModifiers.Control, virtualKeyEnter).IsValid);
        }
    }
}
