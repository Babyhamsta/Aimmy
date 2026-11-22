using Aimmy2.AILogic;
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
using System.Globalization;
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

        #endregion

        #region UI State
        public SettingsMenuControl? SettingsMenuControlInstance { get; set; }
        internal Dictionary<string, AToggle> toggleInstances = new();
        private readonly Dictionary<string, UserControl?> _menuControls = new();
        private string _currentMenu = "AimMenu";
        public double ActualFOV { get; set; } = 640;
        private double _currentGradientAngle;
        private bool _performanceHelperOpen;

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
                InitializeApplication();
                ApplyThemeGradients();
                ThemeManager.LoadMediaSettings();
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
            // Don't call UpdateSliderVisibility here - it would override collapsed menu states
            // Visibility is handled by the toggle click actions when user interacts with toggles
        }

        private void InitializeApplication()
        {
            CheckRunningFromTemp();

            // Initialize DisplayManager FIRST before anything else that depends on display info
            DisplayManager.Initialize();

            // Now that DisplayManager is initialized, we can create windows
            InitializeWindows();

            EnsureRequiredFiles();

            SetupKeybindings();
            ConfigurePropertyChangers();
            ApplyInitialSettings();
            ListenForKeybinds();

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
                // Load configurations that don't create UI
                var configs = new[]
                {
                    (Dictionary.minimizeState, "bin\\minimize.cfg"),
                    (Dictionary.bindingSettings, "bin\\binding.cfg"),
                    (Dictionary.colorState, "bin\\colors.cfg"),
                    (Dictionary.filelocationState, "bin\\filelocations.cfg"),
                    (Dictionary.dropdownState, "bin\\dropdown.cfg"),
                    (Dictionary.toggleState, "bin\\toggles.cfg")
                };

                foreach (var (dict, path) in configs)
                {
                    SaveDictionary.LoadJSON(dict, path);
                }
            });

            // Load these on UI thread since they might show notifications
            LoadConfig();
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
                "Emergency Stop Keybind", "Model Switch Keybind"
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

        private void UpdateAboutSpecs(AboutMenuControl aboutMenu)
        {
            aboutMenu.AboutSpecsControl.Content = "Loading system specs...";

            Task.Run(() =>
            {
                var specs = $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";
                Dispatcher.Invoke(() => aboutMenu.AboutSpecsControl.Content = specs);
            });
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
            FileManager.ModelLoaded -= OnModelLoaded;
            FileManager.ModelLoaded += OnModelLoaded;

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

        private void OnModelLoaded(AIManager manager)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => OnModelLoaded(manager));
                return;
            }

            if (!PerformanceHelperState.ShouldPrompt())
            {
                return;
            }

            ShowPerformanceHelper(manager, PerformanceHelperWindow.LaunchMode.CompactPrompt);
        }

        internal void ShowPerformanceHelper()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(ShowPerformanceHelper);
                return;
            }

            var manager = FileManager.AIManager;
            if (manager == null || !manager.IsLoaded)
            {
                LogManager.Log(LogManager.LogLevel.Warning, "Load a model before opening the Performance Helper.", true, 3000);
                return;
            }

            ShowPerformanceHelper(manager, PerformanceHelperWindow.LaunchMode.FullHelper);
        }

        private void ShowPerformanceHelper(AIManager manager, PerformanceHelperWindow.LaunchMode launchMode)
        {
            if (_performanceHelperOpen ||
                !ReferenceEquals(FileManager.AIManager, manager) ||
                !manager.IsLoaded)
            {
                return;
            }

            _performanceHelperOpen = true;
            try
            {
                var helper = new PerformanceHelperWindow(this, manager, launchMode)
                {
                    Owner = this
                };
                helper.ShowDialog();
            }
            finally
            {
                _performanceHelperOpen = false;
            }
        }

        internal async Task<bool> ChangeImageSizeAsync(string newSize)
        {
            if (string.IsNullOrWhiteSpace(newSize))
                return false;

            newSize = newSize.Trim();
            if (!int.TryParse(newSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requestedSize))
                return false;

            await FileManager.ModelOperationLock.WaitAsync();
            FileManager.CurrentlyLoadingModel = true;
            string? previousSize = null;
            string? loadedModel = null;
            string? modelPath = null;

            try
            {
                if (FileManager.AIManager == null || Dictionary.lastLoadedModel == "N/A")
                {
                    Dictionary.dropdownState["Image Size"] = newSize;
                    SettingsMenuControlInstance?.UpdateImageSizeDropdown(newSize);
                    LogManager.Log(LogManager.LogLevel.Info, $"Image size set to {newSize}x{newSize} (no model loaded)", true, 2000);
                    return true;
                }

                previousSize = Dictionary.dropdownState["Image Size"];
                loadedModel = Dictionary.lastLoadedModel;
                modelPath = Path.Combine("bin/models", loadedModel);
                AIManager managerToReplace = FileManager.AIManager;
                LogManager.Log(LogManager.LogLevel.Info, $"Image size changing to {newSize}");

                managerToReplace.RequestSizeChange(requestedSize);
                await Task.Delay(100);

                FileManager.AIManager = null;
                managerToReplace.Dispose();

                Dictionary.dropdownState["Image Size"] = newSize;

                var manager = new AIManager(modelPath);
                bool loaded = await manager.InitializationTask;

                if (loaded)
                {
                    FileManager.AIManager = manager;
                    Dictionary.lastLoadedModel = loadedModel;
                    string actualSize = AimSettings.ImageSize.ToString(CultureInfo.InvariantCulture);
                    Dictionary.dropdownState["Image Size"] = actualSize;
                    SettingsMenuControlInstance?.UpdateImageSizeDropdown(actualSize);

                    if (actualSize == newSize)
                    {
                        LogManager.Log(LogManager.LogLevel.Info, $"Successfully changed image size to {actualSize}x{actualSize}", true, 2000);
                        return true;
                    }

                    LogManager.Log(
                        LogManager.LogLevel.Warning,
                        $"Model loaded at {actualSize}x{actualSize}; requested {newSize}x{newSize} was not applied.",
                        true,
                        5000);
                    return false;
                }

                manager.Dispose();
                FileManager.AIManager = null;
                Dictionary.dropdownState["Image Size"] = previousSize;
                SettingsMenuControlInstance?.UpdateImageSizeDropdown(previousSize);
                LogManager.Log(LogManager.LogLevel.Error, $"Model could not reload at {newSize}x{newSize}. Restoring {previousSize}x{previousSize}.", true, 5000);

                var restoredManager = new AIManager(modelPath);
                if (await restoredManager.InitializationTask)
                {
                    FileManager.AIManager = restoredManager;
                    Dictionary.lastLoadedModel = loadedModel;
                    string actualSize = AimSettings.ImageSize.ToString(CultureInfo.InvariantCulture);
                    Dictionary.dropdownState["Image Size"] = actualSize;
                    SettingsMenuControlInstance?.UpdateImageSizeDropdown(actualSize);
                }
                else
                {
                    restoredManager.Dispose();
                    FileManager.AIManager = null;
                    Dictionary.lastLoadedModel = "N/A";
                }
                return false;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(previousSize))
                {
                    Dictionary.dropdownState["Image Size"] = previousSize;
                    SettingsMenuControlInstance?.UpdateImageSizeDropdown(previousSize);
                }

                LogManager.Log(LogManager.LogLevel.Error, $"Error changing image size: {ex.Message}", true, 5000);
                if (FileManager.AIManager == null &&
                    !string.IsNullOrWhiteSpace(modelPath) &&
                    File.Exists(modelPath))
                {
                    try
                    {
                        var restoredManager = new AIManager(modelPath);
                        if (await restoredManager.InitializationTask)
                        {
                            FileManager.AIManager = restoredManager;
                            if (!string.IsNullOrWhiteSpace(loadedModel))
                            {
                                Dictionary.lastLoadedModel = loadedModel;
                            }

                            string actualSize = AimSettings.ImageSize.ToString(CultureInfo.InvariantCulture);
                            Dictionary.dropdownState["Image Size"] = actualSize;
                            SettingsMenuControlInstance?.UpdateImageSizeDropdown(actualSize);
                        }
                        else
                        {
                            restoredManager.Dispose();
                            FileManager.AIManager = null;
                            Dictionary.lastLoadedModel = "N/A";
                        }
                    }
                    catch
                    {
                        Dictionary.lastLoadedModel = "N/A";
                    }
                }

                return false;
            }
            finally
            {
                FileManager.CurrentlyLoadingModel = false;
                FileManager.ModelOperationLock.Release();
            }
        }

        internal async Task<bool> ApplyPerformanceRecommendationAsync(PerformanceRecommendation recommendation)
        {
            if (recommendation.CanChangeImageSize &&
                recommendation.SuggestedImageSize != AimSettings.ImageSize)
            {
                bool changed = await ChangeImageSizeAsync(recommendation.SuggestedImageSize.ToString());
                if (!changed)
                    return false;
            }

            Dictionary.sliderSettings["AI FPS Limit"] = recommendation.SuggestedFpsLimit;
            if (uiManager.S_AIFpsLimit != null)
            {
                uiManager.S_AIFpsLimit.Slider.Value = recommendation.SuggestedFpsLimit;
            }

            return true;
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

            SaveDictionary.WriteJSON(Dictionary.sliderSettings
                .Concat(Dictionary.dropdownState)
                //.Where(kvp => kvp.Key != "Screen Capture Method")
                .GroupBy(kvp => kvp.Key)
                .ToDictionary(g => g.Key, g => g
                .First().Value));
            SaveDictionary.WriteJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
            SaveDictionary.WriteJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
            SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
            SaveDictionary.WriteJSON(Dictionary.colorState, "bin\\colors.cfg");
            SaveDictionary.WriteJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
            SaveDictionary.WriteJSON(Dictionary.toggleState, "bin\\toggles.cfg");
        }

        #endregion

        #region Menu Management

        private UserControl GetOrCreateMenuControl(string menuName)
        {
            if (_menuControls[menuName] is { } cached) return cached;

            var control = CreateMenuControl(menuName);
            switch (control)
            {
                case AimMenuControl aim:
                    aim.Initialize(this);
                    LoadDropdownStates();
                    break;
                case SettingsMenuControl settings:
                    settings.Initialize(this);
                    LoadDropdownStates();
                    SettingsMenuControlInstance = settings;
                    break;
                case AboutMenuControl about:
                    about.Initialize(this);
                    UpdateAboutSpecs(about);
                    break;
                case ModelMenuControl models:
                    models.Initialize(this);
                    break;
            }

            _menuControls[menuName] = control;
            return control;
        }

        private UserControl CreateMenuControl(string menuName) => menuName switch
        {
            "AimMenu" => new AimMenuControl(),
            "ModelMenu" => new ModelMenuControl(),
            "SettingsMenu" => new SettingsMenuControl(),
            "AboutMenu" => new AboutMenuControl(),
            _ => throw new ArgumentException($"Unknown menu: {menuName}")
        };

        private void LoadMenu(string menuName)
        {
            var control = GetOrCreateMenuControl(menuName);
            if (control.Parent == null) ContentArea.Children.Add(control);
            if (_menuControls.TryGetValue(_currentMenu, out var previous) && previous != control && previous != null)
                previous.Visibility = Visibility.Collapsed;
            control.Visibility = Visibility.Visible;
            _currentMenu = menuName;
        }

        private void MenuSwitch(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string menu } button ||
                menu == _currentMenu || !_menuControls.ContainsKey(menu)) return;
            try
            {
                bool cached = _menuControls[menu] != null;
                var start = System.Diagnostics.Stopwatch.GetTimestamp();
                LoadMenu(menu);
                MenuHighlighter.Margin = button.Margin;
                if (Dictionary.toggleState["Debug Mode"])
                    LogManager.Log(LogManager.LogLevel.Info,
                        $"Opened {menu} ({(cached ? "cached" : "first visit")}) in {System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2} ms");
            }
            catch (Exception ex)
            {
                LogManager.Log(LogManager.LogLevel.Error, $"Could not open {menu}. {ex.Message}", true);
            }
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
                ["Sticky Aim"] = () => UpdateSliderVisibility(uiManager),
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
                ["StreamGuard"] = () =>
                {
                    StreamGuardManager.ApplyStreamGuardToAllWindows(Dictionary.toggleState[title]);
                },
                ["EMA Smoothening"] = () =>
                {
                    MouseManager.IsEMASmoothingEnabled = Dictionary.toggleState[title];
                },
                ["X Axis Percentage Adjustment"] = () => UpdateSliderVisibility(uiManager),
                ["Y Axis Percentage Adjustment"] = () => UpdateSliderVisibility(uiManager)
            };

            if (actions.TryGetValue(title, out var action))
            {
                action();
            }
        }
        private static void UpdateSliderVisibility(UI uiManager)
        {
            bool useYPercent = Dictionary.toggleState["Y Axis Percentage Adjustment"];
            bool useXPercent = Dictionary.toggleState["X Axis Percentage Adjustment"];

            // Null checks in case AimMenu hasn't been loaded yet
            UpdateStickySliderVisibility(uiManager);

            if (uiManager.S_YOffset != null)
                uiManager.S_YOffset.Visibility = useYPercent ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_YOffsetPercent != null)
                uiManager.S_YOffsetPercent.Visibility = useYPercent ? Visibility.Visible : Visibility.Collapsed;

            if (uiManager.S_XOffset != null)
                uiManager.S_XOffset.Visibility = useXPercent ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_XOffsetPercent != null)
                uiManager.S_XOffsetPercent.Visibility = useXPercent ? Visibility.Visible : Visibility.Collapsed;
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
                ["Emergency Stop Keybind"] = HandleEmergencyStop
            };

            handlers.GetValueOrDefault(bindingId)?.Invoke();
        }

        private void HandleKeybindReleased(string bindingId)
        {
            var handlers = new Dictionary<string, Action>
            {
                ["Dynamic FOV Keybind"] = () => ApplyDynamicFOV(false)
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
            if (!Dictionary.toggleState["Dynamic FOV"])
            {
                FOVWindow.Circle.BeginAnimation(FrameworkElement.WidthProperty, null);
                FOVWindow.Circle.BeginAnimation(FrameworkElement.HeightProperty, null);
                FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.WidthProperty, null);
                FOVWindow.RectangleShape.BeginAnimation(FrameworkElement.HeightProperty, null);

                FOVWindow.UpdateFOVSize(ActualFOV);
                return;
            }
            var targetSize = apply ? Convert.ToDouble(Dictionary.sliderSettings["Dynamic FOV Size"]) : ActualFOV;
            Dictionary.sliderSettings["FOV Size"] = targetSize;
            AnimateFOVSize(targetSize);
        }
        private void AnimateFOVSize(double targetSize)
        {
            var duration = TimeSpan.FromMilliseconds(500);
            var dpi = VisualTreeHelper.GetDpi(FOVWindow);
            double width = targetSize / dpi.DpiScaleX, height = targetSize / dpi.DpiScaleY;
            Animator.WidthShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualWidth, width);
            Animator.HeightShift(duration, FOVWindow.Circle, FOVWindow.Circle.ActualHeight, height);
            Animator.WidthShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualWidth, width);
            Animator.HeightShift(duration, FOVWindow.RectangleShape, FOVWindow.RectangleShape.ActualHeight, height);
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
            LogManager.Log(LogManager.LogLevel.Info, "[Emergency Stop Keybind] Disabled all AI features.", true);
        }

        #endregion

        #region UI Effects

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (!Dictionary.toggleState["Mouse Background Effect"]) return;

            var translatedMousePos = e.GetPosition(MainBorder);

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
            RestoreCommonDropdowns();
            uiManager.D_ScreenCaptureMethod?.RestoreSelection();
            UpdatePredictionSliderVisibility();
            UpdateAimAssistSliderVisibility();
            UpdateAimConfigSliderVisibility();
        }

        internal static void ApplyConfigLoadDefaults(IDictionary<string, dynamic> sliderSettings)
        {
            sliderSettings["AI FPS Limit"] = 0;
            sliderSettings["Sticky Aim Grace"] = StickyAimSelector.DefaultGraceMilliseconds;
        }

        private void LoadConfig(string path = "bin\\configs\\Default.cfg", bool loading_from_configlist = false)
        {
            ApplyConfigLoadDefaults(Dictionary.sliderSettings);
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, path);
            SaveDictionary.LoadJSON(Dictionary.dropdownState, path);

            if (!loading_from_configlist || _menuControls["AimMenu"] == null)
                return;

            try
            {
                ShowSuggestedModelIfSpecified();
                ApplyConfigToSliders();
                ApplyConfigToDropdowns();
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
                ("FOV Size", uiManager.S_FOVSize, 640.0),
                ("Mouse Sensitivity (+/-)", uiManager.S_MouseSensitivity, 0.8),
                ("AI FPS Limit", uiManager.S_AIFpsLimit, 0.0),
                ("Mouse Jitter", uiManager.S_MouseJitter, 0.0),
                ("Sticky Aim Threshold", uiManager.S_StickyAimThreshold, 50),
                ("Sticky Aim Grace", uiManager.S_StickyAimGrace, StickyAimSelector.DefaultGraceMilliseconds),
                ("EMA Smoothening", uiManager.S_EMASmoothing, 0.5),
                ("Y Offset (Up/Down)", uiManager.S_YOffset, 0.0),
                ("X Offset (Left/Right)", uiManager.S_XOffset, 0.0),
                ("Y Offset (%)", uiManager.S_YOffsetPercent, 0.0),
                ("X Offset (%)", uiManager.S_XOffsetPercent, 0.0),
                ("Auto Trigger Delay", uiManager.S_AutoTriggerDelay, 0.25),
                ("AI Minimum Confidence", uiManager.S_AIMinimumConfidence, 50.0),
                ("Kalman Lead Time", uiManager.S_KalmanLeadTime, 0.10),
                ("WiseTheFox Lead Time", uiManager.S_WiseTheFoxLeadTime, 0.15),
                ("Shalloe Lead Multiplier", uiManager.S_ShalloeLeadMultiplier, 3.0)
            };

            ApplySliderValues(sliderConfigs, Dictionary.sliderSettings);
        }


        private void ApplyConfigToDropdowns()
        {
            RestoreCommonDropdowns();
            uiManager.D_MovementPath?.RestoreSelection();
            uiManager.D_TracerPosition?.RestoreSelection();
            uiManager.D_TargetClass?.RestoreSelection();
            UpdatePredictionSliderVisibility();
        }

        public void UpdatePredictionSliderVisibility()
        {
            // Hide all prediction sliders first
            if (uiManager.S_KalmanLeadTime != null)
                uiManager.S_KalmanLeadTime.Visibility = Visibility.Collapsed;
            if (uiManager.S_WiseTheFoxLeadTime != null)
                uiManager.S_WiseTheFoxLeadTime.Visibility = Visibility.Collapsed;
            if (uiManager.S_ShalloeLeadMultiplier != null)
                uiManager.S_ShalloeLeadMultiplier.Visibility = Visibility.Collapsed;

            // Don't show sliders if Predictions section is collapsed
            if (Dictionary.minimizeState.TryGetValue("Predictions", out var collapsed) && collapsed == true)
                return;

            // Get selected method from actual dropdown selection
            var selectedItem = uiManager.D_PredictionMethod?.DropdownBox?.SelectedItem as ComboBoxItem;
            string selectedMethod = selectedItem?.Content?.ToString() ?? "";

            // Show only the relevant slider based on selected method
            switch (selectedMethod)
            {
                case "Kalman Filter":
                    if (uiManager.S_KalmanLeadTime != null)
                        uiManager.S_KalmanLeadTime.Visibility = Visibility.Visible;
                    break;
                case "Shall0e's Prediction":
                    if (uiManager.S_ShalloeLeadMultiplier != null)
                        uiManager.S_ShalloeLeadMultiplier.Visibility = Visibility.Visible;
                    break;
                case "wisethef0x's EMA Prediction":
                    if (uiManager.S_WiseTheFoxLeadTime != null)
                        uiManager.S_WiseTheFoxLeadTime.Visibility = Visibility.Visible;
                    break;
            }
        }

        public void UpdateAimAssistSliderVisibility() => UpdateStickySliderVisibility(uiManager);

        private static void UpdateStickySliderVisibility(UI uiManager)
        {
            bool collapsed = Dictionary.minimizeState.TryGetValue("Aim Assist", out var value) && value == true;
            var visibility = !collapsed && Dictionary.toggleState["Sticky Aim"] ? Visibility.Visible : Visibility.Collapsed;
            if (uiManager.S_StickyAimThreshold != null) uiManager.S_StickyAimThreshold.Visibility = visibility;
            if (uiManager.S_StickyAimGrace != null) uiManager.S_StickyAimGrace.Visibility = visibility;
        }

        public void UpdateAimConfigSliderVisibility()
        {
            // Don't show sliders if Aim Config section is collapsed
            if (Dictionary.minimizeState.TryGetValue("Aim Config", out var collapsed) && collapsed == true)
            {
                if (uiManager.S_YOffset != null) uiManager.S_YOffset.Visibility = Visibility.Collapsed;
                if (uiManager.S_YOffsetPercent != null) uiManager.S_YOffsetPercent.Visibility = Visibility.Collapsed;
                if (uiManager.S_XOffset != null) uiManager.S_XOffset.Visibility = Visibility.Collapsed;
                if (uiManager.S_XOffsetPercent != null) uiManager.S_XOffsetPercent.Visibility = Visibility.Collapsed;
                return;
            }

            // Y Axis: Show pixel offset when toggle is OFF, percentage offset when toggle is ON
            bool yPercentEnabled = Dictionary.toggleState["Y Axis Percentage Adjustment"];
            if (uiManager.S_YOffset != null)
                uiManager.S_YOffset.Visibility = yPercentEnabled ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_YOffsetPercent != null)
                uiManager.S_YOffsetPercent.Visibility = yPercentEnabled ? Visibility.Visible : Visibility.Collapsed;

            // X Axis: Show pixel offset when toggle is OFF, percentage offset when toggle is ON
            bool xPercentEnabled = Dictionary.toggleState["X Axis Percentage Adjustment"];
            if (uiManager.S_XOffset != null)
                uiManager.S_XOffset.Visibility = xPercentEnabled ? Visibility.Collapsed : Visibility.Visible;
            if (uiManager.S_XOffsetPercent != null)
                uiManager.S_XOffsetPercent.Visibility = xPercentEnabled ? Visibility.Visible : Visibility.Collapsed;
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

        private void RestoreCommonDropdowns()
        {
            uiManager.D_PredictionMethod?.RestoreSelection();
            uiManager.D_DetectionAreaType?.RestoreSelection();
            uiManager.D_AimingBoundariesAlignment?.RestoreSelection();
            uiManager.D_MouseMovementMethod?.RestoreSelection();
            uiManager.D_ImageSize?.RestoreSelection();
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
