namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ACredit.xaml
    /// </summary>
    public partial class ACredit : System.Windows.Controls.UserControl
    {
        public ACredit(string Text, string Desc)
        {
            InitializeComponent();
            NameLabel.Content = Text;
            Description.Content = Desc;
        }
    }
}