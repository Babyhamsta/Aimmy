using System.Windows;
using System.Windows.Controls;
using Aimmy2.InputLogic;
using Other;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : System.Windows.Controls.UserControl
    {
        public AKeyChanger(string text, string keybind)
        {
            InitializeComponent();
            SetContent(text, keybind);
        }

        public event EventHandler<EventArgs> KeyDeleted;

        public bool InUpdateMode { get; set; }

        public void SetContent(string text, string keybind)
        {
            KeyChangerTitle.Content = text;
            SetContent(keybind);
        }

        public void SetContent(string keybind)
        {
            if(GamepadReader.GamepadEventArgs.IsGamepadKey(keybind))
            {
                KeyNotifierLabel.Content = GamepadReader.GamepadEventArgs.GetButtonName(keybind);
                GamepadInfo.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                KeyNotifierLabel.Content = KeybindNameManager.ConvertToRegularKey(keybind);
                GamepadInfo.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void DeleteBinding_Click(object sender, RoutedEventArgs e)
        {
            KeyDeleted?.Invoke(this, EventArgs.Empty);
        }

        private void ContextMenu_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = InUpdateMode;
        }
    }
}