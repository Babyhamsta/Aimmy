namespace Aimmy2.Config;

public class DropdownState: BaseSettings
{
    private string _gamepadProcess = "";
    private string _headArea = "";
    private TriggerCheck _triggerCheck = TriggerCheck.HeadIntersectingCenter;
    private PredictionMethod _predictionMethod = PredictionMethod.KalmanFilter;
    private DetectionAreaType _detectionAreaType = DetectionAreaType.ClosestToCenterScreen;
    private AimingBoundariesAlignment _aimingBoundariesAlignment = AimingBoundariesAlignment.Center;
    private MouseMovementMethod _mouseMovementMethod = MouseMovementMethod.MouseEvent;
    private OverlayDrawingMethod _overlayDrawingMethod;

    public string GamepadProcess
    {
        get => _gamepadProcess;
        set => SetField(ref _gamepadProcess, value);
    }

    public string HeadArea
    {
        get => _headArea;
        set => SetField(ref _headArea, value);
    }

    public TriggerCheck TriggerCheck
    {
        get => _triggerCheck;
        set => SetField(ref _triggerCheck, value);
    }

    public OverlayDrawingMethod OverlayDrawingMethod    
    {
        get => _overlayDrawingMethod;
        set => SetField(ref _overlayDrawingMethod, value);
    }

    public PredictionMethod PredictionMethod
    {
        get => _predictionMethod;
        set => SetField(ref _predictionMethod, value);
    }

    public DetectionAreaType DetectionAreaType
    {
        get => _detectionAreaType;
        set => SetField(ref _detectionAreaType, value);
    }

    public AimingBoundariesAlignment AimingBoundariesAlignment
    {
        get => _aimingBoundariesAlignment;
        set => SetField(ref _aimingBoundariesAlignment, value);
    }

    public MouseMovementMethod MouseMovementMethod
    {
        get => _mouseMovementMethod;
        set => SetField(ref _mouseMovementMethod, value);
    }
}