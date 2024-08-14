using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using Aimmy2.Class;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.HidHide;
using Aimmy2.Models;
using Aimmy2.MouseMovementLibraries.GHubSupport;
using Aimmy2.Other;
using Aimmy2.Types;
using Aimmy2.UILibrary;
using AimmyWPF.Class;
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

    private bool _uiCreated;
    private InputBindingManager? bindingManager;
    private FileManager fileManager;
    private static GithubManager githubManager = new();
    public AntiRecoilManager arManager = new();


    private bool CurrentlySwitching;
    private ScrollViewer? CurrentScrollViewer;

    private readonly HashSet<string> AvailableModels = new();
    private readonly HashSet<string> AvailableConfigs = new();

    #endregion Main Variables

    #region Loading Window

    public AppConfig? Config { get; }

    public MainWindow()
    {
        InitializeComponent();
        var writer = new TextBoxStreamWriter(OutputTextBox);
        Console.SetOut(writer);
        AppConfig.ConfigLoaded += (s, e) => CreateUI();
        Console.WriteLine("Init UI");

        try
        {
            GamepadManager.Init();
        }
        catch
        {}

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
        var theme = ThemePalette.All.FirstOrDefault(x => x.Name == AppConfig.Current.ThemeName) ?? ThemePalette.PurplePalette;
        ApplicationConstants.Theme = theme;

        CurrentScrollViewer = FindName("AimMenu") as ScrollViewer;
        if (CurrentScrollViewer == null) throw new NullReferenceException("CurrentScrollViewer is null");

        FOV.Create();

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
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey),
            AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey);
        bindingManager.SetupDefault(nameof(AppConfig.Current.BindingSettings.RapidFireKey),
            AppConfig.Current.BindingSettings.RapidFireKey);

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

        bindingManager.OnBindingPressed += BindingOnKeyPressed;
        bindingManager.OnBindingReleased += BindingOnKeyReleased;
        _uiCreated = true;
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
        TopCenterGrid.Add(new CaptureSourceSelect(), select =>
        {
            select.Selected += (sender, source) =>
            {
                FileManager.AIManager?.Dispose();
                FileManager.AIManager = null;
                FileManager.CurrentlyLoadingModel = false;
                LoadLastModel();
            };
        });
        TopCenterGrid.AddToggleWithKeyBind("Global Active", bindingManager, toggle =>
        {
            toggle.BindTo(() => AppConfig.Current.ToggleState.GlobalActive);
            toggle.Changed += (s, e) => SetActive(e.Value);
        }, border => border.Background = Brushes.Transparent);
    }

    public void SetActive(bool active)
    {
        AppConfig.Current.ToggleState.GlobalActive = active;
        var theme = ThemePalette.All.FirstOrDefault(x => x.Name == AppConfig.Current.ThemeName) ?? ThemePalette.PurplePalette;
        var themeActive = ThemePalette.ThemeForActive;
        ApplicationConstants.Theme = active ? themeActive : theme;
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


        FOV.Instance?.Close();

        if (AppConfig.Current.DropdownState.MouseMovementMethod == MouseMovementMethod.LGHUB) LGMouse.Close();

        AppConfig.Current.Save();
        GamepadManager.Dispose();
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
            var buttonIndx = MenuButtons.Children.IndexOf(clickedButton);
            var margin = buttonIndx * Menu1B.Height;
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), MenuHighlighter, MenuHighlighter.Margin, new Thickness(0, margin, 0, 0));
            await SwitchScrollPanels(FindName(clickedButton.Tag.ToString()) as ScrollViewer ??
                                     throw new NullReferenceException("Scrollpanel is null"));
            CurrentMenu = clickedButton.Tag.ToString()!;
        }
    }

    private async Task SwitchScrollPanels(ScrollViewer movingScrollViewer)
    {
        movingScrollViewer.Visibility = Visibility.Visible;
        Animator.Fade(movingScrollViewer);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(350), movingScrollViewer, movingScrollViewer.Margin,
            new Thickness(50, 50, 0, 0));

        Animator.FadeOut(CurrentScrollViewer!);
        Animator.ObjectShift(TimeSpan.FromMilliseconds(350), CurrentScrollViewer!, CurrentScrollViewer!.Margin,
            new Thickness(50, 450, 0, -400));
        await Task.Delay(350);

        CurrentScrollViewer.Visibility = Visibility.Collapsed;
        CurrentScrollViewer = movingScrollViewer;
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
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.FOVSize);
                Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.FOVSize);
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
                AppConfig.Current.SliderSettings.OnPropertyChanged(nameof(AppConfig.Current.SliderSettings.ActualFovSize));
                Animator.WidthShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualWidth, AppConfig.Current.SliderSettings.ActualFovSize);
                Animator.HeightShift(TimeSpan.FromMilliseconds(500), FOV.Instance.Circle, FOV.Instance.Circle.ActualHeight, AppConfig.Current.SliderSettings.ActualFovSize);
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
        AimAssist.AddToggleWithKeyBind("Aim Assist", bindingManager).BindTo(() => AppConfig.Current.ToggleState.AimAssist).BindActiveStateColor(AimAssist);
        

        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AimKeybind), () => keybind.AimKeybind, bindingManager);
        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.SecondAimKeybind), () => keybind.SecondAimKeybind, bindingManager);
        AimAssist.AddToggle("Constant AI Tracking").BindTo(() => AppConfig.Current.ToggleState.ConstantAITracking);

        AimAssist.AddToggle("Predictions").BindTo(() => AppConfig.Current.ToggleState.Predictions);
        AimAssist.AddToggle("EMA Smoothening").BindTo(() => AppConfig.Current.ToggleState.EMASmoothening);
        AimAssist.AddToggle("Enable Model Switch Keybind").BindTo(() => AppConfig.Current.ToggleState.EnableModelSwitchKeybind);
        AimAssist.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.ModelSwitchKeybind), () => keybind.ModelSwitchKeybind, bindingManager);
        AimAssist.AddSeparator();
        AimAssist.Visibility = GetVisibilityFor(nameof(AimAssist));

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
                    await FOV.Instance.UpdateStrictEnclosure();
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
        AimConfig.Visibility = GetVisibilityFor(nameof(AimConfig));

        #endregion Config

        #region Rapid

        RapidFire.AddTitle("Rapid Fire", true);
        RapidFire.AddToggleWithKeyBind("Rapid Fire", bindingManager).BindTo(() => AppConfig.Current.ToggleState.RapidFire).BindActiveStateColor(RapidFire);
        RapidFire.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.RapidFireKey), () => keybind.RapidFireKey, bindingManager);
        RapidFire.AddSeparator();
        RapidFire.Visibility = GetVisibilityFor(nameof(RapidFire));

        #endregion

        #region Trigger Bot

        TriggerBot.AddTitle("Auto Trigger", true);

        TriggerBot.AddToggleWithKeyBind("Auto Trigger", bindingManager).BindTo(() => AppConfig.Current.ToggleState.AutoTrigger).BindActiveStateColor(TriggerBot);
        
        TriggerBot.AddToggleWithKeyBind("Charge Mode", bindingManager, null, b => b.ToolTip = "If this is on, mouse will be clicked down when enemy is detected and trigger key is hold and then released when configured area is reached")
            .BindTo(() => AppConfig.Current.ToggleState.AutoTriggerCharged);
        

        TriggerBot.AddDropdown("Trigger Check", AppConfig.Current.DropdownState.TriggerCheck,
            check => AppConfig.Current.DropdownState.TriggerCheck = check);

        TriggerBot.AddButton("Configure Head Area", b =>
        {
            Config.DropdownState.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Config.DropdownState.TriggerCheck))
                {
                    b.IsEnabled = Config.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter;
                }
            };
            b.IsEnabled = Config.DropdownState.TriggerCheck == TriggerCheck.HeadIntersectingCenter;
            b.ToolTip = "Specify the area of the Head when this interaction center the trigger will be executed";
        }).Reader.Click += (s, e) =>
            new EditHeadArea(AppConfig.Current.DropdownState.HeadArea).Show();


        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerKey), () => keybind.TriggerKey, bindingManager);
        TriggerBot.AddSlider("Min Time Trigger Key", "Seconds", 0.01, 0.1, 0.0, 5).InitWith(slider =>
        {
            Config.BindingSettings.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(Config.BindingSettings.TriggerKey))
                {
                    slider.IsEnabled = InputBindingManager.IsValidKey(Config.BindingSettings.TriggerKey);
                }
            };
            slider.IsEnabled = InputBindingManager.IsValidKey(Config.BindingSettings.TriggerKey);
            slider.ToolTip = "The minimum time the trigger key must be held down before the trigger is executed";
        }).BindTo(() => AppConfig.Current.SliderSettings.TriggerKeyMin);
        TriggerBot.AddSlider("Auto Trigger Delay", "Seconds", 0.01, 0.1, 0.00, 5).BindTo(() => AppConfig.Current.SliderSettings.AutoTriggerDelay);


        TriggerBot.AddKeyChanger("Trigger Additional Command", () => keybind.TriggerAdditionalSend, bindingManager);

        TriggerBot.AddDropdown("Trigger Additional Command Check", AppConfig.Current.DropdownState.TriggerAdditionalCommandCheck,
            check => AppConfig.Current.DropdownState.TriggerAdditionalCommandCheck = check);
        TriggerBot.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.TriggerAdditionalCommandKey), () => keybind.TriggerAdditionalCommandKey, bindingManager);


        TriggerBot.AddSeparator();
        TriggerBot.Visibility = GetVisibilityFor(nameof(TriggerBot));

        #endregion Trigger Bot

        #region Anti Recoil

        AntiRecoil.AddTitle("Anti Recoil", true);
        AntiRecoil.AddToggleWithKeyBind("Anti Recoil", bindingManager).BindTo(() => AppConfig.Current.ToggleState.AntiRecoil).BindActiveStateColor(AntiRecoil);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind), "Left", bindingManager);
        AntiRecoil.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DisableAntiRecoilKeybind), "Oem6", bindingManager);
        AntiRecoil.AddSlider("Hold Time", "Milliseconds", 1, 1, 1, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.HoldTime);
        AntiRecoil.AddButton("Record Fire Rate").Reader.Click += (s, e) => new SetAntiRecoil(this).Show();
        AntiRecoil.AddSlider("Fire Rate", "Milliseconds", 1, 1, 1, 5000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.FireRate);
        AntiRecoil.AddSlider("Y Recoil (Up/Down)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.YRecoil);
        AntiRecoil.AddSlider("X Recoil (Left/Right)", "Move", 1, 1, -1000, 1000, true).BindTo(() => AppConfig.Current.AntiRecoilSettings.XRecoil);
        AntiRecoil.AddSeparator();
        AntiRecoil.Visibility = GetVisibilityFor(nameof(AntiRecoil));

        #endregion Anti Recoil

        #region Anti Recoil Config

        // Anti-Recoil Config
        ARConfig.AddTitle("Anti Recoil Config", true);
        ARConfig.AddToggleWithKeyBind("Enable Gun Switching Keybind", bindingManager).BindTo(() => AppConfig.Current.ToggleState.EnableGunSwitchingKeybind).BindActiveStateColor(ARConfig);
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
        ARConfig.Visibility = GetVisibilityFor(nameof(ARConfig));

        #endregion Anti Recoil Config

        #region FOV Config

        FOVConfig.AddTitle("FOV Config", true);
        FOVConfig.AddToggleWithKeyBind("FOV", bindingManager).BindTo(() => AppConfig.Current.ToggleState.FOV).BindActiveStateColor(FOVConfig);
        FOVConfig.AddToggleWithKeyBind("Dynamic FOV", bindingManager).BindTo(() => AppConfig.Current.ToggleState.DynamicFOV);
        FOVConfig.AddKeyChanger(nameof(AppConfig.Current.BindingSettings.DynamicFOVKeybind), () => keybind.DynamicFOVKeybind, bindingManager);
        FOVConfig.AddColorChanger("FOV Color").BindTo(() => AppConfig.Current.ColorState.FOVColor);

        FOVConfig.AddSlider("FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.FOVSize);
        FOVConfig.AddSlider("Dynamic FOV Size", "Size", 1, 1, 10, 640).BindTo(() => AppConfig.Current.SliderSettings.DynamicFOVSize);
        FOVConfig.AddSlider("FOV Opacity", "FOV Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.FOVOpacity);

        FOVConfig.AddSeparator();
        FOVConfig.Visibility = GetVisibilityFor(nameof(FOVConfig));

        #endregion FOV Config

        #region ESP Config

        ESPConfig.AddTitle("ESP Config", true);
        ESPConfig.AddToggleWithKeyBind("Show Detected Player", bindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowDetectedPlayer).BindActiveStateColor(ESPConfig);
        ESPConfig.AddToggleWithKeyBind("Show Trigger Head Area", bindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTriggerHeadArea);
        ESPConfig.AddToggleWithKeyBind("Show AI Confidence", bindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowAIConfidence);
        ESPConfig.AddToggleWithKeyBind("Show Tracers", bindingManager).BindTo(() => AppConfig.Current.ToggleState.ShowTracers);

        ESPConfig.AddDropdown("Drawing Method", AppConfig.Current.DropdownState.OverlayDrawingMethod, v => AppConfig.Current.DropdownState.OverlayDrawingMethod = v);

        ESPConfig.AddColorChanger("Detected Player Color").BindTo(() => AppConfig.Current.ColorState.DetectedPlayerColor);


        ESPConfig.AddSlider("AI Confidence Font Size", "Size", 1, 1, 1, 30).BindTo(() => AppConfig.Current.SliderSettings.AIConfidenceFontSize);

        ESPConfig.AddSlider("Corner Radius", "Radius", 1, 1, 0, 100).BindTo(() => AppConfig.Current.SliderSettings.CornerRadius);

        ESPConfig.AddSlider("Border Thickness", "Thickness", 0.1, 1, 0.1, 10).BindTo(() => AppConfig.Current.SliderSettings.BorderThickness);

        ESPConfig.AddSlider("Opacity", "Opacity", 0.1, 0.1, 0, 1).BindTo(() => AppConfig.Current.SliderSettings.Opacity);

        ESPConfig.AddSeparator();
        ESPConfig.Visibility = GetVisibilityFor(nameof(ESPConfig));

        #endregion ESP Config
    }

    private void LoadGamepadSettingsMenu()
    {
        void Reload()
        {
            if (_uiCreated)
            {
                _uiCreated = false;
                LoadGamepadSettingsMenu();
                _uiCreated = true;
            }
        }

        string error = "";
        try
        {
            GamepadManager.Init();
        }
        catch (Exception e)
        {
            error = e.Message;
        }
        ButtonGamepadSettings.Foreground = !string.IsNullOrWhiteSpace(error) || !GamepadManager.CanSend ? Brushes.Red : Brushes.White;
        GamepadSettingsConfig.RemoveAll();
        GamepadSettingsConfig.AddTitle($"Gamepad Settings", false);

        GamepadSettingsConfig.AddDropdown("Gamepad Send Command mode", AppConfig.Current.DropdownState.GamepadSendMode, v =>
        {
            AppConfig.Current.DropdownState.GamepadSendMode = v;
            Reload();
        });

        if (!string.IsNullOrEmpty(error))
        {
            GamepadSettingsConfig.AddCredit("Status",
                "Error: " + error, credit => credit.Description.Foreground = Brushes.Red);
        }

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.None)
        {
            GamepadSettingsConfig.AddCredit("None",
                "If you don't need sending Gamepad Commands it make sense to disable it to decrease CPU usage");
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm)
        {
            GamepadSettingsConfig.AddCredit("",
                "Virtual Gamepad with ViGEm\r\n" +
                "In order to use the Gamepad to send actions or AIM you need to ensure you have installed the ViGEm Bus Driver fom https://vigembusdriver.com/\r\n" +
                "Then a Virtual Xbox360 Controller will be created and your current controller will be synced with the virtual one. Please then ensure you use the virtual device in your game");
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton("Goto vigembusdriver.com", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "https://vigembusdriver.com",
                            UseShellExecute = true
                        });
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddCredit("Status",
                    "GREAT, It looks like you have installed all necessary software for it.");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy)
        {
            GamepadSettingsConfig.AddCredit("",
                "Virtual Gamepad with vJoy\r\n" +
                "In order to use the Gamepad with vJoy you need to install vJoy from https://sourceforge.net/projects/vjoystick/\r\n" +
                "Then a Virtual Xbox360 Controller will be created and your current controller will be synced with the virtual one. Please then ensure you use the virtual device in your game");
            if (!GamepadManager.CanSend)
            {
                GamepadSettingsConfig.AddButton("Install vJoy", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Resources", "vJoySetup.exe");
                        if (File.Exists(fileName))
                        {
                            var p = Process.Start(new ProcessStartInfo
                            {
                                FileName = fileName,
                            });
                            p.Exited += (sender, args) => Reload();
                        }
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddCredit("Status",
                    "GREAT, It looks like you have installed all necessary software for it.");
            }
        }
        else if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.XInputHook)
        {
            GamepadSettingsConfig.AddCredit("Notice",
                "In order to use XInputEmulation you need to ensure this tool is started with Admin Privileges");
            GamepadSettingsConfig.Add<AProcessPicker>(picker =>
            {
                picker.SelectedProcessModel = new ProcessModel { Title = AppConfig.Current.DropdownState.GamepadProcess };
                picker.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(picker.SelectedProcessModel))
                    {
                        AppConfig.Current.DropdownState.GamepadProcess = picker.SelectedProcessModel.Title;
                        Reload();
                    }
                };
            });
        }

        GamepadSettingsConfig.AddSeparator();

        if (AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.VJoy ||
            AppConfig.Current.DropdownState.GamepadSendMode == GamepadSendMode.ViGEm)
        {
            GamepadSettingsConfig.AddTitle($"Hide Physical Controller", false);
            GamepadSettingsConfig.AddCredit("If you are not able to select the controller in your game you can hide it's HID",
                "With HidHide you can Hide your Physical Controller and we can do this automatically for you");

            GamepadSettingsConfig.AddToggle("Automatically hide and reactivate physical Controller", toggle =>
            {
                toggle.IsEnabled = File.Exists(HidHideHelper.GetHidHidePath());
                toggle.Changed += (s, e) => Reload();
            }).BindTo(() => AppConfig.Current.ToggleState.AutoHideController);

            GamepadSettingsConfig.AddFileLocator("HidHide Path", "HidHideCLI.exe (HidHideCLI.exe)|HidHideCLI.exe", HidHideHelper.GetHidHidePath(), cfg:
                locator =>
                {
                    locator.FileSelected += (sender, args) => Reload();
                });


            if (!File.Exists(HidHideHelper.GetHidHidePath()))
            {
                GamepadSettingsConfig.AddButton("Install HidHide", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName =
                            Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                "Resources", "HidHide_1.5.230_x64.exe");
                        if (File.Exists(fileName))
                        {
                            var p = Process.Start(fileName);
                            p.Exited += (sender, args) => Reload();
                        }
                    };
                });
            }
            else
            {
                GamepadSettingsConfig.AddButton("Launch HidHide UI", b =>
                {
                    b.Reader.Click += (s, e) =>
                    {
                        var fileName = Path.Combine(Path.GetDirectoryName(HidHideHelper.GetHidHidePath()), "HidHideClient.exe");
                        if (File.Exists(fileName))
                            Process.Start(fileName);
                    };
                });
            }

            GamepadSettingsConfig.AddSeparator();

            GamepadSettingsConfig.AddButton("Show and Test Controller", b =>
            {
                b.Reader.Click += (s, e) =>
                {
                   
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c joy.cpl",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(startInfo);
                };
            });
        }

    }

    private void LoadSettingsMenu()
    {
        SettingsConfig.RemoveAll();
        SettingsConfig.AddTitle("Settings Menu", true);

        SettingsConfig.AddDropdown("Theme", ApplicationConstants.Theme, ThemePalette.All, palette =>
        {
            ApplicationConstants.Theme = palette;
            if (Config != null)
                Config.ThemeName = palette.Name;
        });
        var themeOnActive = ThemePalette.ThemeForActive;
        SettingsConfig.AddDropdown("Theme when active", themeOnActive, ThemePalette.All, palette =>
        {
            if (Config != null)
                Config.ActiveThemeName = palette.Name;
        });

        SettingsConfig.AddToggle("Collect Data While Playing").BindTo(() => AppConfig.Current.ToggleState.CollectDataWhilePlaying);
        SettingsConfig.AddToggle("Auto Label Data").BindTo(() => AppConfig.Current.ToggleState.AutoLabelData);
        SettingsConfig.AddDropdown("Mouse Movement Method",
            AppConfig.Current.DropdownState.MouseMovementMethod, async v =>
            {
                AppConfig.Current.DropdownState.MouseMovementMethod = v;
                if ((v == MouseMovementMethod.LGHUB && !new LGHubMain().Load())
                    || (v == MouseMovementMethod.RazerSynapse && !await RZMouse.Load())
                    || (v == MouseMovementMethod.ddxoft && !await DdxoftMain.Load())
                   )
                {
                    AppConfig.Current.DropdownState.MouseMovementMethod = MouseMovementMethod.MouseEvent;
                }
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

        SettingsConfig.AddCredit(string.Empty, "When fire (click action) is executed a random value for waiting from press to release is used.\r\nHere you can control the maximum time for this random value");
        SettingsConfig.AddSlider("Fire max delay", "Seconds", 0.01, 0.1, 0.00, 2).BindTo(() => AppConfig.Current.SliderSettings.FirePressDelay);

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