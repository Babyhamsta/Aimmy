using Nextended.Core.Types;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Visuality;

namespace Aimmy2.Config;

public abstract class BaseSettings : INotifyPropertyChanged
{
    protected void RaiseAllPropertiesChanged()
    {
        var processedObjects = new HashSet<object>();
        RaiseAllPropertiesChanged(processedObjects);
    }

    private void RaiseAllPropertiesChanged(HashSet<object> processedObjects)
    {
        // Mark this object as processed
        if (!processedObjects.Add(this))
        {
            // If the object was already processed, return to avoid infinite recursion
            return;
        }

        GetType().GetProperties().ToList().ForEach(p =>
        {
            if (typeof(BaseSettings).IsAssignableFrom(p.PropertyType))
            {
                var settingsObj = p.GetValue(this) as BaseSettings;
                settingsObj?.RaiseAllPropertiesChanged(processedObjects);
            }
            else
            {
                Console.WriteLine(p.Name);
                OnPropertyChanged(p.Name);
            }
        });
    }

    public void Load<T>(string path) where T : BaseSettings
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var obj = JsonSerializer.Deserialize<T>(json);
                foreach (var property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var value = property.GetValue(obj);
                    property.SetValue(this, value);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            new NoticeBar($"{ex.Message}", 5000).Show();
        }

    }

    public void Save<T>(string path) where T : BaseSettings
    {
        try
        {
            string json = JsonSerializer.Serialize(this as T, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }


    protected string PrepareName(string name)
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