using AimmyWPF.Class;
using System.Windows;

namespace Visualization
{
    /// <summary>
    /// Interaction logic for PlayerDetectionWindow.xaml
    /// </summary>
    public partial class PlayerDetectionWindow : Window
    {
        public PlayerDetectionWindow()
        {
            InitializeComponent();

            AwfulPropertyChanger.ReceivePDWSize = ChangeSize;
            AwfulPropertyChanger.ReceivePDWCornerRadius = ChangeCornerRadius;
            AwfulPropertyChanger.ReceivePDWBorderThickness = ChangeBorderThickness;
            AwfulPropertyChanger.ReceivePDWOpacity = ChangeOpacity;
        }

        private void ChangeSize(int newint)
        {
            DetectedPlayerFocus.Width = newint;
            DetectedPlayerFocus.Height = newint;

            UnfilteredPlayerFocus.Width = newint;
            UnfilteredPlayerFocus.Height = newint;

            PredictionFocus.Width = newint;
            PredictionFocus.Height = newint;
        }

        private void ChangeCornerRadius(int newint)
        {
            DetectedPlayerFocus.CornerRadius = new CornerRadius(newint);
            UnfilteredPlayerFocus.CornerRadius = new CornerRadius(newint);
            PredictionFocus.CornerRadius = new CornerRadius(newint);
        }

        private void ChangeBorderThickness(int newint)
        {
            DetectedPlayerFocus.BorderThickness = new Thickness(newint);
            UnfilteredPlayerFocus.BorderThickness = new Thickness(newint);
            PredictionFocus.BorderThickness = new Thickness(newint);
        }

        private void ChangeOpacity(double newdouble)
        {
            DetectedPlayerFocus.Opacity = newdouble;
            UnfilteredPlayerFocus.Opacity = newdouble;
            PredictionFocus.Opacity = newdouble;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}