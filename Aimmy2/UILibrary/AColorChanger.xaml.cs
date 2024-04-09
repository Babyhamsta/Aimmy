namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for AColorChanger.xaml
    /// </summary>
    public partial class AColorChanger : System.Windows.Controls.UserControl
    {
        public AColorChanger(string title)
        {
            InitializeComponent();
            ColorChangerTitle.Content = title;
        }
    }
}