using Visuality;

namespace Aimmy2.Class
{
    public static class Dictionary
    {
        public static string lastLoadedModel = "N/A";
        public static string lastLoadedConfig = "N/A";
        public static DetectedPlayerWindow? DetectedPlayerOverlay;
        public static FOV? FOVWindow;

        public static Dictionary<string, dynamic> bindingSettings = new()
        {
            { "Aim Keybind", "Right"},
            { "Second Aim Keybind", "LMenu"},
            { "Dynamic FOV Keybind", "Left"},
            { "Emergency Stop Keybind", "Delete"},
            { "Model Switch Keybind", "OemPipe"}
        };

        public static Dictionary<string, dynamic> sliderSettings = new()
        {
            { "Suggested Model", ""},
            { "FOV Size", 640 },
            { "Dynamic FOV Size", 200 },
            { "Mouse Sensitivity (+/-)", 0.80 },
            { "Mouse Jitter", 4 },
            { "Sticky Aim Threshold", 50 },
            { "Y Offset (Up/Down)", 0 },
            { "Y Offset (%)", 50 },
            { "X Offset (Left/Right)", 0 },
            { "X Offset (%)", 50 },
            { "EMA Smoothening", 0.5},
            { "Kalman Lead Time", 0.10 },
            { "WiseTheFox Lead Time", 0.15 },
            { "Shalloe Lead Multiplier", 3.0 },
            { "Auto Trigger Delay", 0.1 },
            { "AI Minimum Confidence", 45 },
            { "AI Confidence Font Size", 20 },
            { "Corner Radius", 0 },
            { "Border Thickness", 1 },
            { "Opacity", 1 }
        };

        // Make sure the Settings Name is the EXACT Same as the Toggle Name or I will smack you :joeangy:
        // nori
        public static Dictionary<string, dynamic> toggleState = new()
        {
            { "Aim Assist", false },
            { "Sticky Aim", false },
            { "Constant AI Tracking", false },
            { "Predictions", false },
            { "EMA Smoothening", false },
            { "Enable Model Switch Keybind", true },
            { "Auto Trigger", false },
            { "FOV", false },
            { "Dynamic FOV", false },
            { "Third Person Support", false },
            { "Masking", false },
            { "Show Detected Player", false },
            { "Cursor Check", false },
            { "Spray Mode", false },
            //{ "Only When Held", false },
            { "Show FOV", true },
            { "Show AI Confidence", false },
            { "Show Tracers", false },
            { "Collect Data While Playing", false },
            { "Auto Label Data", false },
            { "LG HUB Mouse Movement", false },
            { "Mouse Background Effect", true },
            { "Debug Mode", false },
            { "UI TopMost", false },
            //--
            { "StreamGuard", false },
            //--
            { "X Axis Percentage Adjustment", false },
            { "Y Axis Percentage Adjustment", false }
        };

        public static Dictionary<string, dynamic> minimizeState = new()
        {
            { "Aim Assist", false },
            { "Aim Config", false },
            { "Predictions", false },
            { "Auto Trigger", false },
            { "FOV Config", false },
            { "ESP Config", false },
            { "Model Settings", false },
            { "Settings Menu", false },
            { "X/Y Percentage Adjustment", false },
            { "Theme Settings", false },
            { "Screen Settings", false}
        };

        public static Dictionary<string, dynamic> dropdownState = new()
        {
            { "Prediction Method", "Kalman Filter" },
            { "Detection Area Type", "Closest to Center Screen" },
            { "Aiming Boundaries Alignment", "Center" },
            { "Mouse Movement Method", "Mouse Event" },
            { "Screen Capture Method", "DirectX" },
            { "Tracer Position", "Bottom" },
            { "Movement Path", "Cubic Bezier" },
            { "Image Size", "640" },
            { "Target Class", "Best Confidence" }
        };

        public static Dictionary<string, dynamic> colorState = new()
        {
            { "FOV Color", "#FF8080FF"},
            { "Detected Player Color", "#FF00FFFF"},
            { "Theme Color", "#FF722ED1" }
        };

        public static Dictionary<string, dynamic> filelocationState = new()
        {
            { "ddxoft DLL Location", ""}
        };
    }
}