using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Aimmy2.Models;

public class ProcessModel: INotifyPropertyChanged
{
    private string _title;
    private Process? _process;
    private int _id = 0;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Display => ToString();

    public int Id
    {
        get => _id;
        set
        {
            if (value == _id) return;
            _id = value;
            Process = FindProcessById(value);
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged();
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            if (value == _title) return;
            _title = value;
            Process = FindProcessByTitle(value);
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged();
        }
    }

    public Process? Process
    {
        get => _process ??= FindProcessByTitle(Title);
        set
        {
            if (Equals(value, _process)) return;
            _process = value;
            _title = value?.MainWindowTitle ?? string.Empty;
            _id = value?.Id ?? 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(Title));
        }
    }

    public static Process? FindProcessByTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        return Process.GetProcesses().FirstOrDefault(p => p.MainWindowTitle == title);
    }

    public static ProcessModel Empty => new() {Title = "NO PROCESS SELECTED"};

    public static Process? FindProcessById(int id)
    {
        if (id <= 0) return null;
        return Process.GetProcesses().FirstOrDefault(p => p.Id == id);
    }

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Title) && Id > 0)
            return $"{Title} | {Id}";
        return Title;
    }

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