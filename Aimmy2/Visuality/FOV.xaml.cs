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
        public Rectangle Area { get; set; } = Screen.PrimaryScreen.Bounds;

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
                FOVStrictEnclosure.Margin = new Thickness(
                    Convert.ToInt16(Area.Width / 2 / WinAPICaller.scalingFactorX) - 320,
                    Convert.ToInt16(Area.Height / 2 / WinAPICaller.scalingFactorY) - 320,
                    0, 0));
        }

        public static void Create()
        {
            Instance = new FOV();
        }
    }
}