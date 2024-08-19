using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Aimmy2.Config;

namespace Launcher;

public abstract class BaseDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual bool SaveRestorePosition => true;
    protected Func<bool> ShouldBindGradientMouse = () => false;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        if (SaveRestorePosition)
        {
            var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
            settingsManager.LoadWindowSettings(this);
        }
    }
    
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (SaveRestorePosition)
        {
            var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
            settingsManager.SaveWindowSettings(this);
        }
    }

    private string GetSettingsFilePath()
    {
        var dialogType = GetType().Name;
        var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-M");
        Directory.CreateDirectory(folderPath);
        return Path.Combine(folderPath, $"{dialogType}_WindowSettings.bin");
    }
}