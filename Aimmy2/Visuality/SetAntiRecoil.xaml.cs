using Aimmy2;
using Aimmy2.Class;
using AimmyWPF.Class;
using InputLogic;
using System.Windows;
using System.Windows.Threading;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for SetAntiRecoil.xaml
    /// </summary>
    public partial class SetAntiRecoil : Window
    {
        private MainWindow MainWin { get; set; }
        private DispatcherTimer HoldDownTimer = new DispatcherTimer();
        private DateTime LastClickTime;
        private int FireRate;
        private int ChangingFireRate;

        public SetAntiRecoil(MainWindow MW)
        {
            InitializeComponent();

            MW.WindowState = WindowState.Minimized;

            MainWin = MW;

            BulletBorder.Opacity = 0;
            BulletBorder.Margin = new Thickness(0, 0, 0, -140);

            HoldDownTimer.Tick += HoldDownTimerTicker;
            HoldDownTimer.Interval = TimeSpan.FromMilliseconds(1);
            HoldDownTimer.Start();

            ChangingFireRate = (int)Dictionary.AntiRecoilSettings["Fire Rate"];
        }

        private void HoldDownTimerTicker(object? sender, EventArgs e)
        {
            if (InputBindingManager.IsHoldingBinding("Anti Recoil Keybind"))
            {
                GetReading();
                HoldDownTimer.Stop();
            }
        }

        private async void GetReading()
        {
            LastClickTime = DateTime.Now;
            while (InputBindingManager.IsHoldingBinding("Anti Recoil Keybind"))
            {
                await Task.Delay(1);
            }
            FireRate = (int)(DateTime.Now - LastClickTime).TotalMilliseconds;

            Animator.Fade(BulletBorder);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), BulletBorder, BulletBorder.Margin, new Thickness(0, 0, 0, 100));

            UpdateFireRate();
        }

        private void BulletNumberTextbox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (BulletBorder.Opacity == 1 && BulletBorder.Margin == new Thickness(0, 0, 0, 100))
            {
                UpdateFireRate();
            }
        }

        private void UpdateFireRate()
        {
            if (BulletNumberTextbox.Text != null && BulletNumberTextbox.Text.Any(char.IsDigit))
            {
                ChangingFireRate = (int)(FireRate / Convert.ToInt64(BulletNumberTextbox.Text));
            }
            else
            {
                ChangingFireRate = FireRate;
            }

            SettingLabel.Content = $"Fire Rate has been set to {ChangingFireRate}ms, please confirm to save it.";
        }

        private void ConfirmB_Click(object sender, RoutedEventArgs e)
        {
            Dictionary.AntiRecoilSettings["Fire Rate"] = ChangingFireRate;
            MainWin.uiManager.S_FireRate!.Slider.Value = ChangingFireRate;

            MainWin.WindowState = WindowState.Normal;

            new NoticeBar("The Fire Rate is set successfully.", 5000).Show();

            Close();
        }

        private void TryAgainB_Click(object sender, RoutedEventArgs e)
        {
            SettingLabel.Content = $"Press and hold the mouse button the bullet is removed.";

            Animator.FadeOut(BulletBorder);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), BulletBorder, BulletBorder.Margin, new Thickness(0, 0, 0, -140));

            HoldDownTimer.Start();
        }
    }
}