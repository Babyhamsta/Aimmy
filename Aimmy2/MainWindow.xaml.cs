using Aimmy2.Class;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Other;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
using Class;
using InputLogic;
using Microsoft.Win32;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UILibrary;
using Visuality;
using static Aimmy2.Other.GithubManager;
using Application = System.Windows.Application;

namespace Aimmy2
{
    public partial class MainWindow : Window
    {
        #region Main Variables

        private InputBindingManager bindingManager;
        private FileManager fileManager;
        private static FOV FOVWindow = new();
        private static DetectedPlayerWindow DPWindow = new();
        private static GithubManager githubManager = new();
        public UI uiManager = new();
        public AntiRecoilManager arManager = new();

        private Dictionary<string, AToggle> toggleInstances = [];

        private bool CurrentlySwitching;
        private ScrollViewer? CurrentScrollViewer;

        private static double ActualFOV = 640;

        #endregion Main Variables

        #region Loading Window

        public MainWindow()
        {
            InitializeComponent();

            if (Directory.GetCurrentDirectory().Contains("Temp")) MessageBox.Show("Hi, it is made aware that you are running Aimmy without extracting it from the zip file. Please extract Aimmy from the zip file or Aimmy will not be able to run properly.\n\nThank you.", "Aimmy V2");

            CurrentScrollViewer = FindName("AimMenu") as ScrollViewer;
            if (CurrentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

            Dictionary.DetectedPlayerOverlay = DPWindow;
            Dictionary.FOVWindow = FOVWindow;

            fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);
            //fileManager.RetrieveAndAddFiles();

            // Needed to import annotations into MakeSense
            if (!File.Exists("bin\\labels\\labels.txt")) { File.WriteAllText("bin\\labels\\labels.txt", "Enemy"); }

            arManager.HoldDownLoad();

            LoadConfig();
            LoadAntiRecoilConfig();

            SaveDictionary.LoadJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
            SaveDictionary.LoadJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
            SaveDictionary.LoadJSON(Dictionary.colorState, "bin\\colors.cfg");
            SaveDictionary.LoadJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
            SaveDictionary.LoadJSON(Dictionary.repoList, "bin\\repoList.cfg", false);

            bindingManager = new InputBindingManager();
            bindingManager.SetupDefault("Aim Keybind", Dictionary.bindingSettings["Aim Keybind"]);
            bindingManager.SetupDefault("Second Aim Keybind", Dictionary.bindingSettings["Second Aim Keybind"]);
            bindingManager.SetupDefault("Dynamic FOV Keybind", Dictionary.bindingSettings["Dynamic FOV Keybind"]);
            bindingManager.SetupDefault("Emergency Stop Keybind", Dictionary.bindingSettings["Emergency Stop Keybind"]);
            bindingManager.SetupDefault("Model Switch Keybind", Dictionary.bindingSettings["Model Switch Keybind"]);

            bindingManager.SetupDefault("Anti Recoil Keybind", Dictionary.bindingSettings["Anti Recoil Keybind"]);
            bindingManager.SetupDefault("Disable Anti Recoil Keybind", Dictionary.bindingSettings["Disable Anti Recoil Keybind"]);
            bindingManager.SetupDefault("Gun 1 Key", Dictionary.bindingSettings["Gun 1 Key"]);
            bindingManager.SetupDefault("Gun 2 Key", Dictionary.bindingSettings["Gun 2 Key"]);

            LoadAimMenu();
            LoadSettingsMenu();
            LoadCreditsMenu();
            LoadStoreMenuAsync();

            SaveDictionary.LoadJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
            LoadDropdownStates();

            PropertyChanger.ReceiveNewConfig = LoadConfig;

            ActualFOV = Dictionary.sliderSettings["FOV Size"];
            PropertyChanger.PostNewFOVSize(Dictionary.sliderSettings["FOV Size"]);
            PropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["FOV Color"]));

            PropertyChanger.PostDPColor((Color)ColorConverter.ConvertFromString(Dictionary.colorState["Detected Player Color"]));
            PropertyChanger.PostDPFontSize((int)Dictionary.sliderSettings["AI Confidence Font Size"]);
            PropertyChanger.PostDPWCornerRadius((int)Dictionary.sliderSettings["Corner Radius"]);
            PropertyChanger.PostDPWBorderThickness((double)Dictionary.sliderSettings["Border Thickness"]);
            PropertyChanger.PostDPWOpacity((double)Dictionary.sliderSettings["Opacity"]);

            ListenForKeybinds();
            LoadMenuMinimizers();
        }

        private async void LoadStoreMenuAsync()
        {
            await LoadStoreMenu();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => AboutSpecs.Content = $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            fileManager.InQuittingState = true;

            Dictionary.toggleState["Aim Assist"] = false;
            Dictionary.toggleState["FOV"] = false;
            Dictionary.toggleState["Show Detected Player"] = false;

            FOVWindow.Close();
            DPWindow.Close();

            if (Dictionary.dropdownState["Mouse Movement Method"] == "LG HUB")
            {
                LGMouse.Close();
            }

            SaveDictionary.WriteJSON(Dictionary.sliderSettings);
            SaveDictionary.WriteJSON(Dictionary.minimizeState, "bin\\minimize.cfg");
            SaveDictionary.WriteJSON(Dictionary.bindingSettings, "bin\\binding.cfg");
            SaveDictionary.WriteJSON(Dictionary.dropdownState, "bin\\dropdown.cfg");
            SaveDictionary.WriteJSON(Dictionary.colorState, "bin\\colors.cfg");
            SaveDictionary.WriteJSON(Dictionary.filelocationState, "bin\\filelocations.cfg");
            SaveDictionary.WriteJSON(Dictionary.AntiRecoilSettings, "bin\\anti_recoil_configs\\Default.cfg");
            SaveDictionary.WriteJSON(Dictionary.repoList, "bin\\repoList.cfg");

            FileManager.AIManager?.Dispose();

            Application.Current.Shutdown();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion Loading Window

        #region Menu Logic

        private string CurrentMenu = "AimMenu";

        private async void MenuSwitch(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton && !CurrentlySwitching && CurrentMenu != clickedButton.Tag.ToString())
            {
                CurrentlySwitching = true;
                Animator.ObjectShift(TimeSpan.FromMilliseconds(350), MenuHighlighter, MenuHighlighter.Margin, clickedButton.Margin);
                await SwitchScrollPanels(FindName(clickedButton.Tag.ToString()) as ScrollViewer ?? throw new NullReferenceException("Scrollpanel is null"));
                CurrentMenu = clickedButton.Tag.ToString()!;
            }
        }

        private async Task SwitchScrollPanels(ScrollViewer MovingScrollViewer)
        {
            MovingScrollViewer.Visibility = Visibility.Visible;
            Animator.Fade(MovingScrollViewer);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), MovingScrollViewer, MovingScrollViewer.Margin, new Thickness(50, 50, 0, 0));

            Animator.FadeOut(CurrentScrollViewer!);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), CurrentScrollViewer!, CurrentScrollViewer!.Margin, new Thickness(50, 450, 0, -400));
            await Task.Delay(350);

            CurrentScrollViewer.Visibility = Visibility.Collapsed;
            CurrentScrollViewer = MovingScrollViewer;
            CurrentlySwitching = false;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateVisibilityBasedOnSearchText((TextBox)sender, ModelStoreScroller);
        }

        private void CSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateVisibilityBasedOnSearchText((TextBox)sender, ConfigStoreScroller);
        }

        private void UpdateVisibilityBasedOnSearchText(TextBox textBox, Panel panel)
        {
            string searchText = textBox.Text.ToLower();

            foreach (ADownloadGateway item in panel.Children.OfType<ADownloadGateway>())
            {
                item.Visibility = item.Title.Content.ToString()?.ToLower().Contains(searchText) == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var mousePosition = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(mousePosition.X, mousePosition.Y));

                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = CalculateAngleDifference(targetAngle, 360, 180, 1);
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        private void LoadDropdownStates()
        {
            // Prediction Method Dropdown
            uiManager.D_PredictionMethod!.DropdownBox.SelectedIndex = Dictionary.dropdownState["Prediction Method"] switch
            {
                "Shall0e's Prediction" => 1,
                "wisethef0x's EMA Prediction" => 2,
                _ => 0 // Default case if none of the above matches
            };

            // Detection Area Type Dropdown
            uiManager.D_DetectionAreaType!.DropdownBox.SelectedIndex = Dictionary.dropdownState["Detection Area Type"] switch
            {
                "Closest to Mouse" => 1,
                // Add more cases as needed
                _ => 0 // Default case
            };

            // Aiming Boundaries Alignment Dropdown
            uiManager.D_AimingBoundariesAlignment!.DropdownBox.SelectedIndex = Dictionary.dropdownState["Aiming Boundaries Alignment"] switch
            {
                "Top" => 1,
                "Bottom" => 2,
                _ => 0 // Default case if none of the above matches
            };

            // Mouse Movement Method Dropdown
            uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = Dictionary.dropdownState["Mouse Movement Method"] switch
            {
                "SendInput" => 1,
                "LG HUB" => 2,
                "Razer Synapse (Require Razer Peripheral)" => 3,
                "ddxoft Virtual Input Driver" => 4,
                _ => 0 // Default case if none of the above matches
            };
        }

        private AToggle AddToggle(StackPanel panel, string title)
        {
            var toggle = new AToggle(title);
            toggleInstances[title] = toggle;

            // Load Toggle State
            (Dictionary.toggleState[title] ? (Action)(() => toggle.EnableSwitch()) : () => toggle.DisableSwitch())();

            toggle.Reader.Click += (sender, e) =>
            {
                Dictionary.toggleState[title] = !Dictionary.toggleState[title];

                UpdateToggleUI(toggle, Dictionary.toggleState[title]);

                Toggle_Action(title);
            };

            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(toggle));
            return toggle;
        }

        private void UpdateToggleUI(AToggle toggle, bool isEnabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isEnabled)
                {
                    toggle.EnableSwitch();
                }
                else
                {
                    toggle.DisableSwitch();
                }
            });
        }

        private void Toggle_Action(string title)
        {
            switch (title)
            {
                case "FOV":
                    FOVWindow.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Hidden;
                    break;

                case "Show Detected Player":
                    ShowHideDPWindow();
                    DPWindow.DetectedPlayerFocus.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case "Show AI Confidence":
                    DPWindow.DetectedPlayerConfidence.Visibility = Dictionary.toggleState[title] ? Visibility.Visible : Visibility.Collapsed;
                    break;

                case "Mouse Background Effect":
                    if (!Dictionary.toggleState[title]) { RotaryGradient.Angle = 0; }
                    break;

                case "UI TopMost":
                    Topmost = Dictionary.toggleState[title];
                    break;
                case "EMA Smoothening":
                    MouseManager.IsEMASmoothingEnabled = Dictionary.toggleState[title];
                    Debug.WriteLine(MouseManager.IsEMASmoothingEnabled);
                    break;
            }
        }

        private AKeyChanger AddKeyChanger(StackPanel panel, string title, string keybind)
        {
            var keyChanger = new AKeyChanger(title, keybind);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(keyChanger));

            keyChanger.Reader.Click += (sender, e) =>
            {
                keyChanger.KeyNotifier.Content = "...";
                bindingManager.StartListeningForBinding(title);

                // Event handler for setting the binding
                Action<string, string>? bindingSetHandler = null;
                bindingSetHandler = (bindingId, key) =>
                {
                    if (bindingId == title)
                    {
                        keyChanger.KeyNotifier.Content = key;
                        Dictionary.bindingSettings[bindingId] = key;
                        bindingManager.OnBindingSet -= bindingSetHandler; // Unsubscribe after setting

                        keyChanger.KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(key);
                    }
                };

                bindingManager.OnBindingSet += bindingSetHandler;
            };

            return keyChanger;
        }

        // All Keybind Listening is moved to a seperate function because having it stored in "AddKeyChanger" was making these functions run several times.
        // Nori
        private void ListenForKeybinds()
        {
            bindingManager.OnBindingPressed += (bindingId) =>
            {
                switch (bindingId)
                {
                    case "Model Switch Keybind":
                        if (Dictionary.toggleState["Enable Model Switch Keybind"])
                        {
                            if (!FileManager.CurrentlyLoadingModel)
                            {
                                if (ModelListBox.SelectedIndex >= 0 && ModelListBox.SelectedIndex < (ModelListBox.Items.Count - 1))
                                {
                                    ModelListBox.SelectedIndex += 1;
                                }
                                else
                                {
                                    ModelListBox.SelectedIndex = 0;
                                }
                            }
                        }
                        break;

                    case "Dynamic FOV Keybind":
                        if (Dictionary.toggleState["Dynamic FOV"])
                        {
                            Dictionary.sliderSettings["FOV Size"] = Dictionary.sliderSettings["Dynamic FOV Size"];
                            Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle, FOVWindow.Circle.ActualWidth, Dictionary.sliderSettings["Dynamic FOV Size"]);
                            Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle, FOVWindow.Circle.ActualHeight, Dictionary.sliderSettings["Dynamic FOV Size"]);
                        }
                        break;

                    case "Emergency Stop Keybind":
                        // Disable Aim Assist
                        Dictionary.toggleState["Aim Assist"] = false;
                        Dictionary.toggleState["Constant AI Tracking"] = false;
                        Dictionary.toggleState["Auto Trigger"] = false;

                        UpdateToggleUI(uiManager.T_AimAligner!, false);
                        UpdateToggleUI(uiManager.T_ConstantAITracking!, false);
                        UpdateToggleUI(uiManager.T_AutoTrigger!, false);
                        new NoticeBar($"[Emergency Stop Keybind] Disabled all AI features.", 4000).Show();
                        break;
                    // Anti Recoil
                    case "Anti Recoil Keybind":
                        if (Dictionary.toggleState["Anti Recoil"])
                        {
                            arManager.IndependentMousePress = 0;
                            arManager.HoldDownTimer.Start();
                        }
                        break;

                    case "Disable Anti Recoil Keybind":
                        if (Dictionary.toggleState["Anti Recoil"])
                        {
                            Dictionary.toggleState["Anti Recoil"] = false;
                            UpdateToggleUI(uiManager.T_AntiRecoil!, false);
                            new NoticeBar($"[Disable Anti Recoil Keybind] Disabled Anti-Recoil.", 4000).Show();
                        }
                        break;

                    case "Gun 1 Key":
                        if (Dictionary.toggleState["Enable Gun Switching Keybind"])
                        {
                            LoadAntiRecoilConfig(Dictionary.filelocationState["Gun 1 Config"], true);
                        }
                        break;

                    case "Gun 2 Key":
                        if (Dictionary.toggleState["Enable Gun Switching Keybind"])
                        {
                            LoadAntiRecoilConfig(Dictionary.filelocationState["Gun 2 Config"], true);
                        }
                        break;
                }
            };

            bindingManager.OnBindingReleased += (bindingId) =>
            {
                switch (bindingId)
                {
                    case "Dynamic FOV Keybind":
                        if (Dictionary.toggleState["Dynamic FOV"])
                        {
                            Dictionary.sliderSettings["FOV Size"] = ActualFOV;
                            Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle, FOVWindow.Circle.ActualWidth, ActualFOV);
                            Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle, FOVWindow.Circle.ActualHeight, ActualFOV);
                        }
                        break;
                    // Anti Recoil
                    case "Anti Recoil Keybind":
                        if (Dictionary.toggleState["Anti Recoil"])
                        {
                            arManager.HoldDownTimer.Stop();
                            arManager.IndependentMousePress = 0;
                        }
                        break;
                }
            };
        }

        private AColorChanger AddColorChanger(StackPanel panel, string title)
        {
            var colorChanger = new AColorChanger(title);
            colorChanger.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState[title]);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(colorChanger));
            return colorChanger;
        }

        private ASlider AddSlider(StackPanel panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool For_Anti_Recoil = false)
        {
            var slider = new ASlider(title, label, buttonsteps)
            {
                Slider = { Minimum = min, Maximum = max, TickFrequency = frequency }
            };

            // Determine the correct settings dictionary based on the slider type
            var settings = For_Anti_Recoil ? Dictionary.AntiRecoilSettings : Dictionary.sliderSettings;
            slider.Slider.Value = settings.TryGetValue(title, out var value) ? value : min;

            // Update the settings when the slider value changes
            slider.Slider.ValueChanged += (s, e) => settings[title] = slider.Slider.Value;

            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(slider));
            return slider;
        }

        private ADropdown AddDropdown(StackPanel panel, string title)
        {
            var dropdown = new ADropdown(title, title);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(dropdown));
            return dropdown;
        }

        private AFileLocator AddFileLocator(StackPanel panel, string title, string filter = "All files (*.*)|*.*", string DLExtension = "")
        {
            var afilelocator = new AFileLocator(title, title, filter, DLExtension);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(afilelocator));
            return afilelocator;
        }

        private ComboBoxItem AddDropdownItem(ADropdown dropdown, string title)
        {
            var dropdownitem = new ComboBoxItem();
            dropdownitem.Content = title;
            dropdownitem.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            dropdownitem.FontFamily = TryFindResource("Atkinson Hyperlegible") as FontFamily;

            dropdownitem.Selected += (s, e) =>
            {
                string? key = dropdown.DropdownTitle.Content?.ToString();
                if (key != null) Dictionary.dropdownState[key] = title;
                else throw new NullReferenceException("dropdown.DropdownTitle.Content.ToString() is null");
            };

            Application.Current.Dispatcher.Invoke(() => dropdown.DropdownBox.Items.Add(dropdownitem));
            return dropdownitem;
        }

        private ATitle AddTitle(StackPanel panel, string title, bool CanMinimize = false)
        {
            var atitle = new ATitle(title, CanMinimize);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(atitle));
            return atitle;
        }

        private APButton AddButton(StackPanel panel, string title)
        {
            var button = new APButton(title);
            Application.Current.Dispatcher.Invoke(() => panel.Children.Add(button));
            return button;
        }

        private void AddCredit(StackPanel panel, string name, string role) => Application.Current.Dispatcher.Invoke(() => panel.Children.Add(new ACredit(name, role)));

        private void AddSeparator(StackPanel panel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                panel.Children.Add(new ARectangleBottom());
                panel.Children.Add(new ASpacer());
            });
        }

        #endregion Menu Logic

        #region Menu Loading

        private void LoadAimMenu()
        {
            #region Aim Assist

            uiManager.AT_Aim = AddTitle(AimAssist, "Aim Assist", true);
            uiManager.T_AimAligner = AddToggle(AimAssist, "Aim Assist");
            uiManager.T_AimAligner.Reader.Click += (s, e) =>
            {
                if (Dictionary.toggleState["Aim Assist"] && Dictionary.lastLoadedModel == "N/A")
                {
                    Dictionary.toggleState["Aim Assist"] = false;
                    UpdateToggleUI(uiManager.T_AimAligner, false);

                    new NoticeBar("Please load a model first", 5000).Show();
                }
            };
            uiManager.C_Keybind = AddKeyChanger(AimAssist, "Aim Keybind", Dictionary.bindingSettings["Aim Keybind"]);
            uiManager.C_Keybind = AddKeyChanger(AimAssist, "Second Aim Keybind", Dictionary.bindingSettings["Second Aim Keybind"]);
            uiManager.T_ConstantAITracking = AddToggle(AimAssist, "Constant AI Tracking");
            uiManager.T_ConstantAITracking.Reader.Click += (s, e) =>
            {
                if (Dictionary.toggleState["Constant AI Tracking"] && Dictionary.lastLoadedModel == "N/A")
                {
                    Dictionary.toggleState["Constant AI Tracking"] = false;
                    UpdateToggleUI(uiManager.T_ConstantAITracking, false);
                }
                else if (Dictionary.toggleState["Constant AI Tracking"])
                {
                    Dictionary.toggleState["Aim Assist"] = true;
                    UpdateToggleUI(uiManager.T_AimAligner, true);
                }
            };
            uiManager.T_Predictions = AddToggle(AimAssist, "Predictions");
            uiManager.T_EMASmoothing = AddToggle(AimAssist, "EMA Smoothening");
            uiManager.C_EmergencyKeybind = AddKeyChanger(AimAssist, "Emergency Stop Keybind", Dictionary.bindingSettings["Emergency Stop Keybind"]);
            uiManager.T_EnableModelSwitchKeybind = AddToggle(AimAssist, "Enable Model Switch Keybind");
            uiManager.C_ModelSwitchKeybind = AddKeyChanger(AimAssist, "Model Switch Keybind", Dictionary.bindingSettings["Model Switch Keybind"]);
            AddSeparator(AimAssist);

            #endregion Aim Assist

            #region Config

            uiManager.AT_AimConfig = AddTitle(AimConfig, "Aim Config", true);
            uiManager.D_PredictionMethod = AddDropdown(AimConfig, "Prediction Method");

            AddDropdownItem(uiManager.D_PredictionMethod, "Kalman Filter");
            AddDropdownItem(uiManager.D_PredictionMethod, "Shall0e's Prediction");
            AddDropdownItem(uiManager.D_PredictionMethod, "wisethef0x's EMA Prediction");

            uiManager.D_DetectionAreaType = AddDropdown(AimConfig, "Detection Area Type");
            uiManager.DDI_ClosestToCenterScreen = AddDropdownItem(uiManager.D_DetectionAreaType, "Closest to Center Screen");
            uiManager.DDI_ClosestToCenterScreen.Selected += async (sender, e) =>
            {
                await Task.Delay(100);
                await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                Convert.ToInt16((WinAPICaller.ScreenWidth / 2) / WinAPICaller.scalingFactorX) - 320,
                Convert.ToInt16((WinAPICaller.ScreenHeight / 2) / WinAPICaller.scalingFactorY) - 320,
                0, 0));
            };

            AddDropdownItem(uiManager.D_DetectionAreaType, "Closest to Mouse");

            uiManager.D_AimingBoundariesAlignment = AddDropdown(AimConfig, "Aiming Boundaries Alignment");

            AddDropdownItem(uiManager.D_AimingBoundariesAlignment, "Center");
            AddDropdownItem(uiManager.D_AimingBoundariesAlignment, "Top");
            AddDropdownItem(uiManager.D_AimingBoundariesAlignment, "Bottom");

            uiManager.S_MouseSensitivity = AddSlider(AimConfig, "Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1);
            uiManager.S_MouseSensitivity.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                if (uiManager.S_MouseSensitivity.Slider.Value >= 0.98) new NoticeBar("The Mouse Sensitivity you have set can cause Aimmy to be unable to aim, please decrease if you suffer from this problem", 10000).Show();
                else if (uiManager.S_MouseSensitivity.Slider.Value <= 0.1) new NoticeBar("The Mouse Sensitivity you have set can cause Aimmy to be unstable to aim, please increase if you suffer from this problem", 10000).Show();
            };
            uiManager.S_MouseJitter = AddSlider(AimConfig, "Mouse Jitter", "Jitter", 1, 1, 0, 15);

            uiManager.S_YOffset = AddSlider(AimConfig, "Y Offset (Up/Down)", "Offset", 1, 1, -150, 150);
            uiManager.S_YOffset = AddSlider(AimConfig, "Y Offset (%)", "Percent", 1, 1, 0, 100);

            uiManager.S_XOffset = AddSlider(AimConfig, "X Offset (Left/Right)", "Offset", 1, 1, -150, 150);
            uiManager.S_XOffset = AddSlider(AimConfig, "X Offset (%)", "Percent", 1, 1, 0, 100);

            uiManager.S_EMASmoothing = AddSlider(AimConfig, "EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1);

            AddSeparator(AimConfig);

            #endregion Config

            #region Trigger Bot

            uiManager.AT_TriggerBot = AddTitle(TriggerBot, "Auto Trigger", true);
            uiManager.T_AutoTrigger = AddToggle(TriggerBot, "Auto Trigger");
            uiManager.S_AutoTriggerDelay = AddSlider(TriggerBot, "Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.01, 1);
            AddSeparator(TriggerBot);

            #endregion Trigger Bot

            #region Anti Recoil

            uiManager.AT_AntiRecoil = AddTitle(AntiRecoil, "Anti Recoil", true);
            uiManager.T_AntiRecoil = AddToggle(AntiRecoil, "Anti Recoil");
            uiManager.C_AntiRecoilKeybind = AddKeyChanger(AntiRecoil, "Anti Recoil Keybind", "Left");
            uiManager.C_ToggleAntiRecoilKeybind = AddKeyChanger(AntiRecoil, "Disable Anti Recoil Keybind", "Oem6");
            uiManager.S_HoldTime = AddSlider(AntiRecoil, "Hold Time", "Milliseconds", 1, 1, 1, 1000, true);
            uiManager.B_RecordFireRate = AddButton(AntiRecoil, "Record Fire Rate");
            uiManager.B_RecordFireRate.Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
            uiManager.S_FireRate = AddSlider(AntiRecoil, "Fire Rate", "Milliseconds", 1, 1, 1, 5000, true);
            uiManager.S_YAntiRecoilAdjustment = AddSlider(AntiRecoil, "Y Recoil (Up/Down)", "Move", 1, 1, -1000, 1000, true);
            uiManager.S_XAntiRecoilAdjustment = AddSlider(AntiRecoil, "X Recoil (Left/Right)", "Move", 1, 1, -1000, 1000, true);
            AddSeparator(AntiRecoil);

            #endregion Anti Recoil

            #region Anti Recoil Config

            // Anti-Recoil Config
            uiManager.AT_AntiRecoilConfig = AddTitle(ARConfig, "Anti Recoil Config", true);
            uiManager.T_EnableGunSwitchingKeybind = AddToggle(ARConfig, "Enable Gun Switching Keybind");
            uiManager.B_SaveRecoilConfig = AddButton(ARConfig, "Save Anti Recoil Config");
            uiManager.B_SaveRecoilConfig.Reader.Click += (s, e) =>
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.InitialDirectory = $"{Directory.GetCurrentDirectory}\\bin\\anti_recoil_configs";
                saveFileDialog.Filter = "Aimmy Style Recoil Config (*.cfg)|*.cfg";
                if (saveFileDialog.ShowDialog() == true)
                {
                    SaveDictionary.WriteJSON(Dictionary.AntiRecoilSettings, saveFileDialog.FileName);
                    new NoticeBar($"[Anti Recoil] Config has been saved to \"{saveFileDialog.FileName}\"", 2000).Show();
                }
            };
            uiManager.C_Gun1Key = AddKeyChanger(ARConfig, "Gun 1 Key", "D1");
            uiManager.AFL_Gun1Config = AddFileLocator(ARConfig, "Gun 1 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");
            uiManager.C_Gun2Key = AddKeyChanger(ARConfig, "Gun 2 Key", "D2");
            uiManager.AFL_Gun2Config = AddFileLocator(ARConfig, "Gun 2 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");

            uiManager.B_LoadGun1Config = AddButton(ARConfig, "Load Gun 1 Config");
            uiManager.B_LoadGun1Config.Reader.Click += (s, e) => LoadAntiRecoilConfig(Dictionary.filelocationState["Gun 1 Config"], true);
            uiManager.B_LoadGun2Config = AddButton(ARConfig, "Load Gun 2 Config");
            uiManager.B_LoadGun2Config.Reader.Click += (s, e) => LoadAntiRecoilConfig(Dictionary.filelocationState["Gun 2 Config"], true);
            AddSeparator(ARConfig);

            #endregion Anti Recoil Config

            #region FOV Config

            uiManager.AT_FOV = AddTitle(FOVConfig, "FOV Config", true);
            uiManager.T_FOV = AddToggle(FOVConfig, "FOV");
            uiManager.T_DynamicFOV = AddToggle(FOVConfig, "Dynamic FOV");
            uiManager.C_DynamicFOV = AddKeyChanger(FOVConfig, "Dynamic FOV Keybind", Dictionary.bindingSettings["Dynamic FOV Keybind"]);
            uiManager.CC_FOVColor = AddColorChanger(FOVConfig, "FOV Color");
            uiManager.CC_FOVColor.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState["FOV Color"]);
            uiManager.CC_FOVColor.Reader.Click += (s, x) =>
            {
                System.Windows.Forms.ColorDialog colorDialog = new();
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    uiManager.CC_FOVColor.ColorChangingBorder.Background = new SolidColorBrush(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                    Dictionary.colorState["FOV Color"] = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B).ToString();
                    PropertyChanger.PostColor(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                }
            };

            uiManager.S_FOVSize = AddSlider(FOVConfig, "FOV Size", "Size", 1, 1, 10, 640);
            uiManager.S_FOVSize.Slider.ValueChanged += (s, x) =>
            {
                double FovSize = uiManager.S_FOVSize.Slider.Value;
                ActualFOV = FovSize;
                PropertyChanger.PostNewFOVSize(ActualFOV);
            };
            uiManager.S_DynamicFOVSize = AddSlider(FOVConfig, "Dynamic FOV Size", "Size", 1, 1, 10, 640);
            uiManager.S_DynamicFOVSize.Slider.ValueChanged += (s, x) =>
            {
                if (Dictionary.toggleState["Dynamic FOV"])
                {
                    PropertyChanger.PostNewFOVSize(uiManager.S_DynamicFOVSize.Slider.Value);
                }
            };
            uiManager.S_EMASmoothing.Slider.ValueChanged += (s, x) =>
            {
                if (Dictionary.toggleState["EMA Smoothening"])
                {
                    MouseManager.smoothingFactor = uiManager.S_EMASmoothing.Slider.Value;
                    Debug.WriteLine(MouseManager.smoothingFactor);
                }
            };
            AddSeparator(FOVConfig);

            #endregion FOV Config

            #region ESP Config

            uiManager.AT_DetectedPlayer = AddTitle(ESPConfig, "ESP Config", true);
            uiManager.T_ShowDetectedPlayer = AddToggle(ESPConfig, "Show Detected Player");
            uiManager.T_ShowAIConfidence = AddToggle(ESPConfig, "Show AI Confidence");
            uiManager.T_ShowTracers = AddToggle(ESPConfig, "Show Tracers");
            uiManager.CC_DetectedPlayerColor = AddColorChanger(ESPConfig, "Detected Player Color");
            uiManager.CC_DetectedPlayerColor.ColorChangingBorder.Background = (Brush)new BrushConverter().ConvertFromString(Dictionary.colorState["Detected Player Color"]);
            uiManager.CC_DetectedPlayerColor.Reader.Click += (s, x) =>
            {
                System.Windows.Forms.ColorDialog colorDialog = new();
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    uiManager.CC_DetectedPlayerColor.ColorChangingBorder.Background = new SolidColorBrush(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                    Dictionary.colorState["Detected Player Color"] = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B).ToString();
                    PropertyChanger.PostDPColor(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                }
            };

            uiManager.S_DPFontSize = AddSlider(ESPConfig, "AI Confidence Font Size", "Size", 1, 1, 1, 30);
            uiManager.S_DPFontSize.Slider.ValueChanged += (s, x) => PropertyChanger.PostDPFontSize((int)uiManager.S_DPFontSize.Slider.Value);

            uiManager.S_DPCornerRadius = AddSlider(ESPConfig, "Corner Radius", "Radius", 1, 1, 0, 100);
            uiManager.S_DPCornerRadius.Slider.ValueChanged += (s, x) => PropertyChanger.PostDPWCornerRadius((int)uiManager.S_DPCornerRadius.Slider.Value);

            uiManager.S_DPBorderThickness = AddSlider(ESPConfig, "Border Thickness", "Thickness", 0.1, 1, 0.1, 10);
            uiManager.S_DPBorderThickness.Slider.ValueChanged += (s, x) => PropertyChanger.PostDPWBorderThickness(uiManager.S_DPBorderThickness.Slider.Value);

            uiManager.S_DPOpacity = AddSlider(ESPConfig, "Opacity", "Opacity", 0.1, 0.1, 0, 1);

            AddSeparator(ESPConfig);

            #endregion ESP Config
        }

        private void LoadSettingsMenu()
        {
            uiManager.AT_SettingsMenu = AddTitle(SettingsConfig, "Settings Menu", true);

            uiManager.T_CollectDataWhilePlaying = AddToggle(SettingsConfig, "Collect Data While Playing");
            uiManager.T_AutoLabelData = AddToggle(SettingsConfig, "Auto Label Data");
            uiManager.D_MouseMovementMethod = AddDropdown(SettingsConfig, "Mouse Movement Method");
            AddDropdownItem(uiManager.D_MouseMovementMethod, "Mouse Event");
            AddDropdownItem(uiManager.D_MouseMovementMethod, "SendInput");
            uiManager.DDI_LGHUB = AddDropdownItem(uiManager.D_MouseMovementMethod, "LG HUB");

            uiManager.DDI_LGHUB.Selected += (sender, e) =>
            {
                if (!new LGHubMain().Load())
                {
                    SelectMouseEvent();
                }
            };

            uiManager.DDI_RazerSynapse = AddDropdownItem(uiManager.D_MouseMovementMethod, "Razer Synapse (Require Razer Peripheral)");
            uiManager.DDI_RazerSynapse.Selected += async (sender, e) =>
            {
                if (!await RZMouse.Load())
                {
                    SelectMouseEvent();
                }
            };
            uiManager.DDI_ddxoft = AddDropdownItem(uiManager.D_MouseMovementMethod, "ddxoft Virtual Input Driver");
            uiManager.DDI_ddxoft.Selected += async (sender, e) =>
            {
                if (!await DdxoftMain.Load())
                {
                    SelectMouseEvent();
                }
            };
            uiManager.S_AIMinimumConfidence = AddSlider(SettingsConfig, "AI Minimum Confidence", "% Confidence", 1, 1, 1, 100);
            uiManager.S_AIMinimumConfidence.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                if (uiManager.S_AIMinimumConfidence.Slider.Value >= 95) new NoticeBar("The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.", 10000).Show();
                else if (uiManager.S_AIMinimumConfidence.Slider.Value <= 35) new NoticeBar("The minimum confidence you have set for Aimmy may be too low can cause false positives.", 10000).Show();
            };
            uiManager.T_MouseBackgroundEffect = AddToggle(SettingsConfig, "Mouse Background Effect");
            uiManager.T_UITopMost = AddToggle(SettingsConfig, "UI TopMost");
            uiManager.B_SaveConfig = AddButton(SettingsConfig, "Save Config");
            uiManager.B_SaveConfig.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();
            uiManager.B_RepoManager = AddButton(SettingsConfig, "Repository Manager");
            uiManager.B_RepoManager.Reader.Click += (s, e) => new RepoManager().Show();

            AddSeparator(SettingsConfig);

            // X/Y Percentage Adjustment Enabler
            uiManager.AT_XYPercentageAdjustmentEnabler = AddTitle(XYPercentageEnablerMenu, "X/Y Percentage Adjustment", true);
            uiManager.T_XAxisPercentageAdjustment = AddToggle(XYPercentageEnablerMenu, "X Axis Percentage Adjustment");
            uiManager.T_YAxisPercentageAdjustment = AddToggle(XYPercentageEnablerMenu, "Y Axis Percentage Adjustment");
            AddSeparator(XYPercentageEnablerMenu);

            // ddxoft Menu
            //AddTitle(SSP2, "ddxoft Configurator");
            //uiManager.AFL_ddxoftDLLLocator = AddFileLocator(SSP2, "ddxoft DLL Location", "ddxoft dll (*.dll)|*.dll");
            //AddSeparator(SSP2);
        }

        private void LoadCreditsMenu()
        {
            AddTitle(CreditsPanel, "Developers");
            AddCredit(CreditsPanel, "Babyhamsta", "AI Logic");
            AddCredit(CreditsPanel, "MarsQQ", "Design");
            AddCredit(CreditsPanel, "Taylor", "Optimization, Cleanup");
            AddSeparator(CreditsPanel);

            AddTitle(CreditsPanel, "Contributors");
            AddCredit(CreditsPanel, "Shall0e", "Prediction Method");
            AddCredit(CreditsPanel, "wisethef0x", "EMA Prediction Method");
            AddCredit(CreditsPanel, "HakaCat", "Idea for Auto Labelling Data");
            AddCredit(CreditsPanel, "Themida", "LGHub check");
            AddCredit(CreditsPanel, "Ninja", "MarsQQ's emotional support");
            AddSeparator(CreditsPanel);

            AddTitle(CreditsPanel, "Model Creators");
            AddCredit(CreditsPanel, "Babyhamsta", "UniversalV4, Phantom Forces");
            AddCredit(CreditsPanel, "Natdog400", "AIO V2, V7");
            AddCredit(CreditsPanel, "Themida", "Arsenal, Strucid, Bad Business, Blade Ball, etc.");
            AddCredit(CreditsPanel, "Hogthewog", "Da Hood, FN, etc.");
            AddSeparator(CreditsPanel);
        }

        public async Task LoadStoreMenu()
        {
            try
            {
                var list = await FileManager.RetrieveAndAddFiles();

                if (list.Count == 0)
                {
                    LackOfConfigsText.Visibility = Visibility.Visible;
                    LackOfModelsText.Visibility = Visibility.Visible;
                    return;
                }

                DownloadGateway(list);
            }
            catch (Exception e)
            {
                new NoticeBar(e.Message, 10000).Show();

                LackOfConfigsText.Visibility = Visibility.Visible;
                LackOfModelsText.Visibility = Visibility.Visible;

                return;
            }
        }

        private void DownloadGateway(Dictionary<string, GitHubFile> entries)
        {
            ModelStoreScroller.Children.Clear();
            ConfigStoreScroller.Children.Clear();

            var modelSHA = GetLocalFileShas("bin\\models");
            var configSHA = GetLocalFileShas("bin\\configs");

            foreach (var entry in entries)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                GitHubFile file = entry.Value;

                if (modelSHA.ContainsValue(file.sha ?? "") || configSHA.ContainsValue(file.sha ?? ""))
                {
                    continue;
                }

                string RepoLink = entry.Value.download_url!;

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    StackPanel targetPanel = file.name.Contains(".onnx") ? ModelStoreScroller : ConfigStoreScroller;
                    ADownloadGateway gateway = new ADownloadGateway(RepoLink, file.name, file.name.Contains(".onnx") ? "models" : "configs");

                    targetPanel.Children.Add(gateway);
                });
            }
        }

        #endregion Menu Loading

        #region Menu Minizations

        private void ToggleAimMenu() => SetMenuVisibility(AimAssist, !Dictionary.minimizeState["Aim Assist"]);

        private void ToggleAimConfig() => SetMenuVisibility(AimConfig, !Dictionary.minimizeState["Aim Config"]);

        private void ToggleAutoTrigger() => SetMenuVisibility(TriggerBot, !Dictionary.minimizeState["Auto Trigger"]);

        private void ToggleAntiRecoilMenu() => SetMenuVisibility(AntiRecoil, !Dictionary.minimizeState["Anti Recoil"]);

        private void ToggleAntiRecoilConfigMenu() => SetMenuVisibility(ARConfig, !Dictionary.minimizeState["Anti Recoil Config"]);

        private void ToggleFOVConfigMenu() => SetMenuVisibility(FOVConfig, !Dictionary.minimizeState["FOV Config"]);

        private void ToggleESPConfigMenu() => SetMenuVisibility(ESPConfig, !Dictionary.minimizeState["ESP Config"]);

        private void ToggleSettingsMenu() => SetMenuVisibility(SettingsConfig, !Dictionary.minimizeState["Settings Menu"]);

        private void ToggleXYPercentageAdjustmentEnabler() => SetMenuVisibility(XYPercentageEnablerMenu, !Dictionary.minimizeState["X/Y Percentage Adjustment"]);

        private void LoadMenuMinimizers()
        {
            ToggleAimMenu();
            ToggleAimConfig();
            ToggleAutoTrigger();
            ToggleAntiRecoilMenu();
            ToggleAntiRecoilConfigMenu();
            ToggleFOVConfigMenu();
            ToggleESPConfigMenu();
            ToggleSettingsMenu();
            ToggleXYPercentageAdjustmentEnabler();

            uiManager.AT_Aim.Minimize.Click += (s, e) => ToggleAimMenu();

            uiManager.AT_AimConfig.Minimize.Click += (s, e) => ToggleAimConfig();

            uiManager.AT_TriggerBot.Minimize.Click += (s, e) => ToggleAutoTrigger();

            uiManager.AT_AntiRecoil.Minimize.Click += (s, e) => ToggleAntiRecoilMenu();

            uiManager.AT_AntiRecoilConfig.Minimize.Click += (s, e) => ToggleAntiRecoilConfigMenu();

            uiManager.AT_FOV.Minimize.Click += (s, e) => ToggleFOVConfigMenu();

            uiManager.AT_DetectedPlayer.Minimize.Click += (s, e) => ToggleESPConfigMenu();

            uiManager.AT_SettingsMenu.Minimize.Click += (s, e) => ToggleSettingsMenu();

            uiManager.AT_XYPercentageAdjustmentEnabler.Minimize.Click += (s, e) => ToggleXYPercentageAdjustmentEnabler();
        }

        private static void SetMenuVisibility(StackPanel panel, bool isVisible)
        {
            foreach (UIElement child in panel.Children)
            {
                if (!(child is ATitle || child is ASpacer || child is ARectangleBottom))
                {
                    child.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    child.Visibility = Visibility.Visible;
                }
            }
        }

        #endregion Menu Minizations

        #region Config Loader

        private void LoadConfig(string path = "bin\\configs\\Default.cfg", bool loading_from_configlist = false)
        {
            SaveDictionary.LoadJSON(Dictionary.sliderSettings, path);
            try
            {
                if (loading_from_configlist)
                {
                    if (Dictionary.sliderSettings["Suggested Model"] != "N/A" || Dictionary.sliderSettings["Suggested Model"] != "")
                    {
                        MessageBox.Show(
                            "The creator of this model suggests you use this model:\n" +
                            Dictionary.sliderSettings["Suggested Model"], "Suggested Model - Aimmy"
                        );
                    }

                    uiManager.S_FireRate!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "Fire Rate", 1);

                    uiManager.S_FOVSize!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "FOV Size", 640);

                    uiManager.S_MouseSensitivity!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "Mouse Sensitivity (+/-)", 0.8);
                    uiManager.S_MouseJitter!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "Mouse Jitter", 0);

                    uiManager.S_YOffset!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "Y Offset (Up/Down)", 0);
                    uiManager.S_XOffset!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "X Offset (Left/Right)", 0);

                    uiManager.S_AutoTriggerDelay!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "Auto Trigger Delay", .25);
                    uiManager.S_AIMinimumConfidence!.Slider.Value = MainWindow.GetValueOrDefault(Dictionary.sliderSettings, "AI Minimum Confidence", 50);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error loading config, possibly outdated\n{e}");
            }
        }

        #endregion Config Loader

        #region Anti Recoil Config Loader

        private void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg", bool loading_outside_startup = false)
        {
            if (File.Exists(path))
            {
                SaveDictionary.LoadJSON(Dictionary.AntiRecoilSettings, path);
                try
                {
                    if (loading_outside_startup)
                    {
                        uiManager.S_HoldTime!.Slider.Value = Dictionary.AntiRecoilSettings["Hold Time"];

                        uiManager.S_FireRate!.Slider.Value = Dictionary.AntiRecoilSettings["Fire Rate"];

                        uiManager.S_YAntiRecoilAdjustment!.Slider.Value = Dictionary.AntiRecoilSettings["Y Recoil (Up/Down)"];
                        uiManager.S_XAntiRecoilAdjustment!.Slider.Value = Dictionary.AntiRecoilSettings["X Recoil (Left/Right)"];
                        new NoticeBar($"[Anti Recoil] Loaded \"{path}\"", 2000).Show();
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading config, possibly outdated\n{e}");
                }
            }
            else
            {
                new NoticeBar("[Anti Recoil] Config not found.", 5000).Show();
            }
        }

        #endregion Anti Recoil Config Loader

        #region Open Folder

        private void OpenFolderB_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Normal,
                        FileName = "explorer.exe",
                        Arguments = "bin\\" + clickedButton.Tag.ToString(),
                        WorkingDirectory = Directory.GetCurrentDirectory()
                    }
                }.Start();
            }
        }

        #endregion Open Folder

        #region Menu Functions

        private async void SelectMouseEvent()
        {
            await Task.Delay(500);
            uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
        }

        private static T GetValueOrDefault<T>(Dictionary<string, T> dictionary, string key, T defaultValue)
        {
            if (dictionary.TryGetValue(key, out T? value))
            {
                //Debug.WriteLine($"Value: {value}, Dictionary: {key}");
                return value;
            }
            else
            {
                //Debug.WriteLine($"Default: {defaultValue}, Dictionary: {key}");
                return defaultValue;
            }
        }

        #endregion Menu Functions

        #region System Information

        private string? GetProcessorName() => GetSpecs.GetSpecification("Win32_Processor", "Name");

        private string? GetVideoControllerName() => GetSpecs.GetSpecification("Win32_VideoController", "Name");

        private string? GetFormattedMemorySize()
        {
            long totalMemorySize = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
            return Math.Round(totalMemorySize / (1024.0 * 1024.0), 0).ToString();
        }

        #endregion System Information

        #region Fancy UI Calculations

        private double currentGradientAngle = 0;

        private double CalculateAngleDifference(double targetAngle, double fullCircle, double halfCircle, double clamp)
        {
            double angleDifference = (targetAngle - currentGradientAngle + fullCircle) % fullCircle;
            if (angleDifference > halfCircle) angleDifference -= fullCircle;
            return Math.Max(Math.Min(angleDifference, clamp), -clamp);
        }

        #endregion Fancy UI Calculations

        #region Window Handling

        private static void ShowHideDPWindow()
        {
            if (!Dictionary.toggleState["Show Detected Player"]) DPWindow.Hide();
            else DPWindow.Show();
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateManager updateManager = new UpdateManager();
            await updateManager.CheckForUpdate("v2.1.5");
            updateManager.Dispose();
        }

        #endregion Window Handling
    }
}
