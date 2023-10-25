using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

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
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            // Update rectangle dimensions.
            OverlayRectangle.Width = FovSize;
            OverlayRectangle.Height = FovSize;

            // Get screen dimensions.
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Update the Canvas dimensions
            OverlayCanvas.Width = screenWidth;
            OverlayCanvas.Height = screenHeight;

            // Update rectangle position within the Canvas.
            Canvas.SetLeft(OverlayRectangle, (screenWidth - FovSize) / 2);
            Canvas.SetTop(OverlayRectangle, (screenHeight - FovSize) / 2);

            // Update OverlayWindow position to be centered on the screen.
            this.Left = 0;
            this.Top = 0;
            this.Width = screenWidth;
            this.Height = screenHeight;
        }
    }
}
