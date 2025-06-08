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

        private void LoadSettingsConfig()
        {
            var uiManager = _mainWindow!.uiManager;

            // Title with minimize button
            uiManager.AT_SettingsMenu = new ATitle("Settings Menu", true);
            uiManager.AT_SettingsMenu.Minimize.Click += (s, e) => TogglePanel("Settings Menu", SettingsConfigPanel);
            SettingsConfig.Children.Add(uiManager.AT_SettingsMenu);

            // Toggles
            AddToggleToPanel(SettingsConfig, "Collect Data While Playing");
            AddToggleToPanel(SettingsConfig, "Auto Label Data");

            // Mouse Movement Method dropdown
            uiManager.D_MouseMovementMethod = CreateDropdown("Mouse Movement Method");
            SettingsConfig.Children.Add(uiManager.D_MouseMovementMethod);
            SetupMouseMovementDropdown(uiManager);

            uiManager.D_ScreenCaptureMethod = CreateDropdown("Screen Capture Method");
            SettingsConfig.Children.Add(uiManager.D_ScreenCaptureMethod);

            _mainWindow.AddDropdownItem(uiManager.D_ScreenCaptureMethod, "DirectX");
            _mainWindow.AddDropdownItem(uiManager.D_ScreenCaptureMethod, "GDI");

            // AI Confidence slider
            uiManager.S_AIMinimumConfidence = CreateSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100);
            SettingsConfig.Children.Add(uiManager.S_AIMinimumConfidence);

            uiManager.S_AIMinimumConfidence.Slider.PreviewMouseLeftButtonUp += (s, e) =>
            {
                var value = uiManager.S_AIMinimumConfidence.Slider.Value;
                if (value >= 95)
                {
                    ShowNotice("The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.");
                }
                else if (value <= 35)
                {
                    ShowNotice("The minimum confidence you have set for Aimmy may be too low can cause false positives.");
                }
            };

            // More toggles
            AddToggleToPanel(SettingsConfig, "Mouse Background Effect");
            AddToggleToPanel(SettingsConfig, "UI TopMost");

            // Save button
            uiManager.B_SaveConfig = new APButton("Save Config");
            uiManager.B_SaveConfig.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
            SettingsConfig.Children.Add(uiManager.B_SaveConfig);

            AddSeparator(SettingsConfig);
        }

        private void SetupMouseMovementDropdown(UI uiManager)
        {
            var dropdown = uiManager.D_MouseMovementMethod!;

            // Add options
            _mainWindow!.AddDropdownItem(dropdown, "Mouse Event");
            _mainWindow.AddDropdownItem(dropdown, "SendInput");

            // Special options with validation
            uiManager.DDI_LGHUB = _mainWindow.AddDropdownItem(dropdown, "LG HUB");
            uiManager.DDI_RazerSynapse = _mainWindow.AddDropdownItem(dropdown, "Razer Synapse (Require Razer Peripheral)");
            uiManager.DDI_ddxoft = _mainWindow.AddDropdownItem(dropdown, "ddxoft Virtual Input Driver");
            uiManager.DDI_MAKCU = _mainWindow.AddDropdownItem(dropdown, "MAKCU Support");

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
        }

        private void LoadXYPercentageMenu()
        {
            var uiManager = _mainWindow!.uiManager;

            // Title with minimize button
            uiManager.AT_XYPercentageAdjustmentEnabler = new ATitle("X/Y Percentage Adjustment", true);
            uiManager.AT_XYPercentageAdjustmentEnabler.Minimize.Click += (s, e) =>
                TogglePanel("X/Y Percentage Adjustment", XYPercentageEnablerMenuPanel);
            XYPercentageEnablerMenu.Children.Add(uiManager.AT_XYPercentageAdjustmentEnabler);

            // Toggles
            AddToggleToPanel(XYPercentageEnablerMenu, "X Axis Percentage Adjustment");
            AddToggleToPanel(XYPercentageEnablerMenu, "Y Axis Percentage Adjustment");

            AddSeparator(XYPercentageEnablerMenu);
        }

        private void LoadDisplaySelectMenu()
        {
            var uiManager = _mainWindow!.uiManager;

            // Title with minimize button
            uiManager.AT_DisplaySelector = new ATitle("Display Settings", true);
            uiManager.AT_DisplaySelector.Minimize.Click += (s, e) =>
                TogglePanel("Display Settings", DisplaySelectMenuPanel);
            DisplaySelectMenu.Children.Add(uiManager.AT_DisplaySelector);

            // Main Display Selector
            uiManager.DisplaySelector = new ADisplaySelector();
            uiManager.DisplaySelector.RefreshDisplays();
            DisplaySelectMenu.Children.Add(uiManager.DisplaySelector);

            // Add refresh button for manual refresh
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
            DisplaySelectMenu.Children.Add(refreshButton);

            AddSeparator(DisplaySelectMenu);
        }

        private void LoadThemeMenu()
        {
            var uiManager = _mainWindow!.uiManager;

            // Title with minimize button
            uiManager.AT_ThemeColorWheel = new ATitle("Theme Settings", true);
            uiManager.AT_ThemeColorWheel.Minimize.Click += (s, e) =>
                TogglePanel("Theme Settings", ThemeMenuPanel);
            ThemeMenu.Children.Add(uiManager.AT_ThemeColorWheel);

            // Main Color Wheel
            uiManager.ThemeColorWheel = new AColorWheel();
            ThemeMenu.Children.Add(uiManager.ThemeColorWheel);

            AddSeparator(ThemeMenu);
        }

        private async Task ResetToMouseEvent()
        {
            await Task.Delay(500);
            _mainWindow!.uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        #region Helper Methods

        private void AddToggleToPanel(Panel panel, string title)
        {
            var toggle = CreateToggle(title);
            panel.Children.Add(toggle);
            _mainWindow!.uiManager.GetType().GetProperty($"T_{title.Replace(" ", "")}")?.SetValue(_mainWindow.uiManager, toggle);
        }

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

        private ASlider CreateSlider(string title, string label, double frequency, double buttonsteps, double min, double max)
        {
            var slider = new ASlider(title, label, buttonsteps)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            slider.Slider.Value = Dictionary.sliderSettings.TryGetValue(title, out var value) ? value : min;
            slider.Slider.ValueChanged += (s, e) => Dictionary.sliderSettings[title] = slider.Slider.Value;

            return slider;
        }

        private ADropdown CreateDropdown(string title) => new(title, title);

        private void AddSeparator(Panel panel)
        {
            panel.Children.Add(new ARectangleBottom());
            panel.Children.Add(new ASpacer());
        }

        private void ShowNotice(string message) => new NoticeBar(message, 4000).Show();

        // Clean up event subscriptions
        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            _mainWindow?.uiManager.DisplaySelector?.Dispose();

            // Save minimize states before disposing
            SaveMinimizeStatesToGlobal();
        }

        #endregion
    }
}