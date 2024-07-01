using System.Windows.Input;

namespace Other
{
    internal class KeybindNameManager
    {
        public static Dictionary<string, string> KeybindNames = new()
        {
            { "D1", "1"},
            { "D2", "2"},
            { "D3", "3"},
            { "D4", "4"},
            { "D5", "5"},
            { "D6", "6"},
            { "D7", "7"},
            { "D8", "8"},
            { "D9", "9"},
            { "D0", "0"},

            { "OemMinus", "-"},
            { "OemPlus", "+"},
            { "Back", "Backspace"},
            { "Oem5", "Backslash (\\)"},
            { "LMenu", "Left Alt"},
            { "RMenu", "Right Alt"},
            { "RShiftKey", "Right Shift" },
            { "LShiftKey", "Left Shift" },

            { "Oem4", "Left Bracket"},
            { "OemOpenBrackets", "Left Bracket"},
            { "Oem6", "Right Bracket"},
            { "Oem1", ";"},
            { "Oem3", "`" },
            { "OemQuotes", "'"},
            { "OemQuestion", "/"},
            { "OemPeriod", "."},
            { "OemComma", ","},
            { "Return", "Enter" },
        };

        public static string ConvertToRegularKey(string keyName)
        {
            try
            {
                KeyConverter kc = new();
                var key = (Key)kc.ConvertFromString(keyName)!;
                return KeybindNames.TryGetValue(key.ToString(), out var displayName) ? displayName : key.ToString();
            }
            catch (Exception)
            {
                return keyName;
            }
        }
    }
}
