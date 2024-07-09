using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.Models;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Other;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
using Class;
using InputLogic;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Other;
using Visuality;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace Aimmy2;

public partial class MainWindow
{
    #region Main Variables

    private readonly ThemePalette _theme = ApplicationConstants.Theme;
    private readonly InputBindingManager bindingManager;
    private readonly FileManager fileManager;
    private static FOV FOVWindow;
    private static DetectedPlayerWindow DPWindow;
    private static GithubManager githubManager = new();
    public UI uiManager = new();
    public AntiRecoilManager arManager = new();


    private bool CurrentlySwitching;
    private ScrollViewer? CurrentScrollViewer;

    private readonly HashSet<string> AvailableModels = new();
    private readonly HashSet<string> AvailableConfigs = new();

    private static double ActualFOV = 640;

    #endregion Main Variables

    #region Loading Window

    public AppConfig Config { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        var writer = new TextBoxStreamWriter(OutputTextBox);
        Console.SetOut(writer);

        Console.WriteLine("Init UI");

        GamepadManager.Init();

        Config = AppConfig.Load();

        CurrentScrollViewer = FindName("AimMenu") as ScrollViewer;
        if (CurrentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        AppConfig.Current.DetectedPlayerOverlay = DPWindow = new();
        AppConfig.Current.FOVWindow = FOVWindow = new();

        fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        arManager.HoldDownLoad();

        LoadAntiRecoilConfig();


        bindingManager = new InputBindingManager();
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.AimKeybind),
            AppConfig.Current.BindingSettings.AimKeybind);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.TriggerKey),
            AppConfig.Current.BindingSettings.TriggerKey);

        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.ActiveToggleKey),
            AppConfig.Current.BindingSettings.ActiveToggleKey);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind),
            AppConfig.Current.BindingSettings.SecondAimKeybind);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind),
            AppConfig.Current.BindingSettings.DynamicFOVKeybind);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind),
            AppConfig.Current.BindingSettings.ModelSwitchKeybind);

        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind),
            AppConfig.Current.BindingSettings.AntiRecoilKeybind);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind),
            AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.Gun1Key),
            AppConfig.Current.BindingSettings.Gun1Key);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.Gun2Key),
            AppConfig.Current.BindingSettings.Gun2Key);

        LoadAimMenu();
        LoadSettingsMenu();
        LoadGamepadSettingsMenu();
        LoadCreditsMenu();
        LoadStoreMenuAsync();
        LoadGlobalUI();


        PropertyChanger.ReceiveNewConfig = LoadConfig;

        ActualFOV = AppConfig.Current.SliderSettings.FOVSize;
        PropertyChanger.PostNewFOVSize(AppConfig.Current.SliderSettings.FOVSize);
        PropertyChanger.PostColor((Color)ColorConverter.ConvertFromString(AppConfig.Current.ColorState.FOVColor));

        PropertyChanger.PostDPColor(
            (Color)ColorConverter.ConvertFromString(AppConfig.Current.ColorState.DetectedPlayerColor));
        PropertyChanger.PostDPFontSize(AppConfig.Current.SliderSettings.AIConfidenceFontSize);
        PropertyChanger.PostDPWCornerRadius(AppConfig.Current.SliderSettings.CornerRadius);
        PropertyChanger.PostDPWBorderThickness(AppConfig.Current.SliderSettings.BorderThickness);
        PropertyChanger.PostDPWOpacity(AppConfig.Current.SliderSettings.Opacity);

        ListenForKeybinds();
        LoadMenuMinimizers();

        DataContext = this;

        var modelPath = Path.Combine("bin/models", ApplicationConstants.DefaultModel);
        if (File.Exists(modelPath) && !FileManager.CurrentlyLoadingModel &&
            FileManager.AIManager?.IsModelLoaded != true)
            _ = fileManager.LoadModel(ApplicationConstants.DefaultModel, modelPath);

        if (!string.IsNullOrEmpty(ApplicationConstants.ShowOnly))
        {
            Sidebar.Visibility = Visibility.Collapsed;
            _ = SwitchScrollPanels(FindName(ApplicationConstants.ShowOnly) as ScrollViewer);
            CurrentMenu = ApplicationConstants.ShowOnly;
        }

        MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        //Console.WriteLine(JsonConvert.SerializeObject(Dictionary.toggleState));
        Console.WriteLine("Init UI Complete");
    }


    public bool IsModelLoaded => FileManager.AIManager?.IsModelLoaded ?? false;
    public bool IsNotModelLoaded => !IsModelLoaded;

    private void LoadGlobalUI()
    {
        uiManager.G_Active = TopCenterGrid.AddToggle("Global Active");
        uiManager.G_Active_Keybind = TopCenterGrid.AddKeyChanger(
            nameof(AppConfig.Current.BindingSettings.ActiveToggleKey),
            () => AppConfig.Current.BindingSettings.ActiveToggleKey, bindingManager);

        uiManager.G_Active.Deactivated += (s, e) => SetActive(false);
        uiManager.G_Active.Activated += (s, e) => SetActive(true);
    }

    public void SetActive(bool active)
    {
        AppConfig.Current.ToggleState.GlobalActive = active;
        if (FileManager.AIManager != null)
            FileManager.AIManager.HeadRelativeRect =
                RelativeRect.ParseOrDefault(AppConfig.Current.DropdownState.HeadArea);

        ApplicationConstants.Theme = active ? ThemePalette.GreenPalette : _theme;
    }

    public Visibility GetVisibilityFor(string feature)
    {
        return ApplicationConstants.DisabledFeatures.Contains(feature) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void LoadStoreMenuAsync()
    {
        await LoadStoreMenu();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        AboutSpecs.Content =
            $"{GetProcessorName()} • {GetVideoControllerName()} • {GetFormattedMemorySize()}GB RAM";
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        fileManager.InQuittingState = true;
        FileManager.AIManager?.Dispose();


        FOVWindow.Close();
        DPWindow.Close();


        if (AppConfig.Current.DropdownState.MouseMovementMethod == MouseMovementMethod.LGHUB) LGMouse.Close();

        AppConfig.Current.Save();


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
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), MenuHighlighter, MenuHighlighter.Margin,
                clickedButton.Margin);
            await SwitchScrollPanels(FindName(clickedButton.Tag.ToString()) as ScrollViewer ??
                                     throw new NullReferenceException("Scrollpanel is null"));
            CurrentMenu = clickedButton.Tag.ToString()!;
        }
    }

    private async Task SwitchScrollPanels(ScrollViewer MovingScrollViewer)
    {
        MovingScrollViewer.Visibility = Visibility.Visible;
        Animator.Fade(MovingScrollViewer);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(350), MovingScrollViewer, MovingScrollViewer.Margin,
            new Thickness(50, 50, 0, 0));

        Animator.FadeOut(CurrentScrollViewer!);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(350), CurrentScrollViewer!, CurrentScrollViewer!.Margin,
            new Thickness(50, 450, 0, -400));
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
        var searchText = textBox.Text.ToLower();

        foreach (var item in panel.Children.OfType<ADownloadGateway>())
            item.Visibility = item.Title.Content.ToString()?.ToLower().Contains(searchText) == true
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdateToggleUI(AToggle toggle, bool isEnabled)
    {
        // TODO: Remove bullshit 
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (isEnabled)
                toggle.EnableSwitch();
            else
                toggle.DisableSwitch();
        });
    }

    // All Keybind Listening is moved to a seperate function because having it stored in "AddKeyChanger" was making these functions run several times.
    // Nori
    private void ListenForKeybinds()
    {
        bindingManager.OnBindingPressed += bindingId =>
        {
            switch (bindingId)
            {
                case nameof(AppConfig.Current.BindingSettings.ActiveToggleKey):
                    if (IsModelLoaded) UpdateToggleUI(uiManager.G_Active!, !uiManager.G_Active.Checked);

                    break;
                case nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind):
                    if (AppConfig.Current.ToggleState.EnableModelSwitchKeybind)
                        if (!FileManager.CurrentlyLoadingModel)
                        {
                            if (ModelListBox.SelectedIndex >= 0 &&
                                ModelListBox.SelectedIndex < ModelListBox.Items.Count - 1)
                                ModelListBox.SelectedIndex += 1;
                            else
                                ModelListBox.SelectedIndex = 0;
                        }

                    break;

                case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                    if (AppConfig.Current.ToggleState.DynamicFOV)
                    {
                        AppConfig.Current.SliderSettings.FOVSize = AppConfig.Current.SliderSettings.DynamicFOVSize;
                        Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle,
                            FOVWindow.Circle.ActualWidth, AppConfig.Current.SliderSettings.DynamicFOVSize);
                        Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle,
                            FOVWindow.Circle.ActualHeight, AppConfig.Current.SliderSettings.DynamicFOVSize);
                    }

                    break;


                case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                    if (AppConfig.Current.ToggleState.AntiRecoil)
                    {
                        arManager.IndependentMousePress = 0;
                        arManager.HoldDownTimer.Start();
                    }

                    break;

                case nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind):
                    if (AppConfig.Current.ToggleState.AntiRecoil)
                    {
                        AppConfig.Current.ToggleState.AntiRecoil = false;
                        UpdateToggleUI(uiManager.T_AntiRecoil!, false);
                        new NoticeBar("[Disable Anti Recoil Keybind] Disabled Anti-Recoil.", 4000).Show();
                    }

                    break;

                case nameof(AppConfig.Current.BindingSettings.Gun1Key):
                    if (AppConfig.Current.ToggleState.EnableModelSwitchKeybind)
                        LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
                    break;

                case nameof(AppConfig.Current.BindingSettings.Gun2Key):
                    if (AppConfig.Current.ToggleState.EnableModelSwitchKeybind)
                        LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
                    break;
            }
        };

        bindingManager.OnBindingReleased += bindingId =>
        {
            switch (bindingId)
            {
                case nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind):
                    if (AppConfig.Current.ToggleState.DynamicFOV)
                    {
                        AppConfig.Current.SliderSettings.FOVSize = ActualFOV;
                        Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle,
                            FOVWindow.Circle.ActualWidth, ActualFOV);
                        Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOVWindow.Circle,
                            FOVWindow.Circle.ActualHeight, ActualFOV);
                    }

                    break;
                // Anti Recoil
                case nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind):
                    if (AppConfig.Current.ToggleState.AntiRecoil)
                    {
                        arManager.HoldDownTimer.Stop();
                        arManager.IndependentMousePress = 0;
                    }

                    break;
            }
        };
    }

    #endregion Menu Logic

    #region Menu Loading

    private void LoadAimMenu()
    {
        #region Aim Assist

        var keybind = AppConfig.Current.BindingSettings;
        uiManager.AT_Aim = AimAssist.AddTitle("Aim Assist", true);
        uiManager.T_AimAligner = AimAssist.AddToggle("Aim Assist");


        uiManager.C_Keybind = AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AimKeybind),
            () => keybind.AimKeybind,
            bindingManager);
        uiManager.C_Keybind = AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind),
            () => keybind.SecondAimKeybind, bindingManager);
        uiManager.T_ConstantAITracking = AimAssist.AddToggle("Constant AI Tracking");

        uiManager.T_ConstantAITracking.Changed += (s, e) =>
        {
            if (e.Value)
            {
                AppConfig.Current.ToggleState.AimAssist = true;
                UpdateToggleUI(uiManager.T_AimAligner, true);
            }
        };
        uiManager.T_Predictions = AimAssist.AddToggle("Predictions");
        uiManager.T_EMASmoothing = AimAssist.AddToggle("EMA Smoothening");
        uiManager.T_EnableModelSwitchKeybind = AimAssist.AddToggle("Enable Model Switch Keybind");
        uiManager.C_ModelSwitchKeybind = AimAssist.AddKeyChanger(
            nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), () => keybind.ModelSwitchKeybind,
            bindingManager);
        AimAssist.AddSeparator();
        AimAssist.Visibility = GetVisibilityFor("AimAssist");

        #endregion Aim Assist

        #region Config

        uiManager.AT_AimConfig = AimConfig.AddTitle("Aim Config", true);
        uiManager.D_PredictionMethod = AimConfig.AddDropdown("Prediction Method",
            AppConfig.Current.DropdownState.PredictionMethod,
            v => AppConfig.Current.DropdownState.PredictionMethod = v);
        uiManager.D_PredictionMethod = AimConfig.AddDropdown("Detection Area Type",
            AppConfig.Current.DropdownState.DetectionAreaType, async v =>
            {
                AppConfig.Current.DropdownState.DetectionAreaType = v;
                if (v == DetectionAreaType.ClosestToCenterScreen)
                {
                    await Task.Delay(100);
                    await Application.Current.Dispatcher.BeginInvoke(() =>
                        FOVWindow.FOVStrictEnclosure.Margin = new Thickness(
                            Convert.ToInt16(WinAPICaller.ScreenWidth / 2 / WinAPICaller.scalingFactorX) - 320,
                            Convert.ToInt16(WinAPICaller.ScreenHeight / 2 / WinAPICaller.scalingFactorY) - 320,
                            0, 0));
                }
            });


        uiManager.D_AimingBoundariesAlignment = AimConfig.AddDropdown("Aiming Boundaries Alignment",
            AppConfig.Current.DropdownState.AimingBoundariesAlignment,
            v => AppConfig.Current.DropdownState.AimingBoundariesAlignment = v);


        uiManager.S_MouseSensitivity =
            AimConfig.AddSlider("Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1);
        uiManager.S_MouseSensitivity.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            if (uiManager.S_MouseSensitivity.Slider.Value >= 0.98)
                new NoticeBar(
                    "The Mouse Sensitivity you have set can cause Aimmy to be unable to aim, please decrease if you suffer from this problem",
                    10000).Show();
            else if (uiManager.S_MouseSensitivity.Slider.Value <= 0.1)
                new NoticeBar(
                    "The Mouse Sensitivity you have set can cause Aimmy to be unstable to aim, please increase if you suffer from this problem",
                    10000).Show();
        };
        uiManager.S_MouseJitter = AimConfig.AddSlider("Mouse Jitter", "Jitter", 1, 1, 0, 15);

        uiManager.S_YOffset = AimConfig.AddSlider("Y Offset (Up/Down)", "Offset", 1, 1, -150, 150);
        uiManager.S_YOffset = AimConfig.AddSlider("Y Offset (%)", "Percent", 1, 1, 0, 100);

        uiManager.S_XOffset = AimConfig.AddSlider("X Offset (Left/Right)", "Offset", 1, 1, -150, 150);
        uiManager.S_XOffset = AimConfig.AddSlider("X Offset (%)", "Percent", 1, 1, 0, 100);

        uiManager.S_EMASmoothing = AimConfig.AddSlider("EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1);

        AimConfig.AddSeparator();
        AimConfig.Visibility = GetVisibilityFor("AimConfig");

        #endregion Config

        #region Trigger Bot

        uiManager.AT_TriggerBot = TriggerBot.AddTitle("Auto Trigger", true);
        uiManager.T_AutoTrigger = TriggerBot.AddToggle("Auto Trigger", true, t =>
        {
            //var ignored = new List<UIElement>{ TriggerBot, uiManager.AT_TriggerBot, uiManager.T_AutoTrigger }.Concat(uiManager.T_AutoTrigger.FindChildren<UIElement>()).Distinct().ToArray();
            //t.Activated += (sender, args) => Array.ForEach(TriggerBot.FindChildren<UIElement>(el => el != sender && !ignored.Contains(el)), el => el.IsEnabled = true);
            //t.Deactivated += (sender, args) => Array.ForEach(TriggerBot.FindChildren<UIElement>(el => el != sender && !ignored.Contains(el)), el => el.IsEnabled = false);
        });

        uiManager.T_TriggerSendKey = TriggerBot.AddKeyChanger("Trigger Additional Send",
            () => keybind.TriggerAdditionalSend, bindingManager);

        uiManager.T_TriggerCheck = TriggerBot.AddDropdown("Trigger Check", AppConfig.Current.DropdownState.TriggerCheck,
            check => AppConfig.Current.DropdownState.TriggerCheck = check);

        uiManager.T_HeadAreaBtn = TriggerBot.AddButton("Configure Head Area", b =>
        {
            b.Visibility = AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter
                ? Visibility.Visible
                : Visibility.Collapsed;
            b.ToolTip = "Specify the area of the Head when this interaction center the trigger will be executed";
        });
        uiManager.T_HeadAreaBtn.Reader.Click += (s, e) =>
            new EditHeadArea(AppConfig.Current.DropdownState.HeadArea).Show();

        uiManager.T_TriggerCheck.DropdownBox.SelectionChanged += (sender, args) =>
        {
            var argsAddedItem = args.AddedItems[0] as ComboBoxItem;
            uiManager.T_HeadAreaBtn.Visibility = argsAddedItem?.Content.ToString() == "Head Intersecting Center"
                ? Visibility.Visible
                : Visibility.Collapsed;
        };

        uiManager.T_TriggerKey = TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerKey),
            () => keybind.TriggerKey, bindingManager);
        uiManager.S_AutoTriggerDelay = TriggerBot.AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.01, 1);
        TriggerBot.AddSeparator();
        TriggerBot.Visibility = GetVisibilityFor("TriggerBot");

        #endregion Trigger Bot

        #region Anti Recoil

        uiManager.AT_AntiRecoil = AntiRecoil.AddTitle("Anti Recoil", true);
        uiManager.T_AntiRecoil = AntiRecoil.AddToggle("Anti Recoil");
        uiManager.C_AntiRecoilKeybind =
            AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), "Left",
                bindingManager);
        uiManager.C_ToggleAntiRecoilKeybind =
            AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), "Oem6",
                bindingManager);
        uiManager.S_HoldTime = AntiRecoil.AddSlider("Hold Time", "Milliseconds", 1, 1, 1, 1000, true);
        uiManager.B_RecordFireRate = AntiRecoil.AddButton("Record Fire Rate");
        uiManager.B_RecordFireRate.Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        uiManager.S_FireRate = AntiRecoil.AddSlider("Fire Rate", "Milliseconds", 1, 1, 1, 5000, true);
        uiManager.S_YAntiRecoilAdjustment = AntiRecoil.AddSlider("Y Recoil (Up/Down)", "Move", 1, 1, -1000, 1000, true);
        uiManager.S_XAntiRecoilAdjustment =
            AntiRecoil.AddSlider("X Recoil (Left/Right)", "Move", 1, 1, -1000, 1000, true);
        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor("AntiRecoil");

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config
        uiManager.AT_AntiRecoilConfig = ARConfig.AddTitle("Anti Recoil Config", true);
        uiManager.T_EnableGunSwitchingKeybind = ARConfig.AddToggle("Enable Gun Switching Keybind");
        uiManager.B_SaveRecoilConfig = ARConfig.AddButton("Save Anti Recoil Config");
        uiManager.B_SaveRecoilConfig.Reader.Click += (s, e) =>
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = $"{Directory.GetCurrentDirectory}\\bin\\anti_recoil_configs",
                Filter = "Aimmy Style Recoil Config (*.cfg)|*.cfg"
            };
            if (saveFileDialog.ShowDialog() == true)
                AppConfig.Current.AntiRecoilSettings.Save<AntiRecoilSettings>(saveFileDialog.FileName);
                new NoticeBar($"[Anti Recoil] Config has been saved to \"{saveFileDialog.FileName}\"", 2000).Show();
        };
        uiManager.C_Gun1Key =
            ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun1Key), "D1", bindingManager);
        uiManager.AFL_Gun1Config = ARConfig.AddFileLocator("Gun 1 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg",
            "\\bin\\anti_recoil_configs");
        uiManager.C_Gun2Key =
            ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun2Key), "D2", bindingManager);
        uiManager.AFL_Gun2Config = ARConfig.AddFileLocator("Gun 2 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg",
            "\\bin\\anti_recoil_configs");

        uiManager.B_LoadGun1Config = ARConfig.AddButton("Load Gun 1 Config");
        uiManager.B_LoadGun1Config.Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
        uiManager.B_LoadGun2Config = ARConfig.AddButton("Load Gun 2 Config");
        uiManager.B_LoadGun2Config.Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
        ARConfig.AddSeparator();
        ARConfig.Visibility = GetVisibilityFor("ARConfig");

        #endregion Anti Recoil Config

        #region FOV Config

        uiManager.AT_FOV = FOVConfig.AddTitle("FOV Config", true);
        uiManager.T_FOV = FOVConfig.AddToggle("FOV");
        uiManager.T_DynamicFOV = FOVConfig.AddToggle("Dynamic FOV");
        uiManager.C_DynamicFOV = FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind),
            () => keybind.DynamicFOVKeybind, bindingManager);
        uiManager.CC_FOVColor = FOVConfig.AddColorChanger("FOV Color");
        uiManager.CC_FOVColor.ColorChangingBorder.Opacity = 0.2;
        uiManager.CC_FOVColor.ColorChangingBorder.Background =
            (Brush)new BrushConverter().ConvertFromString(AppConfig.Current.ColorState.FOVColor);
        uiManager.CC_FOVColor.Reader.Click += (s, x) =>
        {
            ColorDialog colorDialog = new();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                uiManager.CC_FOVColor.ColorChangingBorder.Background = new SolidColorBrush(
                    Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                AppConfig.Current.ColorState.FOVColor = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R,
                    colorDialog.Color.G, colorDialog.Color.B).ToString();
                PropertyChanger.PostColor(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G,
                    colorDialog.Color.B));
            }
        };

        uiManager.S_FOVSize = FOVConfig.AddSlider("FOV Size", "Size", 1, 1, 10, 640);
        uiManager.S_FOVSize.Slider.ValueChanged += (s, x) =>
        {
            var FovSize = uiManager.S_FOVSize.Slider.Value;
            ActualFOV = FovSize;
            PropertyChanger.PostNewFOVSize(ActualFOV);
        };
        uiManager.S_DynamicFOVSize = FOVConfig.AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640);
        uiManager.S_DynamicFOVSize.Slider.ValueChanged += (s, x) =>
        {
            if (AppConfig.Current.ToggleState.DynamicFOV)
                PropertyChanger.PostNewFOVSize(uiManager.S_DynamicFOVSize.Slider.Value);
        };
        uiManager.S_EMASmoothing.Slider.ValueChanged += (s, x) =>
        {
            if (AppConfig.Current.ToggleState.EMASmoothening)
            {
                MouseManager.smoothingFactor = uiManager.S_EMASmoothing.Slider.Value;
                Debug.WriteLine(MouseManager.smoothingFactor);
            }
        };
        uiManager.S_FOVOpacity = FOVConfig.AddSlider("FOV Opacity", "FOV Opacity", 0.1, 0.1, 0, 1);
        uiManager.S_FOVOpacity.Slider.ValueChanged += (s, x) =>
        {
            uiManager.CC_FOVColor.ColorChangingBorder.Opacity = x.NewValue;
            AppConfig.Current.SliderSettings.FOVOpacity = x.NewValue;
            PropertyChanger.PostOpacity(x.NewValue);
        };

        FOVConfig.AddSeparator();
        FOVConfig.Visibility = GetVisibilityFor("FOVConfig");

        #endregion FOV Config

        #region ESP Config

        uiManager.AT_DetectedPlayer = ESPConfig.AddTitle("ESP Config", true);
        uiManager.T_ShowDetectedPlayer = ESPConfig.AddToggle("Show Detected Player");
        uiManager.T_ShowHeadArea = ESPConfig.AddToggle("Show Trigger Head Area");
        uiManager.T_ShowAIConfidence = ESPConfig.AddToggle("Show AI Confidence");
        uiManager.T_ShowTracers = ESPConfig.AddToggle("Show Tracers");
        uiManager.CC_DetectedPlayerColor = ESPConfig.AddColorChanger("Detected Player Color");
        uiManager.CC_DetectedPlayerColor.ColorChangingBorder.Background =
            (Brush)new BrushConverter().ConvertFromString(AppConfig.Current.ColorState.DetectedPlayerColor);
        uiManager.CC_DetectedPlayerColor.Reader.Click += (s, x) =>
        {
            ColorDialog colorDialog = new();
            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                uiManager.CC_DetectedPlayerColor.ColorChangingBorder.Background = new SolidColorBrush(
                    Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B));
                AppConfig.Current.ColorState.DetectedPlayerColor = Color.FromArgb(colorDialog.Color.A,
                    colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B).ToString();
                PropertyChanger.PostDPColor(Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R,
                    colorDialog.Color.G, colorDialog.Color.B));
            }
        };

        uiManager.S_DPFontSize = ESPConfig.AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30);
        uiManager.S_DPFontSize.Slider.ValueChanged += (s, x) =>
            PropertyChanger.PostDPFontSize((int)uiManager.S_DPFontSize.Slider.Value);

        uiManager.S_DPCornerRadius = ESPConfig.AddSlider("Corner Radius", "Radius", 1, 1, 0, 100);
        uiManager.S_DPCornerRadius.Slider.ValueChanged += (s, x) =>
            PropertyChanger.PostDPWCornerRadius((int)uiManager.S_DPCornerRadius.Slider.Value);

        uiManager.S_DPBorderThickness = ESPConfig.AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10);
        uiManager.S_DPBorderThickness.Slider.ValueChanged += (s, x) =>
            PropertyChanger.PostDPWBorderThickness(uiManager.S_DPBorderThickness.Slider.Value);

        uiManager.S_DPOpacity = ESPConfig.AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1);

        ESPConfig.AddSeparator();
        ESPConfig.Visibility = GetVisibilityFor("ESPConfig");

        #endregion ESP Config
    }

    private void LoadGamepadSettingsMenu()
    {
        GamepadSettingsConfig.AddTitle("Gamepad Settings");
        GamepadSettingsConfig.AddCredit("Target Process",
            "In order to use the Gamepad to send actions or AIM you need to select the process where the commands should be send to");
        GamepadSettingsConfig.Add<AProcessPicker>(picker =>
        {
            picker.SelectedProcessModel = new ProcessModel { Title = AppConfig.Current.DropdownState.GamepadProcess };
            picker.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(picker.SelectedProcessModel))
                    AppConfig.Current.DropdownState.GamepadProcess = picker.SelectedProcessModel.Title;
            };
        });
        GamepadSettingsConfig.AddSeparator();
    }

    private void LoadSettingsMenu()
    {
        uiManager.AT_SettingsMenu = SettingsConfig.AddTitle("Settings Menu", true);

        uiManager.T_CollectDataWhilePlaying = SettingsConfig.AddToggle("Collect Data While Playing");
        uiManager.T_AutoLabelData = SettingsConfig.AddToggle("Auto Label Data");
        uiManager.D_MouseMovementMethod = SettingsConfig.AddDropdown("Mouse Movement Method",
            AppConfig.Current.DropdownState.MouseMovementMethod, async v =>
            {
                AppConfig.Current.DropdownState.MouseMovementMethod = v;
                if ((v == MouseMovementMethod.LGHUB && !new LGHubMain().Load())
                    || (v == MouseMovementMethod.RazerSynapse && !await RZMouse.Load())
                    || (v == MouseMovementMethod.ddxoft && !await DdxoftMain.Load())
                   )
                    SelectMouseEvent();
            });

        uiManager.S_AIMinimumConfidence =
            SettingsConfig.AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100);
        uiManager.S_AIMinimumConfidence.Slider.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            if (uiManager.S_AIMinimumConfidence.Slider.Value >= 95)
                new NoticeBar(
                    "The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.",
                    10000).Show();
            else if (uiManager.S_AIMinimumConfidence.Slider.Value <= 35)
                new NoticeBar("The minimum confidence you have set for Aimmy may be too low can cause false positives.",
                    10000).Show();
        };

        uiManager.S_MinimumLT = SettingsConfig.AddSlider("Gamepad Minimum LT", "LT", 0.1, 0.1, 0.1, 1);
        uiManager.S_MinimumRT = SettingsConfig.AddSlider("Gamepad Minimum RT", "RT", 0.1, 0.1, 0.1, 1);

        uiManager.T_MouseBackgroundEffect = SettingsConfig.AddToggle("Mouse Background Effect");
        uiManager.T_UITopMost = SettingsConfig.AddToggle("UI TopMost");
        uiManager.B_SaveConfig = SettingsConfig.AddButton("Save Config");
        uiManager.B_SaveConfig.Reader.Click += (s, e) => new ConfigSaver().ShowDialog();

        SettingsConfig.AddSeparator();

        // X/Y Percentage Adjustment Enabler
        uiManager.AT_XYPercentageAdjustmentEnabler =
            XYPercentageEnablerMenu.AddTitle("X/Y Percentage Adjustment", true);
        uiManager.T_XAxisPercentageAdjustment = XYPercentageEnablerMenu.AddToggle("X Axis Percentage Adjustment");
        uiManager.T_YAxisPercentageAdjustment = XYPercentageEnablerMenu.AddToggle("Y Axis Percentage Adjustment");
        XYPercentageEnablerMenu.AddSeparator();

        // ddxoft Menu
        //AddTitle(SSP2, "ddxoft Configurator");
        //uiManager.AFL_ddxoftDLLLocator = AddFileLocator(SSP2, "ddxoft DLL Location", "ddxoft dll (*.dll)|*.dll");
        //AddSeparator(SSP2);
    }

    private void LoadCreditsMenu()
    {
        CreditsPanel.AddTitle("Developers");
        CreditsPanel.AddCredit("Babyhamsta", "AI Logic");
        CreditsPanel.AddCredit("MarsQQ", "Design");
        CreditsPanel.AddCredit("Taylor", "Optimization, Cleanup");
        CreditsPanel.AddCredit("Florian Gilde", "Optimization, Cleanup, Trigger Bot improvements, Gamepad support");
        CreditsPanel.AddSeparator();

        CreditsPanel.AddTitle("Contributors");
        CreditsPanel.AddCredit("Shall0e", "Prediction Method");
        CreditsPanel.AddCredit("wisethef0x", "EMA Prediction Method");
        CreditsPanel.AddCredit("whoswhip", "Bug fixes & EMA");
        CreditsPanel.AddCredit("HakaCat", "Idea for Auto Labelling Data");
        CreditsPanel.AddCredit("Themida", "LGHub check");
        CreditsPanel.AddCredit("Ninja", "MarsQQ's emotional support");
        CreditsPanel.AddSeparator();

        CreditsPanel.AddTitle("Model Creators");
        CreditsPanel.AddCredit("Babyhamsta", "UniversalV4, Phantom Forces");
        CreditsPanel.AddCredit("Natdog400", "AIO V2, V7");
        CreditsPanel.AddCredit("Themida", "Arsenal, Strucid, Bad Business, Blade Ball, etc.");
        CreditsPanel.AddCredit("Hogthewog", "Da Hood, FN, etc.");
        CreditsPanel.AddSeparator();
    }

    public async Task LoadStoreMenu()
    {
        try
        {
            Task models = FileManager.RetrieveAndAddFiles(
                "https://api.github.com/repos/Babyhamsta/Aimmy/contents/models", "bin\\models", AvailableModels);
            Task configs = FileManager.RetrieveAndAddFiles(
                "https://api.github.com/repos/Babyhamsta/Aimmy/contents/configs", "bin\\configs", AvailableConfigs);

            await Task.WhenAll(models, configs);
        }
        catch (Exception e)
        {
            new NoticeBar(e.Message, 10000).Show();
            return;
        }

        Application.Current.Dispatcher.Invoke(() =>
        {
            DownloadGateway(ModelStoreScroller, AvailableModels, "models");
            DownloadGateway(ConfigStoreScroller, AvailableConfigs, "configs");
        });
    }

    private void DownloadGateway(StackPanel Scroller, HashSet<string> entries, string folder)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Scroller.Children.Clear();

            if (entries.Count > 0)
            {
                foreach (var entry in entries)
                {
                    ADownloadGateway gateway = new(entry, folder);
                    Scroller.Children.Add(gateway);
                }
            }
            else
            {
                LackOfConfigsText.Visibility = Visibility.Visible;
                LackOfModelsText.Visibility = Visibility.Visible;
            }
        });
    }

    #endregion Menu Loading

    #region Menu Minizations

    // TODO: Refactor this region completely its also bullshit
    private void ToggleAimMenu()
    {
        SetMenuVisibility(AimAssist, !AppConfig.Current.MinimizeState.AimAssist);
    }

    private void ToggleAimConfig()
    {
        SetMenuVisibility(AimConfig, !AppConfig.Current.MinimizeState.AimConfig);
    }

    private void ToggleAutoTrigger()
    {
        SetMenuVisibility(TriggerBot, !AppConfig.Current.MinimizeState.AutoTrigger);
    }

    private void ToggleAntiRecoilMenu()
    {
        SetMenuVisibility(AntiRecoil, !AppConfig.Current.MinimizeState.AntiRecoil);
    }

    private void ToggleAntiRecoilConfigMenu()
    {
        SetMenuVisibility(ARConfig, !AppConfig.Current.MinimizeState.AntiRecoilConfig);
    }

    private void ToggleFOVConfigMenu()
    {
        SetMenuVisibility(FOVConfig, !AppConfig.Current.MinimizeState.FOVConfig);
    }

    private void ToggleESPConfigMenu()
    {
        SetMenuVisibility(ESPConfig, !AppConfig.Current.MinimizeState.ESPConfig);
    }

    private void ToggleSettingsMenu()
    {
        SetMenuVisibility(SettingsConfig, !AppConfig.Current.MinimizeState.SettingsMenu);
    }

    private void ToggleXYPercentageAdjustmentEnabler()
    {
        SetMenuVisibility(XYPercentageEnablerMenu, !AppConfig.Current.MinimizeState.XYPercentageAdjustment);
    }

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
            if (!(child is ATitle || child is ASpacer || child is ARectangleBottom))
                child.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            else
                child.Visibility = Visibility.Visible;
    }

    #endregion Menu Minizations

    #region Config Loader

    private void LoadConfig(string path = AppConfig.DefaultConfigPath, bool loading_from_configlist = false)
    {
        AppConfig.Load(path);
        // TODO: Refactor this method
        
        try
        {
            if (loading_from_configlist)
            {
                if (AppConfig.Current.SuggestedModelName != "N/A")
                    MessageBox.Show(
                        "The creator of this model suggests you use this model:\n" +
                        AppConfig.Current.SuggestedModelName, "Suggested Model - Aimmy"
                    );

                //uiManager.S_FireRate!.Slider.Value = GetValueOrDefault(Dictionary.sliderSettings, "Fire Rate", 1);

                //uiManager.S_FOVSize!.Slider.Value = GetValueOrDefault(Dictionary.sliderSettings, "FOV Size", 640);

                //uiManager.S_MouseSensitivity!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "Mouse Sensitivity (+/-)", 0.8);
                //uiManager.S_MouseJitter!.Slider.Value = GetValueOrDefault(Dictionary.sliderSettings, "Mouse Jitter", 0);

                //uiManager.S_YOffset!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "Y Offset (Up/Down)", 0);
                //uiManager.S_XOffset!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "X Offset (Left/Right)", 0);

                //uiManager.S_AutoTriggerDelay!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "Auto Trigger Delay", .25);
                //uiManager.S_AIMinimumConfidence!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "AI Minimum Confidence", 50);
                //uiManager.S_MinimumLT!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "Gamepad Minimum LT", 0.7);
                //uiManager.S_MinimumRT!.Slider.Value =
                //    GetValueOrDefault(Dictionary.sliderSettings, "Gamepad Minimum RT", 0.7);
            }
        }
        catch (Exception e)
        {
            MessageBox.Show($"Error loading config, possibly outdated\n{e}");
        }
    }

    #endregion Config Loader

    #region Anti Recoil Config Loader

    private void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg",
        bool loading_outside_startup = false)
    {
        // TODO: Refactor this method
        //if (File.Exists(path))
        //{
        //    SaveDictionary.LoadJSON(Dictionary.AntiRecoilSettings, path);
        //    try
        //    {
        //        if (loading_outside_startup)
        //        {
        //            uiManager.S_HoldTime!.Slider.Value = Dictionary.AntiRecoilSettings["Hold Time"];

        //            uiManager.S_FireRate!.Slider.Value = Dictionary.AntiRecoilSettings["Fire Rate"];

        //            uiManager.S_YAntiRecoilAdjustment!.Slider.Value =
        //                Dictionary.AntiRecoilSettings["Y Recoil (Up/Down)"];
        //            uiManager.S_XAntiRecoilAdjustment!.Slider.Value =
        //                Dictionary.AntiRecoilSettings["X Recoil (Left/Right)"];
        //            new NoticeBar($"[Anti Recoil] Loaded \"{path}\"", 2000).Show();
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception($"Error loading config, possibly outdated\n{e}");
        //    }
        //}
        //else
        //{
        //    new NoticeBar("[Anti Recoil] Config not found.", 5000).Show();
        //}
    }

    #endregion Anti Recoil Config Loader

    #region Open Folder

    private void OpenFolderB_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button clickedButton)
            Process.Start("explorer.exe", Path.Combine(Directory.GetCurrentDirectory(), "bin", clickedButton.Tag.ToString()));
    }

    #endregion Open Folder

    #region Menu Functions

    private async void SelectMouseEvent()
    {
        await Task.Delay(500);
        uiManager.D_MouseMovementMethod!.DropdownBox.SelectedIndex = 0;
    }


    #endregion Menu Functions

    #region System Information

    private static string? GetProcessorName()
    {
        return GetSpecs.GetSpecification("Win32_Processor", "Name");
    }

    private static string? GetVideoControllerName()
    {
        return GetSpecs.GetSpecification("Win32_VideoController", "Name");
    }

    private static string? GetFormattedMemorySize()
    {
        var totalMemorySize = long.Parse(GetSpecs.GetSpecification("CIM_OperatingSystem", "TotalVisibleMemorySize")!);
        return Math.Round(totalMemorySize / (1024.0 * 1024.0), 0).ToString();
    }

    #endregion System Information

    #region Fancy UI Calculations

    private double currentGradientAngle;

    private double CalculateAngleDifference(double targetAngle, double fullCircle, double halfCircle, double clamp)
    {
        var angleDifference = (targetAngle - currentGradientAngle + fullCircle) % fullCircle;
        if (angleDifference > halfCircle) angleDifference -= fullCircle;
        return Math.Max(Math.Min(angleDifference, clamp), -clamp);
    }

    #endregion Fancy UI Calculations

    #region Window Handling

    private static void ShowHideDPWindow()
    {
        if (!AppConfig.Current.ToggleState.ShowDetectedPlayer) DPWindow.Hide();
        else DPWindow.Show();
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var updateManager = new UpdateManager();
        await updateManager.CheckForUpdate("v2.2.0");
        updateManager.Dispose();
    }

    #endregion Window Handling


    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == nameof(IsModelLoaded))
            OnPropertyChanged(nameof(IsNotModelLoaded));
        base.OnPropertyChanged(propertyName);
    }

    public void CallPropertyChanged(string name)
    {
        OnPropertyChanged(name);
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        OutputTextBox.Clear();
    }
}