using System.Windows.Media;

namespace Aimmy2.Config;

public class ColorState : BaseSettings
{
    private Color _fovColor = Colors.LightBlue;
    private Color _detectedPlayerColor = Colors.IndianRed;

    public Color FOVColor
    {
        get => _fovColor;
        set => SetField(ref _fovColor, value);
    }

    public Color DetectedPlayerColor
    {
        get => _detectedPlayerColor;
        set => SetField(ref _detectedPlayerColor, value);
    }
}