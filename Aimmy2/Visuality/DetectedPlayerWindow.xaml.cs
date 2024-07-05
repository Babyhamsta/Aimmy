using Aimmy2.Class;
using Class;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Aimmy2.Types;
using Color = System.Windows.Media.Color;
using System.Windows.Controls;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for DetectedPlayerWindow.xaml
    /// </summary>
    public partial class DetectedPlayerWindow : Window
    {
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);
        }

        public DetectedPlayerWindow()
        {
            InitializeComponent();

            Title = "";

            DetectedTracers.X1 = (WinAPICaller.ScreenWidth / 2) / WinAPICaller.scalingFactorX;
            DetectedTracers.Y1 = WinAPICaller.ScreenHeight / WinAPICaller.scalingFactorY;

            PropertyChanger.ReceiveDPColor = UpdateDPColor;
            PropertyChanger.ReceiveDPFontSize = UpdateDPFontSize;
            PropertyChanger.ReceiveDPWCornerRadius = ChangeCornerRadius;
            PropertyChanger.ReceiveDPWBorderThickness = ChangeBorderThickness;
            PropertyChanger.ReceiveDPWOpacity = ChangeOpacity;
        }

        private void UpdateDPColor(Color NewColor)
        {
            DetectedPlayerFocus.BorderBrush = new SolidColorBrush(NewColor);
            DetectedPlayerConfidence.Foreground = new SolidColorBrush(NewColor);
            DetectedTracers.Stroke = new SolidColorBrush(NewColor);
        }

        private void UpdateDPFontSize(int newint) => DetectedPlayerConfidence.FontSize = newint;

        private void ChangeCornerRadius(int newint) => DetectedPlayerFocus.CornerRadius = new CornerRadius(newint);

        private void ChangeBorderThickness(double newdouble)
        {
            DetectedPlayerFocus.BorderThickness = new Thickness(newdouble);
            DetectedTracers.StrokeThickness = newdouble;
        }

        private void ChangeOpacity(double newdouble) => DetectedPlayerFocus.Opacity = newdouble;

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        public RelativeRect? HeadRelativeArea { get; set; }

        private void UpdateHeadArea()
        {
            if (HeadRelativeArea == null)
            {
                HeadAreaBorder.Visibility = Visibility.Collapsed;
                return;
            }

            double parentWidth = DetectedPlayerFocus.ActualWidth;
            double parentHeight = DetectedPlayerFocus.ActualHeight;

            double headAreaWidth = parentWidth * HeadRelativeArea.Value.WidthPercentage;
            double headAreaHeight = parentHeight * HeadRelativeArea.Value.HeightPercentage;
            double headAreaLeft = parentWidth * HeadRelativeArea.Value.LeftMarginPercentage;
            double headAreaTop = parentHeight * HeadRelativeArea.Value.TopMarginPercentage;

            HeadAreaBorder.Width = headAreaWidth;
            HeadAreaBorder.Height = headAreaHeight;
            Canvas.SetLeft(HeadAreaBorder, headAreaLeft);
            Canvas.SetTop(HeadAreaBorder, headAreaTop);

            HeadAreaBorder.Visibility = Visibility.Visible;
        }

        public void SetHeadRelativeArea(RelativeRect? relativeRect)
        {
            HeadRelativeArea = relativeRect;
            UpdateHeadArea();
        }
    }
}
