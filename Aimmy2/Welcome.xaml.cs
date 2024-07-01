using Aimmy2.Class;
using Class;
using System.Windows;
using System.Windows.Input;

namespace Aimmy2
{
    /// <summary>
    /// Interaction logic for Welcome.xaml
    /// </summary>
    public partial class Welcome : Window
    {
        public Welcome()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ActivateNoButton();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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

        private void KeyButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void DonateButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void ActivateNoButton()
        {
            NoButton.IsEnabled = false;
            for (int i = 8; i >= 0; i--)
            {
                await Task.Delay(1000);
                NoButton.Content = $"No ({i})";
            }
            NoButton.Content = "No";
            NoButton.IsEnabled = true;
        }
    }
}