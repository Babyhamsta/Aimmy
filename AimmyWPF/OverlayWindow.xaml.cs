using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AimmyWPF.Class;

namespace AimmyWPF
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public int FovSize { get; set; } = 640;

        public OverlayWindow()
        {
            InitializeComponent();
            this.Title = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

            // Listens for any new stuff
            AwfulPropertyChanger.ReceiveColor = UpdateFOVColor;
            AwfulPropertyChanger.ReceiveFOVSize = UpdateFOVSize;

        }

        void UpdateFOVColor(Color NewColor) => OverlayCircle.Stroke = new SolidColorBrush(NewColor);

        void UpdateFOVSize()
        {
            // Update circle dimensions.
            OverlayCircle.Width = FovSize;
            OverlayCircle.Height = FovSize;

            // Get screen dimensions.
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Update the Canvas dimensions
            OverlayCanvas.Width = screenWidth;
            OverlayCanvas.Height = screenHeight;

            // Update circle position within the Canvas.
            Canvas.SetLeft(OverlayCircle, (screenWidth - FovSize) / 2);
            Canvas.SetTop(OverlayCircle, (screenHeight - FovSize) / 2);

            // Update OverlayWindow position to be centered on the screen.
            this.Left = 0;
            this.Top = 0;
            this.Width = screenWidth;
            this.Height = screenHeight;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
