using System.ComponentModel;
using Aimmy2.Models;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using Visuality;
using ListBox = System.Windows.Forms.ListBox;

namespace Aimmy2.UILibrary
{
    public partial class AProcessPicker : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        private ProcessModel _selectedProcessModel;

        
        public AProcessPicker()
        {
            InitializeComponent();
            DataContext = this;
        }

        
        public ProcessModel SelectedProcessModel
        {
            get => _selectedProcessModel;
            set
            {
                if (Equals(value, _selectedProcessModel)) return;
                _selectedProcessModel = value;
                OnPropertyChanged();
            }
        }

        private void ProcessPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var processDialog = new ProcessPickerDialog();
            if (processDialog.ShowDialog() == true)
            {
                var selectedProcess = processDialog.SelectedProcess;
                if (selectedProcess != null)
                {
                    SelectedProcessModel = new ProcessModel { Process = selectedProcess };

                }
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

    //public class ProcessPickerForm : Form
    //{
    //    public Process SelectedProcess { get; private set; }

    //    public ProcessPickerForm()
    //    {
    //        var listBox = new ListBox { Dock = DockStyle.Fill };
    //        listBox.Items.AddRange(Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)).ToArray());
    //        listBox.DisplayMember = "MainWindowTitle";
    //        listBox.DoubleClick += (sender, args) =>
    //        {
    //            SelectedProcess = listBox.SelectedItem as Process;
    //            DialogResult = DialogResult.OK;
    //            Close();
    //        };

    //        Controls.Add(listBox);
    //        Width = 400;
    //        Height = 300;
    //    }
    //}
}