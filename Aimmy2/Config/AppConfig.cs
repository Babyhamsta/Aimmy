using System.IO;
using System.Text.Json;
using Accord.IO;
using Visuality;

namespace Aimmy2.Config;

public class AppConfig : BaseSettings
{
    public const string DefaultConfigPath = "bin\\configs\\Default.config";
    public static AppConfig Current { get; private set; }

    public string lastLoadedModel = "N/A";
    public string lastLoadedConfig = "N/A";
    public string SuggestedModelName => SliderSettings.SuggestedModel;
    public DetectedPlayerWindow? DetectedPlayerOverlay;
    public FOV? FOVWindow;

    public BindingSettings BindingSettings { get; set; } = new BindingSettings();
    public SliderSettings SliderSettings { get; set; } = new SliderSettings();
    public ToggleState ToggleState { get; set; } = new ToggleState();
    public MinimizeState MinimizeState { get; set; } = new MinimizeState();
    public DropdownState DropdownState { get; set; } = new DropdownState();
    public ColorState ColorState { get; set; } = new ColorState();
    public AntiRecoilSettings AntiRecoilSettings { get; set; } = new AntiRecoilSettings();
    public FileLocationState FileLocationState { get; set; } = new FileLocationState();

    


    public static AppConfig Load(string path = DefaultConfigPath)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                
                Current = JsonSerializer.Deserialize<AppConfig>(json);
                Current.lastLoadedConfig = path;
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
        return Current;
    }


    public void Save(string path = DefaultConfigPath)
    {
        Save<AppConfig>(path);
    }
}
