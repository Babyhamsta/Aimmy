using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.UILibrary;
using Class;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using MouseMovementLibraries.MakcuSupport;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UILibrary;
using Visuality;

namespace Aimmy2.Controls
{
    public partial class SettingsMenuControl : UserControl
    {
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        // Local minimize state management
        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Settings Menu", false },
            { "X/Y Percentage Adjustment", false },
            { "Theme Settings", false },
            { "Display Settings", false }
        };

        // Public properties for MainWindow access
        public StackPanel SettingsConfigPanel => SettingsConfig;
        public StackPanel XYPercentageEnablerMenuPanel => XYPercentageEnablerMenu;
        public StackPanel ThemeMenuPanel => ThemeMenu;
        public StackPanel DisplaySelectMenuPanel => DisplaySelectMenu;
        public ScrollViewer SettingsMenuScrollViewer => SettingsMenu;

        public SettingsMenuControl()
        {
            InitializeComponent();
        }

        public void Initialize(MainWindow mainWindow)
        {
            if (_isInitialized) return;

            _mainWindow = mainWindow;
            _isInitialized = true;

            // Load minimize states from global dictionary if they exist
            LoadMinimizeStatesFromGlobal();

            LoadSettingsConfig();
            LoadXYPercentageMenu();
            LoadThemeMenu();
            LoadDisplaySelectMenu();

            // Apply minimize states after loading
            ApplyMinimizeStates();

            // Subscribe to display changes
            DisplayManager.DisplayChanged += OnDisplayChanged;
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
            ApplyPanelState("Settings Menu", SettingsConfigPanel);
            ApplyPanelState("X/Y Percentage Adjustment", XYPercentageEnablerMenuPanel);
            ApplyPanelState("Theme Settings", ThemeMenuPanel);
            ApplyPanelState("Display Settings", DisplaySelectMenuPanel);
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
                // Keep titles, spacers, and bottom rectangles always visible
                bool shouldStayVisible = child is ATitle || child is ASpacer || child is ARectangleBottom;

                child.Visibility = shouldStayVisible
                    ? Visibility.Visible
                    : (isVisible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        private void TogglePanel(string stateName, StackPanel panel)
        {
            if (!_localMinimizeState.ContainsKey(stateName)) return;

            // Toggle the state
            _localMinimizeState[stateName] = !_localMinimizeState[stateName];

            // Apply the new visibility
            SetPanelVisibility(panel, !_localMinimizeState[stateName]);

            // Save to global dictionary
            SaveMinimizeStatesToGlobal();
        }

        #endregion

        #region Menu Section Loaders

        private void LoadSettingsConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, SettingsConfig);

            builder
                .AddTitle("Settings Menu", true, t =>
                {
                    uiManager.AT_SettingsMenu = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Settings Menu", SettingsConfigPanel);
                })
                .AddToggle("Collect Data While Playing", t => uiManager.T_CollectDataWhilePlaying = t)
                .AddToggle("Auto Label Data", t => uiManager.T_AutoLabelData = t)
                .AddDropdown("Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection

                    // Add options
                    _mainWindow.AddDropdownItem(d, "Mouse Event");
                    _mainWindow.AddDropdownItem(d, "SendInput");
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");
                    uiManager.DDI_MAKCU = _mainWindow.AddDropdownItem(d, "MAKCU Support");

                    // Setup handlers
                    uiManager.DDI_LGHUB.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!new LGHubMain().Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_RazerSynapse.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await RZMouse.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_ddxoft.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await DdxoftMain.Load())
                            await ResetToMouseEvent();
                    };
                })
                .AddDropdown("Screen Capture Method", d =>
                {
                    uiManager.D_ScreenCaptureMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection
                    _mainWindow.AddDropdownItem(d, "DirectX");
                    _mainWindow.AddDropdownItem(d, "GDI+");
                })
                .AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100, s =>
                {
                    uiManager.S_AIMinimumConfidence = s;
                    s.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        var value = s.Slider.Value;
                        if (value >= 95)
                            ShowNotice("The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.");
                        else if (value <= 35)
                            ShowNotice("The minimum confidence you have set for Aimmy may be too low can cause false positives.");
                    };
                })
                .AddToggle("Mouse Background Effect", t => uiManager.T_MouseBackgroundEffect = t)
                .AddToggle("UI TopMost", t => uiManager.T_UITopMost = t)
                .AddButton("Save Config", b =>
                {
                    uiManager.B_SaveConfig = b;
                    b.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
                })
                .AddSeparator();
        }

        private void LoadXYPercentageMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, XYPercentageEnablerMenu);

            builder
                .AddTitle("X/Y Percentage Adjustment", true, t =>
                {
                    uiManager.AT_XYPercentageAdjustmentEnabler = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("X/Y Percentage Adjustment", XYPercentageEnablerMenuPanel);
                })
                .AddToggle("X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t)
                .AddToggle("Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t)
                .AddSeparator();
        }

        private void LoadDisplaySelectMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, DisplaySelectMenu);

            builder
                .AddTitle("Display Settings", true, t =>
                {
                    uiManager.AT_DisplaySelector = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Display Settings", DisplaySelectMenuPanel);
                })
                .AddSeparator();

            // Handle DisplaySelector separately as it's a custom control
            uiManager.DisplaySelector = new ADisplaySelector();
            uiManager.DisplaySelector.RefreshDisplays();

            // Insert after title but before separator
            var insertIndex = DisplaySelectMenu.Children.Count - 2;
            DisplaySelectMenu.Children.Insert(insertIndex, uiManager.DisplaySelector);

            // Add refresh button after DisplaySelector
            var refreshButton = new APButton("Refresh Displays");
            refreshButton.Reader.Click += (s, e) =>
            {
                try
                {
                    DisplayManager.RefreshDisplays();
                    uiManager.DisplaySelector.RefreshDisplays();
                    ShowNotice("Display list refreshed successfully");
                }
                catch (Exception ex)
                {
                    ShowNotice($"Error refreshing displays: {ex.Message}");
                }
            };
            DisplaySelectMenu.Children.Insert(insertIndex + 1, refreshButton);
        }

        private void LoadThemeMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ThemeMenu);

            builder
                .AddTitle("Theme Settings", true, t =>
                {
                    uiManager.AT_ThemeColorWheel = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Theme Settings", ThemeMenuPanel);
                })
                .AddSeparator();

            // Handle ColorWheel separately as it's a custom control
            uiManager.ThemeColorWheel = new AColorWheel();

            // Insert before separator
            var insertIndex = ThemeMenu.Children.Count - 1;
            ThemeMenu.Children.Insert(insertIndex, uiManager.ThemeColorWheel);
        }

        #endregion

        #region Helper Methods

        #endregion

        #region Menu Section Loaders

        private void LoadSettingsConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, SettingsConfig);

            builder
                .AddTitle("Settings Menu", true, t =>
                {
                    uiManager.AT_SettingsMenu = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Settings Menu", SettingsConfigPanel);
                })
                .AddToggle("Collect Data While Playing", t => uiManager.T_CollectDataWhilePlaying = t)
                .AddToggle("Auto Label Data", t => uiManager.T_AutoLabelData = t)
                .AddDropdown("Mouse Movement Method", d =>
                {
                    uiManager.D_MouseMovementMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection

                    // Add options
                    _mainWindow!.AddDropdownItem(d, "Mouse Event");
                    _mainWindow.AddDropdownItem(d, "SendInput");

                    // Special options with validation
                    uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(d, "LG HUB");
                    uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(d, "Razer Synapse (Require Razer Peripheral)");
                    uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(d, "ddxoft Virtual Input Driver");
                    uiManager.DDI_MAKCU = _mainWindow.AddDropdownItem(d, "MAKCU Support");
                    // Setup handlers
                    uiManager.DDI_LGHUB.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!new LGHubMain().Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_RazerSynapse.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await RZMouse.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_ddxoft.Selected += async (s, e) =>
                    {
                        MakcuMain.Unload();
                        if (!await DdxoftMain.Load())
                            await ResetToMouseEvent();
                    };

                    uiManager.DDI_MAKCU.Selected += async (s, e) =>
                    {
                        if (!await MakcuMain.Load())
                            await ResetToMouseEvent();
                    };
                })
                .AddDropdown("Screen Capture Method", d =>
                {
                    uiManager.D_ScreenCaptureMethod = d;
                    d.DropdownBox.SelectedIndex = -1;  // Prevent auto-selection
                    _mainWindow.AddDropdownItem(d, "DirectX");
                    _mainWindow.AddDropdownItem(d, "GDI+");
                })
                .AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100, s =>
                {
                    uiManager.S_AIMinimumConfidence = s;
                    s.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        var value = s.Slider.Value;
                        if (value >= 95)
                            ShowNotice("The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.");
                        else if (value <= 35)
                            ShowNotice("The minimum confidence you have set for Aimmy may be too low can cause false positives.");
                    };
                })
                .AddToggle("Mouse Background Effect", t => uiManager.T_MouseBackgroundEffect = t)
                .AddToggle("UI TopMost", t => uiManager.T_UITopMost = t)
                .AddButton("Save Config", b =>
                {
                    uiManager.B_SaveConfig = b;
                    b.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
                })
                .AddSeparator();
        }

        private void LoadXYPercentageMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, XYPercentageEnablerMenu);

            builder
                .AddTitle("X/Y Percentage Adjustment", true, t =>
                {
                    uiManager.AT_XYPercentageAdjustmentEnabler = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("X/Y Percentage Adjustment", XYPercentageEnablerMenuPanel);
                })
                .AddToggle("X Axis Percentage Adjustment", t => uiManager.T_XAxisPercentageAdjustment = t)
                .AddToggle("Y Axis Percentage Adjustment", t => uiManager.T_YAxisPercentageAdjustment = t)
                .AddSeparator();
        }

        private void LoadDisplaySelectMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, DisplaySelectMenu);

            builder
                .AddTitle("Display Settings", true, t =>
                {
                    uiManager.AT_DisplaySelector = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Display Settings", DisplaySelectMenuPanel);
                })
                .AddSeparator();

            // Handle DisplaySelector separately as it's a custom control
            uiManager.DisplaySelector = new ADisplaySelector();
            uiManager.DisplaySelector.RefreshDisplays();

            // Insert after title but before separator
            var insertIndex = DisplaySelectMenu.Children.Count - 2;
            DisplaySelectMenu.Children.Insert(insertIndex, uiManager.DisplaySelector);

            // Add refresh button after DisplaySelector
            var refreshButton = new APButton("Refresh Displays");
            refreshButton.Reader.Click += (s, e) =>
            {
                try
                {
                    DisplayManager.RefreshDisplays();
                    uiManager.DisplaySelector.RefreshDisplays();
                    ShowNotice("Display list refreshed successfully");
                }
                catch (Exception ex)
                {
                    ShowNotice($"Error refreshing displays: {ex.Message}");
                }
            };
            DisplaySelectMenu.Children.Insert(insertIndex + 1, refreshButton);
        }

        private void LoadThemeMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ThemeMenu);

            builder
                .AddTitle("Theme Settings", true, t =>
                {
                    uiManager.AT_ThemeColorWheel = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Theme Settings", ThemeMenuPanel);
                })
                .AddSeparator();

            // Handle ColorWheel separately as it's a custom control
            uiManager.ThemeColorWheel = new AColorWheel();

            // Insert before separator
            var insertIndex = ThemeMenu.Children.Count - 1;
            ThemeMenu.Children.Insert(insertIndex, uiManager.ThemeColorWheel);
        }

        #endregion

        #region Helper Methods

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    ShowNotice($"AI focus switched to Display {e.DisplayIndex + 1} ({e.Bounds.Width}x{e.Bounds.Height})");
                    UpdateDisplayRelatedSettings(e);
                }
                catch (Exception ex)
                {
                }
            });
        }

        private void UpdateDisplayRelatedSettings(DisplayChangedEventArgs e)
        {
            Dictionary.sliderSettings["SelectedDisplay"] = e.DisplayIndex;
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        private void ShowNotice(string message, int duration = 4000) =>
            new NoticeBar(message, duration).Show();

        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            _mainWindow?.uiManager.DisplaySelector?.Dispose();

            // Save minimize states before disposing
            SaveMinimizeStatesToGlobal();
        }

        #endregion

        #region Control Creation Methods

        private AToggle CreateToggle(string title)
        {
            var toggle = new AToggle(title);
            _mainWindow!.toggleInstances[title] = toggle;

            // Set initial state
            if (Dictionary.toggleState[title])
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();

            // Handle click
            toggle.Reader.Click += (sender, e) =>
            {
                Dictionary.toggleState[title] = !Dictionary.toggleState[title];
                _mainWindow.UpdateToggleUI(toggle, Dictionary.toggleState[title]);
                _mainWindow.Toggle_Action(title);
            };

            return toggle;
        }

        private ASlider CreateSlider(string title, string label, double frequency, double buttonSteps,
            double min, double max)
        {
            var slider = new ASlider(title, label, buttonSteps)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            slider.Slider.Value = Dictionary.sliderSettings.TryGetValue(title, out var value) ? value : min;
            slider.Slider.ValueChanged += (s, e) => Dictionary.sliderSettings[title] = slider.Slider.Value;

            return slider;
        }

        private ADropdown CreateDropdown(string title) => new(title, title);

        #endregion

        #region Section Builder

        private class SectionBuilder
        {
            private readonly SettingsMenuControl _parent;
            private readonly StackPanel _panel;

            public SectionBuilder(SettingsMenuControl parent, StackPanel panel)
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

            public SectionBuilder AddToggle(string title, Action<AToggle>? configure = null)
            {
                var toggle = _parent.CreateToggle(title);
                configure?.Invoke(toggle);
                _panel.Children.Add(toggle);
                return this;
            }

            public SectionBuilder AddSlider(string title, string label, double frequency, double buttonSteps,
                double min, double max, Action<ASlider>? configure = null)
            {
                var slider = _parent.CreateSlider(title, label, frequency, buttonSteps, min, max);
                configure?.Invoke(slider);
                _panel.Children.Add(slider);
                return this;
            }

            public SectionBuilder AddDropdown(string title, Action<ADropdown>? configure = null)
            {
                var dropdown = _parent.CreateDropdown(title);
                configure?.Invoke(dropdown);
                _panel.Children.Add(dropdown);
                return this;
            }

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null)
            {
                var button = new APButton(title);
                configure?.Invoke(button);
                _panel.Children.Add(button);
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
    }
}