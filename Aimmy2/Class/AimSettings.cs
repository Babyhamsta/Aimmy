using System.Globalization;
using Aimmy2.AILogic;

namespace Aimmy2.Class
{
    internal static class AimSettings
    {
        public static int ImageSize
        {
            get => int.Parse(GetDropdown("Image Size"), CultureInfo.InvariantCulture);
            set => SetDropdown("Image Size", value.ToString(CultureInfo.InvariantCulture));
        }

        public static string ScreenCaptureMethod
        {
            get => GetDropdown("Screen Capture Method");
            set => SetDropdown("Screen Capture Method", value);
        }

        public static string DetectionAreaType => GetDropdown("Detection Area Type");
        public static string TargetClass => GetDropdown("Target Class");
        public static string PredictionMethod => GetDropdown("Prediction Method");
        public static string AimingBoundariesAlignment => GetDropdown("Aiming Boundaries Alignment");
        public static string TracerPosition => GetDropdown("Tracer Position");
        public static string MovementPath => GetDropdown("Movement Path");
        public static string MouseMovementMethod => GetDropdown("Mouse Movement Method");

        public static double FovSize => GetSlider("FOV Size");
        public static float MinimumConfidence => (float)(GetSlider("AI Minimum Confidence") / 100.0);
        public static double StickyAimThreshold => GetSlider("Sticky Aim Threshold");
        public static double StickyAimGraceMs => GetSlider("Sticky Aim Grace");
        public static double StickyMaxMatchCost => BoundedSlider("Lock Strictness", StickyAimSelector.DefaultMaxMatchCost, 1.2, 3.5);
        public const double DefaultRescueRatio = .50;
        public static double StickyRescueFloorRatio => BoundedSlider("Lock Rescue Confidence", DefaultRescueRatio, .25, 1);
        public static double NmsIouThreshold => BoundedSlider("NMS IoU Threshold", PredictionFilter.DefaultNmsIou, .35, .75);
        public static double OverlayOpacity => GetSlider("Opacity");
        public static double YOffset => GetSlider("Y Offset (Up/Down)");
        public static double XOffset => GetSlider("X Offset (Left/Right)");
        public static double YOffsetPercent => GetSlider("Y Offset (%)");
        public static double XOffsetPercent => GetSlider("X Offset (%)");
        public static int AutoTriggerDelayMilliseconds => (int)(GetSlider("Auto Trigger Delay") * 1000);
        public static int MouseJitter => (int)GetSlider("Mouse Jitter");
        public static double MouseSensitivity => GetSlider("Mouse Sensitivity (+/-)");
        public static double KalmanLeadTime => GetSlider("Kalman Lead Time");
        public static double WiseTheFoxLeadTime => GetSlider("WiseTheFox Lead Time");
        public static double ShalloeLeadMultiplier => GetSlider("Shalloe Lead Multiplier");
        public static int AiFpsLimit => Math.Max(0, (int)Math.Round(GetSlider("AI FPS Limit")));

        public static bool AutoTrigger => GetToggle("Auto Trigger");
        public static bool ConstantAiTracking => GetToggle("Constant AI Tracking");
        public static bool SprayMode => GetToggle("Spray Mode");
        public static bool CursorCheck => GetToggle("Cursor Check");
        public static bool AimAssist => GetToggle("Aim Assist");
        public static bool ShowDetectedPlayer => GetToggle("Show Detected Player");
        public static bool ShowFov => GetToggle("FOV");
        public static bool ShowAiConfidence => GetToggle("Show AI Confidence");
        public static bool ShowTracers => GetToggle("Show Tracers");
        public static bool UseXAxisPercentageAdjustment => GetToggle("X Axis Percentage Adjustment");
        public static bool UseYAxisPercentageAdjustment => GetToggle("Y Axis Percentage Adjustment");
        public static bool Predictions => GetToggle("Predictions");
        public static bool StickyAim => GetToggle("Sticky Aim");
        public static bool CollectDataWhilePlaying => GetToggle("Collect Data While Playing");
        public static bool AutoLabelData => GetToggle("Auto Label Data");
        public static bool ThirdPersonSupport => GetToggle("Third Person Support");

        public static double GetSlider(string key) =>
            Convert.ToDouble((object)Dictionary.sliderSettings[key], CultureInfo.InvariantCulture);

        private static double BoundedSlider(string key, double fallback, double minimum, double maximum)
        {
            double value = GetSlider(key);
            return double.IsNaN(value) ? fallback : Math.Clamp(value, minimum, maximum);
        }

        public static bool GetToggle(string key) =>
            Convert.ToBoolean((object)Dictionary.toggleState[key], CultureInfo.InvariantCulture);

        public static string GetDropdown(string key) =>
            Convert.ToString((object)Dictionary.dropdownState[key], CultureInfo.InvariantCulture) ?? string.Empty;

        private static void SetDropdown(string key, string value)
        {
            Dictionary.dropdownState[key] = value;
        }
    }
}
