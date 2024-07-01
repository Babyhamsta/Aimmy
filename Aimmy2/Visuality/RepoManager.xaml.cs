using Aimmy2;
using Aimmy2.Class;
using Aimmy2.UILibrary;
using Class;
using System.Windows;
using System.Windows.Input;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for RepoManager.xaml
    /// </summary>
    public partial class RepoManager : Window
    {
        public RepoManager()
        {
            InitializeComponent();
            UpdateRepoList();
        }

        private void Add_Click(object sender, RoutedEventArgs e) => new RepoAdd(this).Show();

        public async void UpdateRepoList()
        {
            bool config = false;
            RepoListScroller.Children.Clear();

            foreach (var repo in Dictionary.repoList)
            {
                config = repo.Key.Contains("configs", StringComparison.CurrentCultureIgnoreCase);
                await Application.Current.Dispatcher.InvokeAsync(() => RepoListScroller.Children.Add(new ARepoListing(repo.Key, config)));
            }

            UpdateStoreMenu(config);
        }

        public static async void UpdateStoreMenu(bool config)
        {
            MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
            if (config)
            {
                await mainWindow.Dispatcher.InvokeAsync(mainWindow.ConfigStoreScroller.Children.Clear);
            }
            else
            {
                await mainWindow.Dispatcher.InvokeAsync(mainWindow.ModelStoreScroller.Children.Clear);
            }
            await mainWindow.LoadStoreMenu();
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