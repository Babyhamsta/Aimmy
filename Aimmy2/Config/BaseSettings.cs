using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Aimmy2.Config;

public abstract class BaseSettings : INotifyPropertyChanged
{
    public void Save<T>(string path) where T : BaseSettings
    {
        try
        {
            string json = JsonSerializer.Serialize(this as T, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            // Fehlerbehandlung hier hinzufügen
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }


    private string PrepareName(string name)
    {
        name = name.Replace("(Up/Down)", "").Replace("(Left/Right)", "");
        var res = name.Replace(" ", "").Replace(":", "").Replace("(", "").Replace(")", "").Replace("/", "").Replace("\\", "").Replace("?", "").Replace("!", "").Replace("'", "").Replace("\"", "").Replace(";", "").Replace(",", "").Replace(".", "").Replace("[", "").Replace("]", "").Replace("{", "").Replace("}", "").Replace("|", "").Replace("=", "").Replace("+", "").Replace("-", "").Replace("*", "").Replace("&", "").Replace("^", "").Replace("%", "").Replace("$", "").Replace("#", "").Replace("@", "").Replace("~", "").Replace("`", "").Replace("<", "").Replace(">", "").Replace(" ", "");
        return res;
    }
    // TODO: Remove reflection indexer
    public object? this[string propertyName]
    {
        get
        {
            var name = PrepareName(propertyName);
            PropertyInfo? property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) throw new ArgumentException($"Property '{propertyName}' not found on '{GetType().Name}'");
            return property.GetValue(this);
        }
        set
        {
            var name = PrepareName(propertyName);
            PropertyInfo? property = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) throw new ArgumentException($"Property '{propertyName}' not found on '{GetType().Name}'");
            property.SetValue(this, value);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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