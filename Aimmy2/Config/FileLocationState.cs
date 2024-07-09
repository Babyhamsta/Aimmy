namespace Aimmy2.Config;

public class FileLocationState : BaseSettings
{
    private string _ddxoftDllLocation = "";
    private string _gun1Config = "";
    private string _gun2Config = "";

    public string DdxoftDLLLocation
    {
        get => _ddxoftDllLocation;
        set => SetField(ref _ddxoftDllLocation, value);
    }

    public string Gun1Config
    {
        get => _gun1Config;
        set => SetField(ref _gun1Config, value);
    }

    public string Gun2Config
    {
        get => _gun2Config;
        set => SetField(ref _gun2Config, value);
    }
}