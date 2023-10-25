using AimmyAimbot.Class;
using AimmyAimbot.UserController;
using AimmyWPF;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace AimmyAimbot
{
    public partial class MainWindow : Window
    {
        private OverlayWindow FOVOverlay;
        private FileSystemWatcher fileWatcher;
        private string lastLoadedModel;

        private readonly BrushConverter brushcolor = new BrushConverter();
        private System.Windows.Forms.Panel fovPanel;

        private int TimeSinceLastClick = 0;
        private DateTime LastClickTime = DateTime.MinValue;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_MOVE = 0x0001; // Movement flag

        private static int ScreenWidth = Screen.PrimaryScreen.Bounds.Width;
        private static int ScreenHeight = Screen.PrimaryScreen.Bounds.Height;

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

        private Dictionary<string, double> aimmySettings = new Dictionary<string, double>
        {
            { "FOV_Size", 640 },
            { "Mouse_Sens", 0.4 },
            { "Y_Offset", 50 },
            { "X_Offset", 0 },
            { "Trigger_Delay", 0.1 },
            { "AI_Min_Conf", 60 }
        };


        private Dictionary<string, bool> toggleState = new Dictionary<string, bool>
        {
            { "AimbotToggle", false },
            { "AimViewToggle", false },
            { "TriggerBot", false },
            { "CollectData", false }
        };


        Thickness WinTooLeft = new Thickness(-1680, 0, 1680, 0);
        Thickness WinVeryLeft = new Thickness(-1120, 0, 1120, 0);
        Thickness WinLeft = new Thickness(-560, 0, 560, 0);

        Thickness WinCenter = new Thickness(0, 0, 0, 0);

        Thickness WinRight = new Thickness(560, 0, -560, 0);
        Thickness WinVeryRight = new Thickness(1120, 0, -1120, 0);
        Thickness WinTooRight = new Thickness(1680, 0, -1680, 0);

        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        public MainWindow()
        {
            InitializeComponent();

            // Setup key/mouse hook
            bindingManager = new InputBindingManager();
            bindingManager.SetupDefault("Right");
            bindingManager.OnBindingPressed += (binding) => { IsHolding_Binding = true; };
            bindingManager.OnBindingReleased += (binding) => { IsHolding_Binding = false; };

            // Load settings
            if (!string.IsNullOrEmpty(AimmyWPF.Properties.Settings.Default.AppData))
            {
                foreach (var pair in AimmyWPF.Properties.Settings.Default.AppData.Split(';'))
                {
                    var parts = pair.Split('=');
                    if (aimmySettings.ContainsKey(parts[0]) && double.TryParse(parts[1], out double value))
                    {
                        aimmySettings[parts[0]] = value;
                    }
                }
            }

            // Load UI
            InitializeMenuPositions();
            LoadAimMenu();
            LoadTriggerMenu();
            LoadSettingsMenu();
            InitializeFileWatcher();

            // Load all models into listbox
            LoadModelsIntoListBox();
            InitializeModel();
            SelectorListBox.SelectionChanged += new SelectionChangedEventHandler(SelectorListBox_SelectionChanged);

            FOVOverlay = new OverlayWindow();
            FOVOverlay.Hide();
            FOVOverlay.FovSize = (int)aimmySettings["FOV_Size"];

            // Start the loop that runs the model
            Task.Run(() => StartModelCaptureLoop());
        }

        #region Mouse Movement / Clicking Handler

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private static double Lerp(double start, double end, double alpha)
        {
            return start + alpha * (end - start);
        }

        private async Task DoTriggerClick()
        {
            TimeSinceLastClick = (int)(DateTime.Now - LastClickTime).TotalMilliseconds;
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

            int moveX = (int)Lerp(0, detectedX - halfScreenWidth, Alpha);
            int moveY = (int)Lerp(0, detectedY - halfScreenHeight, Alpha);

            mouse_event(MOUSEEVENTF_MOVE, (uint)moveX, (uint)moveY, 0, 0);

            if (toggleState["TriggerBot"])
            {
                Task.Run(() => DoTriggerClick());
            }
        }

        #endregion

        #region Aim Aligner Main and Loop
        public async Task ModelCapture(bool TriggerOnly = false)
        {
            var closestPrediction = await _onnxModel.GetClosestPredictionToCenterAsync();
            if (closestPrediction == null)
            {
                return;
            }

            if (!TriggerOnly)
            {
                // Scale the coordinates from the 640x640 image to the actual screen size
                float scaleX = (float)ScreenWidth / 640f;
                float scaleY = (float)ScreenHeight / 640f;

                double YOffset = aimmySettings["Y_Offset"];
                double XOffset = aimmySettings["X_Offset"];
                int detectedX = (int)((closestPrediction.Rectangle.X * scaleX) + XOffset);
                int detectedY = (int)((closestPrediction.Rectangle.Y * scaleY) + YOffset);

                MoveCrosshair(detectedX, detectedY);
            }
            else 
            {
                Task.Run(() => DoTriggerClick());
            }
        }

        private async Task StartModelCaptureLoop()
        {
            // Create a new CancellationTokenSource
            cts = new CancellationTokenSource();

            while (!cts.Token.IsCancellationRequested)
            {
                if (toggleState["AimbotToggle"] && IsHolding_Binding)
                {
                    await ModelCapture();
                }
                else if (!toggleState["AimbotToggle"] && toggleState["TriggerBot"] && IsHolding_Binding) // Triggerbot Only
                {
                    await ModelCapture(true);
                }

                await Task.Delay(1);
            }
        }

        public void StopModelCaptureLoop()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts = null;
            }
        }
        #endregion

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

            toggle.Reader.Click += (s, x) =>
            {
                bool currentState = (bool)toggle.Reader.Tag;
                toggle.Reader.Tag = !currentState;
                SetToggleState(toggle);
                action.Invoke(!currentState);
            };
        }

        private void SetToggleState(AToggle toggle)
        {
            bool state = (bool)toggle.Reader.Tag;

            if (state)
            {
                toggle.EnableSwitch();
            }
            else
            {
                toggle.DisableSwitch();
            }

            toggleState[toggle.Reader.Name] = state;

            // Handle sending to AIModel
            if (toggle.Reader.Name == "CollectData")
            {
                if (state)
                {
                    _onnxModel.CollectData = true;
                }
            }
            else if (toggle.Reader.Name == "ShowFOV")
            {
                if (state)
                {
                    FOVOverlay.Show();
                }
                else 
                {
                    FOVOverlay.Hide();
                }
            }
        }

        #endregion

        #region Menu Controls

        private async void Selection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button clickedButton)
            {
                MenuPosition position = (MenuPosition)Enum.Parse(typeof(MenuPosition), clickedButton.Tag.ToString());
                ResetMenuColors();
                clickedButton.Foreground = (System.Windows.Media.Brush)brushcolor.ConvertFromString("#3e8fb0");
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
            Thickness highlighterMargin = new Thickness(0, 30, 414, 0);
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
        #endregion


        void LoadAimMenu()
        {
            AToggle Enable_AIAimAligner = new AToggle("Enable AI Aim Aligner");
            Enable_AIAimAligner.Reader.Name = "AimbotToggle";
            SetupToggle(Enable_AIAimAligner, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Enable_AIAimAligner);

            /*AToggle ThirdPersonAim = new AToggle("Enable Third Person Aim");
            ThirdPersonAim.Reader.Name = "AimViewToggle";
            SetupToggle(ThirdPersonAim, state => Bools.ThirdPersonAim = state, Bools.ThirdPersonAim);
            AimScroller.Children.Add(ThirdPersonAim);*/

            AKeyChanger Change_KeyPress = new AKeyChanger("Change Keybind", "Right");
            Change_KeyPress.Reader.Click += (s, x) =>
            {
                Change_KeyPress.KeyNotifier.Content = "Listening..";
                bindingManager.StartListeningForBinding();
            };

            bindingManager.OnBindingSet += (binding) =>
            {
                Change_KeyPress.KeyNotifier.Content = binding;
            };

            AimScroller.Children.Add(Change_KeyPress);

            AToggle Show_FOV = new AToggle("Show FOV");
            Show_FOV.Reader.Name = "ShowFOV";
            SetupToggle(Show_FOV, state => Bools.AIAimAligner = state, Bools.AIAimAligner);
            AimScroller.Children.Add(Show_FOV);

            ASlider FovSlider = new ASlider("FOV Size", "Size of FOV");

            FovSlider.Slider.Minimum = 10;
            FovSlider.Slider.Maximum = 640;
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

                // Update the overlay's FOV size
                FOVOverlay.FovSize = (int)FovSize;
            };

            AimScroller.Children.Add(FovSlider);

            ASlider MouseSensitivty = new ASlider("Mouse Sensitivty", "Sensitivty");

            MouseSensitivty.Slider.Minimum = 0.01;
            MouseSensitivty.Slider.Maximum = 1;
            MouseSensitivty.Slider.Value = aimmySettings["Mouse_Sens"];
            MouseSensitivty.Slider.TickFrequency = 0.01;
            MouseSensitivty.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Mouse_Sens"] = MouseSensitivty.Slider.Value;
            };

            AimScroller.Children.Add(MouseSensitivty);

            ASlider YOffset = new ASlider("Y Offset (Down/Up)", "Offset");

            YOffset.Slider.Minimum = -50;
            YOffset.Slider.Maximum = 50;
            YOffset.Slider.Value = aimmySettings["Y_Offset"];
            YOffset.Slider.TickFrequency = 1;
            YOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["Y_Offset"] = YOffset.Slider.Value;
            };

            AimScroller.Children.Add(YOffset);

            ASlider XOffset = new ASlider("X Offset (Left/Right)", "Offset");

            XOffset.Slider.Minimum = -50;
            XOffset.Slider.Maximum = 50;
            XOffset.Slider.Value = aimmySettings["X_Offset"];
            XOffset.Slider.TickFrequency = 1;
            XOffset.Slider.ValueChanged += (s, x) =>
            {
                aimmySettings["X_Offset"] = XOffset.Slider.Value;
            };

            AimScroller.Children.Add(XOffset);
        }

        void LoadTriggerMenu()
        {
            AToggle Enable_TriggerBot = new AToggle("Enable Auto Trigger");
            Enable_TriggerBot.Reader.Name = "TriggerBot";
            SetupToggle(Enable_TriggerBot, state => Bools.Triggerbot = state, Bools.Triggerbot);
            TriggerScroller.Children.Add(Enable_TriggerBot);

            ASlider TriggerBot_Delay = new ASlider("Auto Trigger Delay", "Seconds");

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
            this.Dispatcher.Invoke(() =>
            {
                LoadModelsIntoListBox();
                InitializeModel();
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

        private void InitializeModel()
        {
            if (SelectorListBox.SelectedItem != null)
            {
                string modelFileName = SelectorListBox.SelectedItem.ToString();
                string modelPath = Path.Combine("bin/models", modelFileName);

                _onnxModel?.Dispose();

                _onnxModel = new AIModel(modelPath);
                _onnxModel.ConfidenceThreshold = (float)(aimmySettings["AI_Min_Conf"] / 100.0f);
                _onnxModel.CollectData = toggleState["CollectData"];
                _onnxModel.FovSize = (int)aimmySettings["FOV_Size"];
                SelectedModelNotifier.Content = "Loaded Model: " + modelFileName;
                lastLoadedModel = modelFileName;
            }
        }

        private void LoadModelsIntoListBox()
        {
            string[] onnxFiles = Directory.GetFiles("bin/models", "*.onnx");
            SelectorListBox.Items.Clear();

            foreach (string filePath in onnxFiles)
            {
                string fileName = Path.GetFileName(filePath);
                SelectorListBox.Items.Add(fileName);
            }

            // Preselect the first file in the ListBox
            if (SelectorListBox.Items.Count > 0)
            {
                if (string.IsNullOrEmpty(lastLoadedModel) || !SelectorListBox.Items.Contains(lastLoadedModel))
                {
                    SelectorListBox.SelectedIndex = 0;
                    lastLoadedModel = SelectorListBox.Items[0].ToString();
                }
                else
                {
                    SelectorListBox.SelectedItem = lastLoadedModel;
                }
                SelectedModelNotifier.Content = "Loaded Model: " + lastLoadedModel;
            }
            else
            {
                System.Windows.MessageBox.Show("No models found, please put a .onnx model in bin/models.");
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void SelectorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            InitializeModel();
        }

        void LoadSettingsMenu()
        {
            AToggle CollectDataWhilePlaying = new AToggle("Collect Data While Playing");
            CollectDataWhilePlaying.Reader.Name = "CollectData";
            SetupToggle(CollectDataWhilePlaying, state => Bools.CollectDataWhilePlaying = state, Bools.CollectDataWhilePlaying);
            SettingsScroller.Children.Add(CollectDataWhilePlaying);

            ASlider AIMinimumConfidence = new ASlider("AI Minimum Confidence", "% Confidence");

            AIMinimumConfidence.Slider.Minimum = 1;
            AIMinimumConfidence.Slider.Maximum = 100;
            AIMinimumConfidence.Slider.Value = aimmySettings["AI_Min_Conf"];
            AIMinimumConfidence.Slider.TickFrequency = 1;
            AIMinimumConfidence.Slider.ValueChanged += (s, x) =>
            {
                double ConfVal = ((double)AIMinimumConfidence.Slider.Value);
                aimmySettings["AI_Min_Conf"] = ConfVal;
                _onnxModel.ConfidenceThreshold = (float)(ConfVal / 100.0f);
            };

            SettingsScroller.Children.Add(AIMinimumConfidence);

            /*AButton ClearSettings = new AButton("Clear Settings");

            ClearSettings.Reader.Click += (s, x) =>
            {
                // Insert Button Functionality Here
            };

            SettingsScroller.Children.Add(ClearSettings);*/
        }

        #region Window Controls
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Unhook keybind/mousehook
            bindingManager.StopListening();

            string serializedData = string.Join(";", aimmySettings.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            AimmyWPF.Properties.Settings.Default.AppData = serializedData;
            AimmyWPF.Properties.Settings.Default.Save();

            FOVOverlay.Close();
            System.Windows.Application.Current.Shutdown();
        }
        #endregion
    }
}
