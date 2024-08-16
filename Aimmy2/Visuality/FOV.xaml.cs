using System.Drawing;
using Aimmy2.Class;
using Class;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Aimmy2.Config;
using Application = System.Windows.Application;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for FOV.xaml
    /// </summary>
    public partial class FOV : Window
    {

        public static FOV Instance { get; private set; }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);
        }

        public FOV()
        {
            InitializeComponent();
            
            AppConfig.BindToDataContext(this);
            _ = UpdateStrictEnclosure();
        }

        public async Task UpdateStrictEnclosure()
        {
            await Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var targetScreen = AIManager.Instance?.ImageCapture?.Screen ?? Screen.PrimaryScreen;
                var area = AIManager.Instance?.ImageCapture?.GetCaptureArea() ?? Screen.PrimaryScreen.Bounds;
                
                var cursorPosition = WinAPICaller.GetCursorPosition();
                
                var targetX = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.X - area.Left : area.Width / 2;
                var targetY = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.Y - area.Top : area.Height / 2;

                var centerX = area.Left + targetX;
                var centerY = area.Top + targetY;

                FOVStrictEnclosure.Margin = new Thickness(
                    Convert.ToInt16(centerX / WinAPICaller.scalingFactorX) - 320,
                    Convert.ToInt16(centerY / WinAPICaller.scalingFactorY) - 320,
                    0, 0);
            });
        }


        public static void Create()
        {
            Instance = new FOV();
        }
    }
}