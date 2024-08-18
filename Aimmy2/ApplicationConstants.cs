using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Aimmy2.Types;

namespace Aimmy2;

public static class ApplicationConstants
{
    private static ThemePalette _theme = ThemePalette.DarkPalette;
    private static readonly string[] Names =
    {
        "AI-M ME Winehouse",
        "AI-Machine",
        "Aim A.I. Little Higher",
        "AIM-Botox",
        "Drunken AIrcher",
        "AI'll Be Back",
        "AIM-Possible",
        "A.I.migo",
        "AI-M King",
        "Aimmy",
        "Mousemovement Machine",
        "Micro AI-mbot",
    };

    private static readonly string[] Infos =
    {
        "The only thing impossible is missing your target.",
        "For those who believe aiming high isn't high enough.",
        "Giving your aim that extra lift, without the needles.",
        "Perfect aim, even when you've had one too many.",
        "Your aim just got terminated.",
        "Mission accomplished, every single time.",
        "Your new best friend in hitting the bullseye."
    };

    private static readonly string[] Slogans =
    {
        "AI'mpossible - Aim for the stars, even when sober.",
        "Aim A.I. Little Higher - Because the sky's just the beginning.",
        "AIM-Botox - Smooth and wrinkle-free aiming.",
        "Drunken AIrcher - Aim like nobody's watching.",
        "AI'll Be Back - Hasta la vista, missed shots.",
        "AIM-Possible - The odds are always in your favor.",
        "A.I.migo - Always by your side, and never missing."
    };
    private static readonly Random random = new Random();


    public static event PropertyChangedEventHandler StaticPropertyChanged;

    private static void OnStaticPropertyChanged(string propertyName)
    {
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
    }

    //public const string ApplicationName = "Aimmy2";
    //public const string ApplicationInfo = "Aimmy is free, and will never be for sale.";
    //public const string ApplicationSlogan = "Aimmy - Universal Second Eye";

    public static string ApplicationVersionStr => $"v{ApplicationVersion.ToString()}";

    private static Version? ApplicationVersion => typeof(ApplicationConstants).Assembly.GetName().Version;

    public static string ApplicationName => Names[random.Next(Names.Length)];
    public static string ApplicationInfo => Infos[random.Next(Infos.Length)];
    public static string ApplicationSlogan => Slogans[random.Next(Slogans.Length)];

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = ""; 
    public const bool EasyMode = false;

    public static Visibility EasyModeHidden => EasyMode ? Visibility.Collapsed : Visibility.Visible;
    public static string[] DisabledFeatures => EasyMode ? ["AimAssist", "AntiRecoil", "ASP2", "AimConfig", "ARConfig"] : [];

    public static ThemePalette Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            OnStaticPropertyChanged(nameof(Theme));
            OnStaticPropertyChanged(nameof(MainColor));
            OnStaticPropertyChanged(nameof(AccentColor));
            OnStaticPropertyChanged(nameof(EffectColor));
        }
    }

    public static Color Foreground => GetForegroundFor(MainColor);

    private static Color GetForegroundFor(Color background)
    {
        var luminance = 1 - (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
        return luminance < 0.5 ? Colors.White : Colors.Black;
    }

    public static Color MainColor => Theme.MainColor;
    public static Color AccentColor => Theme.AccentColor;
    public static Color EffectColor => Theme.EffectColor;

}
