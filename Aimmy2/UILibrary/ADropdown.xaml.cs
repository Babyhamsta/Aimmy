using Aimmy2.Class;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;

namespace UILibrary
{
    /// <summary>
    /// Interaction logic for ADropdown.xaml
    /// </summary>
    public partial class ADropdown : UserControl
    {
        private readonly string _settingKey;

        public ADropdown(string title, string dictionary_path, string? tooltip = null)
        {
            InitializeComponent();
            DropdownTitle.Content = title;
            _settingKey = dictionary_path;

            if (!string.IsNullOrEmpty(tooltip))
            {
                var tt = new System.Windows.Controls.ToolTip { Content = tooltip };
                if (TryFindResource("Tooltip") is System.Windows.Style style)
                    tt.Style = style;
                ToolTip = tt;
            }
        }

        internal void RestoreSelection()
        {
            if (Dictionary.dropdownState.TryGetValue(_settingKey, out var value))
                SelectOption(Convert.ToString((object?)value) ?? string.Empty);
        }

        internal void SelectOption(string value)
        {
            foreach (ComboBoxItem option in DropdownBox.Items)
            {
                if (Equals(option.Content, value))
                {
                    DropdownBox.SelectedItem = option;
                    return;
                }
            }
        }

        private void DropdownBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItemContent = ((ComboBoxItem)DropdownBox.SelectedItem)?.Content?.ToString();
            if (selectedItemContent != null)
            {
                Dictionary.dropdownState[_settingKey] = selectedItemContent;
            }
        }
    }
}