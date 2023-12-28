using System.Windows.Controls;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AColorChanger.xaml
    /// </summary>
    public partial class AColorChanger : UserControl
    {
        public AColorChanger(string Text)
        {
            InitializeComponent();
            Title.Content = Text;
        }
    }
}