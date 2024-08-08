using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Aimmy2.AILogic;
using Aimmy2.Types;


namespace Aimmy2.Config;

public class AppConfig : BaseSettings
{
    public const string DefaultConfigPath = "bin\\configs\\Default.cfg";

    public string? Path;

    public static AppConfig Current { get; private set; }

    public string LastLoadedModel { get; set; } = "N/A";
    
    public string LastLoadedConfig = "N/A";
    private CaptureSource _captureSource = AILogic.CaptureSource.MainScreen();
    public string SuggestedModelName => SliderSettings.SuggestedModel;
    
    public string ThemeName { get; set; } = ThemePalette.DefaultPalette.Name;
    public string ActiveThemeName { get; set; } = ThemePalette.GreenPalette.Name;
    public BindingSettings BindingSettings { get; set; } = new BindingSettings();
    public SliderSettings SliderSettings { get; set; } = new SliderSettings();
    public ToggleState ToggleState { get; set; } = new ToggleState();
    public MinimizeState MinimizeState { get; set; } = new MinimizeState();
    public DropdownState DropdownState { get; set; } = new DropdownState();
    public ColorState ColorState { get; set; } = new ColorState();
    public AntiRecoilSettings AntiRecoilSettings { get; set; } = new AntiRecoilSettings();
    public FileLocationState FileLocationState { get; set; } = new FileLocationState();

    public CaptureSource CaptureSource
    {
        get => _captureSource;
        set => SetField(ref _captureSource, value);
    }


    public static AppConfig Load(string path = DefaultConfigPath)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                
                Current = JsonSerializer.Deserialize<AppConfig>(json);
                Current.Path = path;
                Current.LastLoadedConfig = path;
            }
            else
            {
                Current = new AppConfig(); 
            }
        }
        catch (Exception ex)
        {
            // Fehlerbehandlung hier hinzufügen
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            Current = new AppConfig();
        }
        ConfigLoaded?.Invoke(null, new EventArgs<AppConfig>(Current));
        Current.RaiseAllPropertiesChanged();
        return Current;
    }

    public static void BindToDataContext(FrameworkElement element)
    {
        element.DataContext = Current;
        ConfigLoaded += (sender, e) => element.DataContext = e.Value;
    }

    public void Save(string? path = null)
    {
        var cs = CaptureSource;
        path ??= Path ?? DefaultConfigPath;
        Save<AppConfig>(path);
    }

    public static event EventHandler<EventArgs<AppConfig>>? ConfigLoaded;

}
