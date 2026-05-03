using Aimmy2.UILibrary;
using System.Windows.Controls;
using UILibrary;

namespace Class
{
    public class UI
    {
        // Aim Menu
        public ATitle? AT_Aim { get; set; }

        public AToggle? T_AimAligner { get; set; }

        public AKeyChanger? C_Keybind { get; set; }
        public AToggle? T_ConstantAITracking { get; set; }
        public AToggle? T_StickyAim { get; set; }
        public AToggle? T_Predictions { get; set; }
        public AToggle? T_EMASmoothing { get; set; }
        public AKeyChanger? C_EmergencyKeybind { get; set; }
        public AToggle? T_EnableModelSwitchKeybind { get; set; }
        public AKeyChanger? C_ModelSwitchKeybind { get; set; }

        //Aim Config
        public ATitle? AT_AimConfig { get; set; }

        // Predictions
        public ATitle? AT_Predictions { get; set; }

        public ADropdown? D_PredictionMethod { get; set; }
        public ADropdown? D_MovementPath { get; set; }
        public ADropdown? D_DetectionAreaType { get; set; }
        public ComboBoxItem? DDI_ClosestToCenterScreen { get; set; }
        public ADropdown? D_AimingBoundariesAlignment { get; set; }
        public ADropdown? D_TargetClass { get; set; }
        public ASlider? S_MouseSensitivity { get; set; }
        public ASlider? S_MouseJitter { get; set; }
        public ASlider? S_StickyAimThreshold { get; set; }
        public ASlider? S_YOffset { get; set; }
        public ASlider? S_YOffsetPercent { get; set; }
        public ASlider? S_XOffset { get; set; }
        public ASlider? S_XOffsetPercent { get; set; }
        public ASlider? S_EMASmoothing { get; set; }
        public ASlider? S_KalmanLeadTime { get; set; }
        public ASlider? S_WiseTheFoxLeadTime { get; set; }
        public ASlider? S_ShalloeLeadMultiplier { get; set; }

        // Triggerbot
        public ATitle? AT_TriggerBot { get; set; }

        public AToggle? T_AutoTrigger { get; set; }
        public AToggle? T_SprayMode { get; set; }
        //public AToggle? T_OnlyWhenHeld { get; set; }
        public AToggle? T_CursorCheck { get; set; }

        public ASlider? S_AutoTriggerDelay { get; set; }

        // FOV
        public ATitle? AT_FOV { get; set; }
        public AToggle? T_FOV { get; set; }
        public AToggle? T_DynamicFOV { get; set; }
        public AToggle? T_ThirdPersonSupport { get; set; }
        public AKeyChanger? C_DynamicFOV { get; set; }
        //--
        public ADropdown? D_FOVSTYLE { get; set; }
        //--
        public AColorChanger? CC_FOVColor { get; set; }
        public ASlider? S_FOVSize { get; set; }
        public ASlider? S_DynamicFOVSize { get; set; }

        // Player Detection
        public ATitle? AT_DetectedPlayer { get; set; }

        public AToggle? T_ShowDetectedPlayer { get; set; }

        public AToggle? T_ShowAIConfidence { get; set; }
        public AToggle? T_ShowTracers { get; set; }
        public ADropdown? D_TracerPosition { get; set; }
        public AColorChanger? CC_DetectedPlayerColor { get; set; }
        public ASlider? S_DPFontSize { get; set; }
        public ASlider? S_DPCornerRadius { get; set; }
        public ASlider? S_DPBorderThickness { get; set; }
        public ASlider? S_DPOpacity { get; set; }

        // Model Settings
        public ATitle? AT_ModelSettings { get; set; }

        // Settings UI
        public ATitle? AT_SettingsMenu { get; set; }
        public AToggle? T_CollectDataWhilePlaying { get; set; }
        public AToggle? T_AutoLabelData { get; set; }
        public ADropdown? D_MouseMovementMethod { get; set; }
        public ADropdown? D_ScreenCaptureMethod { get; set; }
        public ADropdown? D_ImageSize { get; set; }
        public ComboBoxItem? DDI_LGHUB { get; set; }
        public ComboBoxItem? DDI_RazerSynapse { get; set; }
        public ComboBoxItem? DDI_ddxoft { get; set; }
        public ADropdown? D_Language { get; set; }
        public AToggle? T_DebugMode { get; set; }
        public ASlider? S_AIMinimumConfidence { get; set; }
        public AToggle? T_MouseBackgroundEffect { get; set; }
        public AToggle? T_UITopMost { get; set; }
        public APButton? B_SaveConfig { get; set; }
        public APButton? B_Debug { get; set; }
        //--
        public AToggle? T_StreamGuard { get; set; }
        //--

        // X/Y Percentage Adjustment (in Aim Config)
        public AToggle? T_XAxisPercentageAdjustment { get; set; }
        public AToggle? T_YAxisPercentageAdjustment { get; set; }

        // Theme Color Changer
        public ATitle? AT_ThemeColorWheel { get; set; }
        public AColorWheel? ThemeColorWheel { get; set; }

        // Display Selector
        public ATitle? AT_DisplaySelector { get; set; }
        public ADisplaySelector? DisplaySelector { get; set; }

        // ddxoft UI
        public AFileLocator? AFL_ddxoftDLLLocator { get; set; }

        // Stores
        public APButton? B_RepoManager { get; set; }

    }
}
