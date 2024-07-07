using Other;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : System.Windows.Controls.UserControl
    {
        public AKeyChanger(string Text, string Keybind)
        {
            InitializeComponent();
            KeyChangerTitle.Content = Text;

            KeyNotifier.Content = KeybindNameManager.ConvertToRegularKey(Keybind);
        }
    }
}