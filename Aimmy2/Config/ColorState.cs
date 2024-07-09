namespace Aimmy2.Config;

public class ColorState : BaseSettings
{
    private string _fovColor = "#FF8080FF";
    private string _detectedPlayerColor = "#FF00FFFF";

    public string FOVColor
    {
        get => _fovColor;
        set => SetField(ref _fovColor, value);
    }

    public string DetectedPlayerColor
    {
        get => _detectedPlayerColor;
        set => SetField(ref _detectedPlayerColor, value);
    }
}