using AimmyAimbot;
using AimmyWPF.Class;
using AimmyWPF.UserController;
using Class;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Visualization;
using static AimmyWPF.PredictionManager;

namespace AimmyWPF
{
    public partial class MainWindow : Window
    {
        private PredictionManager predictionManager;
        private OverlayWindow FOVOverlay;
        private PlayerDetectionWindow DetectedPlayerOverlay;
        private FileSystemWatcher fileWatcher;
        private FileSystemWatcher ConfigfileWatcher;

        private string lastLoadedModel = "N/A";
        private string lastLoadedConfig = "N/A";

        private readonly BrushConverter brushcolor = new();

        private int TimeSinceLastClick = 0;
        private DateTime LastClickTime = DateTime.MinValue;

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001; // Movement flag

        private static int ScreenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
        private static int ScreenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

        private AIModel _onnxModel;
        private InputBindingManager bindingManager;
        private bool IsHolding_Binding = false;
        private CancellationTokenSource cts;

        private enum MenuPosition
        {
            AimMenu,
            TriggerMenu,
            SelectorMenu,
            SettingsMenu
        }

        // Changed to Dynamic from Double because it was making the Config System hard to rework :/
        public Dictionary<string, dynamic> aimmySettings = new()
        {
            { "Suggested_Model", ""},
            { "FOV_Size", 640 },
            { "Mouse_Sens", 0.80 },
            { "Mouse_Jitter", 4 },
            { "Y_Offset", 0 },
            { "X_Offset", 0 },
            { "Trigger_Delay", 0.1 },
            { "AI_Min_Conf", 50 }
        };

        private Dictionary<string, bool> toggleState = new()
        {
            { "AimbotToggle", false },
            { "AlwaysOn", false },
            { "PredictionToggle", false },
            { "AimViewToggle", false },
            { "TriggerBot", false },
            { "CollectData", false },
            { "TopMost", false }
        };

        // PDW == PlayerDetectionWindow
        public Dictionary<string, dynamic> OverlayProperties = new()
        {
            { "FOV_Color", "#ff0000"},
            { "PDW_Size", 50 },
            { "PDW_CornerRadius", 0 },
            { "PDW_BorderThickness", 1 },
            { "PDW_Opacity", 1 }
        };

        private Thickness WinTooLeft = new(-1680, 0, 1680, 0);
        private Thickness WinVeryLeft = new(-1120, 0, 1120, 0);
        private Thickness WinLeft = new(-560, 0, 560, 0);

        private Thickness WinCenter = new(0, 0, 0, 0);

        private Thickness WinRight = new(560, 0, -560, 0);
        private Thickness WinVeryRight = new(1120, 0, -1120, 0);
        private Thickness WinTooRight = new(1680, 0, -1680, 0);

        public MainWindow()
        {
            InitializeComponent();
            this.Title = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

            // Check to see if certain items are installed
            RequirementsManager RM = new();
            if (!RM.IsVCRedistInstalled())
            {
                MessageBox.Show("这个设备上没有安装Visual c++ redistributable x64，请在使用aimy之前安装它们，以避免问题。", "加载错误");
                Process.Start("https://aka.ms/vs/17/release/vc_redist.x64.exe");
                Application.Current.Shutdown();
            }
            //if(!RM.IsDotNetInstalled()) not working
            //{
            //    MessageBox.Show("DotNet 7.0 is not installed on this device, please install them before using Aimmy to avoid issues.", "Load Error");
            //    Process.Start("https://dotnet.microsoft.com/download/dotnet/7.0");
            //    Application.Current.Shutdown();
            //}

            // Check for required folders
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] dirs = { "bin", "bin/models", "bin/images", "bin/configs" };

            try
            {
                foreach (string dir in dirs)
                {
                    string fullPath = Path.Combine(baseDir, dir);
                    if (!Directory.Exists(fullPath))
                    {
                        // Create the directory
                        Directory.CreateDirectory(fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating a required directory: {ex}");
                Application.Current.Shutdown(); // We don't want to continue running without that folder.
            }

            // Setup key/mouse hook
            bindingManager = new InputBindingManager();
            bindingManager.SetupDefault("Right");
            bindingManager.OnBindingPressed += (binding) => { IsHolding_Binding = true; };
            bindingManager.OnBindingReleased += (binding) => { IsHolding_Binding = false; };

            // Load UI
            InitializeMenuPositions();
            //LoadAimMenu();
            //LoadTriggerMenu();
            //LoadSettingsMenu();
            ReloadMenu();
            InitializeFileWatcher();
            InitializeConfigWatcher();

            // Load PredictionManager
            predictionManager = new PredictionManager();

            // Load all models into listbox
            LoadModelsIntoListBox();
            LoadConfigsIntoListBox();

            SelectorListBox.SelectionChanged += new SelectionChangedEventHandler(SelectorListBox_SelectionChanged);
            ConfigSelectorListBox.SelectionChanged += new SelectionChangedEventHandler(ConfigSelectorListBox_SelectionChanged);

            // Create FOV Overlay
            FOVOverlay = new OverlayWindow();
            FOVOverlay.Hide();
            FOVOverlay.FovSize = (int)aimmySettings["FOV_Size"];
            AwfulPropertyChanger.PostNewFOVSize();
            AwfulPropertyChanger.PostTravellingFOV(false);

            // Create Current Detected Player Overlay
            DetectedPlayerOverlay = new PlayerDetectionWindow();
            DetectedPlayerOverlay.Hide();

            // Start the loop that runs the model
            Task.Run(() => StartModelCaptureLoop());
        }

        private HashSet<string> AvailableModels = new();
        private HashSet<string> AvailableConfigs = new();

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadOverlayPropertiesAsync("bin/Overlay.cfg");
            await LoadConfigAsync("bin/configs/Default.cfg");
            try
            {
                Task modelTask = RetrieveAndAddFilesAsync("models", "bin\\models", AvailableModels);
                Task configTask = RetrieveAndAddFilesAsync("configs", "bin\\configs", AvailableConfigs);
                await Task.WhenAll(modelTask, configTask);
                LoadStoreMenu();
            }
            catch
            {
                MessageBox.Show("Github现在无法恢复，下载的模型菜单现在不能工作，抱歉!");
            }
        }

        private async Task RetrieveAndAddFilesAsync(string repositoryPath, string localPath, HashSet<string> availableFiles)
        {
            IEnumerable<string> results = await RetrieveGithubFiles.ListContents(repositoryPath);

            foreach (var file in results)
            {
                string filePath = Path.Combine(localPath, file);
                if (!availableFiles.Contains(file) && !File.Exists(filePath))
                {
                    availableFiles.Add(file);
                }
            }
        }

        #region Mouse Movement / Clicking Handler

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private static Random MouseRandom = new();

        private static Point CubicBezier(Point start, Point end, Point control1, Point control2, double t)
        {
            double u = 1 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            double x = uuu * start.X + 3 * uu * t * control1.X + 3 * u * tt * control2.X + ttt * end.X;
            double y = uuu * start.Y + 3 * uu * t * control1.Y + 3 * u * tt * control2.Y + ttt * end.Y;

            return new Point((int)x, (int)y);
        }

        private async Task DoTriggerClick()
        {
            int TimeSinceLastClick = (int)(DateTime.Now - LastClickTime).TotalMilliseconds;
            int Trigger_Delay_Milliseconds = (int)(aimmySettings["Trigger_Delay"] * 1000);

            if (TimeSinceLastClick >= Trigger_Delay_Milliseconds || LastClickTime == DateTime.MinValue)
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                await Task.Delay(20);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                LastClickTime = DateTime.Now;
            }

            return;
        }

        private void MoveCrosshair(int detectedX, int detectedY)
        {
            double Alpha = aimmySettings["Mouse_Sens"];

            int halfScreenWidth = ScreenWidth / 2;
            int halfScreenHeight = ScreenHeight / 2;

            int targetX = detectedX - halfScreenWidth;
            int targetY = detectedY - halfScreenHeight;

            // Aspect ratio correction factor
            double aspectRatioCorrection = (double)ScreenWidth / ScreenHeight;
            targetY = (int)(targetY * aspectRatioCorrection);

            // Introduce random jitter
            int MouseJitter = (int)aimmySettings["Mouse_Jitter"];
            int jitterX = MouseRandom.Next(-MouseJitter, MouseJitter);
            int jitterY = MouseRandom.Next(-MouseJitter, MouseJitter);

            targetX += jitterX;
            targetY += jitterY;

            // Define Bezier curve control points
            Point start = new(0, 0); // Current cursor position (locked to center screen)
            Point end = new(targetX, targetY);
            Point control1 = new(start.X + (end.X - start.X) / 3, start.Y + (end.Y - start.Y) / 3);
            Point control2 = new(start.X + 2 * (end.X - start.X) / 3, start.Y + 2 * (end.Y - start.Y) / 3);

            // Calculate new position along the Bezier curve
            Point newPosition = CubicBezier(start, end, control1, control2, 1 - Alpha);

            mouse_event(MOUSEEVENTF_MOVE, (uint)newPosition.X, (uint)newPosition.Y, 0, 0);

            if (toggleState["TriggerBot"])
            {
                Task.Run(DoTriggerClick);
            }
        }

        #endregion Mouse Movement / Clicking Handler

        #region Aim Aligner Main and Loop

        public async Task ModelCapture(bool TriggerOnly = false)
        {
            var closestPrediction = await _onnxModel.GetClosestPredictionToCenterAsync();
            if (closestPrediction == null)
            {
                return;
            }
            else if (TriggerOnly)
            {
                Task.Run(DoTriggerClick);
                return;
            }

            float scaleX = (float)ScreenWidth / 640f;
            float scaleY = (float)ScreenHeight / 640f;

            double YOffset = aimmySettings["Y_Offset"];
            double XOffset = aimmySettings["X_Offset"];
            int detectedX = (int)((closestPrediction.Rectangle.X + closestPrediction.Rectangle.Width / 2) * scaleX + XOffset);
            int detectedY = (int)((closestPrediction.Rectangle.Y + closestPrediction.Rectangle.Height / 2) * scaleY + YOffset);

            Console.WriteLine(AIModel.AIConfidence.ToString());

            // Handle Prediction
            if (toggleState["PredictionToggle"])
            {
                Detection detection = new()
                {
                    X = detectedX,
                    Y = detectedY,
                    Timestamp = DateTime.UtcNow
                };

                predictionManager.UpdateKalmanFilter(detection);
                var predictedPosition = predictionManager.GetEstimatedPosition();

                if ((Bools.AimOnlyWhenBindingHeld && IsHolding_Binding) || Bools.AimOnlyWhenBindingHeld == false)
                    MoveCrosshair(predictedPosition.X, predictedPosition.Y);
                //MoveCrosshair(predictedPosition.X, predictedPosition.Y);

                if (Bools.ShowDetectedPlayerWindow && Bools.ShowPrediction)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        DetectedPlayerOverlay.PredictionFocus.Margin = new Thickness(
                            predictedPosition.X - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            predictedPosition.Y - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            0, 0);
                    });
                }
            }
            else
            {
                if ((Bools.AimOnlyWhenBindingHeld && IsHolding_Binding) || Bools.AimOnlyWhenBindingHeld == false)
                    MoveCrosshair(detectedX, detectedY);
            }

            if (Bools.ShowDetectedPlayerWindow)
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (Bools.ShowCurrentDetectedPlayer)
                        DetectedPlayerOverlay.DetectedPlayerFocus.Margin = new Thickness(
                            detectedX - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            detectedY - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            0, 0);

                    if (Bools.ShowUnfilteredDetectedPlayer)
                        DetectedPlayerOverlay.UnfilteredPlayerFocus.Margin = new Thickness(
                            (int)((closestPrediction.Rectangle.X + closestPrediction.Rectangle.Width / 2) * scaleX) - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            (int)((closestPrediction.Rectangle.Y + closestPrediction.Rectangle.Height / 2) * scaleY) - (OverlayProperties["PDW_Size"] / (OverlayProperties["PDW_Size"] / 2)),
                            0, 0);
                });
            }
        }

        private async Task StartModelCaptureLoop()
        {
            // Create a new CancellationTokenSource
            cts = new CancellationTokenSource();

            while (!cts.Token.IsCancellationRequested)
            {
                if (toggleState["AimbotToggle"] && Bools.ConstantTracking == false)
                {
                    if (IsHolding_Binding)
                        await ModelCapture();
                }
                else if (toggleState["AimbotToggle"] && Bools.ConstantTracking)
                {
                    await ModelCapture();
                }
                else if (!toggleState["AimbotToggle"] && toggleState["TriggerBot"] && IsHolding_Binding && Bools.ConstantTracking == false) // Triggerbot Only
                {
                    await ModelCapture(true);
                }

                //if (toggleState["AimbotToggle"] && (IsHolding_Binding || toggleState["AlwaysOn"]))
                //{
                //    await ModelCapture();
                //}
                //else if (!toggleState["AimbotToggle"] && toggleState["TriggerBot"] && IsHolding_Binding) // Triggerbot Only
                //{
                //    await ModelCapture(true);
                //}

                // We have to have some sort of delay here to not overload the CPU / reduce CPU usage.
                await Task.Delay(1);
            }
        }

        public void StopModelCaptureLoop()
        {
            if (cts != null)
            {
                cts?.Cancel();
                cts = null;
            }
        }

        #endregion Aim Aligner Main and Loop

        #region Menu Initialization and Setup

        private void InitializeMenuPositions()
        {
            AimMenu.Margin = new Thickness(0, 0, 0, 0);
            TriggerMenu.Margin = new Thickness(560, 0, -560, 0);
            SelectorMenu.Margin = new Thickness(560, 0, -560, 0);
            SettingsMenu.Margin = new Thickness(1680, 0, -1680, 0);
        }

        private void SetupToggle(AToggle toggle, Action<bool> action, bool initialState)
        {
            toggle.Reader.Tag = initialState;
            (initialState ? (Action)(() => toggle.EnableSwitch()) : () => toggle.DisableSwitch())();

            toggle.Reader.Click += (s, x) =>
            {
                bool currentState = (bool)toggle.Reader.Tag;
                toggle.Reader.Tag = !currentState;
                action.Invoke(!currentState);
                SetToggleState(toggle);
            };
        }

        private void SetToggleState(AToggle toggle)
        {
            bool state = (bool)toggle.Reader.Tag;

            // Stop them from turning on anything until the model has been selected.
            if ((toggle.Reader.Name == "AimbotToggle" || toggle.Reader.Name == "AimOnlyWhenBindingHeld" || toggle.Reader.Name == "ConstantAITracking" || toggle.Reader.Name == "TriggerBot" || toggle.Reader.Name == "CollectData") && lastLoadedModel == "N/A")
            {
                SetToggleStatesOnModelNotSelected();
                MessageBox.Show("切换前请在模型选择器中选择一个模型。", "切换错误");
                return;
            }

            (state ? (Action)toggle.EnableSwitch : (Action)toggle.DisableSwitch)();

            toggleState[toggle.Reader.Name] = state;

            HandleToggleSpecificActions(toggle);
        }

        private void SetToggleStatesOnModelNotSelected()
        {
            Bools.AIAimAligner = false;
            Bools.Triggerbot = false;
            Bools.CollectDataWhilePlaying = false;
        }

        private void HandleToggleSpecificActions(AToggle toggle)
        {
            string toggleName = toggle.Reader.Name;

            switch (toggleName)
            {
                case "CollectData":
                    _onnxModel.CollectData = (bool)toggle.Reader.Tag;
                    break;

                case "ShowFOV":
                    ((bool)toggle.Reader.Tag ? (Action)FOVOverlay.Show : (Action)FOVOverlay.Hide)();
                    break;

                case "TravellingFOV":
                    AwfulPropertyChanger.PostTravellingFOV((bool)toggle.Reader.Tag);
                    break;

                case "ShowDetectedPlayerWindow":
                    ((bool)toggle.Reader.Tag ? (Action)DetectedPlayerOverlay.Show : (Action)DetectedPlayerOverlay.Hide)();
                    break;

                case "ShowCurrentDetectedPlayer":
                    ((bool)toggle.Reader.Tag ? (Action)(() => DetectedPlayerOverlay.DetectedPlayerFocus.Visibility = Visibility.Visible) : () => DetectedPlayerOverlay.DetectedPlayerFocus.Visibility = Visibility.Collapsed)();
                    break;

                case "ShowUnfilteredDetectedPlayer":
                    ((bool)toggle.Reader.Tag ? (Action)(() => DetectedPlayerOverlay.UnfilteredPlayerFocus.Visibility = Visibility.Visible) : () => DetectedPlayerOverlay.UnfilteredPlayerFocus.Visibility = Visibility.Collapsed)();
                    break;

                case "ShowAIPrediction":
                    ((bool)toggle.Reader.Tag ? (Action)(() => DetectedPlayerOverlay.PredictionFocus.Visibility = Visibility.Visible) : () => DetectedPlayerOverlay.PredictionFocus.Visibility = Visibility.Collapsed)();
                    break;

                case "TopMost":
                    Topmost = (bool)toggle.Reader.Tag;
                    break;
            }
        }

        #endregion Menu Initialization and Setup

        #region Menu Controls

        private async void Selection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button clickedButton)
            {
                MenuPosition position = (MenuPosition)Enum.Parse(typeof(MenuPosition), clickedButton.Tag.ToString());
                ResetMenuColors();
                clickedButton.Foreground = (Brush)brushcolor.ConvertFromString("#3e8fb0");
                ApplyMenuAnimations(position);
                UpdateMenuVisibility(position);
            }
        }

        private void ResetMenuColors()
        {
            Selection1.Foreground = Selection2.Foreground = Selection3.Foreground = Selection4.Foreground =
                (System.Windows.Media.Brush)brushcolor.ConvertFromString("#ffffff");
        }

        private void ApplyMenuAnimations(MenuPosition position)
        {
            Thickness highlighterMargin = new(0, 30, 414, 0);
            switch (position)
            {
                case MenuPosition.AimMenu:
                    highlighterMargin = new Thickness(10, 30, 414, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinVeryRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinTooRight);
                    break;

                case MenuPosition.TriggerMenu:
                    highlighterMargin = new Thickness(144, 30, 278, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinRight);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinVeryRight);
                    break;

                case MenuPosition.SelectorMenu:
                    highlighterMargin = new Thickness(280, 30, 144, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinCenter);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinRight);
                    break;

                case MenuPosition.SettingsMenu:
                    highlighterMargin = new Thickness(414, 30, 10, 0);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), MenuHighlighter, MenuHighlighter.Margin, highlighterMargin);

                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), AimMenu, AimMenu.Margin, WinTooLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), TriggerMenu, TriggerMenu.Margin, WinVeryLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SelectorMenu, SelectorMenu.Margin, WinLeft);
                    Animator.ObjectShift(TimeSpan.FromMilliseconds(500), SettingsMenu, SettingsMenu.Margin, WinCenter);
                    break;
            }
        }

        private void UpdateMenuVisibility(MenuPosition position)
        {
            AimMenu.Visibility = (position == MenuPosition.AimMenu) ? Visibility.Visible : Visibility.Collapsed;
            TriggerMenu.Visibility = (position == MenuPosition.TriggerMenu) ? Visibility.Visible : Visibility.Collapsed;
            SelectorMenu.Visibility = (position == MenuPosition.SelectorMenu) ? Visibility.Visible : Visibility.Collapsed;
            SettingsMenu.Visibility = (position == MenuPosition.SettingsMenu) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion Menu Controls

        #region More Info Function

        public void ActivateMoreInfo(string info)
        {
            SetMenuState(false);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(1000), MoreInfoBox, MoreInfoBox.Margin, new Thickness(5, 0, 5, 5));
            MoreInfoBox.Visibility = Visibility.Visible;
            InfoText.Text = info;
        }

        private async void MoreInfoExit_Click(object sender, RoutedEventArgs e)
        {
            Animator.ObjectShift(TimeSpan.FromMilliseconds(1000), MoreInfoBox, MoreInfoBox.Margin, new Thickness(5, 0, 5, -180));
            await Task.Delay(1000);
            MoreInfoBox.Visibility = Visibility.Collapsed;
            SetMenuState(true);
        }

        private void SetMenuState(bool state)
        {
            AimMenu.IsEnabled = state;
            TriggerMenu.IsEnabled = state;
            SelectorMenu.IsEnabled = state;
            SettingsMenu.IsEnabled = state;
        }

        #endregion More Info Function

        private void LoadAimMenu()
        {
            AToggle Enable_AIAimAligner = new(this, "启用AI瞄准校准器",
                "这将使AI能够调整目标。");
            Enable_AIAimAligner.Reader.Name = "AimbotToggle";
            SetupToggle(Enable_AIAimAligner, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Enable_AIAimAligner);

            AToggle Enable_ConstantAITracking = new(this, "启用恒定AI校准器",
    "这将让AI运行24/7可视化调试运行。");
            Enable_ConstantAITracking.Reader.Name = "ConstantAITracking";
            SetupToggle(Enable_ConstantAITracking, state => Bools.ConstantTracking = state, Bools.ConstantTracking);
            AimScroller.Children.Add(Enable_ConstantAITracking);

            AToggle AimOnlyWhenBindingHeld = new(this, "只有当触发按钮被按住时才恒定", // this can be simplifed with a toggle between constant and hold (toggle/hold), ill do it later.
"这将阻止AI瞄准，除非触发按钮被按住。");
            AimOnlyWhenBindingHeld.Reader.Name = "AimOnlyWhenBindingHeld";
            SetupToggle(AimOnlyWhenBindingHeld, state => Bools.AimOnlyWhenBindingHeld = state, Bools.AimOnlyWhenBindingHeld);
            AimScroller.Children.Add(AimOnlyWhenBindingHeld);

            AKeyChanger Change_KeyPress = new("改变按键监听", "右键");
            Change_KeyPress.Reader.Click += (s, x) =>
            {
                Change_KeyPress.KeyNotifier.Content = "监听中..";
                bindingManager.StartListeningForBinding();
            };

            bindingManager.OnBindingSet += (binding) =>
            {
                Change_KeyPress.KeyNotifier.Content = binding;
            };

            AimScroller.Children.Add(Change_KeyPress);

            //AToggle Enable_AlwaysOn = new AToggle(this, "Aim Align Always On",
            //   "This will keep the aim aligner on 24/7 so you don't have to hold a toggle.");
            //Enable_AlwaysOn.Reader.Name = "AlwaysOn";
            //SetupToggle(Enable_AlwaysOn, state => Bools.AIAlwaysOn = state, Bools.AIAlwaysOn);
            //AimScroller.Children.Add(Enable_AlwaysOn);

            AToggle Enable_AIPredictions = new(this, "启用预测",
               "这将使用KalmanFilter算法来预测目标模式，以便更好地追踪敌人。");
            Enable_AIPredictions.Reader.Name = "PredictionToggle";
            SetupToggle(Enable_AIPredictions, state => Bools.AIPredictions = state, Bools.AIPredictions);
            AimScroller.Children.Add(Enable_AIPredictions);

            #region FOV System

            AimScroller.Children.Add(new ALabel("FOV 系统"));

            AToggle Show_FOV = new(this, "显示 FOV",
                "这将在你的屏幕周围显示一个圆圈，显示AI在特定时刻在屏幕上考虑的内容。");
            Show_FOV.Reader.Name = "ShowFOV";
            SetupToggle(Show_FOV, state => Bools.ShowFOV = state, Bools.ShowFOV);
            AimScroller.Children.Add(Show_FOV);

            AToggle Travelling_FOV = new(this, "移动 FOV",
    "这将允许FOV圈与鼠标一起移动。\n" +
    "[请注意]: 这对AI的个人FOV没有任何影响，这个功能只是为了视觉效果。");
            Travelling_FOV.Reader.Name = "TravellingFOV";
            SetupToggle(Travelling_FOV, state => Bools.TravellingFOV = state, Bools.TravellingFOV);
            AimScroller.Children.Add(Travelling_FOV);

            AColorChanger Change_FOVColor = new("FOV 颜色");
            Change_FOVColor.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(OverlayProperties["FOV_Color"]);
            Change_FOVColor.Reader.Click += (s, x) =>
            {
                System.Windows.Forms.ColorDialog colorDialog = new();
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Change_FOVColor.ColorChangingBorder.Background = new SolidColorBrush(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                    OverlayProperties["FOV_Color"] = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B).ToString();
                    AwfulPropertyChanger.PostColor(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                }
            };
            AimScroller.Children.Add(Change_FOVColor);

            ASlider FovSlider = new(this, "FOV 大小", "大小 FOV",
                "这一设置控制着AI在你的屏幕上所占的比例以及屏幕上的圆圈的大小。",
                1);

            FovSlider.Slider.Minimum = 10;
            FovSlider.Slider.Maximum = 320;
            FovSlider.Slider.Value = aimmySettings["FOV_Size"];
            FovSlider.Slider.TickFrequency = 1;
            FovSlider.Slider.ValueChanged += (s, x) =>
            {
                double FovSize = FovSlider.Slider.Value;
                aimmySettings["FOV_Size"] = FovSize;
                if (_onnxModel != null)
                {
                    _onnxModel.FovSize = (int)FovSize;
                }

                FOVOverlay.FovSize = (int)FovSize;
                AwfulPropertyChanger.PostNewFOVSize();
            };

            AimScroller.Children.Add(FovSlider);

            #endregion FOV System

            #region Aiming Configuration

            AimScroller.Children.Add(new ALabel("目标配置"));

            ASlider MouseSensitivty = new(this, "鼠标灵敏度", "灵敏度",
                "此设置控制您的鼠标移动到检测的速度，如果移动太快，则需要将其设置为更高的数字。",
                0.01);

            MouseSensitivty.Slider.Minimum = 0.01;
            MouseSensitivty.Slider.Maximum = 1;
            MouseSensitivty.Slider.Value = aimmySettings["Mouse_Sens"];
            MouseSensitivty.Slider.TickFrequency = 0.01;
            MouseSensitivty.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Mouse_Sens"] = MouseSensitivty.Slider.Value;
            };

            AimScroller.Children.Add(MouseSensitivty);

            ASlider MouseJitter = new(this, "鼠标抖动", "抖动",
                "这个设置控制了鼠标移动的假抖动程度。Aim几乎从不稳定，所以这给Aim增加了一个很好的人性化层。",
                0.01);

            MouseJitter.Slider.Minimum = 0;
            MouseJitter.Slider.Maximum = 15;
            MouseJitter.Slider.Value = aimmySettings["Mouse_Jitter"];
            MouseJitter.Slider.TickFrequency = 1;
            MouseJitter.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Mouse_Jitter"] = MouseJitter.Slider.Value;
            };
            AimScroller.Children.Add(MouseJitter);

            ASlider YOffset = new(this, "Y偏移量(上/下)", "偏移",
                "这个设置控制你的目标有多高/多低。数字越低，目标越高。较高的数字将导致较低的目标。",
                1);

            YOffset.Slider.Minimum = -150;
            YOffset.Slider.Maximum = 150;
            YOffset.Slider.Value = aimmySettings["Y_Offset"];
            YOffset.Slider.TickFrequency = 1;
            YOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Y_Offset"] = YOffset.Slider.Value;
            };

            AimScroller.Children.Add(YOffset);

            ASlider XOffset = new(this, "X偏移量(左/右)", "偏移",
                "这个设置控制你瞄准的方向。数字越低，目标就越偏左。数字越大，目标就越偏右",
                1);

            XOffset.Slider.Minimum = -150;
            XOffset.Slider.Maximum = 150;
            XOffset.Slider.Value = aimmySettings["X_Offset"];
            XOffset.Slider.TickFrequency = 1;
            XOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["X_Offset"] = XOffset.Slider.Value;
            };

            AimScroller.Children.Add(XOffset);

            #endregion Aiming Configuration

            #region Visual Debugging

            AimScroller.Children.Add(new ALabel("可视化调试"));

            AToggle Show_DetectedPlayerWindow = new(this, "显示检测到的玩家窗口",
                "显示检测到的玩家覆盖，如果启用，下面的选项将不起作用!");
            Show_DetectedPlayerWindow.Reader.Name = "ShowDetectedPlayerWindow";
            SetupToggle(Show_DetectedPlayerWindow, state => Bools.ShowDetectedPlayerWindow = state, Bools.ShowDetectedPlayerWindow);
            AimScroller.Children.Add(Show_DetectedPlayerWindow);

            AToggle Show_CurrentDetectedPlayer = new(this, "显示当前检测到的玩家[红色]",
    "这将在玩家上显示一个矩形，AI在给定时刻在屏幕上考虑。");
            Show_CurrentDetectedPlayer.Reader.Name = "ShowCurrentDetectedPlayer";
            SetupToggle(Show_CurrentDetectedPlayer, state => Bools.ShowCurrentDetectedPlayer = state, Bools.ShowCurrentDetectedPlayer);
            AimScroller.Children.Add(Show_CurrentDetectedPlayer);

            AToggle Show_UnfilteredDetectedPlayer = new(this, "显示当前检测到的玩家的原始版本[紫色]",
                "这将在玩家上显示AI在给定时刻在屏幕上考虑的矩形，而不考虑调整的X和Y轴。");
            Show_UnfilteredDetectedPlayer.Reader.Name = "ShowUnfilteredDetectedPlayer";
            SetupToggle(Show_UnfilteredDetectedPlayer, state => Bools.ShowUnfilteredDetectedPlayer = state, Bools.ShowUnfilteredDetectedPlayer);
            AimScroller.Children.Add(Show_UnfilteredDetectedPlayer);

            AToggle Show_Prediction = new(this, "显示AI预测[绿色]",
                "这将显示一个矩形，AI假设玩家在给定时刻将出现在屏幕上。");
            Show_Prediction.Reader.Name = "ShowAIPrediction";
            SetupToggle(Show_Prediction, state => Bools.ShowPrediction = state, Bools.ShowPrediction);
            AimScroller.Children.Add(Show_Prediction);

            #endregion Visual Debugging

            #region Visual Debugging Customizer

            AimScroller.Children.Add(new ALabel("可视化调试自定义"));

            ASlider Change_PDW_Size = new(this, "检测窗口大小", "大小",
                "此设置控制检测到的玩家窗口的大小。",
                1);

            Change_PDW_Size.Slider.Minimum = 10;
            Change_PDW_Size.Slider.Maximum = 100;
            Change_PDW_Size.Slider.Value = OverlayProperties["PDW_Size"];
            Change_PDW_Size.Slider.TickFrequency = 1;
            Change_PDW_Size.Slider.ValueChanged += (s, x) =>
            {
                int PDWSize = (int)Change_PDW_Size.Slider.Value;
                OverlayProperties["PDW_Size"] = PDWSize;
                AwfulPropertyChanger.PostPDWSize(PDWSize);
            };

            AimScroller.Children.Add(Change_PDW_Size);

            ASlider Change_PDW_CornerRadius = new(this, "检测窗口圆角半径", "圆角半径",
                "此设置控制检测到的玩家窗口的圆角半径。",
                1);

            Change_PDW_CornerRadius.Slider.Minimum = 0;
            Change_PDW_CornerRadius.Slider.Maximum = 100;
            Change_PDW_CornerRadius.Slider.Value = OverlayProperties["PDW_CornerRadius"];
            Change_PDW_CornerRadius.Slider.TickFrequency = 1;
            Change_PDW_CornerRadius.Slider.ValueChanged += (s, x) =>
            {
                int CornerRadiusSize = (int)Change_PDW_CornerRadius.Slider.Value;
                OverlayProperties["PDW_CornerRadius"] = CornerRadiusSize;
                AwfulPropertyChanger.PostPDWCornerRadius(CornerRadiusSize);
            };

            AimScroller.Children.Add(Change_PDW_CornerRadius);

            ASlider Change_PDW_BorderThickness = new(this, "检测窗口边框粗细", "边框粗细",
                "此设置控制检测到的玩家窗口的边框粗细。",
                1);

            Change_PDW_BorderThickness.Slider.Minimum = 0.1;
            Change_PDW_BorderThickness.Slider.Maximum = 10;
            Change_PDW_BorderThickness.Slider.Value = OverlayProperties["PDW_BorderThickness"];
            Change_PDW_BorderThickness.Slider.TickFrequency = 0.1;
            Change_PDW_BorderThickness.Slider.ValueChanged += (s, x) =>
            {
                double BorderThicknessSize = (double)Change_PDW_BorderThickness.Slider.Value;
                OverlayProperties["PDW_BorderThickness"] = BorderThicknessSize;
                AwfulPropertyChanger.PostPDWBorderThickness(BorderThicknessSize);
            };

            AimScroller.Children.Add(Change_PDW_BorderThickness);

            ASlider Change_PDW_Opacity = new(this, "检测窗口不透明度", "不透明度",
                "此设置控制检测到的玩家窗口的不透明度。",
                0.1);

            Change_PDW_Opacity.Slider.Minimum = 0;
            Change_PDW_Opacity.Slider.Maximum = 1;
            Change_PDW_Opacity.Slider.Value = OverlayProperties["PDW_Opacity"];
            Change_PDW_Opacity.Slider.TickFrequency = 0.1;
            Change_PDW_Opacity.Slider.ValueChanged += (s, x) =>
            {
                double WindowOpacity = (double)Change_PDW_Opacity.Slider.Value;
                OverlayProperties["PDW_Opacity"] = WindowOpacity;
                AwfulPropertyChanger.PostPDWOpacity(WindowOpacity);
            };

            AimScroller.Children.Add(Change_PDW_Opacity);

            #endregion Visual Debugging Customizer
        }

        private void LoadTriggerMenu()
        {
            AToggle Enable_TriggerBot = new(this, "开启自动触发",
                "这将使AI能够在看到目标时射击。");
            Enable_TriggerBot.Reader.Name = "TriggerBot";
            SetupToggle(Enable_TriggerBot, state => Bools.Triggerbot = state, Bools.Triggerbot);
            TriggerScroller.Children.Add(Enable_TriggerBot);

            ASlider TriggerBot_Delay = new(this, "自动触发延时", "秒",
                "这个滑块将控制触发所需的秒数。",
                0.1);

            TriggerBot_Delay.Slider.Minimum = 0.01;
            TriggerBot_Delay.Slider.Maximum = 1;
            TriggerBot_Delay.Slider.Value = aimmySettings["Trigger_Delay"];
            TriggerBot_Delay.Slider.TickFrequency = 0.01;
            TriggerBot_Delay.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Trigger_Delay"] = TriggerBot_Delay.Slider.Value;
            };

            TriggerScroller.Children.Add(TriggerBot_Delay);
        }

        private void FileWatcher_Reload(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                LoadModelsIntoListBox();

                // Maybe I broke something removing this, fix it, because it was causing a bug where ListBox stops working when something was added =)
                // nori
                //InitializeModel();
            });
        }

        private void InitializeFileWatcher()
        {
            fileWatcher = new FileSystemWatcher();
            fileWatcher.Path = "bin/models";
            fileWatcher.Filter = "*.onnx";
            fileWatcher.EnableRaisingEvents = true;
            fileWatcher.Created += FileWatcher_Reload;
            fileWatcher.Deleted += FileWatcher_Reload;
            fileWatcher.Renamed += FileWatcher_Reload;
        }

        private bool ModelLoadDebounce = false;

        private void InitializeModel()
        {
            if (!ModelLoadDebounce)
            {
                if (!(Bools.ConstantTracking && Bools.AIAimAligner))
                {
                    ModelLoadDebounce = true;

                    string selectedModel = SelectorListBox.SelectedItem?.ToString();
                    if (selectedModel == null) return;

                    string modelPath = Path.Combine("bin/models", selectedModel);

                    _onnxModel?.Dispose();
                    _onnxModel = new AIModel(modelPath)
                    {
                        ConfidenceThreshold = (float)(aimmySettings["AI_Min_Conf"] / 100.0f),
                        CollectData = toggleState["CollectData"],
                        FovSize = (int)aimmySettings["FOV_Size"]
                    };

                    SelectedModelNotifier.Content = "加载模型: " + selectedModel;
                    lastLoadedModel = selectedModel;

                    ModelLoadDebounce = false;
                }
            }
        }

        private void LoadModelsIntoListBox()
        {
            string[] onnxFiles = Directory.GetFiles("bin/models", "*.onnx");
            SelectorListBox.Items.Clear();

            foreach (string filePath in onnxFiles)
            {
                SelectorListBox.Items.Add(Path.GetFileName(filePath));
            }

            if (SelectorListBox.Items.Count > 0)
            {
                if (!SelectorListBox.Items.Contains(lastLoadedModel) && lastLoadedModel != "N/A")
                {
                    SelectorListBox.SelectedIndex = 0;
                    lastLoadedModel = SelectorListBox.Items[0].ToString();
                }
                else
                {
                    SelectorListBox.SelectedItem = lastLoadedModel;
                }
                SelectedModelNotifier.Content = "加载模型: " + lastLoadedModel;
            }
            ModelLoadDebounce = false;
        }

        private void SelectorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeModel();
        }

        private async Task LoadConfigAsync(string path)
        {
            if (File.Exists(path))
            {
                string json = await File.ReadAllTextAsync(path);

                var config = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
                if (config != null)
                {
                    foreach (var (key, value) in config)
                    {
                        if (aimmySettings.TryGetValue(key, out var currentValue))
                        {
                            aimmySettings[key] = value;
                        }
                        else if (key == "TopMost" && value is bool topMostValue)
                        {
                            toggleState["TopMost"] = topMostValue;
                            this.Topmost = topMostValue;
                        }
                    }
                }
            }
            if (aimmySettings["Suggested_Model"] != string.Empty)
            {
                MessageBox.Show("这个模型的创建者推荐您使用这个模型:" +
                    "\n" +
                    aimmySettings["Suggested_Model"], "推荐模型 - Aimmy");
            }

            // We'll attempt to update the AI Settings but it may not be loaded yet.
            try
            {
                int fovSize = (int)aimmySettings["FOV_Size"];
                FOVOverlay.FovSize = fovSize;
                AwfulPropertyChanger.PostNewFOVSize();

                _onnxModel.FovSize = fovSize;
                _onnxModel.ConfidenceThreshold = (float)(aimmySettings["AI_Min_Conf"] / 100.0f);
            }
            catch { }

            string fileName = Path.GetFileName(path);
            if (ConfigSelectorListBox.Items.Contains(fileName))
            {
                ConfigSelectorListBox.SelectedItem = fileName;
                lastLoadedConfig = ConfigSelectorListBox.SelectedItem.ToString();
                SetSelectedConfig();
            }

            ReloadMenu();
        }

        private async Task LoadOverlayPropertiesAsync(string path)
        {
            if (File.Exists(path))
            {
                string json = await File.ReadAllTextAsync(path);

                var config = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(json);
                if (config != null)
                {
                    foreach (var (key, value) in config)
                    {
                        if (OverlayProperties.TryGetValue(key, out var currentValue))
                        {
                            OverlayProperties[key] = value;
                        }
                    }
                }
            }

            try
            {
                AwfulPropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(OverlayProperties["FOV_Color"]));
                AwfulPropertyChanger.PostPDWSize((int)OverlayProperties["PDW_Size"]);
                AwfulPropertyChanger.PostPDWCornerRadius((int)OverlayProperties["PDW_CornerRadius"]);
                AwfulPropertyChanger.PostPDWBorderThickness((int)OverlayProperties["PDW_BorderThickness"]);
                AwfulPropertyChanger.PostPDWOpacity((Double)OverlayProperties["PDW_Opacity"]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

            //ReloadMenu();
        }

        private void ReloadMenu()
        {
            AimScroller.Children.Clear();
            TriggerScroller.Children.Clear();
            SettingsScroller.Children.Clear();

            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
        }

        private void ConfigWatcher_Reload(object sender, FileSystemEventArgs e)
        {
            this.Dispatcher.Invoke(LoadConfigsIntoListBox);
        }

        private void InitializeConfigWatcher()
        {
            ConfigfileWatcher = new FileSystemWatcher();
            ConfigfileWatcher.Path = "bin/configs";
            ConfigfileWatcher.Filters.Add("*.json");
            ConfigfileWatcher.Filters.Add("*.cfg");
            ConfigfileWatcher.EnableRaisingEvents = true;
            ConfigfileWatcher.Created += ConfigWatcher_Reload;
            ConfigfileWatcher.Deleted += ConfigWatcher_Reload;
            ConfigfileWatcher.Renamed += ConfigWatcher_Reload;
        }

        private void LoadConfigsIntoListBox()
        {
            ConfigSelectorListBox.Items.Clear();

            foreach (string filePath in Directory.GetFiles("bin/configs"))
            {
                string fileName = Path.GetFileName(filePath);
                ConfigSelectorListBox.Items.Add(fileName);
            }

            //SetSelectedConfig();
        }

        private void SetSelectedConfig()
        {
            if (ConfigSelectorListBox.Items.Count > 0)
            {
                if (!ConfigSelectorListBox.Items.Contains(lastLoadedConfig) && lastLoadedConfig != "N/A")
                {
                    ConfigSelectorListBox.SelectedIndex = 0;
                    lastLoadedConfig = ConfigSelectorListBox.Items[0].ToString();
                }
                else
                {
                    ConfigSelectorListBox.SelectedItem = lastLoadedConfig;
                }
                SelectedConfigNotifier.Content = "加载配置: " + lastLoadedConfig;
            }
        }

        private async void ConfigSelectorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigSelectorListBox.SelectedItem != null)
            {
                await LoadConfigAsync($"bin/configs/{ConfigSelectorListBox.SelectedItem.ToString()}");
            }
        }

        private void LoadStoreMenu()
        {
            DownloadGateway(ModelStoreScroller, AvailableModels, "models");
            DownloadGateway(ConfigStoreScroller, AvailableConfigs, "configs");
        }

        private void DownloadGateway(StackPanel Scroller, HashSet<string> entries, string folder)
        {
            if (entries.Count > 0)
            {
                foreach (var entry in entries)
                    Scroller.Children.Add(new ADownloadGateway(entry, folder));
            }
            else
            {
                Scroller.Children.Clear();
                LackOfConfigsText.Visibility = Visibility.Visible;
            }
        }

        private void LoadSettingsMenu()
        {
            SettingsScroller.Children.Add(new AInfoSection());

            AToggle CollectDataWhilePlaying = new(this, "玩游戏时收集数据",
                "这将使AI能够在按下触发键时拍摄你的屏幕。");
            CollectDataWhilePlaying.Reader.Name = "CollectData";
            SetupToggle(CollectDataWhilePlaying, state => Bools.CollectDataWhilePlaying = state, Bools.CollectDataWhilePlaying);
            SettingsScroller.Children.Add(CollectDataWhilePlaying);

            ASlider AIMinimumConfidence = new(this, "AI最小置信度", "% 置信度",
                "这一设置控制着AI在做出瞄准决定前的自信程度。",
                1);

            AIMinimumConfidence.Slider.Minimum = 1;
            AIMinimumConfidence.Slider.Maximum = 100;
            AIMinimumConfidence.Slider.Value = aimmySettings["AI_Min_Conf"];
            AIMinimumConfidence.Slider.TickFrequency = 1;
            AIMinimumConfidence.Slider.ValueChanged += (s, x) =>
            {
                if (lastLoadedModel != "N/A")
                {
                    double ConfVal = ((double)AIMinimumConfidence.Slider.Value);
                    aimmySettings["AI_Min_Conf"] = ConfVal;
                    _onnxModel.ConfidenceThreshold = (float)(ConfVal / 100.0f);
                }
                else
                {
                    // Prevent double messageboxes..
                    if (AIMinimumConfidence.Slider.Value != aimmySettings["AI_Min_Conf"])
                    {
                        MessageBox.Show("无法设置置信度，请选择一个模型再试一次。", "滑块的错误");
                        AIMinimumConfidence.Slider.Value = aimmySettings["AI_Min_Conf"];
                    }
                }
            };

            SettingsScroller.Children.Add(AIMinimumConfidence);

            bool topMostInitialState = toggleState.ContainsKey("TopMost") ? toggleState["TopMost"] : false;

            AToggle TopMost = new(this, "用户界面最顶端",
                "这将切换UI的顶部，这意味着它可以隐藏在其他窗口后面，而不是始终在顶部。");
            TopMost.Reader.Name = "TopMost";
            SetupToggle(TopMost, state => Bools.TopMost = state, topMostInitialState);

            SettingsScroller.Children.Add(TopMost);

            AButton SaveConfigSystem = new(this, "保存当前配置",
   "这将保存当前配置以便发布。");

            SaveConfigSystem.Reader.Click += (s, e) =>
            {
                new SecondaryWindows.ConfigSaver(aimmySettings, lastLoadedModel).ShowDialog();
            };

            SettingsScroller.Children.Add(SaveConfigSystem);
        }

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private static bool SavedData = false;

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Prevent saving overwrite
            if (SavedData) return;

            // Save to Default Config
            try
            {
                var extendedSettings = new Dictionary<string, object>();
                foreach (var kvp in aimmySettings)
                {
                    extendedSettings[kvp.Key] = kvp.Value;
                }

                // Add topmost
                extendedSettings["TopMost"] = this.Topmost ? true : false;

                string json = JsonConvert.SerializeObject(extendedSettings, Formatting.Indented);
                File.WriteAllText("bin/configs/Default.cfg", json);
            }
            catch (Exception x)
            {
                Console.WriteLine("保存配置时出错: " + x.Message);
            }

            // Save Overlay Properties Data
            // Nori
            try
            {
                var OverlaySettings = new Dictionary<string, object>();
                foreach (var kvp in OverlayProperties)
                {
                    OverlaySettings[kvp.Key] = kvp.Value;
                }

                string json = JsonConvert.SerializeObject(OverlaySettings, Formatting.Indented);
                File.WriteAllText("bin/Overlay.cfg", json);
            }
            catch (Exception x)
            {
                Console.WriteLine("保存配置时出错: " + x.Message);
            }

            SavedData = true;

            // Unhook keybind hooker
            bindingManager.StopListening();
            FOVOverlay.Close();
            DetectedPlayerOverlay.Close();

            // Dispose (best practice)
            fileWatcher.Dispose();
            ConfigfileWatcher.Dispose();
            cts.Dispose();

            // Close
            Application.Current.Shutdown();
        }

        #endregion Window Controls
    }
}