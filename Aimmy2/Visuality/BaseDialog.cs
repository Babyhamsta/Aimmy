using Class;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Class;
using Aimmy2.Config;

namespace Visuality;

public abstract class BaseDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected Func<bool> ShouldBindGradientMouse = () => AppConfig.Current.ToggleState.MouseBackgroundEffect;

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

}