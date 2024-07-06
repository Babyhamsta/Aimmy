using System.ComponentModel;
using System.Windows.Media;
using Aimmy2.Types;

namespace Aimmy2;

public static class ApplicationConstants
{
    private static ThemePalette _theme = ThemePalette.DefaultPalette;
    public static event PropertyChangedEventHandler StaticPropertyChanged;

    private static void OnStaticPropertyChanged(string propertyName)
    {
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
    }

    public const string ApplicationName = "Aimmy2";
    public const string ApplicationInfo = "Aimmy is free, and will never be for sale.";
    public const string ApplicationSlogan = "Aimmy - Universal Second Eye";

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = ""; 
    public const bool EasyMode = false;
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

    public static Color MainColor => Theme.MainColor;
    public static Color AccentColor => Theme.AccentColor;
    public static Color EffectColor => Theme.EffectColor;

}
