using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace BlogHelper9000.Tui.Input;

/// <summary>
/// Translates Terminal.Gui Key events to Neovim key notation strings.
/// See :help keycodes in Neovim for the notation format.
/// </summary>
public static class KeyTranslator
{
    private static readonly Dictionary<KeyCode, string> SpecialKeys = new()
    {
        [KeyCode.Enter] = "<CR>",
        [KeyCode.Esc] = "<Esc>",
        [KeyCode.Backspace] = "<BS>",
        [KeyCode.Tab] = "<Tab>",
        [KeyCode.Space] = "<Space>",
        [KeyCode.Delete] = "<Del>",
        [KeyCode.Insert] = "<Insert>",
        [KeyCode.Home] = "<Home>",
        [KeyCode.End] = "<End>",
        [KeyCode.PageUp] = "<PageUp>",
        [KeyCode.PageDown] = "<PageDown>",
        [KeyCode.CursorUp] = "<Up>",
        [KeyCode.CursorDown] = "<Down>",
        [KeyCode.CursorLeft] = "<Left>",
        [KeyCode.CursorRight] = "<Right>",
        [KeyCode.F1] = "<F1>",
        [KeyCode.F2] = "<F2>",
        [KeyCode.F3] = "<F3>",
        [KeyCode.F4] = "<F4>",
        [KeyCode.F5] = "<F5>",
        [KeyCode.F6] = "<F6>",
        [KeyCode.F7] = "<F7>",
        [KeyCode.F8] = "<F8>",
        [KeyCode.F9] = "<F9>",
        [KeyCode.F10] = "<F10>",
        [KeyCode.F11] = "<F11>",
        [KeyCode.F12] = "<F12>",
    };

    /// <summary>
    /// Translates a Terminal.Gui Key to the Neovim input notation.
    /// Returns null if the key cannot be translated.
    /// </summary>
    public static string? ToNvimNotation(Key key)
    {
        var code = key.KeyCode;
        var isCtrl = key.IsCtrl;
        var isAlt = key.IsAlt;
        var isShift = key.IsShift;

        // Strip modifier masks from the key code to get the base key
        var baseCode = code & ~(KeyCode.CtrlMask | KeyCode.AltMask | KeyCode.ShiftMask);

        // Check for special keys
        if (SpecialKeys.TryGetValue(baseCode, out var special))
        {
            return WrapModifiers(special[1..^1], isCtrl, isAlt, isShift);
        }

        // Printable characters (A-Z, 0-9, etc.)
        if (key.IsKeyCodeAtoZ)
        {
            var ch = (char)baseCode;

            if (isCtrl)
            {
                return $"<C-{char.ToLower(ch)}>";
            }

            if (isAlt && isShift)
            {
                return $"<M-{char.ToUpper(ch)}>";
            }

            if (isAlt)
            {
                return $"<M-{char.ToLower(ch)}>";
            }

            // For regular letters, Shift produces uppercase
            return isShift ? char.ToUpper(ch).ToString() : char.ToLower(ch).ToString();
        }

        // Numbers and other printable characters
        if (baseCode >= KeyCode.D0 && baseCode <= KeyCode.D9)
        {
            var digit = (char)('0' + (baseCode - KeyCode.D0));
            if (isCtrl) return $"<C-{digit}>";
            if (isAlt) return $"<M-{digit}>";
            return digit.ToString();
        }

        // Try to get it as a rune for remaining printable characters
        var rune = key.AsRune;
        if (rune.Value > 0 && rune.Value < 128)
        {
            var c = (char)rune.Value;
            if (isCtrl) return $"<C-{c}>";
            if (isAlt) return $"<M-{c}>";
            return c.ToString();
        }

        // Multi-byte unicode
        if (rune.Value >= 128)
        {
            return rune.ToString();
        }

        return null;
    }

    private static string WrapModifiers(string keyName, bool ctrl, bool alt, bool shift)
    {
        if (!ctrl && !alt && !shift)
            return $"<{keyName}>";

        var mods = "";
        if (shift) mods += "S-";
        if (ctrl) mods += "C-";
        if (alt) mods += "M-";

        return $"<{mods}{keyName}>";
    }
}
