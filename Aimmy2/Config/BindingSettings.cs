namespace Aimmy2.Config;

public class BindingSettings: BaseSettings
{
    private string _triggerKey = "";
    private string _triggerAdditionalSend = "";
    private string _aimKeybind = "Right";
    private string _secondAimKeybind = "LMenu";
    private string _dynamicFovKeybind = "Left";
    private string _modelSwitchKeybind = "OemPipe";
    private string _antiRecoilKeybind = "Left";
    private string _disableAntiRecoilKeybind = "Oem6";
    private string _gun1Key = "D1";
    private string _gun2Key = "D2";
    private string _rapidFireKey;
    private string _triggerAdditionalCommandKey;

    public string RapidFireKey
    {
        get => _rapidFireKey;
        set => SetField(ref _rapidFireKey, value);
    }

    public string TriggerAdditionalCommandKey
    {
        get => _triggerAdditionalCommandKey;
        set => SetField(ref _triggerAdditionalCommandKey, value);
    }

    public string TriggerKey
    {
        get => _triggerKey;
        set => SetField(ref _triggerKey, value);
    }

    public string TriggerAdditionalSend
    {
        get => _triggerAdditionalSend;
        set => SetField(ref _triggerAdditionalSend, value);
    }

    public string AimKeybind
    {
        get => _aimKeybind;
        set => SetField(ref _aimKeybind, value);
    }

    public string SecondAimKeybind
    {
        get => _secondAimKeybind;
        set => SetField(ref _secondAimKeybind, value);
    }

    public string DynamicFOVKeybind
    {
        get => _dynamicFovKeybind;
        set => SetField(ref _dynamicFovKeybind, value);
    }

    public string ModelSwitchKeybind
    {
        get => _modelSwitchKeybind;
        set => SetField(ref _modelSwitchKeybind, value);
    }

    public string AntiRecoilKeybind
    {
        get => _antiRecoilKeybind;
        set => SetField(ref _antiRecoilKeybind, value);
    }

    public string DisableAntiRecoilKeybind
    {
        get => _disableAntiRecoilKeybind;
        set => SetField(ref _disableAntiRecoilKeybind, value);
    }

    public string Gun1Key
    {
        get => _gun1Key;
        set => SetField(ref _gun1Key, value);
    }

    public string Gun2Key
    {
        get => _gun2Key;
        set => SetField(ref _gun2Key, value);
    }
}