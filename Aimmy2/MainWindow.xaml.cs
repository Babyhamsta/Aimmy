using Aimmy2.Class;
using Aimmy2.Controls;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Other;
using Aimmy2.Theme;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
using Class;
using InputLogic;
using Other;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UILibrary;
using Visuality;

namespace Aimmy2
{
    public partial class MainWindow : Window
    {
        #region Managers and Windows

        // Core managers (lazy-loaded)
        private readonly Lazy<InputBindingManager> _bindingManager = new(() => new InputBindingManager());
        private static readonly Lazy<GithubManager> _githubManager = new(() => new GithubManager());
        private readonly Lazy<UI> _uiManager = new(() => new UI());
        private readonly Lazy<AntiRecoilManager> _arManager = new(() => new AntiRecoilManager());
        private Lazy<FileManager>? _fileManager;

        // Windows
        private static readonly Lazy<FOV> _fovWindow = new(() =>
        {
            var window = new FOV();
            // Force immediate reposition to current display
            window.ForceReposition();
            return window;
        });

        private static readonly Lazy<DetectedPlayerWindow> _dpWindow = new(() =>
        {
            var window = new DetectedPlayerWindow();
            // Force immediate reposition to current display
            window.ForceReposition();
            return window;
        });

        // Public accessors
        internal InputBindingManager bindingManager => _bindingManager.Value;
        internal FileManager fileManager => _fileManager?.Value ?? throw new InvalidOperationException("FileManager not initialized");
        public static FOV FOVWindow => _fovWindow.Value;
        public static DetectedPlayerWindow DPWindow => _dpWindow.Value;
        public static GithubManager githubManager => _githubManager.Value;
        public UI uiManager => _uiManager.Value;
        public AntiRecoilManager arManager => _arManager.Value;

        #endregion

        #region UI State

        internal Dictionary<string, AToggle> toggleInstances = new();
        private readonly Dictionary<string, UserControl?> _menuControls = new();
        private readonly Dictionary<string, bool> _menuInitialized = new();
        private UserControl? _currentControl;
        private string _currentMenu = "AimMenu";
        private bool _currentlySwitching;
        private ScrollViewer? CurrentScrollViewer;
        public double ActualFOV { get; set; } = 640;
        private double _currentGradientAngle;

        // Menu names constant
        private static readonly string[] MenuNames = { "AimMenu", "ModelMenu", "SettingsMenu", "AboutMenu" };

        #endregion

        #region Initialization

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveDictionary.EnsureDirectoriesExist();

                InitializeMenus();
                InitializeFileManagerEarly();

                // Load configurations BEFORE loading any menus
                // This ensures minimize states are loaded from file before menu initialization
                await LoadConfigurationsAsync();

                // Now load the initial menu - it will use the loaded minimize states
                LoadInitialMenu();

                // Continue with the rest of initialization
                await InitializeApplicationAsync();
                UpdateAboutSpecs();
                ApplyThemeGradients();
            }
            catch (Exception ex)
            {
                ShowError($"Error during startup: {ex.Message}", ex);
            }
        }

        private void InitializeMenus()
        {
            foreach (var menu in MenuNames)
            {
                _menuControls[menu] = null;
                _menuInitialized[menu] = false;
            }
        }

        private void InitializeFileManagerEarly()
        {
            var modelMenu = new ModelMenuControl();
            modelMenu.Initialize(this);
            _menuControls["ModelMenu"] = modelMenu;
            InitializeFileManager(modelMenu);
        }

        private void LoadInitialMenu()
        {
            LoadMenu("AimMenu");
            _currentMenu = "AimMenu";
        }

        private async Task InitializeApplicationAsync()
        {
            CheckRunningFromTemp();

            // Initialize DisplayManager FIRST before anything else that depends on display info
            DisplayManager.Initialize();

            // Now that DisplayManager is initialized, we can create windows
            InitializeWindows();

            EnsureRequiredFiles();

            // Configuration loading has been moved to Window_Loaded before menu initialization
            // Only load specific configurations that aren't related to UI state
            await Task.Run(() =>
            {
                arManager.HoldDownLoad();
            });

            SetupKeybindings();
            ConfigurePropertyChangers();
            ApplyInitialSettings();
            ListenForKeybinds();

            // Subscribe to display changes after everything is initialized
            DisplayManager.DisplayChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object? sender, DisplayChangedEventArgs e)
        {

            // Force update all windows to new display
            DisplayManager.ForceUpdateWindows();
        }

        private void CheckRunningFromTemp()
        {
            if (Directory.GetCurrentDirectory().Contains("Temp"))
            {
                MessageBox.Show(
                    "Hi, it is made aware that you are running Aimmy without extracting it from the zip file. " +
                    "Please extract Aimmy from the zip file or Aimmy will not be able to run properly.\n\nThank you.",
                    "Aimmy V2");
            }
        }

        private void InitializeWindows()
        {
            // Create windows but don't show them yet
            var fov = FOVWindow;  // This triggers lazy initialization
            var dpw = DPWindow;   // This triggers lazy initialization

            // Ensure they're positioned on the current display
            fov.ForceReposition();
            dpw.ForceReposition();

            // Set references in Dictionary
            Dictionary.DetectedPlayerOverlay = dpw;
            Dictionary.FOVWindow = fov;
        }

        private void EnsureRequiredFiles()
        {
            var labelsPath = "bin\\labels\\labels.txt";
            var labelsDir = Path.GetDirectoryName(labelsPath);

            // Ensure the directory exists
            if (!string.IsNullOrEmpty(labelsDir) && !Directory.Exists(labelsDir))
            {
                Directory.CreateDirectory(labelsDir);
            }

            // Create the file if it doesn't exist
            if (!File.Exists(labelsPath))
            {
                File.WriteAllText(labelsPath, "Enemy");
            }
        }

        private async Task LoadConfigurationsAsync()
        {
            // Run non-UI operations in background
            await Task.Run(() =>
            {
                arManager.HoldDownLoad();

                // Load configurations that don't create UI
                var configs = new[]
                {
                    (Dictionary.minimizeState, "bin\\minimize.cfg"),
                    (Dictionary.bindingSettings, "bin\\binding.cfg"),
                    (Dictionary.colorState, "bin\\colors.cfg"),
                    (Dictionary.filelocationState, "bin\\filelocations.cfg"),
                    (Dictionary.dropdownState, "bin\\dropdown.cfg")
                };

                foreach (var (dict, path) in configs)
                {
                    SaveDictionary.LoadJSON(dict, path);
                }
            });

            // Load these on UI thread since they might show notifications
            LoadConfig();
            LoadAntiRecoilConfig();

            ApplyThemeColorFromConfig();
        }


        private void ApplyThemeColorFromConfig()
        {
            if (Dictionary.colorState.TryGetValue("Theme Color", out var themeColor))
            {
                var colorString = themeColor?.ToString();
                if (!string.IsNullOrEmpty(colorString))
                {
                    try
                    {
                        ThemeManager.SetThemeColor(colorString);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        private void SetupKeybindings()
        {
            var keybinds = new[]
            {
                "Aim Keybind", "Second Aim Keybind", "Dynamic FOV Keybind",
                "Emergency Stop Keybind", "Model Switch Keybind",
                "Anti Recoil Keybind", "Disable Anti Recoil Keybind",
                "Gun 1 Key", "Gun 2 Key"
            };

            foreach (var keybind in keybinds)
            {
                bindingManager.SetupDefault(keybind, Dictionary.bindingSettings[keybind].ToString());
            }
        }

        private void ConfigurePropertyChangers()
        {
            PropertyChanger.ReceiveNewConfig = LoadConfig;
        }

        private void ApplyInitialSettings()
        {
            // FOV settings
            ActualFOV = Convert.ToDouble(Dictionary.sliderSettings["FOV Size"]);
            PropertyChanger.PostNewFOVSize(ActualFOV);
            PropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["FOV Color"].ToString()));

            // Detected player window settings
            var dpSettings = new[]
            {
                ("Detected Player Color", (Action<object>)(c => PropertyChanger.PostDPColor((Color)c))),
                ("AI Confidence Font Size", (Action<object>)(s => PropertyChanger.PostDPFontSize((int)(double)s))),
                ("Corner Radius", (Action<object>)(r => PropertyChanger.PostDPWCornerRadius((int)(double)r))),
                ("Border Thickness", (Action<object>)(t => PropertyChanger.PostDPWBorderThickness((double)t))),
                ("Opacity", (Action<object>)(o => PropertyChanger.PostDPWOpacity((double)o)))
            };

            foreach (var (key, action) in dpSettings)
            {
                if (key.Contains("Color"))
                {
                    action(ColorConverter.ConvertFromString(Dictionary.colorState[key].ToString()));
                }
                else
                {
                    action(Convert.ToDouble(Dictionary.sliderSettings[key]));
                }
            }
        }

        private void UpdateAboutSpecs()
        {
            if (_menuControls["AboutMenu"] is AboutMenuControl aboutMenu)
            {
                aboutMenu.AboutSpecsControl.Content = "Loading system specs...";

                Task.Run(() =>
                {
                    var specs = $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";
                    Dispatcher.Invoke(() => aboutMenu.AboutSpecsControl.Content = specs);
                });
            }
        }

        private void ShowError(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\nStack trace: {ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ApplyThemeGradients()
        {
            if (!Dictionary.colorState.TryGetValue("Theme Color", out var themeColor)) return;

            var colorString = themeColor?.ToString();
            if (string.IsNullOrEmpty(colorString)) return;

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(colorString);

                var gradientMappings = new Dictionary<string, Func<Color, Color>>
                {
                    ["GradientThemeStop"] = c => Color.FromRgb((byte)(c.R * 0.3), (byte)(c.G * 0.3), (byte)(c.B * 0.3)),
                    ["HighlighterGradient1"] = c => c,
                    ["HighlighterGradient2"] = c => Color.FromArgb(102, c.R, c.G, c.B)
                };

                foreach (var (elementName, colorTransform) in gradientMappings)
                {
                    if (FindName(elementName) is GradientStop gradientStop)
                    {
                        gradientStop.Color = colorTransform(color);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void InitializeFileManager(ModelMenuControl modelMenu)
        {
            if (_fileManager == null)
            {
                _fileManager = new Lazy<FileManager>(() => new FileManager(
                    modelMenu.ModelListBoxControl,
                    modelMenu.SelectedModelNotifierControl,
                    modelMenu.ConfigsListBoxControl,
                    modelMenu.SelectedConfigNotifierControl));

                try
                {
                    var fm = _fileManager.Value;
                }
                catch (Exception ex)
                {
                }
            }
        }

        #endregion

        #region Window Events

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_fileManager?.IsValueCreated == true)
            {
                fileManager.InQuittingState = true;
            }

            DisableAllFeatures();
            CloseWindows();
            CleanupDrivers();

            // Dispose menu controls to save their states
            if (_menuControls["AimMenu"] is AimMenuControl aimMenu)
                aimMenu.Dispose();

            if (_menuControls["SettingsMenu"] is SettingsMenuControl settingsMenu)
                settingsMenu.Dispose();

            SaveAllConfigurations();
            FileManager.AIManager?.Dispose();

            // Clean up display manager
            DisplayManager.DisplayChanged -= OnDisplayChanged;
            DisplayManager.Dispose();

            Application.Current.Shutdown();
        }

        private void DisableAllFeatures()
        {
            var features = new[] { "Aim Assist", "FOV", "Show Detected Player" };
            foreach (var feature in features)
            {
                Dictionary.toggleState[feature] = false;
            }
        }

        private void CloseWindows()
        {
            FOVWindow.Close();
            DPWindow.Close();
        }

        private void CleanupDrivers()
        {
            if (Dictionary.dropdownState.TryGetValue("Mouse Movement Method", out var method) &&
                method?.ToString() == "LG HUB")
            {
                LGMouse.Close();
            }
        }

        private void SaveAllConfigurations()
        {
            Dictionary.colorState["Theme Color"] = ThemeManager.GetThemeColorHex();

            SaveDictionary.WriteJSON(Dictionary.sliderSettings);
            SaveDictionary.WriteJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
            SaveDictionary.WriteJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
            SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
            SaveDictionary.WriteJSON(Dictionary.colorState, "bin\\colors.cfg");
            SaveDictionary.WriteJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
            SaveDictionary.WriteJSON(Dictionary.AntiRecoilSettings, "bin\\anti_recoil_configs\\Default.cfg");
        }

        #endregion

        #region Menu Management

        private UserControl GetOrCreateMenuControl(string menuName)
        {
            if (_menuControls[menuName] != null)
                return _menuControls[menuName]!;

            var newControl = menuName == "ModelMenu" && _menuControls["ModelMenu"] != null
                ? _menuControls["ModelMenu"]!
                : CreateMenuControl(menuName);

            _menuControls[menuName] = newControl;

            if (!_menuInitialized[menuName])
            {
                InitializeMenuControl(menuName, newControl);
                _menuInitialized[menuName] = true;
            }

            return newControl;
        }

        private UserControl CreateMenuControl(string menuName) => menuName switch
        {
            "AimMenu" => new AimMenuControl(),
            "ModelMenu" => new ModelMenuControl(),
            "SettingsMenu" => new SettingsMenuControl(),
            "AboutMenu" => new AboutMenuControl(),
            _ => throw new ArgumentException($"Unknown menu: {menuName}")
        };

        private void InitializeMenuControl(string menuName, UserControl control)
        {
            try
            {
                switch (control)
                {
                    case AimMenuControl aimMenu:
                        aimMenu.Initialize(this);
                        CurrentScrollViewer = aimMenu.AimMenuScrollViewer;
                        LoadDropdownStates();
                        break;

                    case ModelMenuControl modelMenu:
                        if (!_menuInitialized["ModelMenu"])
                            modelMenu.Initialize(this);
                        break;

                    case SettingsMenuControl settingsMenu:
                        settingsMenu.Initialize(this);
                        LoadDropdownStates();
                        break;

                    case AboutMenuControl aboutMenu:
                        aboutMenu.Initialize(this);
                        UpdateAboutSpecs();
                        break;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void LoadMenu(string menuName)
        {
            var control = GetOrCreateMenuControl(menuName);
            ContentArea.Children.Clear();
            ContentArea.Children.Add(control);
            _currentControl = control;
            UpdateCurrentScrollViewer(menuName, control);
        }

        private void UpdateCurrentScrollViewer(string menuName, UserControl control)
        {
            CurrentScrollViewer = control switch
            {
                AimMenuControl aim => aim.AimMenuScrollViewer,
                ModelMenuControl model => model.ModelMenuScrollViewer,
                SettingsMenuControl settings => settings.SettingsMenuScrollViewer,
                AboutMenuControl about => about.AboutMenuScrollViewer,
                _ => CurrentScrollViewer
            };
        }

        private async void MenuSwitch(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string newMenuName } ||
                !IsValidMenu(newMenuName) ||
                _currentlySwitching ||
                _currentMenu == newMenuName) return;

            _currentlySwitching = true;

            try
            {
                Animator.ObjectShift(
                    TimeSpan.FromMilliseconds(150), // Fade between menu buttons
                    MenuHighlighter,
                    MenuHighlighter.Margin,
                    ((Button)sender).Margin);

                await SwitchToMenu(newMenuName);
                _currentMenu = newMenuName;
            }
            catch (Exception ex)
            {
            }
            finally
            {
                _currentlySwitching = false;
            }
        }

        private bool IsValidMenu(string? menuName) =>
            !string.IsNullOrEmpty(menuName) && _menuControls.ContainsKey(menuName!);

        private async Task SwitchToMenu(string menuName)
        {
            if (_currentControl != null)
            {
                Animator.FadeOut(_currentControl);
                await Task.Delay(150); // Fade between menu content
            }

            LoadMenu(menuName);
            Animator.Fade(_currentControl!);
        }

        #endregion

        #region Toggle Actions

        internal void Toggle_Action(string title)
        {
            var actions = new Dictionary<string, Action>
            {
                ["FOV"] = () =>
                {
                    FOVWindow.Visibility = GetToggleVisibility(title);
                    // Force reposition when showing the window
                    if (Dictionary.toggleState[title])
                    {
                        FOVWindow.ForceReposition();
                    }
                },
                ["Show Detected Player"] = () =>
                {
                    ShowHideDPWindow();
                    DPWindow.DetectedPlayerFocus.Visibility = GetToggleVisibility(title, true);
                    // Force reposition when showing the window
                    if (Dictionary.toggleState[title])
                    {
                        DPWindow.ForceReposition();
                    }
                },
                ["Show AI Confidence"] = () => DPWindow.DetectedPlayerConfidence.Visibility = GetToggleVisibility(title, true),
                ["Mouse Background Effect"] = () => { if (!Dictionary.toggleState[title]) RotaryGradient.Angle = 0; },
                ["UI TopMost"] = () => Topmost = Dictionary.toggleState[title],
                ["EMA Smoothening"] = () =>
                {
                    MouseManager.IsEMASmoothingEnabled = Dictionary.toggleState[title];
                }
            };

            if (actions.TryGetValue(title, out var action))
            {
                action();
            }
        }

        private Visibility GetToggleVisibility(string title, bool collapsed = false) =>
            Dictionary.toggleState[title]
                ? Visibility.Visible
                : (collapsed ? Visibility.Collapsed : Visibility.Hidden);

        private static void ShowHideDPWindow()
        {
            if (Dictionary.toggleState["Show Detected Player"])
            {
                DPWindow.Show();
                // Force reposition when showing
                DPWindow.ForceReposition();
            }
            else
            {
                DPWindow.Hide();
            }
        }

        #endregion

        #region UI Helper Methods

        public void UpdateToggleUI(AToggle toggle, bool isEnabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isEnabled)
                    toggle.EnableSwitch();
                else
                    toggle.DisableSwitch();
            });
        }

        public ComboBoxItem AddDropdownItem(ADropdown dropdown, string title)
        {
            var dropdownitem = new ComboBoxItem
            {
                Content = title,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                FontFamily = TryFindResource("Atkinson Hyperlegible") as FontFamily
            };

            dropdownitem.Selected += (s, e) =>
            {
                var key = dropdown.DropdownTitle.Content?.ToString()
                        ?? throw new NullReferenceException("dropdown.DropdownTitle.Content.ToString() is null");
                Dictionary.dropdownState[key] = title;
            };

            dropdown.DropdownBox.Items.Add(dropdownitem);
            return dropdownitem;
        }

        #endregion

        #region Keybind Handling

        private void ListenForKeybinds()
        {
            bindingManager.OnBindingPressed += HandleKeybindPressed;
            bindingManager.OnBindingReleased += HandleKeybindReleased;
        }

        private void HandleKeybindPressed(string bindingId)
        {
            var handlers = new Dictionary<string, Action>
            {
                ["Model Switch Keybind"] = HandleModelSwitch,
                ["Dynamic FOV Keybind"] = () => ApplyDynamicFOV(true),
                ["Emergency Stop Keybind"] = HandleEmergencyStop,
                ["Anti Recoil Keybind"] = () => HandleAntiRecoil(true),
                ["Disable Anti Recoil Keybind"] = DisableAntiRecoil,
                ["Gun 1 Key"] = () => LoadGunConfig("Gun 1 Config"),
                ["Gun 2 Key"] = () => LoadGunConfig("Gun 2 Config")
            };

            handlers.GetValueOrDefault(bindingId)?.Invoke();
        }

        private void HandleKeybindReleased(string bindingId)
        {
            var handlers = new Dictionary<string, Action>
            {
                ["Dynamic FOV Keybind"] = () => ApplyDynamicFOV(false),
                ["Anti Recoil Keybind"] = () => HandleAntiRecoil(false)
            };

            handlers.GetValueOrDefault(bindingId)?.Invoke();
        }

        private void HandleModelSwitch()
        {
            if (!Dictionary.toggleState["Enable Model Switch Keybind"] || FileManager.CurrentlyLoadingModel)
                return;

            if (_menuControls["ModelMenu"] is ModelMenuControl modelMenu)
            {
                var modelListBox = modelMenu.ModelListBoxControl;
                modelListBox.SelectedIndex = (modelListBox.SelectedIndex >= 0 &&
                    modelListBox.SelectedIndex < modelListBox.Items.Count - 1)
                    ? modelListBox.SelectedIndex + 1
                    : 0;
            }
        }

        private void ApplyDynamicFOV(bool apply)
        {
            if (!Dictionary.toggleState["Dynamic FOV"]) return;

            var targetSize = apply ? Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]) : ActualFOV;
            Dictionary.sliderSettings["FOV Size"] = targetSize;

            AnimateFOVSize(targetSize);
        }

        private void AnimateFOVSize(double targetSize)
        {
            var duration = TimeSpan.FromMilliseconds(500);
            Animator.WidthShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualWidth, targetSize);
            Animator.HeightShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualHeight, targetSize);
        }

        private void HandleEmergencyStop()
        {
            var features = new[] { "Aim Assist", "Constant AI Tracking", "Auto Trigger" };
            var toggles = new[] { uiManager.T_AimAligner, uiManager.T_ConstantAITracking, uiManager.T_AutoTrigger };

            for (int i = 0; i < features.Length; i++)
            {
                Dictionary.toggleState[features[i]] = false;
                if (toggles[i] != null)
                    UpdateToggleUI(toggles[i], false);
            }

            new NoticeBar("[Emergency Stop Keybind] Disabled all AI features.", 4000).Show();
        }

        private void HandleAntiRecoil(bool start)
        {
            if (!Dictionary.toggleState["Anti Recoil"]) return;

            if (start)
            {
                arManager.IndependentMousePress = 0;
                arManager.HoldDownTimer.Start();
            }
            else
            {
                arManager.HoldDownTimer.Stop();
                arManager.IndependentMousePress = 0;
            }
        }

        private void DisableAntiRecoil()
        {
            if (!Dictionary.toggleState["Anti Recoil"]) return;

            Dictionary.toggleState["Anti Recoil"] = false;
            UpdateToggleUI(uiManager.T_AntiRecoil!, false);
            new NoticeBar("[Disable Anti Recoil Keybind] Disabled Anti-Recoil.", 4000).Show();
        }

        private void LoadGunConfig(string configKey)
        {
            if (Dictionary.toggleState["Enable Gun Switching Keybind"])
            {
                if (Dictionary.filelocationState.TryGetValue(configKey, out var configPath))
                {
                    LoadAntiRecoilConfig(configPath.ToString(), true);
                }
            }
        }

        #endregion

        #region UI Effects

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (!Dictionary.toggleState["Mouse Background Effect"]) return;

            var mousePosition = WinAPICaller.GetCursorPosition();
            var translatedMousePos = PointFromScreen(new Point(mousePosition.X, mousePosition.Y));

            var targetAngle = Math.Atan2(
                translatedMousePos.Y - (MainBorder.ActualHeight * 0.5),
                translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

            _currentGradientAngle = CalculateSmoothedAngle(targetAngle);
            RotaryGradient.Angle = _currentGradientAngle;
        }

        private double CalculateSmoothedAngle(double targetAngle)
        {
            const double fullCircle = 360;
            const double halfCircle = 180;
            const double clamp = 1;

            var angleDifference = (targetAngle - _currentGradientAngle + fullCircle) % fullCircle;
            if (angleDifference > halfCircle)
                angleDifference -= fullCircle;

            var clampedDifference = Math.Max(Math.Min(angleDifference, clamp), -clamp);
            return (_currentGradientAngle + clampedDifference + fullCircle) % fullCircle;
        }

        #endregion

        #region Configuration Management

        private void LoadDropdownStates()
        {

            var dropdownConfigs = new[]
            {
                // AimMenu dropdowns
                (uiManager.D_PredictionMethod, "Prediction Method", new Dictionary<string, int>
                {
                    ["Kalman Filter"] = 0,
                    ["Shall0e's Prediction"] = 1,
                    ["wisethef0x's EMA Prediction"] = 2
                }),
                (uiManager.D_DetectionAreaType, "Detection Area Type", new Dictionary<string, int>
                {
                    ["Closest to Center Screen"] = 0,
                    ["Closest to Mouse"] = 1
                }),
                (uiManager.D_AimingBoundariesAlignment, "Aiming Boundaries Alignment", new Dictionary<string, int>
                {
                    ["Center"] = 0,
                    ["Top"] = 1,
                    ["Bottom"] = 2
                }),
                // SettingsMenu dropdowns
                (uiManager.D_MouseMovementMethod, "Mouse Movement Method", new Dictionary<string, int>
                {
                    ["Mouse Event"] = 0,
                    ["SendInput"] = 1,
                    ["LG HUB"] = 2,
                    ["Razer Synapse (Require Razer Peripheral)"] = 3,
                    ["ddxoft Virtual Input Driver"] = 4
                }),
                (uiManager.D_ScreenCaptureMethod, "Screen Capture Method", new Dictionary<string, int>
                {
                    ["DirectX"] = 0,
                    ["GDI+"] = 1
                })
            };

            foreach (var (dropdown, key, mappings) in dropdownConfigs)
            {
                if (dropdown == null)
                {
                    continue;
                }

                if (Dictionary.dropdownState.TryGetValue(key, out var value))
                {
                    var stringValue = value?.ToString() ?? "";

                    if (mappings.TryGetValue(stringValue, out int index))
                    {
                        dropdown.DropdownBox.SelectedIndex = index;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"No mapping found for '{stringValue}'");
                    }
                }
            }
        }

        private void LoadConfig(string path = "bin\\configs\\Default.cfg", bool loading_from_configlist = false)
        {
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, path);

            if (!loading_from_configlist || _menuControls["AimMenu"] == null || !_menuInitialized["AimMenu"])
                return;

            try
            {
                ShowSuggestedModelIfSpecified();
                ApplyConfigToSliders();
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading config, possibly outdated\n{e}");
            }
        }

        private void ShowSuggestedModelIfSpecified()
        {
            if (Dictionary.sliderSettings.TryGetValue("Suggested Model", out var model))
            {
                var suggestedModel = model?.ToString() ?? "N/A";
                if (suggestedModel != "N/A" && !string.IsNullOrEmpty(suggestedModel))
                {
                    MessageBox.Show(
                        $"The creator of this model suggests you use this model:\n{suggestedModel}",
                        "Suggested Model - Aimmy");
                }
            }
        }

        private void ApplyConfigToSliders()
        {
            var sliderConfigs = new[]
            {
                ("Fire Rate", uiManager.S_FireRate, 1.0),
                ("FOV Size", uiManager.S_FOVSize, 640.0),
                ("Mouse Sensitivity (+/-)", uiManager.S_MouseSensitivity, 0.8),
                ("Mouse Jitter", uiManager.S_MouseJitter, 0.0),
                ("Y Offset (Up/Down)", uiManager.S_YOffset, 0.0),
                ("X Offset (Left/Right)", uiManager.S_XOffset, 0.0),
                ("Y Offset (%)", uiManager.S_YOffsetPercent, 0.0),
                ("X Offset (%)", uiManager.S_XOffsetPercent, 0.0),
                ("Auto Trigger Delay", uiManager.S_AutoTriggerDelay, 0.25),
                ("AI Minimum Confidence", uiManager.S_AIMinimumConfidence, 50.0)
            };

            ApplySliderValues(sliderConfigs, Dictionary.sliderSettings);
        }

        public void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg", bool loading_outside_startup = false)
        {
            try
            {
                // Ensure directory exists
                string? directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(path))
                {
                    // Create default config file
                    SaveDictionary.WriteJSON(Dictionary.AntiRecoilSettings, path);

                    // Only show notification if not during startup
                    if (loading_outside_startup)
                    {
                        // Use dispatcher to ensure UI operations happen on UI thread
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            new NoticeBar("[Anti Recoil] Created default config.", 2000).Show();
                        });
                    }
                    return;
                }

                SaveDictionary.LoadJSON(Dictionary.AntiRecoilSettings, path);

                if (!loading_outside_startup || _menuControls["AimMenu"] == null || !_menuInitialized["AimMenu"])
                    return;

                ApplyAntiRecoilConfig();

                // Only show notification if not during startup
                if (loading_outside_startup)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        new NoticeBar($"[Anti Recoil] Loaded \"{path}\"", 2000).Show();
                    });
                }
            }
            catch (Exception e)
            {
                // Only show error if not during startup
                if (loading_outside_startup)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error loading config, possibly outdated\n{e}");
                    });
                }
                else
                {
                    // During startup, just log the error
                    System.Diagnostics.Debug.WriteLine($"Error loading anti-recoil config: {e.Message}");
                }
            }
        }

        private void ApplyAntiRecoilConfig()
        {
            var sliderConfigs = new[]
            {
                ("Hold Time", uiManager.S_HoldTime, 0.0),
                ("Fire Rate", uiManager.S_FireRate, 1.0),
                ("Y Recoil (Up/Down)", uiManager.S_YAntiRecoilAdjustment, 0.0),
                ("X Recoil (Left/Right)", uiManager.S_XAntiRecoilAdjustment, 0.0)
            };

            ApplySliderValues(sliderConfigs, Dictionary.AntiRecoilSettings);
        }

        private void ApplySliderValues((string key, ASlider? slider, double defaultValue)[] configs, Dictionary<string, dynamic> source)
        {
            foreach (var (key, slider, defaultValue) in configs)
            {
                if (slider != null && source.TryGetValue(key, out var value))
                {
                    slider.Slider.Value = Convert.ToDouble(value);
                }
                else if (slider != null)
                {
                    slider.Slider.Value = defaultValue;
                }
            }
        }

        #endregion

        #region System Information

        private static string? GetProcessorName() => GetSpecs.GetSpecification("Win32_Processor", "Name");
        private static string? GetVideoControllerName() => GetSpecs.GetSpecification("Win32_VideoController", "Name");
        private static string? GetFormattedMemorySize()
        {
            var totalMemorySize = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
            return Math.Round(totalMemorySize / (1024.0 * 1024.0), 0).ToString();
        }

        #endregion

        #region Unimplemented Methods (For Controls)

        public AToggle AddToggle(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AKeyChanger AddKeyChanger(StackPanel panel, string title, string keybind) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AColorChanger AddColorChanger(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public ASlider AddSlider(StackPanel panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool For_Anti_Recoil = false) =>
            throw new NotImplementedException("Use control's internal implementation");

        public ADropdown AddDropdown(StackPanel panel, string title) =>
            throw new NotImplementedException("Use control's internal implementation");

        public AFileLocator AddFileLocator(StackPanel panel, string title, string filter = "All files (*.*)|*.*", string DLExtension = "") =>
            throw new NotImplementedException("Use control's internal implementation");

        #endregion
    }

    #region Extension Methods

    internal static class DictionaryExtensions
    {
        public static T GetValueOrDefault<T>(this Dictionary<string, T> dictionary, string key, T defaultValue) =>
            dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        public static T GetValueOrDefault<T>(this Dictionary<string, dynamic> dictionary, string key, T defaultValue)
        {
            if (dictionary.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }

    #endregion
}