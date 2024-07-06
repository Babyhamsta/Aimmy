using System.Windows.Media;

namespace Aimmy2.Types;

public class ThemePalette
{
    public ThemePalette(string name)
    {
        Name = name;
    }

    public string Name;
    public Color MainColor;
    public Color AccentColor;
    public Color EffectColor;

    public static ThemePalette DefaultPalette = new ThemePalette("Default")
    {
        MainColor = Color.FromArgb(255, 18, 3, 56),
        AccentColor = Color.FromArgb(255, 105, 53, 180),
        EffectColor = Color.FromArgb(255, 211, 173, 247)
    };

    public static ThemePalette GreenPalette = new ThemePalette("Green")
    {
        MainColor = Color.FromArgb(255, 3, 56, 18),
        AccentColor = Color.FromArgb(255, 53, 180, 105),
        EffectColor = Color.FromArgb(255, 173, 247, 211)
    };

    public static ThemePalette RedPalette = new ThemePalette("Red")
    {
        MainColor = Color.FromArgb(255, 56, 3, 18),
        AccentColor = Color.FromArgb(255, 180, 53, 105),
        EffectColor = Color.FromArgb(255, 247, 173, 211)
    };

    public static ThemePalette BluePalette = new ThemePalette("Blue")
    {
        MainColor = Color.FromArgb(255, 3, 18, 56),
        AccentColor = Color.FromArgb(255, 53, 105, 180),
        EffectColor = Color.FromArgb(255, 173, 211, 247)
    };
}