using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AimmyWPF.Class;

namespace AimmyWPF
{
    /// <summary>
    /// Interaction logic for OverlayWindow.xaml
    /// </summary>
    public partial class OverlayWindow : Window
    {
        public int FovSize { get; set; } = 640;

        public int CursorXPos = System.Windows.Forms.Cursor.Position.X;
        public int CursorYPos = System.Windows.Forms.Cursor.Position.Y;
        public static double CursorHeight = SystemParameters.CursorHeight / (SystemParameters.CursorWidth / 2);
        public static double CursorWidth = SystemParameters.CursorWidth / (SystemParameters.CursorWidth / 2);

        public OverlayWindow()
        {
            InitializeComponent();
            this.Title = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location);

            // Listens for any new stuff
            AwfulPropertyChanger.ReceiveColor = UpdateFOVColor;
            AwfulPropertyChanger.ReceiveFOVSize = UpdateFOVSize;
            AwfulPropertyChanger.ReceiveTravellingFOV = UpdateFOVState;

            TravellingFOVTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Normal, async delegate
            {
                CursorXPos = System.Windows.Forms.Cursor.Position.X;
                CursorYPos = System.Windows.Forms.Cursor.Position.Y;
                
                OverlayCircle.Margin = new Thickness(
                    CursorXPos - ((OverlayCircle.Width / 2) - CursorWidth),
                    CursorYPos - ((OverlayCircle.Height / 2) - CursorHeight),
                    0, 0);
            }, Application.Current.Dispatcher);
        }

        DispatcherTimer TravellingFOVTimer;
        void UpdateFOVState(bool TravellingFOV = false)
        {
            if (TravellingFOV == true)
            {
                OverlayCircle.HorizontalAlignment = HorizontalAlignment.Left;
                OverlayCircle.VerticalAlignment = VerticalAlignment.Top;
                TravellingFOVTimer.Start();
            }
            else
            {
                TravellingFOVTimer.Stop();
                OverlayCircle.HorizontalAlignment = HorizontalAlignment.Center;
                OverlayCircle.VerticalAlignment = VerticalAlignment.Center;
                OverlayCircle.Margin = new Thickness(0,0,0,0);
            }
        }

        void UpdateFOVColor(Color NewColor) => OverlayCircle.Stroke = new SolidColorBrush(NewColor);

        void UpdateFOVSize()
        {
            //// Update circle dimensions.
            OverlayCircle.Width = FovSize;
            OverlayCircle.Height = FovSize;

            //// Get screen dimensions.
            //double screenWidth = SystemParameters.PrimaryScreenWidth;
            //double screenHeight = SystemParameters.PrimaryScreenHeight;

            //// Update the Canvas dimensions
            //OverlayCanvas.Width = screenWidth;
            //OverlayCanvas.Height = screenHeight;

            //// Update circle position within the Canvas.
            //Canvas.SetLeft(OverlayCircle, (screenWidth - FovSize) / 2);
            //Canvas.SetTop(OverlayCircle, (screenHeight - FovSize) / 2);

            //// Update OverlayWindow position to be centered on the screen.
            //this.Left = 0;
            //this.Top = 0;
            //this.Width = screenWidth;
            //this.Height = screenHeight;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
