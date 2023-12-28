using AimmyWPF.Class;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

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
        public static double CursorHeight = SystemParameters.CursorHeight / 4;
        public static double CursorWidth = SystemParameters.CursorWidth / 4;

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
                // Perform asynchronous cursor position update
                await Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CursorXPos = System.Windows.Forms.Cursor.Position.X;
                        CursorYPos = System.Windows.Forms.Cursor.Position.Y;

                        // Use UI thread to update UI elements
                        OverlayCircle.Margin = new Thickness(
                            CursorXPos - (OverlayCircle.Width / 2),
                            CursorYPos - (OverlayCircle.Height / 2),
                            0, 0);
                    });
                });
            }, Application.Current.Dispatcher);
        }

        private DispatcherTimer TravellingFOVTimer;

        private void UpdateFOVState(bool TravellingFOV = false)
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
                OverlayCircle.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void UpdateFOVColor(Color NewColor) => OverlayCircle.Stroke = new SolidColorBrush(NewColor);

        private void UpdateFOVSize()
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