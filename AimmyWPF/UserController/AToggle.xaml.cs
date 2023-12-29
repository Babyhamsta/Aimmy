using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AToggle.xaml
    /// </summary>
    public partial class AToggle : UserControl
    {
        private static MainWindow MainWin = new MainWindow();

        public AToggle(MainWindow MW, string Text, string Info)
        {
            InitializeComponent();
            Title.Content = Text;

            MainWin = MW;

            QuestionButton.Click += (s, e) =>
            {
                MainWin.ActivateMoreInfo(Info);
            };
        }

        // Reference: https://stackoverflow.com/questions/34815532/start-storyboard-from-c-sharp-code

        public void EnableSwitch()
        {
            Storyboard Animation = (Storyboard)TryFindResource("EnableSwitch");
            Animation.Begin();
        }

        public void DisableSwitch()
        {
            Storyboard Animation = (Storyboard)TryFindResource("DisableSwitch");
            Animation.Begin();
        }
    }
}