using Aimmy2.Class;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    public partial class ADropdown : UserControl
    {
        private string main_dictionary_path { get; set; }
        public string DropdownStateKey => main_dictionary_path;

        public ADropdown(string title, string dictionary_path, string? tooltip = null)
        {
            InitializeComponent();
            DropdownTitle.Content = title;
            main_dictionary_path = dictionary_path;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DropdownBox.SelectedItem is ComboBoxItem item)
            {
                var stableValue = item.Tag?.ToString() ?? item.Content?.ToString();
                if (stableValue != null)
                {
                    Dictionary.dropdownState[main_dictionary_path] = stableValue;
                }
            }
        }
    }
}
