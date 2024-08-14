using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aimmy2.InputLogic.Contracts;
using Aimmy2.InputLogic.Gamepad.Interaction;
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
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3C3C3C"));
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF"));
            InitializeComponent();
            DataContext = this;
            SetContent(text, keybind);
        }

        public event EventHandler<EventArgs> KeyDeleted;

        public bool InUpdateMode { get; set; }

        public void SetContent(string text, string keybind)
        {
            KeyChangerTitle.Content = text;
            SetContent(keybind);
        }

        public bool HasKeySet { get; private set; }

        public bool ShowTitle
        {
            get => Dispatcher.Invoke(() => KeyChangerTitle.Visibility == Visibility.Visible);
            set => Dispatcher.Invoke(() => KeyChangerTitle.Visibility = value ? Visibility.Visible : Visibility.Collapsed);
        }

        public void SetContent(string keybind)
        {
            HasKeySet = !string.IsNullOrWhiteSpace(keybind);
            if (GamepadEventArgs.IsGamepadKey(keybind))
            {
                KeyNotifierLabel.Content = GamepadEventArgs.GetButtonName(keybind);
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

        public static string CodeFor(string title)
        {
            return "DYN_" + title.Replace(" ", "_").ToUpper();
        }
    }
}