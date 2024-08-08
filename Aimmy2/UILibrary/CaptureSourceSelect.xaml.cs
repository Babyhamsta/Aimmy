using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Accord.Math;
using Aimmy2.AILogic;
using Aimmy2.Config;
using Class;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Aimmy2.Models;
using Visuality;
using System.Threading;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ACredit.xaml
    /// </summary>
    public partial class CaptureSourceSelect : INotifyPropertyChanged
    {

        public CaptureSource CaptureSource
        {
            get => (CaptureSource)GetValue(CaptureSourceProperty);
            set => SetValue(CaptureSourceProperty, value);
        }

        public static readonly DependencyProperty CaptureSourceProperty =
            DependencyProperty.Register(nameof(CaptureSource), typeof(CaptureSource), typeof(CaptureSourceSelect), new PropertyMetadata(AppConfig.Current.CaptureSource));

        public Brush ScreenForeground => CaptureSource.TargetType == CaptureTargetType.Screen ? Brushes.Green : Brushes.White;
        public Brush ProcessForeground
        {
            get
            {
                if (CaptureSource.TargetType == CaptureTargetType.Process)
                {
                    var process = ProcessModel.FindProcessById(CaptureSource.ProcessOrScreenId ?? 0) ?? ProcessModel.FindProcessByTitle(CaptureSource.Title);
                    return process != null ? Brushes.Green : Brushes.Red;
                }
                return Brushes.White;
            }
        }

        public event EventHandler<CaptureSource> Selected;


        public CaptureSourceSelect()
        {
            InitializeComponent();
            DataContext = this;
        }


        private void ProcessBtnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            btn.ContextMenu = new ContextMenu();
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.PlacementTarget = btn;
            var primary = new MenuItem { Header = "Select Application..." };
            primary.Click += (o, args) => OnSelect();
            btn.ContextMenu.Items.Add(primary);
            btn.ContextMenu.Items.Add(new Separator());
            foreach (var process in WinAPICaller.RecordableProcesses())
            {
                var menuItem = new MenuItem() { Header = process.MainWindowTitle };
                menuItem.IsCheckable = true;
                menuItem.IsChecked = CaptureSource.TargetType == CaptureTargetType.Process && (process.MainWindowTitle == CaptureSource.Title || process.Id == CaptureSource.ProcessOrScreenId);
                menuItem.Click += (o, args) => OnSelect(process);
                btn.ContextMenu.Items.Add(menuItem);
            }
            btn.ContextMenu.IsOpen = true;

        }

        private void MonitorBtnClick(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;

            btn.ContextMenu = new ContextMenu();
            btn.ContextMenu.Placement = PlacementMode.Bottom;
            btn.ContextMenu.PlacementTarget = btn;
            var primary = new MenuItem { Header = "Use Primary Monitor" };
            primary.Click += (o, args) => OnSelect(Screen.PrimaryScreen);
            btn.ContextMenu.Items.Add(primary);
            btn.ContextMenu.Items.Add(new Separator());
            foreach (var monitor in Screen.AllScreens)
            {
                var menuItem = new MenuItem() { Header = monitor.DeviceName };
                menuItem.IsCheckable = true;
                menuItem.IsChecked = CaptureSource.TargetType == CaptureTargetType.Screen && ((CaptureSource.ProcessOrScreenId == null && Equals(monitor, Screen.PrimaryScreen)) || CaptureSource.ProcessOrScreenId == Screen.AllScreens.IndexOf(monitor));
                menuItem.Click += (o, args) => OnSelect(monitor);
                btn.ContextMenu.Items.Add(menuItem);
            }

            btn.ContextMenu.IsOpen = true;

        }


        private async void OnSelect()
        {
            var processDialog = new ProcessPickerDialog();
            if (processDialog.ShowDialog() == true)
            {
                var selectedProcess = processDialog.SelectedProcess;
                if (selectedProcess != null)
                {
                    OnSelect(selectedProcess);
                }
            }
        }

        private void OnSelect(Process process)
        {
            CaptureSource = AILogic.CaptureSource.Process(process);
            AppConfig.Current.CaptureSource = CaptureSource;
            Selected?.Invoke(this, CaptureSource);
            OnPropertyChanged(nameof(ProcessForeground));
            OnPropertyChanged(nameof(ScreenForeground));
        }

        private void OnSelect(Screen monitor)
        {
            CaptureSource = AILogic.CaptureSource.Screen(monitor);
            AppConfig.Current.CaptureSource = CaptureSource;
            Selected?.Invoke(this, CaptureSource);
            OnPropertyChanged(nameof(ProcessForeground));
            OnPropertyChanged(nameof(ScreenForeground));
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

}
