using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AimmyWPF.Class;
using static AimmyWPF.MainWindow;

namespace AimmyWPF
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public int FovSize { get; set; } = 640;
        //public static Color FOVColor = Color.FromArgb(255, 255, 0, 0);

        public OverlayWindow()
        {
            InitializeComponent();

            // if you want to remove any of my additions in the FOV Size aspect, just look for "Nori's Additions"

            // Timer is disabled atm, I want to see if my changes does anything tangible.
            //var timer = new DispatcherTimer();
            //timer.Interval = TimeSpan.FromMilliseconds(250);
            //timer.Tick += Timer_Tick;
            //timer.Start();

            // Reference: https://www.codeproject.com/Questions/5363839/How-to-change-property-of-element-from-outside-of
            // not a good coder nori

            //SetCanvasDimensions();

            // Listens for any new stuff
            AwfulPropertyChanger.ReceiveColor = UpdateFOVColor;
            AwfulPropertyChanger.ReceiveFOVSize = UpdateFOVSize;

        }

        void UpdateFOVColor(Color NewColor)
        {
            // Similar idea to @iamgiga
            // maybe not the best way of doing it though :/
            // nori

            // Changes the color of ur fov circle
            OverlayCircle.Stroke = new SolidColorBrush(NewColor);
        }

        // Nori's Additions
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


        private void Timer_Tick(object sender, EventArgs e)
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
