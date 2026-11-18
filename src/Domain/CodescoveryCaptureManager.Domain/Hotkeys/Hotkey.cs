using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CodescoveryCaptureManager.Domain.Hotkeys
{
    /// <summary>Values match the Win32 MOD_* flags of RegisterHotKey.</summary>
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8,
    }

    /// <summary>
    /// A system-wide key combination, stored as text ("Ctrl+Alt+P"). Keys: letters, digits, F1 to F24 and Pause.
    /// </summary>
    public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
    {
        private const int VirtualKeyPause = 0x13;
        private const int VirtualKeyF1 = 0x70;
        private const int VirtualKeyF12 = 0x7B;
        private const int VirtualKeyF24 = 0x87;
        private const char Separator = '+';

        private static readonly (HotkeyModifiers Modifier, string Name)[] ModifierNames =
        {
            (HotkeyModifiers.Control, "Ctrl"),
            (HotkeyModifiers.Alt, "Alt"),
            (HotkeyModifiers.Shift, "Shift"),
            (HotkeyModifiers.Windows, "Win"),
        };

        /// <summary>
        /// Usable as a global hotkey without taking a key people type: a supported key together with Ctrl, Alt or Win.
        /// Pause and F13 to F24 may stand alone. Shift alone is not enough (Shift+P is a capital letter).
        /// </summary>
        public bool IsValid
            => KeyName(VirtualKey) != null && (HasStrongModifier || StandsAlone(VirtualKey));

        private bool HasStrongModifier
            => (Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows)) != 0;

        public override string ToString()
        {
            var modifiers = Modifiers;
            var parts = ModifierNames.Where(entry => modifiers.HasFlag(entry.Modifier)).Select(entry => entry.Name).ToList();
            parts.Add(KeyName(VirtualKey) ?? $"0x{VirtualKey:X2}");
            return string.Join(Separator, parts);
        }

        public static bool TryParse(string text, out Hotkey hotkey)
        {
            hotkey = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Split(Separator, StringSplitOptions.TrimEntries);
            var modifiers = HotkeyModifiers.None;
            foreach (var part in parts.Take(parts.Length - 1))
            {
                if (!TryParseModifier(part, out var modifier) || modifiers.HasFlag(modifier)) return false;
                modifiers |= modifier;
            }

            if (!TryParseKey(parts[^1], out var virtualKey)) return false;
            hotkey = new Hotkey(modifiers, virtualKey);
            return hotkey.IsValid;
        }

        private static bool TryParseModifier(string name, out HotkeyModifiers modifier)
        {
            var match = ModifierNames.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
            modifier = match.Modifier;
            return match.Name != null;
        }

        private static bool TryParseKey(string name, out int virtualKey)
        {
            virtualKey = SupportedKeys().FirstOrDefault(key => string.Equals(KeyName(key), name, StringComparison.OrdinalIgnoreCase));
            return virtualKey != 0;
        }

        private static IEnumerable<int> SupportedKeys()
            => Enumerable.Range('0', 10).Concat(Enumerable.Range('A', 26)).Concat(Enumerable.Range(VirtualKeyF1, 24)).Append(VirtualKeyPause);

        private static bool StandsAlone(int virtualKey)
            => virtualKey == VirtualKeyPause || (virtualKey > VirtualKeyF12 && virtualKey <= VirtualKeyF24);

        private static string KeyName(int virtualKey)
        {
            if (virtualKey is >= '0' and <= '9' or >= 'A' and <= 'Z') return ((char)virtualKey).ToString();
            if (virtualKey is >= VirtualKeyF1 and <= VirtualKeyF24)
                return "F" + (virtualKey - VirtualKeyF1 + 1).ToString(CultureInfo.InvariantCulture);
            return virtualKey == VirtualKeyPause ? "Pause" : null;
        }
    }
}
