using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private InputBindingManager? bindingManager;
    private FileManager fileManager;
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
        AppConfig.ConfigLoaded += (s, e) => CreateUI();
        Console.WriteLine("Init UI");

        GamepadManager.Init();

        Config = AppConfig.Load();


        DataContext = this;

        LoadLastModel();

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

    private void CreateUI()
    {
        CurrentScrollViewer = FindName("AimMenu") as ScrollViewer;
        if (CurrentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        AppConfig.Current.DetectedPlayerOverlay = DPWindow = new();
        AppConfig.Current.FOVWindow = FOVWindow = new();

        fileManager = new FileManager(ModelListBox, SelectedModelNotifier, ConfigsListBox, SelectedConfigNotifier);

        // Needed to import annotations into MakeSense
        if (!File.Exists("bin\\labels\\labels.txt")) File.WriteAllText("bin\\labels\\labels.txt", "Enemy");

        arManager.HoldDownLoad();

        if (bindingManager != null)
        {
            bindingManager.OnBindingPressed -= BindingOnKeyPressed;
            bindingManager.OnBindingReleased -= BindingOnKeyReleased;
        }
        
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


        ActualFOV = AppConfig.Current.SliderSettings.FOVSize;
        
        bindingManager.OnBindingPressed += BindingOnKeyPressed;
        bindingManager.OnBindingReleased += BindingOnKeyReleased;
    }

    private void LoadLastModel()
    {
        var lastLoaded = Path.Combine("bin/models", Config.LastLoadedModel);
        var modelPath = File.Exists(lastLoaded) ? lastLoaded : Path.Combine("bin/models", ApplicationConstants.DefaultModel);
        if (File.Exists(modelPath) && !FileManager.CurrentlyLoadingModel &&
            FileManager.AIManager?.IsModelLoaded != true)
        {
            _ = fileManager.LoadModel(Path.GetFileName(modelPath), modelPath);
        }
    }


    public bool IsModelLoaded => FileManager.AIManager?.IsModelLoaded ?? false;
    public bool IsNotModelLoaded => !IsModelLoaded;

    private void LoadGlobalUI()
    {
        TopCenterGrid.RemoveAll();
        TopCenterGrid.AddToggle("Global Active", toggle =>
        {
            toggle.Changed += (s, e) => SetActive(e.Value);
        }).BindTo(() => AppConfig.Current.ToggleState.GlobalActive);
        TopCenterGrid.AddKeyChanger(
            nameof(AppConfig.Current.BindingSettings.ActiveToggleKey),
            () => AppConfig.Current.BindingSettings.ActiveToggleKey, bindingManager);

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


    private void BindingOnKeyReleased(string bindingId)
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
    }

    private void BindingOnKeyPressed(string bindingId)
    {
        switch (bindingId)
        {
            case nameof(AppConfig.Current.BindingSettings.ActiveToggleKey):
                if (IsModelLoaded)
                {
                    AppConfig.Current.ToggleState.GlobalActive = !AppConfig.Current.ToggleState.GlobalActive;
                    //UpdateToggleUI(uiManager.G_Active!, !uiManager.G_Active.Checked);
                }

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
    }

    #endregion Menu Logic

    #region Menu Loading

    private void LoadAimMenu()
    {
        AimAssist.RemoveAll();
        AimConfig.RemoveAll();
        TriggerBot.RemoveAll();
        AntiRecoil.RemoveAll();
        ARConfig.RemoveAll();
        FOVConfig.RemoveAll();
        ESPConfig.RemoveAll();

        #region Aim Assist

        var keybind = AppConfig.Current.BindingSettings;
        AimAssist.AddTitle("Aim Assist", true);
        AimAssist.AddToggle("Aim Assist").BindTo(() => AppConfig.Current.ToggleState.AimAssist);


        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AimKeybind), () => keybind.AimKeybind, bindingManager);
        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind), () => keybind.SecondAimKeybind, bindingManager);
        AimAssist.AddToggle("Constant AI Tracking").BindTo(() => AppConfig.Current.ToggleState.ConstantAITracking);

        AimAssist.AddToggle("Predictions").BindTo(() => AppConfig.Current.ToggleState.Predictions);
        AimAssist.AddToggle("EMA Smoothening").BindTo(() => AppConfig.Current.ToggleState.EMASmoothening);
        AimAssist.AddToggle("Enable Model Switch Keybind").BindTo(() => AppConfig.Current.ToggleState.EnableModelSwitchKeybind);
        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), () => keybind.ModelSwitchKeybind, bindingManager);
        AimAssist.AddSeparator();
        AimAssist.Visibility = GetVisibilityFor("AimAssist");

        #endregion Aim Assist

        #region Config

        AimConfig.AddTitle("Aim Config", true);
        AimConfig.AddDropdown("Prediction Method", AppConfig.Current.DropdownState.PredictionMethod, v => AppConfig.Current.DropdownState.PredictionMethod = v);
        AimConfig.AddDropdown("Detection Area Type",
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


        AimConfig.AddDropdown("Aiming Boundaries Alignment", AppConfig.Current.DropdownState.AimingBoundariesAlignment, v => AppConfig.Current.DropdownState.AimingBoundariesAlignment = v);
        AimConfig.AddSlider("Mouse Sensitivity (+/-)", "Sensitivity", 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.MouseSensitivity);

        AimConfig.AddSlider("Mouse Jitter", "Jitter", 1, 1, 0, 15).BindTo(() => AppConfig.Current.SliderSettings.MouseJitter);

        AimConfig.AddSlider("Y Offset (Up/Down)", "Offset", 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.YOffset);
        AimConfig.AddSlider("Y Offset (%)", "Percent", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.YOffsetPercentage);

        AimConfig.AddSlider("X Offset (Left/Right)", "Offset", 1, 1, -150, 150).BindTo(() => AppConfig.Current.SliderSettings.XOffset);
        AimConfig.AddSlider("X Offset (%)", "Percent", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.XOffsetPercentage);

        AimConfig.AddSlider("EMA Smoothening", "Amount", 0.01, 0.01, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.EMASmoothening);

        AimConfig.AddSeparator();
        AimConfig.Visibility = GetVisibilityFor("AimConfig");

        #endregion Config

        #region Trigger Bot

        TriggerBot.AddTitle("Auto Trigger", true);
        TriggerBot.AddToggle("Auto Trigger").BindTo(() => AppConfig.Current.ToggleState.AutoTrigger);

        TriggerBot.AddKeyChanger("Trigger Additional Send", () => keybind.TriggerAdditionalSend, bindingManager);

        TriggerBot.AddDropdown("Trigger Check", AppConfig.Current.DropdownState.TriggerCheck,
            check => AppConfig.Current.DropdownState.TriggerCheck = check);

        TriggerBot.AddButton("Configure Head Area", b =>
        {
            Config.DropdownState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Config.DropdownState.TriggerCheck))
                {
                    b.Visibility = AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            };
            b.Visibility = AppConfig.Current.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter
                ? Visibility.Visible
                : Visibility.Collapsed;
            b.ToolTip = "Specify the area of the Head when this interaction center the trigger will be executed";
        }).Reader.Click += (s, e) => 
            new EditHeadArea(AppConfig.Current.DropdownState.HeadArea).Show();


        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerKey),
            () => keybind.TriggerKey, bindingManager);
        TriggerBot.AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.01, 1).BindTo(() => AppConfig.Current.SliderSettings.AutoTriggerDelay);
        TriggerBot.AddSeparator();
        TriggerBot.Visibility = GetVisibilityFor("TriggerBot");

        #endregion Trigger Bot

        #region Anti Recoil

        AntiRecoil.AddTitle("Anti Recoil", true);
        AntiRecoil.AddToggle("Anti Recoil").BindTo(() => AppConfig.Current.ToggleState.AntiRecoil);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), "Left", bindingManager);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), "Oem6", bindingManager);
        AntiRecoil.AddSlider("Hold Time", "Milliseconds", 1, 1, 1, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.HoldTime);
        AntiRecoil.AddButton("Record Fire Rate").Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        AntiRecoil.AddSlider("Fire Rate", "Milliseconds", 1, 1, 1, 5000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.FireRate); 
        AntiRecoil.AddSlider("Y Recoil (Up/Down)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.YRecoil);
        AntiRecoil.AddSlider("X Recoil (Left/Right)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.XRecoil);
        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor("AntiRecoil");

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config
        ARConfig.AddTitle("Anti Recoil Config", true);
        ARConfig.AddToggle("Enable Gun Switching Keybind").BindTo(() => AppConfig.Current.ToggleState.EnableGunSwitchingKeybind);
        ARConfig.AddButton("Save Anti Recoil Config").Reader.Click += (s, e) =>
        {
            var saveFileDialog = new SaveFileDialog
            {
                InitialDirectory = $"{Directory.GetCurrentDirectory}\\bin\\anti_recoil_configs",
                Filter = "Aimmy Style Recoil Config (*.cfg)|*.cfg"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                AppConfig.Current.AntiRecoilSettings.Save<AntiRecoilSettings>(saveFileDialog.FileName);
                new NoticeBar($"[Anti Recoil] Config has been saved to \"{saveFileDialog.FileName}\"", 2000).Show();
            }
        };
        ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun1Key), "D1", bindingManager);
        ARConfig.AddFileLocator("Gun 1 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");
        ARConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.Gun2Key), "D2", bindingManager);
        ARConfig.AddFileLocator("Gun 2 Config", "Aimmy Style Recoil Config (*.cfg)|*.cfg", "\\bin\\anti_recoil_configs");

        ARConfig.AddButton("Load Gun 1 Config").Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun1Config, true);
        ARConfig.AddButton("Load Gun 2 Config").Reader.Click +=
            (s, e) => LoadAntiRecoilConfig(AppConfig.Current.FileLocationState.Gun2Config, true);
        ARConfig.AddSeparator();
        ARConfig.Visibility = GetVisibilityFor("ARConfig");

        #endregion Anti Recoil Config

        #region FOV Config

        FOVConfig.AddTitle("FOV Config", true);
        FOVConfig.AddToggle("FOV").BindTo(() => AppConfig.Current.ToggleState.FOV);
        FOVConfig.AddToggle("Dynamic FOV").BindTo(() => AppConfig.Current.ToggleState.DynamicFOV);
        FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), () => keybind.DynamicFOVKeybind, bindingManager);
        FOVConfig.AddColorChanger("FOV Color").BindTo(() => AppConfig.Current.ColorState.FOVColor);

        uiManager.S_FOVSize = FOVConfig.AddSlider("FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.FOVSize);
        uiManager.S_FOVSize.Slider.ValueChanged += (s, x) =>
        {
            ActualFOV = uiManager.S_FOVSize.Slider.Value;
        };
        FOVConfig.AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.DynamicFOVSize);
        FOVConfig.AddSlider("FOV Opacity", "FOV Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.FOVOpacity);

        FOVConfig.AddSeparator();
        FOVConfig.Visibility = GetVisibilityFor("FOVConfig");

        #endregion FOV Config

        #region ESP Config

        uiManager.AT_DetectedPlayer = ESPConfig.AddTitle("ESP Config", true);
        ESPConfig.AddToggle("Show Detected Player").BindTo(() => AppConfig.Current.ToggleState.ShowDetectedPlayer);
        ESPConfig.AddToggle("Show Trigger Head Area").BindTo(() => AppConfig.Current.ToggleState.ShowTriggerHeadArea); 
        ESPConfig.AddToggle("Show AI Confidence").BindTo(() => AppConfig.Current.ToggleState.ShowAIConfidence);
        ESPConfig.AddToggle("Show Tracers").BindTo(() => AppConfig.Current.ToggleState.ShowTracers); 
        ESPConfig.AddColorChanger("Detected Player Color").BindTo(() => AppConfig.Current.ColorState.DetectedPlayerColor);


        ESPConfig.AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30).BindTo(() => AppConfig.Current.SliderSettings.AIConfidenceFontSize);
        
        ESPConfig.AddSlider("Corner Radius", "Radius", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.CornerRadius);

        ESPConfig.AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10).BindTo(() => AppConfig.Current.SliderSettings.BorderThickness);

        ESPConfig.AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.Opacity); 

        ESPConfig.AddSeparator();
        ESPConfig.Visibility = GetVisibilityFor("ESPConfig");

        #endregion ESP Config
    }

    private void LoadGamepadSettingsMenu()
    {
        GamepadSettingsConfig.RemoveAll();
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
        SettingsConfig.RemoveAll();
        uiManager.AT_SettingsMenu = SettingsConfig.AddTitle("Settings Menu", true);

        SettingsConfig.AddToggle("Collect Data While Playing").BindTo(() => AppConfig.Current.ToggleState.CollectDataWhilePlaying);
        SettingsConfig.AddToggle("Auto Label Data").BindTo(() => AppConfig.Current.ToggleState.AutoLabelData);
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

        SettingsConfig.AddSlider("AI Minimum Confidence", "% Confidence", 1, 1, 1, 100).BindTo(() => AppConfig.Current.SliderSettings.AIMinimumConfidence).Slider.PreviewMouseLeftButtonUp += (sender, e) =>
        {
            switch (AppConfig.Current.SliderSettings.AIMinimumConfidence)
            {
                case >= 95:
                    new NoticeBar(
                        "The minimum confidence you have set for Aimmy to be too high and may be unable to detect players.",
                        10000).Show();
                    break;
                case <= 35:
                    new NoticeBar("The minimum confidence you have set for Aimmy may be too low can cause false positives.",
                        10000).Show();
                    break;
            }
        };

        SettingsConfig.AddSlider("Gamepad Minimum LT", "LT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumLT); 
        SettingsConfig.AddSlider("Gamepad Minimum RT", "RT", 0.1, 0.1, 0.1, 1).BindTo(() => AppConfig.Current.SliderSettings.GamepadMinimumRT);

        SettingsConfig.AddToggle("Mouse Background Effect").BindTo(() => AppConfig.Current.ToggleState.MouseBackgroundEffect); 
        SettingsConfig.AddToggle("UI TopMost").BindTo(() => AppConfig.Current.ToggleState.UITopMost);
        SettingsConfig.AddButton("Save Config").Reader.Click += (s, e) => new ConfigSaver().ShowDialog();

        SettingsConfig.AddSeparator();

        // X/Y Percentage Adjustment Enabler
        
        XYPercentageEnablerMenu.AddTitle("X/Y Percentage Adjustment", true);
        XYPercentageEnablerMenu.AddToggle("X Axis Percentage Adjustment").BindTo(() => AppConfig.Current.ToggleState.XAxisPercentageAdjustment);
        XYPercentageEnablerMenu.AddToggle("Y Axis Percentage Adjustment").BindTo(() => AppConfig.Current.ToggleState.YAxisPercentageAdjustment); 
        XYPercentageEnablerMenu.AddSeparator();

        // ddxoft Menu
        //AddTitle(SSP2, "ddxoft Configurator");
        //uiManager.AFL_ddxoftDLLLocator = AddFileLocator(SSP2, "ddxoft DLL Location", "ddxoft dll (*.dll)|*.dll");
        //AddSeparator(SSP2);
    }

    private void LoadCreditsMenu()
    {
        CreditsPanel.RemoveAll();
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


    #region Config Loader

    private void LoadConfig(string path = AppConfig.DefaultConfigPath, bool loading_from_configlist = false)
    {
        AppConfig.Load(path);
        if (loading_from_configlist)
        {
            if (!string.IsNullOrEmpty(AppConfig.Current.SuggestedModelName) && AppConfig.Current.SuggestedModelName != "N/A")
                MessageBox.Show("The creator of this model suggests you use this model:\n" + AppConfig.Current.SuggestedModelName, "Suggested Model - Aimmy");
        }
    }

    #endregion Config Loader

    #region Anti Recoil Config Loader

    private void LoadAntiRecoilConfig(string path = "bin\\anti_recoil_configs\\Default.cfg",
        bool loading_outside_startup = false)
    {
        AppConfig.Current.AntiRecoilSettings.Load<AntiRecoilSettings>(path);
        if (loading_outside_startup)
            CreateUI();
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