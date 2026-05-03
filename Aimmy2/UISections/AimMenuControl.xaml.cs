using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Resources;
using Aimmy2.UILibrary;
using Class;
using InputLogic;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UILibrary;

namespace Aimmy2.Controls
{
    public partial class AimMenuControl : UserControl
    {
        UISections.ColorPicker? colorPickerInstance = null;
        UISections.ColorPicker? fovColorPickerInstance = null;
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Aim Assist", false },
            { "Aim Config", false },
            { "Predictions", false },
            { "Auto Trigger", false },
            { "FOV Config", false },
            { "ESP Config", false }
        };

        public StackPanel AimAssistPanel => AimAssist;
        public StackPanel TriggerBotPanel => TriggerBot;
        public StackPanel ESPConfigPanel => ESPConfig;
        public StackPanel AimConfigPanel => AimConfig;
        public StackPanel PredictionsPanel => Predictions;
        public StackPanel FOVConfigPanel => FOVConfig;
        public ScrollViewer AimMenuScrollViewer => AimMenu;

        public AimMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            LoadMinimizeStatesFromGlobal();

            AIManager.ImageSizeUpdated += OnImageSizeChanged;

            LoadAimAssist();
            LoadAimConfig();
            LoadPredictions();
            LoadTriggerBot();
            LoadFOVConfig();
            LoadESPConfig();

            ApplyMinimizeStates();
        }

        #region Minimize State Management

        private void LoadMinimizeStatesFromGlobal()
        {
            foreach (var key in _localMinimizeState.Keys.ToList())
            {
                if (Dictionary.minimizeState.ContainsKey(key))
                {
                    _localMinimizeState[key] = Dictionary.minimizeState[key];
                }
            }
        }

        private void SaveMinimizeStatesToGlobal()
        {
            foreach (var kvp in _localMinimizeState)
            {
                Dictionary.minimizeState[kvp.Key] = kvp.Value;
            }
        }

        private void ApplyMinimizeStates()
        {
            ApplyPanelState("Aim Assist", AimAssistPanel);
            ApplyPanelState("Aim Config", AimConfigPanel);
            ApplyPanelState("Predictions", PredictionsPanel);
            ApplyPanelState("Auto Trigger", TriggerBotPanel);
            ApplyPanelState("FOV Config", FOVConfigPanel);
            ApplyPanelState("ESP Config", ESPConfigPanel);
        }

        private void ApplyPanelState(string stateName, StackPanel panel)
        {
            if (_localMinimizeState.TryGetValue(stateName, out bool isMinimized))
            {
                SetPanelVisibility(panel, !isMinimized);
            }
        }

        private void SetPanelVisibility(StackPanel panel, bool isVisible)
        {
            foreach (UIElement child in panel.Children)
            {
                bool shouldStayVisible = child is ATitle || child is ASpacer || child is ARectangleBottom;

                child.Visibility = shouldStayVisible
                    ? Visibility.Visible
                    : (isVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        private void TogglePanel(string stateName, StackPanel panel)
        {
            if (!_localMinimizeState.ContainsKey(stateName)) return;

            _localMinimizeState[stateName] = !_localMinimizeState[stateName];

            SetPanelVisibility(panel, !_localMinimizeState[stateName]);

            SaveMinimizeStatesToGlobal();
        }

        #endregion

        #region Menu Section Loaders

        private void LoadAimAssist()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, AimAssist);

            builder
                .AddTitle(LocalizationManager.GetString("AimAssist_Title"), true, t =>
                {
                    uiManager.AT_Aim = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Assist", AimAssistPanel);
                        _mainWindow?.UpdateAimAssistSliderVisibility();
                    };
                })
                .AddToggle(LocalizationManager.GetString("Toggle_AimAssist"), "Aim Assist", t =>
                {
                    uiManager.T_AimAligner = t;
                    t.Reader.Click += (s, e) =>
                    {
                        if (Dictionary.toggleState["Aim Assist"] && Dictionary.lastLoadedModel == "N/A")
                        {
                            Dictionary.toggleState["Aim Assist"] = false;
                            _mainWindow.UpdateToggleUI(t, false);
                            LogManager.Log(LogManager.LogLevel.Warning, LocalizationManager.GetString("Msg_LoadModelFirst"), true);
                        }
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_AimAssist"))
                .AddToggle(LocalizationManager.GetString("Toggle_ConstantAITracking"), "Constant AI Tracking", t =>
                {
                    uiManager.T_ConstantAITracking = t;
                    t.Reader.Click += (s, e) =>
                    {
                        if (Dictionary.toggleState["Constant AI Tracking"])
                        {
                            if (Dictionary.lastLoadedModel == "N/A")
                            {
                                Dictionary.toggleState["Constant AI Tracking"] = false;
                                _mainWindow.UpdateToggleUI(t, false);
                            }
                            else
                            {
                                Dictionary.toggleState["Aim Assist"] = true;
                                if (uiManager.T_AimAligner != null)
                                    _mainWindow.UpdateToggleUI(uiManager.T_AimAligner, true);
                            }
                        }
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_ConstantAITracking"))
                .AddToggle(LocalizationManager.GetString("Toggle_StickyAim"), "Sticky Aim", t => uiManager.T_StickyAim = t,
                    tooltip: LocalizationManager.GetString("Tooltip_StickyAim"))
                .AddSlider(LocalizationManager.GetString("Slider_StickyAimThreshold"), "Sticky Aim Threshold", LocalizationManager.GetString("Unit_Pixels"), 1, 1, 0, 100, s =>
                {
                    uiManager.S_StickyAimThreshold = s;
                    s.Visibility = Dictionary.toggleState["Sticky Aim"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_StickyAimThreshold"))
                .AddKeyChanger(LocalizationManager.GetString("Key_AimKeybind"), "Aim Keybind", k => uiManager.C_Keybind = k,
                    tooltip: LocalizationManager.GetString("Tooltip_AimKeybind"))
                .AddKeyChanger(LocalizationManager.GetString("Key_SecondAimKeybind"), "Second Aim Keybind", tooltip: LocalizationManager.GetString("Tooltip_SecondAimKeybind"))
                .AddSeparator();
        }

        private void LoadAimConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, AimConfig);

            builder
                .AddTitle(LocalizationManager.GetString("AimConfig_Title"), true, t =>
                {
                    uiManager.AT_AimConfig = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Aim Config", AimConfigPanel);
                        _mainWindow?.UpdateAimConfigSliderVisibility();
                    };
                })
                .AddDropdown(LocalizationManager.GetString("Dropdown_MouseMovementMethod"), "Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;

                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_MouseEvent"), "Mouse Event");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_SendInput"), "SendInput");
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_LGHub"), "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_RazerSynapse"), "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_ddxoft"), "ddxoft Virtual Input Driver");

                    uiManager.DDI_LGHUB.Selected += async (s, e) =>
                    {
                        if (!new LGHubMain().Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_RazerSynapse.Selected += async (s, e) =>
                    {
                        if (!await RZMouse.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_ddxoft.Selected += async (s, e) =>
                    {
                        if (!await DdxoftMain.Load())
                            await ResetToMouseEvent();
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_MouseMovementMethod"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_MovementPath"), "Movement Path", d =>
                {
                    d.DropdownBox.SelectedIndex = 0;
                    uiManager.D_MovementPath = d;
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_CubicBezier"), "Cubic Bezier");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Exponential"), "Exponential");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Linear"), "Linear");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Adaptive"), "Adaptive");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_PerlinNoise"), "Perlin Noise");
                }, tooltip: LocalizationManager.GetString("Tooltip_MovementPath"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_DetectionAreaType"), "Detection Area Type", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_DetectionAreaType = d;
                    uiManager.DDI_ClosestToCenterScreen = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_ClosestToCenter"), "Closest to Center Screen");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_ClosestToMouse"), "Closest to Mouse");

                    uiManager.DDI_ClosestToCenterScreen.Selected += async (s, e) =>
                    {
                        await Task.Delay(100);
                        MainWindow.FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                            Convert.ToInt16((WinAPICaller.ScreenWidth / 2) / WinAPICaller.scalingFactorX) - 320,
                            Convert.ToInt16((WinAPICaller.ScreenHeight / 2) / WinAPICaller.scalingFactorY) - 320,
                            0, 0);
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_DetectionAreaType"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_AimingBoundaries"), "Aiming Boundaries Alignment", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_AimingBoundariesAlignment = d;
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Center"), "Center");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Top"), "Top");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Bottom"), "Bottom");
                }, tooltip: LocalizationManager.GetString("Tooltip_AimingBoundaries"));

            AddConfigSliders(builder, uiManager);
            builder.AddSeparator();
        }

        private void AddConfigSliders(SectionBuilder builder, UI uiManager)
        {
            builder
                .AddSlider(LocalizationManager.GetString("Slider_MouseSensitivity"), "Mouse Sensitivity (+/-)", LocalizationManager.GetString("Unit_Sensitivity"), 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_MouseSensitivity = s;
                    s.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        var value = s.Slider.Value;
                        if (value >= 0.98)
                            LogManager.Log(LogManager.LogLevel.Warning,
                                LocalizationManager.GetString("Msg_SensitivityTooHigh"), true);
                        else if (value <= 0.1)
                            LogManager.Log(LogManager.LogLevel.Warning,
                                LocalizationManager.GetString("Msg_SensitivityTooLow"), true);
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_MouseSensitivity"))
                .AddSlider(LocalizationManager.GetString("Slider_MouseJitter"), "Mouse Jitter", LocalizationManager.GetString("Unit_Jitter"), 1, 1, 0, 15, s => uiManager.S_MouseJitter = s,
                    tooltip: LocalizationManager.GetString("Tooltip_MouseJitter"))
                .AddToggle(LocalizationManager.GetString("Toggle_YAxisPercent"), "Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t,
                    tooltip: LocalizationManager.GetString("Tooltip_YAxisPercent"))
                .AddToggle(LocalizationManager.GetString("Toggle_XAxisPercent"), "X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t,
                    tooltip: LocalizationManager.GetString("Tooltip_XAxisPercent"))
                .AddSlider(LocalizationManager.GetString("Slider_YOffset"), "Y Offset (Up/Down)", LocalizationManager.GetString("Unit_Offset"), 1, 1, -150, 150, s =>
                {
                    uiManager.S_YOffset = s;
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: LocalizationManager.GetString("Tooltip_YOffset"))
                .AddSlider(LocalizationManager.GetString("Slider_YOffsetPercent"), "Y Offset (%)", LocalizationManager.GetString("Unit_Percent"), 1, 1, 0, 100, s =>
                {
                    uiManager.S_YOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["Y Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_YOffsetPercent"))
                .AddSlider(LocalizationManager.GetString("Slider_XOffset"), "X Offset (Left/Right)", LocalizationManager.GetString("Unit_Offset"), 1, 1, -150, 150, s =>
                {
                    uiManager.S_XOffset = s;
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Collapsed : Visibility.Visible;
                }, tooltip: LocalizationManager.GetString("Tooltip_XOffset"))
                .AddSlider(LocalizationManager.GetString("Slider_XOffsetPercent"), "X Offset (%)", LocalizationManager.GetString("Unit_Percent"), 1, 1, 0, 100, s =>
                {
                    uiManager.S_XOffsetPercent = s;
                    s.Visibility = Dictionary.toggleState["X Axis Percentage Adjustment"]
                        ? Visibility.Visible : Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_XOffsetPercent"));
        }

        private void LoadPredictions()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, Predictions);

            builder
                .AddTitle(LocalizationManager.GetString("Predictions_Title"), true, t =>
                {
                    uiManager.AT_Predictions = t;
                    t.Minimize.Click += (s, e) =>
                    {
                        TogglePanel("Predictions", PredictionsPanel);
                        _mainWindow?.UpdatePredictionSliderVisibility();
                    };
                })
                .AddToggle(LocalizationManager.GetString("Toggle_Predictions"), "Predictions", t => uiManager.T_Predictions = t,
                    tooltip: LocalizationManager.GetString("Tooltip_PredictionsToggle"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_PredictionMethod"), "Prediction Method", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_PredictionMethod = d;
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_KalmanFilter"), "Kalman Filter");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_ShalloePrediction"), "Shall0e's Prediction");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Wisethef0xEMA"), "wisethef0x's EMA Prediction");

                    d.DropdownBox.SelectionChanged += (s, e) => _mainWindow?.UpdatePredictionSliderVisibility();
                }, tooltip: LocalizationManager.GetString("Tooltip_PredictionMethod"))
                .AddSlider(LocalizationManager.GetString("Slider_KalmanLeadTime"), "Kalman Lead Time", LocalizationManager.GetString("Unit_Seconds"), 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_KalmanLeadTime = s;
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_KalmanLeadTime"))
                .AddSlider(LocalizationManager.GetString("Slider_WiseTheFoxLeadTime"), "WiseTheFox Lead Time", LocalizationManager.GetString("Unit_Seconds"), 0.01, 0.01, 0.02, 0.30, s =>
                {
                    uiManager.S_WiseTheFoxLeadTime = s;
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_WiseTheFoxLeadTime"))
                .AddSlider(LocalizationManager.GetString("Slider_ShalloeLeadMultiplier"), "Shalloe Lead Multiplier", LocalizationManager.GetString("Unit_Frames"), 0.5, 0.5, 1, 10, s =>
                {
                    uiManager.S_ShalloeLeadMultiplier = s;
                    s.Visibility = Visibility.Collapsed;
                }, tooltip: LocalizationManager.GetString("Tooltip_ShalloeLeadMultiplier"))
                .AddToggle(LocalizationManager.GetString("Toggle_EMASmoothing"), "EMA Smoothening", t => uiManager.T_EMASmoothing = t,
                    tooltip: LocalizationManager.GetString("Tooltip_EMASmoothingToggle"))
                .AddSlider(LocalizationManager.GetString("Slider_EMASmoothing"), "EMA Smoothening", LocalizationManager.GetString("Unit_Amount"), 0.01, 0.01, 0.01, 1, s =>
                {
                    uiManager.S_EMASmoothing = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["EMA Smoothening"])
                        {
                            MouseManager.smoothingFactor = s.Slider.Value;
                        }
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_EMASmoothing"))
                .AddSeparator();
        }

        private void LoadTriggerBot()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, TriggerBot);

            builder
                .AddTitle(LocalizationManager.GetString("AutoTrigger_Title"), true, t =>
                {
                    uiManager.AT_TriggerBot = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Auto Trigger", TriggerBotPanel);
                })
                .AddToggle(LocalizationManager.GetString("Toggle_AutoTrigger"), "Auto Trigger", t => uiManager.T_AutoTrigger = t,
                    tooltip: LocalizationManager.GetString("Tooltip_AutoTrigger"))
                .AddToggle(LocalizationManager.GetString("Toggle_CursorCheck"), "Cursor Check", t => uiManager.T_CursorCheck = t,
                    tooltip: LocalizationManager.GetString("Tooltip_CursorCheck"))
                .AddToggle(LocalizationManager.GetString("Toggle_SprayMode"), "Spray Mode", t => uiManager.T_SprayMode = t,
                    tooltip: LocalizationManager.GetString("Tooltip_SprayMode"))
                .AddSlider(LocalizationManager.GetString("Slider_AutoTriggerDelay"), "Auto Trigger Delay", LocalizationManager.GetString("Unit_Seconds"), 0.01, 0.1, 0.01, 1, s => uiManager.S_AutoTriggerDelay = s,
                    tooltip: LocalizationManager.GetString("Tooltip_AutoTriggerDelay"))
                .AddSeparator();
        }

        private void LoadFOVConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, FOVConfig);

            builder
                .AddTitle(LocalizationManager.GetString("FOVConfig_Title"), true, t =>
                {
                    uiManager.AT_FOV = t;
                    t.Minimize.Click += (s, e) => TogglePanel("FOV Config", FOVConfigPanel);
                })
                .AddToggle(LocalizationManager.GetString("Toggle_FOV"), "FOV", t => uiManager.T_FOV = t,
                    tooltip: LocalizationManager.GetString("Tooltip_FOVToggle"))
                .AddToggle(LocalizationManager.GetString("Toggle_DynamicFOV"), "Dynamic FOV", t => uiManager.T_DynamicFOV = t,
                    tooltip: LocalizationManager.GetString("Tooltip_DynamicFOV"))
                .AddToggle(LocalizationManager.GetString("Toggle_ThirdPersonSupport"), "Third Person Support", t => uiManager.T_ThirdPersonSupport = t,
                    tooltip: LocalizationManager.GetString("Tooltip_ThirdPerson"))
                .AddKeyChanger(LocalizationManager.GetString("Key_DynamicFOVKeybind"), "Dynamic FOV Keybind", k => uiManager.C_DynamicFOV = k,
                    tooltip: LocalizationManager.GetString("Tooltip_DynamicFOVKeybind"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_FOVStyle"), "FOV Style", d =>
                {
                    uiManager.D_FOVSTYLE = d;

                    var circleItem = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Circle"), "Circle");
                    var rectangleItem = _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Rectangle"), "Rectangle");

                    circleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Visible;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Collapsed;
                    };

                    rectangleItem.Selected += (s, e) =>
                    {
                        MainWindow.FOVWindow.Circle.Visibility = Visibility.Collapsed;
                        MainWindow.FOVWindow.RectangleShape.Visibility = Visibility.Visible;
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_FOVStyle"))
                .AddColorChanger(LocalizationManager.GetString("Color_FOVColor"), "FOV Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (fovColorPickerInstance != null && fovColorPickerInstance.IsVisible)
                        {
                            fovColorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        fovColorPickerInstance = new UISections.ColorPicker(initialColor, LocalizationManager.GetString("Color_FOVColor"));

                        fovColorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["FOV Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostColor(color);
                        };

                        fovColorPickerInstance!.Closed += (sender, args) =>
                        {
                            fovColorPickerInstance = null;
                        };

                        fovColorPickerInstance.Show();
                    };
                })
                .AddSlider(LocalizationManager.GetString("Slider_FOVSize"), "FOV Size", LocalizationManager.GetString("Unit_Size"), 1, 1, 10, 640, s =>
                {
                    uiManager.S_FOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        _mainWindow.ActualFOV = s.Slider.Value;
                        PropertyChanger.PostNewFOVSize(_mainWindow.ActualFOV);
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_FOVSize"))
                .AddSlider(LocalizationManager.GetString("Slider_DynamicFOVSize"), "Dynamic FOV Size", LocalizationManager.GetString("Unit_Size"), 1, 1, 10, 640, s =>
                {
                    uiManager.S_DynamicFOVSize = s;
                    s.Slider.ValueChanged += (sender, e) =>
                    {
                        if (Dictionary.toggleState["Dynamic FOV"])
                            PropertyChanger.PostNewFOVSize(s.Slider.Value);
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_DynamicFOVSize"))
                .AddSeparator();
        }

        private void LoadESPConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ESPConfig);

            builder
                .AddTitle(LocalizationManager.GetString("ESPConfig_Title"), true, t =>
                {
                    uiManager.AT_DetectedPlayer = t;
                    t.Minimize.Click += (s, e) => TogglePanel("ESP Config", ESPConfigPanel);
                })
                .AddToggle(LocalizationManager.GetString("Toggle_ShowDetectedPlayer"), "Show Detected Player", t => uiManager.T_ShowDetectedPlayer = t,
                    tooltip: LocalizationManager.GetString("Tooltip_ShowDetectedPlayer"))
                .AddToggle(LocalizationManager.GetString("Toggle_ShowAIConfidence"), "Show AI Confidence", t => uiManager.T_ShowAIConfidence = t,
                    tooltip: LocalizationManager.GetString("Tooltip_ShowAIConfidence"))
                .AddToggle(LocalizationManager.GetString("Toggle_ShowTracers"), "Show Tracers", t => uiManager.T_ShowTracers = t,
                    tooltip: LocalizationManager.GetString("Tooltip_ShowTracers"));

            builder.AddDropdown(LocalizationManager.GetString("Dropdown_TracerPosition"), "Tracer Position", d =>
            {
                d.DropdownBox.SelectedIndex = 0;
                uiManager.D_TracerPosition = d;
                _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Top"), "Top");
                _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Middle"), "Middle");
                _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Bottom"), "Bottom");
                d.DropdownBox.SelectionChanged += (s, e) =>
                {
                    if (Dictionary.toggleState["Show Detected Player"])
                    {
                        if (uiManager.T_ShowDetectedPlayer?.Reader != null)
                        {
                            uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                            uiManager.T_ShowDetectedPlayer.Reader.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        }
                    }
                    else
                    {
                        if (Dictionary.DetectedPlayerOverlay is not null)
                        {
                            Dictionary.DetectedPlayerOverlay.ForceReposition();
                        }
                    }
                };
            }, tooltip: LocalizationManager.GetString("Tooltip_TracerPosition"));

            builder
                .AddColorChanger(LocalizationManager.GetString("Color_DetectedPlayerColor"), "Detected Player Color", c =>
                {
                    c.Reader.Click += (s, e) =>
                    {
                        if (colorPickerInstance != null && colorPickerInstance.IsVisible)
                        {
                            colorPickerInstance.Activate();
                            return;
                        }

                        Color initialColor = Colors.White;
                        if (c.ColorChangingBorder.Background is SolidColorBrush scb)
                            initialColor = scb.Color;
                        colorPickerInstance = new UISections.ColorPicker(initialColor, LocalizationManager.GetString("Color_DetectedPlayerColor"));

                        colorPickerInstance.ColorChanged += (color) =>
                        {
                            c.ColorChangingBorder.Background = new SolidColorBrush(color);
                            Dictionary.colorState["Detected Player Color"] = $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
                            PropertyChanger.PostDPColor(color);
                        };

                        colorPickerInstance.Closed += (sender, args) =>
                        {
                            colorPickerInstance = null;
                        };

                        colorPickerInstance.Show();
                    };
                })
                .AddSlider(LocalizationManager.GetString("Slider_AIConfidenceFontSize"), "AI Confidence Font Size", LocalizationManager.GetString("Unit_Size"), 1, 1, 1, 30, s =>
                {
                    uiManager.S_DPFontSize = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPFontSize((int)s.Slider.Value);
                }, tooltip: LocalizationManager.GetString("Tooltip_AIConfidenceFontSize"))
                .AddSlider(LocalizationManager.GetString("Slider_CornerRadius"), "Corner Radius", LocalizationManager.GetString("Unit_Radius"), 1, 1, 0, 100, s =>
                {
                    uiManager.S_DPCornerRadius = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWCornerRadius((int)s.Slider.Value);
                }, tooltip: LocalizationManager.GetString("Tooltip_CornerRadius"))
                .AddSlider(LocalizationManager.GetString("Slider_BorderThickness"), "Border Thickness", LocalizationManager.GetString("Unit_Thickness"), 0.1, 1, 0.1, 10, s =>
                {
                    uiManager.S_DPBorderThickness = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWBorderThickness(s.Slider.Value);
                }, tooltip: LocalizationManager.GetString("Tooltip_BorderThickness"))
                .AddSlider(LocalizationManager.GetString("Slider_Opacity"), "Opacity", LocalizationManager.GetString("Unit_Opacity_Unit"), 0.1, 0.1, 0, 1, s =>
                {
                    uiManager.S_DPOpacity = s;
                    s.Slider.ValueChanged += (sender, e) => PropertyChanger.PostDPWOpacity(s.Slider.Value);
                }, tooltip: LocalizationManager.GetString("Tooltip_Opacity"))
                .AddSeparator();
        }

        #endregion

        #region Helper Methods

        private void OnImageSizeChanged(int imageSize)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (_mainWindow?.uiManager.S_FOVSize != null && _mainWindow?.uiManager.S_DynamicFOVSize != null)
                {
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_FOVSize, imageSize);
                    UpdateFovSizeSlider(_mainWindow.uiManager.S_DynamicFOVSize, imageSize);
                }
            });
        }

        private void UpdateFovSizeSlider(ASlider slider, int imageSize = 640)
        {
            if (slider.Slider == null) return;
            if (imageSize < slider.Slider.Value)
            {
                slider.Slider.Value = imageSize;
            }
            slider.Slider.Maximum = imageSize;
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        private void HandleColorChange(AColorChanger colorChanger, string settingKey, Action<Color> updateAction)
        {
            var colorDialog = new System.Windows.Forms.ColorDialog();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var color = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                colorChanger.ColorChangingBorder.Background = new SolidColorBrush(color);
                Dictionary.colorState[settingKey] = color.ToString();
                updateAction(color);
            }
        }

        public void Dispose()
        {
            SaveMinimizeStatesToGlobal();
        }

        public void RebuildSections()
        {
            if (!_isInitialized) return;

            AimAssist.Children.Clear();
            AimConfig.Children.Clear();
            Predictions.Children.Clear();
            TriggerBot.Children.Clear();
            FOVConfig.Children.Clear();
            ESPConfig.Children.Clear();

            LoadAimAssist();
            LoadAimConfig();
            LoadPredictions();
            LoadTriggerBot();
            LoadFOVConfig();
            LoadESPConfig();

            ApplyMinimizeStates();
        }

        #endregion

        #region Section Builder

        private class SectionBuilder
        {
            private readonly AimMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(AimMenuControl parent, StackPanel panel)
            {
                _parent = parent;
                _panel = panel;
            }

            public SectionBuilder AddTitle(string title, bool canMinimize, Action<ATitle>? configure = null)
            {
                var titleControl = new ATitle(title, canMinimize);
                configure?.Invoke(titleControl);
                _panel.Children.Add(titleControl);
                return this;
            }

            public SectionBuilder AddToggle(string title, string stableKey, Action<AToggle>? configure = null, string? tooltip = null)
            {
                var toggle = _parent.CreateToggle(title, stableKey, tooltip);
                configure?.Invoke(toggle);
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddKeyChanger(string title, string stableKey, Action<AKeyChanger>? configure = null, string? defaultKey = null, string? tooltip = null)
            {
                var key = defaultKey ?? Dictionary.bindingSettings[stableKey];
                var keyChanger = _parent.CreateKeyChanger(title, stableKey, key, tooltip);
                configure?.Invoke(keyChanger);
                _panel.Children.Add(keyChanger);
                return this;
            }

            public SectionBuilder AddSlider(string title, string stableKey, string label, double frequency, double buttonSteps,
                double min, double max, Action<ASlider>? configure = null, string? tooltip = null)
            {
                var slider = _parent.CreateSlider(title, stableKey, label, frequency, buttonSteps, min, max, tooltip);
                configure?.Invoke(slider);
                _panel.Children.Add(slider);
                return this;
            }

            public SectionBuilder AddDropdown(string title, string dictionaryKey, Action<ADropdown>? configure = null, string? tooltip = null)
            {
                var dropdown = _parent.CreateDropdown(title, dictionaryKey, tooltip);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddColorChanger(string title, string stableKey, Action<AColorChanger>? configure = null)
            {
                var colorChanger = _parent.CreateColorChanger(title, stableKey);
                configure?.Invoke(colorChanger);
                _panel.Children.Add(colorChanger);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null, string? tooltip = null)
            {
                var button = new APButton(title, tooltip);
                configure?.Invoke(button);
                _panel.Children.Add(button);
                return this;
            }

            public SectionBuilder AddFileLocator(string title, Action<AFileLocator>? configure = null,
                string filter = "All files (*.*)|*.*", string dlExtension = "")
            {
                var fileLocator = new AFileLocator(title, title, filter, dlExtension);
                configure?.Invoke(fileLocator);
                _panel.Children.Add(fileLocator);
                return this;
            }

            public SectionBuilder AddSeparator()
            {
                _panel.Children.Add(new ARectangleBottom());
                _panel.Children.Add(new ASpacer());
                return this;
            }
        }

        #endregion

        #region Control Creation Methods

        private AToggle CreateToggle(string title, string stableKey, string? tooltip = null)
        {
            var toggle = new AToggle(title, tooltip);
            _mainWindow!.toggleInstances[stableKey] = toggle;

            if (Dictionary.toggleState.TryGetValue(stableKey, out var ts) && ts)
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();

            toggle.Reader.Click += (sender, e) =>
            {
                Dictionary.toggleState[stableKey] = !(Dictionary.toggleState.TryGetValue(stableKey, out var ts2) && ts2);
                _mainWindow.UpdateToggleUI(toggle, Dictionary.toggleState.TryGetValue(stableKey, out var ts3) && ts3);
                _mainWindow.Toggle_Action(stableKey);
            };

            return toggle;
        }

        private AKeyChanger CreateKeyChanger(string title, string stableKey, string keybind, string? tooltip = null)
        {
            var keyChanger = new AKeyChanger(title, keybind, tooltip);

            keyChanger.Reader.Click += (sender, e) =>
            {
                keyChanger.KeyNotifier.Content = "...";
                _mainWindow!.bindingManager.StartListeningForBinding(stableKey);

                Action<string, string>? bindingSetHandler = null;
                bindingSetHandler = (bindingId, key) =>
                {
                    if (bindingId == stableKey)
                    {
                        keyChanger.KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(key);
                        Dictionary.bindingSettings[bindingId] = key;
                        _mainWindow.bindingManager.OnBindingSet -= bindingSetHandler;
                    }
                };

                _mainWindow.bindingManager.OnBindingSet += bindingSetHandler;
            };

            return keyChanger;
        }

        private ASlider CreateSlider(string title, string stableKey, string label, double frequency, double buttonSteps,
            double min, double max, string? tooltip = null)
        {
            var slider = new ASlider(title, label, buttonSteps, tooltip)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            slider.Slider.Value = Dictionary.sliderSettings.TryGetValue(stableKey, out var value) ? value : min;
            slider.Slider.ValueChanged += (s, e) => Dictionary.sliderSettings[stableKey] = slider.Slider.Value;

            return slider;
        }

        private ADropdown CreateDropdown(string title, string dictionaryKey, string? tooltip = null) => new(title, dictionaryKey, tooltip);

        private AColorChanger CreateColorChanger(string title, string stableKey)
        {
            var colorChanger = new AColorChanger(title);
            var colorBrush = Dictionary.colorState.TryGetValue(stableKey, out var colorVal)
                ? (Brush)new BrushConverter().ConvertFromString(colorVal)
                : Brushes.White;
            colorChanger.ColorChangingBorder.Background = colorBrush;
            return colorChanger;
        }

        #endregion
    }
}
