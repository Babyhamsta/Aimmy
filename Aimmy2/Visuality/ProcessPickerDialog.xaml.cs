using Class;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Class;
using System.Diagnostics;
using System.Windows.Controls;

namespace Visuality
{
    public partial class ProcessPickerDialog : Window
    {
        public Process? SelectedProcess { get; private set; }

        public ProcessPickerDialog()
        {
            InitializeComponent();
            LoadProcesses();
        }
        private void LoadProcesses()
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderBy(p => p.MainWindowTitle)
                .ToList();
            ProcessListBox.ItemsSource = processes;
        }

        private double currentGradientAngle = 0;

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var CurrentMousePos = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(CurrentMousePos.X, CurrentMousePos.Y));
                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
                if (angleDifference > 180)
                {
                    angleDifference -= 360;
                }

                angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ProcessListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProcessListBox.SelectedItem is Process selectedProcess)
            {
                SelectedProcess = selectedProcess;
                DialogResult = true;
                Close();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ProcessListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedProcess = e.AddedItems[0] as Process ?? null;
            ApplyButton.IsEnabled = SelectedProcess != null;
        }
    }

}
