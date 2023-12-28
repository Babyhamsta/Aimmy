using System.Windows.Controls;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AKeyChanger.xaml
    /// </summary>
    public partial class AKeyChanger : UserControl
    {
        public AKeyChanger(string Text, string DefaultContent)
        {
            InitializeComponent();
            Title.Content = Text;
            KeyNotifier.Content = DefaultContent;

            //MainWin.ActivateMoreInfo();
        }
    }
}