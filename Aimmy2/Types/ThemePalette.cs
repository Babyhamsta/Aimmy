using Aimmy2.Config;
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

    public override string ToString()
    {
        return Name;
    }

    public static ThemePalette ThemeForActive => All.FirstOrDefault(x => x.Name == AppConfig.Current.ActiveThemeName) ?? ThemePalette.GreenPalette;

    public static ThemePalette PurplePalette = new ThemePalette("Purple")
    {
        MainColor = Color.FromArgb(255, 18, 3, 56),
        AccentColor = Color.FromArgb(255, 105, 53, 180),
        EffectColor = Color.FromArgb(255, 211, 173, 247)
    };

    public static ThemePalette DarkPalette = new ThemePalette("Dark")
    {
        MainColor = Color.FromArgb(255, 26, 26, 26),
        AccentColor = Color.FromArgb(255, 105, 105, 105),
        EffectColor = Color.FromArgb(255, 173, 173, 173)
    };

    public static ThemePalette AquaPalette = new ThemePalette("Aqua")
    {
        MainColor = Colors.Aqua,
        AccentColor = Color.FromArgb(255, 53, 105, 180),
        EffectColor = Color.FromArgb(255, 173, 211, 247)
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

    public static ThemePalette[] All
    {
        get
        {
            return typeof(ThemePalette).GetFields().Where(f => f.FieldType == typeof(ThemePalette)).Select(f => (ThemePalette)f.GetValue(null)).ToArray();
        }
    }

}