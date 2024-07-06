using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Controls;
using Aimmy2.Extensions;

namespace Visuality
{
    public partial class ProcessPickerDialog
    {
        public Process? SelectedProcess { get; private set; }

        public ProcessPickerDialog()
        {
            InitializeComponent();
            LoadProcesses();
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        }
        private void LoadProcesses()
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderBy(p => p.MainWindowTitle)
                .ToList();
            ProcessListBox.ItemsSource = processes;
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
