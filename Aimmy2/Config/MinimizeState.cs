namespace Aimmy2.Config;

// TODO: Remove and just store hashed values for minimized boxes
public class MinimizeState: BaseSettings
{
    private bool _aimAssist = false;
    private bool _aimConfig = false;
    private bool _autoTrigger = false;
    private bool _antiRecoil = false;
    private bool _antiRecoilConfig = false;
    private bool _fovConfig = false;
    private bool _espConfig = false;
    private bool _settingsMenu = false;
    private bool _xyPercentageAdjustment = false;

    public bool AimAssist
    {
        get => _aimAssist;
        set => SetField(ref _aimAssist, value);
    }

    public bool AimConfig
    {
        get => _aimConfig;
        set => SetField(ref _aimConfig, value);
    }

    public bool AutoTrigger
    {
        get => _autoTrigger;
        set => SetField(ref _autoTrigger, value);
    }

    public bool AntiRecoil
    {
        get => _antiRecoil;
        set => SetField(ref _antiRecoil, value);
    }

    public bool AntiRecoilConfig
    {
        get => _antiRecoilConfig;
        set => SetField(ref _antiRecoilConfig, value);
    }

    public bool FOVConfig
    {
        get => _fovConfig;
        set => SetField(ref _fovConfig, value);
    }

    public bool ESPConfig
    {
        get => _espConfig;
        set => SetField(ref _espConfig, value);
    }

    public bool SettingsMenu
    {
        get => _settingsMenu;
        set => SetField(ref _settingsMenu, value);
    }

    public bool XYPercentageAdjustment
    {
        get => _xyPercentageAdjustment;
        set => SetField(ref _xyPercentageAdjustment, value);
    }
}