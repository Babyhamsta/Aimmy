using Aimmy2.Class;
using Microsoft.Win32;
using System.IO;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for AFileLocator.xaml
    /// </summary>
    public partial class AFileLocator : UserControl
    {
        private OpenFileDialog openFileDialog = new OpenFileDialog();
        private string main_dictionary_path { get; set; }
        private string OFDFilter = "All files (*.*)|*.*";
        private string DefaultLocationExtension = "";

        public AFileLocator(string title, string dictionary_path, string FileFilter = "All files (*.*)|*.*", string DLExtension = "")
        {
            InitializeComponent();
            DropdownTitle.Content = title;

            main_dictionary_path = dictionary_path;
            FileLocationTextbox.Text = Dictionary.filelocationState[main_dictionary_path];

            OFDFilter = FileFilter;
            DefaultLocationExtension = DLExtension;
        }

        private void OpenFileB_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.openfiledialog?view=windowsdesktop-8.0
            // Nori

            openFileDialog.InitialDirectory = Directory.GetCurrentDirectory() + DefaultLocationExtension;
            openFileDialog.Filter = OFDFilter;

            if (openFileDialog.ShowDialog() == true)
            {
                FileLocationTextbox.Text = openFileDialog.FileName;
                Dictionary.filelocationState[main_dictionary_path] = openFileDialog.FileName;
            }
        }
    }
}