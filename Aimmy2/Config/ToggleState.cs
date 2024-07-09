namespace Aimmy2.Config;

public class ToggleState : BaseSettings
{
    private bool _globalActive = false;
    private bool _aimAssist = false;
    private bool _constantAiTracking = false;
    private bool _predictions = false;
    private bool _emaSmoothening = false;
    private bool _enableModelSwitchKeybind = true;
    private bool _enableGunSwitchingKeybind = false;
    private bool _autoTrigger = true;
    private bool _antiRecoil = false;
    private bool _fov = false;
    private bool _dynamicFov = false;
    private bool _masking = false;
    private bool _showDetectedPlayer = false;
    private bool _showTriggerHeadArea = false;
    private bool _showAiConfidence = false;
    private bool _showTracers = false;
    private bool _collectDataWhilePlaying = false;
    private bool _autoLabelData = false;
    private bool _lgHubMouseMovement = false;
    private bool _mouseBackgroundEffect = true;
    private bool _uiTopMost = true;
    private bool _xAxisPercentageAdjustment = false;
    private bool _yAxisPercentageAdjustment = false;

    public bool GlobalActive
    {
        get => _globalActive;
        set => SetField(ref _globalActive, value);
    }

    public bool AimAssist
    {
        get => _aimAssist;
        set => SetField(ref _aimAssist, value);
    }

    public bool ConstantAITracking
    {
        get => _constantAiTracking;
        set
        {
            SetField(ref _constantAiTracking, value);
            if (value)
            {
                AimAssist = true;
            }
        }
    }

    public bool Predictions
    {
        get => _predictions;
        set => SetField(ref _predictions, value);
    }

    public bool EMASmoothening
    {
        get => _emaSmoothening;
        set => SetField(ref _emaSmoothening, value);
    }

    public bool EnableModelSwitchKeybind
    {
        get => _enableModelSwitchKeybind;
        set => SetField(ref _enableModelSwitchKeybind, value);
    }

    public bool EnableGunSwitchingKeybind
    {
        get => _enableGunSwitchingKeybind;
        set => SetField(ref _enableGunSwitchingKeybind, value);
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

    public bool FOV
    {
        get => _fov;
        set => SetField(ref _fov, value);
    }

    public bool DynamicFOV
    {
        get => _dynamicFov;
        set => SetField(ref _dynamicFov, value);
    }

    public bool Masking
    {
        get => _masking;
        set => SetField(ref _masking, value);
    }

    public bool ShowDetectedPlayer
    {
        get => _showDetectedPlayer;
        set => SetField(ref _showDetectedPlayer, value);
    }

    public bool ShowTriggerHeadArea
    {
        get => _showTriggerHeadArea;
        set => SetField(ref _showTriggerHeadArea, value);
    }

    public bool ShowAIConfidence
    {
        get => _showAiConfidence;
        set => SetField(ref _showAiConfidence, value);
    }

    public bool ShowTracers
    {
        get => _showTracers;
        set => SetField(ref _showTracers, value);
    }

    public bool CollectDataWhilePlaying
    {
        get => _collectDataWhilePlaying;
        set => SetField(ref _collectDataWhilePlaying, value);
    }

    public bool AutoLabelData
    {
        get => _autoLabelData;
        set => SetField(ref _autoLabelData, value);
    }

    public bool LGHubMouseMovement
    {
        get => _lgHubMouseMovement;
        set => SetField(ref _lgHubMouseMovement, value);
    }

    public bool MouseBackgroundEffect
    {
        get => _mouseBackgroundEffect;
        set => SetField(ref _mouseBackgroundEffect, value);
    }

    public bool UITopMost
    {
        get => _uiTopMost;
        set => SetField(ref _uiTopMost, value);
    }

    public bool XAxisPercentageAdjustment
    {
        get => _xAxisPercentageAdjustment;
        set => SetField(ref _xAxisPercentageAdjustment, value);
    }

    public bool YAxisPercentageAdjustment
    {
        get => _yAxisPercentageAdjustment;
        set => SetField(ref _yAxisPercentageAdjustment, value);
    }
}