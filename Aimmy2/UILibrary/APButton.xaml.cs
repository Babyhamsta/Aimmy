namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for APButton.xaml
    /// </summary>
    public partial class APButton : System.Windows.Controls.UserControl
    {
        public APButton(string Text)
        {
            InitializeComponent();
            ButtonTitle.Content = Text;
        }
    }
}