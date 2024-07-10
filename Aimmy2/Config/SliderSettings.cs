using InputLogic;

namespace Aimmy2.Config;

public class SliderSettings: BaseSettings
{
    private string _suggestedModel = "";
    private double _fovSize = 640;
    private double _dynamicFovSize = 200;
    private double _mouseSensitivity = 0.80;
    private double _mouseJitter = 4;
    private double _yOffset = 0;
    private double _yOffsetPercentage = 50;
    private double _xOffsetPercentage = 50;
    private double _emaSmoothening = 0.5;
    private double _autoTriggerDelay = 0.1;
    private double _aiMinimumConfidence = 45;
    private double _gamepadMinimumLt = 0.7;
    private double _gamepadMinimumRt = 0.7;
    private int _aiConfidenceFontSize = 20;
    private double _cornerRadius = 0;
    private double _borderThickness = 1;
    private double _opacity = 1;
    private double _fovOpacity = 1;
    private double _triggerKeyMin = 0.0;

    public string SuggestedModel
    {
        get => _suggestedModel;
        set => SetField(ref _suggestedModel, value);
    }

    public double ActualFovSize => AppConfig.Current.ToggleState.DynamicFOV && InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind)) ? _dynamicFovSize : _fovSize;

    public double FOVSize
    {
        get => _fovSize;
        set => SetField(ref _fovSize, value);
    }

    public double DynamicFOVSize
    {
        get => _dynamicFovSize;
        set => SetField(ref _dynamicFovSize, value);
    }

    public double MouseSensitivity  
    {
        get => _mouseSensitivity;
        set => SetField(ref _mouseSensitivity, value);
    }

    public double MouseJitter
    {
        get => _mouseJitter;
        set => SetField(ref _mouseJitter, value);
    }

    public double YOffset
    {
        get => _yOffset;
        set => SetField(ref _yOffset, value);
    }

    public double YOffsetPercentage
    {
        get => _yOffsetPercentage;
        set => SetField(ref _yOffsetPercentage, value);
    }

    public double XOffset { get; set; } = 0;

    public double XOffsetPercentage
    {
        get => _xOffsetPercentage;
        set => SetField(ref _xOffsetPercentage, value);
    }

    public double EMASmoothening
    {
        get => _emaSmoothening;
        set => SetField(ref _emaSmoothening, value);
    }

    public double TriggerKeyMin
    {
        get => _triggerKeyMin;
        set => SetField(ref _triggerKeyMin, value);
    }

    public double AutoTriggerDelay
    {
        get => _autoTriggerDelay;
        set => SetField(ref _autoTriggerDelay, value);
    }

    public double AIMinimumConfidence
    {
        get => _aiMinimumConfidence;
        set => SetField(ref _aiMinimumConfidence, value);
    }

    public double GamepadMinimumLT
    {
        get => _gamepadMinimumLt;
        set => SetField(ref _gamepadMinimumLt, value);
    }

    public double GamepadMinimumRT
    {
        get => _gamepadMinimumRt;
        set => SetField(ref _gamepadMinimumRt, value);
    }

    public int AIConfidenceFontSize
    {
        get => _aiConfidenceFontSize;
        set => SetField(ref _aiConfidenceFontSize, value);
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set => SetField(ref _cornerRadius, value);
    }

    public double BorderThickness
    {
        get => _borderThickness;
        set => SetField(ref _borderThickness, value);
    }

    public double Opacity
    {
        get => _opacity;
        set => SetField(ref _opacity, value);
    }

    public double FOVOpacity
    {
        get => _fovOpacity;
        set => SetField(ref _fovOpacity, value);
    }

    internal override void OnPropertyChanged(string? propertyName = null)
    {
        if (propertyName is nameof(FOVSize) or nameof(DynamicFOVSize))
            OnPropertyChanged(nameof(ActualFovSize));
        
        base.OnPropertyChanged(propertyName);
    }
}