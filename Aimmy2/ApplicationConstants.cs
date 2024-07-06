using System.Windows.Media;

namespace Aimmy2;

public static class ApplicationConstants
{
    public const string ApplicationName = "Aimmy2";
    public const string ApplicationInfo = "Aimmy is free, and will never be for sale.";
    public const string ApplicationSlogan = "Aimmy - Universal Second Eye";

    public const string DefaultModel = "default.onnx";
    public const string ShowOnly = ""; 
    public const bool EasyMode = true;


    public static Color ActiveColor = Colors.Green;
    //public static Color MainColor = Color.FromArgb(255, 18, 3, 56);
    public static Color MainColor = Color.FromArgb(255, 118, 3, 56);

    public static string[] DisabledFeatures => EasyMode ? ["AimAssist", "AntiRecoil", "ASP2", "AimConfig", "ARConfig"] : [];
}