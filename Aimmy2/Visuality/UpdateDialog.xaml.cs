using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Controls;
using Aimmy2;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Core;
using Other;

namespace Visuality
{
    public partial class UpdateDialog
    {
        private string[] IgnoreOnUpdate => [AppConfig.DefaultConfigPath];

        private readonly UpdateManager _updateManager;
        private bool _canClose = true;
        private string _status;
        
        protected override bool SaveRestorePosition => false;

        public string NewVersion { get; private set; }
        public string CurrentVersion { get; private set; }

        public string Status
        {
            get => _status;
            private set => SetField(ref _status, value);
        }

        public bool CanClose    
        {
            get => _canClose;
            private set => SetField(ref _canClose, value);
        }

        public string Header => $"A new version {NewVersion} is available!";

        public UpdateDialog(UpdateManager updateManager)
        {
            NewVersion = updateManager?.NewVersion?.ToString() ?? new Version(0,0,0,2).ToString();
            CurrentVersion = ApplicationConstants.ApplicationVersion.ToString();
            _updateManager = updateManager;
            InitializeComponent();
            DataContext = this;
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressBar.Visibility = Visibility.Visible;
            Status = "Begin download...";
            CanClose = false;
            await _updateManager.DoUpdate(new Progress<double>(p =>
            {
                UpdateProgressBar.Value = p;
                Status = $"Downloading... {p:0.00}%";
            }), IgnoreOnUpdate);

            UpdateProgressBar.Visibility = Visibility.Hidden;
            Status = "Finished";
            CanClose = true;
        }

        private void UpdateDialog_OnClosing(object? sender, CancelEventArgs e)
        {
            e.Cancel = !CanClose;
        }
    }

}
