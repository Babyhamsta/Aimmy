using Aimmy2.Class;
using Aimmy2.Config;
using System.Collections.Generic;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for ADropdown.xaml
    /// </summary>
    public partial class ADropdown : UserControl
    {
        private string? main_dictionary_path { get; set; }

        public ADropdown(string title, string? dictionary_path = null)
        {
            InitializeComponent();
            DropdownTitle.Content = title;
            main_dictionary_path = dictionary_path;
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItemContent = ((ComboBoxItem)DropdownBox.SelectedItem)?.Content?.ToString();
            if (selectedItemContent != null && main_dictionary_path != null)
            {
                AppConfig.Current.DropdownState[main_dictionary_path] = selectedItemContent;
            }
        }
    }
}