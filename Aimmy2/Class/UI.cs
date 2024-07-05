using Aimmy2.UILibrary;
using System.Windows.Controls;
using UILibrary;

namespace Class
{
    public class UI
    {
        public AToggle? G_Active { get; set; }
        public AKeyChanger? G_Active_Keybind { get; set; }

        // Aim Menu
        public ATitle? AT_Aim { get; set; }

        public AToggle? T_AimAligner { get; set; }

        public AKeyChanger? C_Keybind { get; set; }
        public AToggle? T_ConstantAITracking { get; set; }
        public AToggle? T_Predictions { get; set; }
        public AToggle? T_EMASmoothing { get; set; }
        public AKeyChanger? C_EmergencyKeybind { get; set; }
        public AToggle? T_EnableModelSwitchKeybind { get; set; }
        public AKeyChanger? C_ModelSwitchKeybind { get; set; }

        //Aim Config
        public ATitle? AT_AimConfig { get; set; }

        public ADropdown? D_PredictionMethod { get; set; }

        public ADropdown? D_DetectionAreaType { get; set; }
        public ComboBoxItem? DDI_ClosestToCenterScreen { get; set; }
        public ADropdown? D_AimingBoundariesAlignment { get; set; }
        public ASlider? S_MouseSensitivity { get; set; }
        public ASlider? S_MouseJitter { get; set; }
        public ASlider? S_YOffset { get; set; }
        public ASlider? S_XOffset { get; set; }
        public ASlider? S_EMASmoothing { get; set; }

        // Triggerbot
        public ATitle? AT_TriggerBot { get; set; }

        public AToggle? T_AutoTrigger { get; set; }
        public AToggle? T_TriggerCenterCheck { get; set; }
        public AKeyChanger? T_TriggerKey { get; set; }
        public AKeyChanger? T_TriggerSendKey { get; set; }
        public ASlider? S_AutoTriggerDelay { get; set; }

        // Anti Recoil
        public ATitle? AT_AntiRecoil { get; set; }

        public AToggle? T_AntiRecoil { get; set; }

        public AKeyChanger? C_AntiRecoilKeybind { get; set; }
        public AKeyChanger? C_ToggleAntiRecoilKeybind { get; set; }
        public ASlider? S_HoldTime { get; set; }
        public APButton? B_RecordFireRate { get; set; }
        public ASlider? S_FireRate { get; set; }
        public ASlider? S_YAntiRecoilAdjustment { get; set; }
        public ASlider? S_XAntiRecoilAdjustment { get; set; }

        // Anti Recoil Config
        public ATitle? AT_AntiRecoilConfig { get; set; }

        public AToggle? T_EnableGunSwitchingKeybind { get; set; }

        public APButton? B_SaveRecoilConfig { get; set; }
        public AKeyChanger? C_Gun1Key { get; set; }
        public AFileLocator? AFL_Gun1Config { get; set; }
        public AKeyChanger? C_Gun2Key { get; set; }
        public AFileLocator? AFL_Gun2Config { get; set; }
        public APButton? B_LoadGun1Config { get; set; }
        public APButton? B_LoadGun2Config { get; set; }

        // FOV
        public ATitle? AT_FOV { get; set; }

        public AToggle? T_FOV { get; set; }

        public AToggle? T_DynamicFOV { get; set; }
        public AKeyChanger? C_DynamicFOV { get; set; }
        public AColorChanger? CC_FOVColor { get; set; }
        public ASlider? S_FOVSize { get; set; }
        public ASlider? S_DynamicFOVSize { get; set; }

        // Player Detection
        public ATitle? AT_DetectedPlayer { get; set; }

        public AToggle? T_ShowDetectedPlayer { get; set; }

        public AToggle? T_ShowAIConfidence { get; set; }
        public AToggle? T_ShowTracers { get; set; }
        public AColorChanger? CC_DetectedPlayerColor { get; set; }
        public ASlider? S_DPFontSize { get; set; }
        public ASlider? S_DPCornerRadius { get; set; }
        public ASlider? S_DPBorderThickness { get; set; }
        public ASlider? S_DPOpacity { get; set; }
        public ASlider? S_FOVOpacity { get; set; }

        // Settings UI
        public ATitle? AT_SettingsMenu { get; set; }
        public AToggle? T_CollectDataWhilePlaying { get; set; }
        public AToggle? T_AutoLabelData { get; set; }
        public ADropdown? D_MouseMovementMethod { get; set; }
        public ComboBoxItem? DDI_LGHUB { get; set; }
        public ComboBoxItem? DDI_RazerSynapse { get; set; }
        public ComboBoxItem? DDI_ddxoft { get; set; }
        public ASlider? S_AIMinimumConfidence { get; set; }
        public ASlider? S_MinimumLT { get; set; }
        public ASlider? S_MinimumRT { get; set; }
        public AToggle? T_MouseBackgroundEffect { get; set; }
        public AToggle? T_UITopMost { get; set; }
        public APButton? B_SaveConfig { get; set; }
        public APButton? B_Debug { get; set; }

        // X/Y Percentage Adjustment Enabler
        public ATitle? AT_XYPercentageAdjustmentEnabler { get; set; }
        public AToggle? T_XAxisPercentageAdjustment { get; set; }
        public AToggle? T_YAxisPercentageAdjustment { get; set; }

        // ddxoft UI
        public AFileLocator? AFL_ddxoftDLLLocator { get; set; }

        // Stores
        public APButton? B_RepoManager { get; set; }
    }
}
