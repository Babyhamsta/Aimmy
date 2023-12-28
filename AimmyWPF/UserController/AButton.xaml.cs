using System.Windows.Controls;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AButton.xaml
    /// </summary>
    public partial class AButton : UserControl
    {
        private static MainWindow MainWin = new MainWindow();

        public AButton(MainWindow MW, string Text, string Info)
        {
            InitializeComponent();
            Title.Content = Text;

            MainWin = MW;

            QuestionButton.Click += (s, e) =>
            {
                MainWin.ActivateMoreInfo(Info);
            };
        }
    }
}