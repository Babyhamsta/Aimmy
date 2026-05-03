using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.Other;
using Aimmy2.Resources;
using Aimmy2.UILibrary;
using Class;
using Other;
using System.Windows;
using System.Windows.Controls;
using UILibrary;
using Visuality;
using LogLevel = Other.LogManager.LogLevel;

namespace Aimmy2.Controls
{
    public partial class SettingsMenuControl : UserControl
    {
        private MainWindow? _mainWindow;
        private bool _isInitialized;

        private readonly Dictionary<string, bool> _localMinimizeState = new()
        {
            { "Model Settings", false },
            { "Settings Menu", false },
            { "Theme Settings", false },
            { "Screen Settings", false }
        };

        public StackPanel ModelSettingsPanel => ModelSettings;
        public StackPanel SettingsConfigPanel => SettingsConfig;
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

            LoadMinimizeStatesFromGlobal();

            LoadModelSettings();
            LoadSettingsConfig();
            LoadThemeMenu();
            LoadDisplaySelectMenu();

            ApplyMinimizeStates();

            DisplayManager.DisplayChanged += OnDisplayChanged;

            AIManager.ClassesUpdated += OnClassesChanged;

            AIManager.DynamicModelStatusChanged += OnDynamicModelStatusChanged;

            UpdateDynamicModelDropdownsVisibility(AIManager.CurrentModelIsDynamic);
            UpdateTargetClassDropdown(_mainWindow!.uiManager.D_TargetClass!);
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
            ApplyPanelState("Model Settings", ModelSettingsPanel);
            ApplyPanelState("Settings Menu", SettingsConfigPanel);
            ApplyPanelState("Theme Settings", ThemeMenuPanel);
            ApplyPanelState("Screen Settings", DisplaySelectMenuPanel);
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

        private void LoadModelSettings()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ModelSettings);

            builder
                .AddTitle(LocalizationManager.GetString("Section_ModelSettings"), true, t =>
                {
                    uiManager.AT_ModelSettings = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Model Settings", ModelSettingsPanel);
                })
                .AddDropdown(LocalizationManager.GetString("Dropdown_ImageSize"), "Image Size", d =>
                {
                    uiManager.D_ImageSize = d;

                    _mainWindow.AddDropdownItem(d, "640");
                    _mainWindow.AddDropdownItem(d, "512");
                    _mainWindow.AddDropdownItem(d, "416");
                    _mainWindow.AddDropdownItem(d, "320");
                    _mainWindow.AddDropdownItem(d, "256");
                    _mainWindow.AddDropdownItem(d, "160");

                    var currentSize = Dictionary.dropdownState["Image Size"];
                    for (int i = 0; i < d.DropdownBox.Items.Count; i++)
                    {
                        if ((d.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == currentSize)
                        {
                            d.DropdownBox.SelectedIndex = i;
                            break;
                        }
                    }

                    d.DropdownBox.SelectionChanged += async (s, e) =>
                    {
                        if (d.DropdownBox.SelectedItem == null || e.AddedItems.Count == 0)
                            return;

                        var newSize = (d.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        if (string.IsNullOrEmpty(newSize))
                            return;

                        if (FileManager.AIManager == null || Dictionary.lastLoadedModel == "N/A")
                        {
                            Dictionary.dropdownState["Image Size"] = newSize;
                            LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_ImageSizeSet", newSize), true, 2000);
                            return;
                        }

                        FileManager.CurrentlyLoadingModel = true;
                        LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_ImageSizeChanging", newSize));

                        try
                        {
                            if (FileManager.AIManager != null)
                            {
                                FileManager.AIManager.RequestSizeChange(int.Parse(newSize));
                                await Task.Delay(100);
                            }

                            var modelPath = System.IO.Path.Combine("bin/models", Dictionary.lastLoadedModel);
                            FileManager.AIManager?.Dispose();
                            FileManager.AIManager = null;

                            Dictionary.dropdownState["Image Size"] = newSize;

                            FileManager.AIManager = new AIManager(modelPath);

                            LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_ImageSizeChanged", newSize), true, 2000);
                        }
                        catch (Exception ex)
                        {
                            LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_ImageSizeError", ex.Message), true, 5000);
                        }
                        finally
                        {
                            FileManager.CurrentlyLoadingModel = false;
                        }
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_ImageSize"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_TargetClass"), "Target Class", d =>
                {
                    d.DropdownBox.SelectedIndex = 0;
                    uiManager.D_TargetClass = d;
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_BestConfidence"), "Best Confidence");
                    UpdateTargetClassDropdown(d);
                }, tooltip: LocalizationManager.GetString("Tooltip_TargetClass"))
                .AddSlider(LocalizationManager.GetString("Slider_AIMinimumConfidence"), "AI Minimum Confidence", LocalizationManager.GetString("Unit_ConfidencePercent"), 1, 1, 1, 100, s =>
                {
                    uiManager.S_AIMinimumConfidence = s;
                    s.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
                    {
                        var value = s.Slider.Value;
                        if (value >= 95)
                            LogManager.Log(LogLevel.Warning, LocalizationManager.GetString("Msg_ConfidenceTooHigh"), true);
                        else if (value <= 35)
                            LogManager.Log(LogLevel.Warning, LocalizationManager.GetString("Msg_ConfidenceTooLow"), true);
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_AIMinimumConfidence"))
                .AddToggle(LocalizationManager.GetString("Toggle_EnableModelSwitchKeybind"), "Enable Model Switch Keybind", t => uiManager.T_EnableModelSwitchKeybind = t,
                    tooltip: LocalizationManager.GetString("Tooltip_EnableModelSwitch"))
                .AddKeyChanger(LocalizationManager.GetString("Key_ModelSwitchKeybind"), "Model Switch Keybind", k => uiManager.C_ModelSwitchKeybind = k,
                    tooltip: LocalizationManager.GetString("Tooltip_ModelSwitchKeybind"))
                .AddKeyChanger(LocalizationManager.GetString("Key_EmergencyStopKeybind"), "Emergency Stop Keybind", k => uiManager.C_EmergencyKeybind = k,
                    tooltip: LocalizationManager.GetString("Tooltip_EmergencyStopKeybind"))
                .AddSeparator();
        }

        private void LoadSettingsConfig()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, SettingsConfig);

            builder
                .AddTitle(LocalizationManager.GetString("Section_SettingsMenu"), true, t =>
                {
                    uiManager.AT_SettingsMenu = t;
                    t.Minimize.Click += (s, e) => TogglePanel("Settings Menu", SettingsConfigPanel);
                })
                .AddToggle(LocalizationManager.GetString("Toggle_CollectData"), "Collect Data While Playing", t => uiManager.T_CollectDataWhilePlaying = t,
                    tooltip: LocalizationManager.GetString("Tooltip_CollectData"))
                .AddToggle(LocalizationManager.GetString("Toggle_AutoLabelData"), "Auto Label Data", t => uiManager.T_AutoLabelData = t,
                    tooltip: LocalizationManager.GetString("Tooltip_AutoLabelData"))
                .AddToggle(LocalizationManager.GetString("Toggle_MouseBackgroundEffect"), "Mouse Background Effect", t => uiManager.T_MouseBackgroundEffect = t,
                    tooltip: LocalizationManager.GetString("Tooltip_MouseBgEffect"))
                .AddToggle(LocalizationManager.GetString("Toggle_UITopMost"), "UI TopMost", t => uiManager.T_UITopMost = t,
                    tooltip: LocalizationManager.GetString("Tooltip_UITopMost"))
                .AddToggle(LocalizationManager.GetString("Toggle_DebugMode"), "Debug Mode", t => uiManager.T_DebugMode = t,
                    tooltip: LocalizationManager.GetString("Tooltip_DebugMode"))
                .AddDropdown(LocalizationManager.GetString("Dropdown_Language"), "Language", d =>
                {
                    uiManager.D_Language = d;

                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_English"), "en-US");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_Chinese"), "zh-CN");

                    d.DropdownBox.SelectedIndex = LocalizationManager.CurrentLanguage == "zh-CN" ? 1 : 0;

                    d.DropdownBox.SelectionChanged += (s, e) =>
                    {
                        if (d.DropdownBox.SelectedIndex == -1)
                            return;

                        var languageCode = d.DropdownBox.SelectedIndex == 1 ? "zh-CN" : "en-US";

                        if (LocalizationManager.CurrentLanguage == languageCode)
                            return;

                        LocalizationManager.SetLanguage(languageCode);

                        Dictionary.languageState["Language"] = languageCode;
                        SaveDictionary.WriteJSON(Dictionary.languageState, "bin\\language.cfg");

                        LogManager.Log(LogLevel.Info, $"Language switched to {languageCode}");

                        _mainWindow.RefreshCurrentMenuUI();
                    };
                }, tooltip: LocalizationManager.GetString("Tooltip_Language"))
                .AddButton(LocalizationManager.GetString("Button_SaveConfig"), b =>
                {
                    uiManager.B_SaveConfig = b;
                    b.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
                }, tooltip: LocalizationManager.GetString("Tooltip_SaveConfig"))
                .AddSeparator();
        }

        private void LoadDisplaySelectMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, DisplaySelectMenu);

            builder
                .AddTitle(LocalizationManager.GetString("Section_ScreenSettings"), true, t =>
                {
                    uiManager.AT_DisplaySelector = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Screen Settings", DisplaySelectMenuPanel);
                })
                .AddDropdown(LocalizationManager.GetString("Dropdown_ScreenCaptureMethod"), "Screen Capture Method", d =>
                {
                    d.DropdownBox.SelectedIndex = -1;
                    uiManager.D_ScreenCaptureMethod = d;
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_DirectX"), "DirectX");
                    _mainWindow.AddDropdownItem(d, LocalizationManager.GetString("DropdownOption_GDIPlus"), "GDI+");
                }, tooltip: LocalizationManager.GetString("Tooltip_ScreenCaptureMethod"))
                .AddToggle(LocalizationManager.GetString("Toggle_StreamGuard"), "StreamGuard", t => uiManager.T_StreamGuard = t,
                    tooltip: LocalizationManager.GetString("Tooltip_StreamGuard"))
                .AddSeparator();

            uiManager.DisplaySelector = new ADisplaySelector();
            uiManager.DisplaySelector.RefreshDisplays();

            var insertIndex = DisplaySelectMenu.Children.Count - 2;
            DisplaySelectMenu.Children.Insert(insertIndex, uiManager.DisplaySelector);

            var refreshButton = new APButton(LocalizationManager.GetString("Button_RefreshDisplays"), LocalizationManager.GetString("Tooltip_RefreshDisplays"));
            refreshButton.Reader.Click += (s, e) =>
            {
                try
                {
                    DisplayManager.RefreshDisplays();
                    uiManager.DisplaySelector.RefreshDisplays();
                    LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_DisplayRefreshed"), true);
                }
                catch (Exception ex)
                {
                    LogManager.Log(LogLevel.Error, LocalizationManager.GetString("Msg_DisplayRefreshError", ex.Message), true);
                }
            };
            DisplaySelectMenu.Children.Insert(insertIndex + 1, refreshButton);
        }

        private void LoadThemeMenu()
        {
            var uiManager = _mainWindow!.uiManager;
            var builder = new SectionBuilder(this, ThemeMenu);

            builder
                .AddTitle(LocalizationManager.GetString("Section_ThemeSettings"), true, t =>
                {
                    uiManager.AT_ThemeColorWheel = t;
                    t.Minimize.Click += (s, e) =>
                        TogglePanel("Theme Settings", ThemeMenuPanel);
                })
                .AddSeparator();

            uiManager.ThemeColorWheel = new AColorWheel();

            if (uiManager.ThemeColorWheel.FindName("ArrowButton") is Button arrowButton)
            {
                arrowButton.Visibility = Visibility.Visible;
            }

            var insertIndex = ThemeMenu.Children.Count - 2;
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
                    LogManager.Log(LogLevel.Info, LocalizationManager.GetString("Msg_AIFocusSwitched", e.DisplayIndex + 1, e.Bounds.Width, e.Bounds.Height), true);
                    UpdateDisplayRelatedSettings(e);
                }
                catch
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

        public void UpdateImageSizeDropdown(string newSize)
        {
            if (_mainWindow?.uiManager.D_ImageSize != null)
            {
                var dropdown = _mainWindow.uiManager.D_ImageSize;
                for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
                {
                    if ((dropdown.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == newSize)
                    {
                        dropdown.DropdownBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void OnClassesChanged(Dictionary<int, string> classes)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_mainWindow?.uiManager.D_TargetClass != null)
                {
                    UpdateTargetClassDropdown(_mainWindow.uiManager.D_TargetClass, classes);
                }
            });
        }

        private void OnDynamicModelStatusChanged(bool isDynamic)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateDynamicModelDropdownsVisibility(isDynamic);
            });
        }

        private void UpdateDynamicModelDropdownsVisibility(bool isDynamic)
        {
            var imageSizeVisibility = isDynamic ? Visibility.Visible : Visibility.Collapsed;

            if (_mainWindow?.uiManager.D_ImageSize != null)
            {
                _mainWindow.uiManager.D_ImageSize.Visibility = imageSizeVisibility;
            }
        }

        private void UpdateTargetClassDropdown(ADropdown dropdown, Dictionary<int, string>? _classes = null)
        {
            if (dropdown?.DropdownBox == null) return;
            var visibility = _classes != null && _classes.Count > 1
                ? Visibility.Visible
                : Visibility.Collapsed;
            dropdown.Visibility = visibility;
            _mainWindow!.uiManager.D_TargetClass!.Visibility = visibility;

            string? selection = (dropdown.DropdownBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

            var removedItems = dropdown.DropdownBox.Items.Cast<ComboBoxItem>()
                .Where(item => item.Content?.ToString() != LocalizationManager.GetString("DropdownOption_BestConfidence"))
                .ToList();

            foreach (var item in removedItems)
            {
                dropdown.DropdownBox.Items.Remove(item);
            }

            var classes = _classes ?? FileManager.AIManager?.ModelClasses ?? new Dictionary<int, string>();

            foreach (var kvp in classes.OrderBy(x => x.Key))
            {
                _mainWindow!.AddDropdownItem(dropdown, kvp.Value);
            }

            if (!string.IsNullOrEmpty(selection))
            {
                for (int i = 0; i < dropdown.DropdownBox.Items.Count; i++)
                {
                    if ((dropdown.DropdownBox.Items[i] as ComboBoxItem)?.Content?.ToString() == selection)
                    {
                        dropdown.DropdownBox.SelectedIndex = i;
                        return;
                    }
                }
            }

            dropdown.DropdownBox.SelectedIndex = 0;
        }

        public void Dispose()
        {
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            AIManager.ClassesUpdated -= OnClassesChanged;
            _mainWindow?.uiManager.DisplaySelector?.Dispose();

            SaveMinimizeStatesToGlobal();
        }

        public void RebuildSections()
        {
            if (!_isInitialized) return;

            ModelSettings.Children.Clear();
            SettingsConfig.Children.Clear();
            ThemeMenu.Children.Clear();
            DisplaySelectMenu.Children.Clear();

            LoadModelSettings();
            LoadSettingsConfig();
            LoadThemeMenu();
            LoadDisplaySelectMenu();

            ApplyMinimizeStates();
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

            public SectionBuilder AddButton(string title, Action<APButton>? configure = null, string? tooltip = null)
            {
                var button = new APButton(title, tooltip);
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

        #region UI Refresh

        private void RefreshCurrentMenuUI()
        {
            SettingsConfig.Children.Clear();
            LoadSettingsConfig();
        }

        #endregion
    }
}
