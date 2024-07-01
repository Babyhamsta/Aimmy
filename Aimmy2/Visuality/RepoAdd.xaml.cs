using Aimmy2.Class;
using Aimmy2.Other;
using Class;
using System.Windows;
using System.Windows.Input;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for RepoAdd.xaml
    /// </summary>
    public partial class RepoAdd : Window
    {
        private RepoManager _repoManager;
        //private GithubManager _githubManager = new GithubManager();

        public RepoAdd(RepoManager repoManager)
        {
            InitializeComponent();
            _repoManager = repoManager;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string repoLinkText = RepoLinkTextbox.Text;

            if (string.IsNullOrEmpty(repoLinkText) || !repoLinkText.Contains("github.com"))
            {
                new NoticeBar("Please enter a valid Github link.", 4000).Show();
                return;
            }

            try
            {
                string ConvertedURL = GithubManager.ConvertToShortURL(repoLinkText);
                string ConvertedAPI = GithubManager.ConvertToApiUrl(repoLinkText);

                if (!Dictionary.repoList.ContainsKey(ConvertedURL))
                {
                    Dictionary.repoList.Add(ConvertedURL, ConvertedAPI);
                }
                else
                {
                    Dictionary.repoList[ConvertedURL] = ConvertedAPI;
                }

                _repoManager.UpdateRepoList();

                new NoticeBar("Added to Repo Manager and the Store.", 4000).Show();
                Close();
            }
            catch (Exception)
            {
                new NoticeBar("Failed to add repo.", 4000).Show();
            }
        }

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private double currentGradientAngle = 0;

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var CurrentMousePos = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(CurrentMousePos.X, CurrentMousePos.Y));
                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
                if (angleDifference > 180)
                {
                    angleDifference -= 360;
                }

                angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        #endregion Window Controls
    }
}