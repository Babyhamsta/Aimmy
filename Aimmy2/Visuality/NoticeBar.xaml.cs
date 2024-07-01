using Aimmy2.Class;
using AimmyWPF.Class;
using System.Windows;
using System.Windows.Interop;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for NoticeBar.xaml
    /// </summary>
    public partial class NoticeBar : Window
    {
        private static int openNoticeCount = 0;
        private const int NoticeHeight = 40; // Height of each notice
        private const int Spacing = 5;       // Spacing between notices
        private const int BaseMargin = 100;  // Base margin from the bottom
        private readonly int WaitingTime = 4000;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);
        }

        public NoticeBar(string text, int waitingTime)
        {
            InitializeComponent();
            ContentText.Content = text;
            Loaded += OnLoaded;
            WaitingTime = waitingTime;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AdjustMargin();
            ShowNotice();
        }

        private async void ShowNotice()
        {
            openNoticeCount++;
            Animator.Fade(Notice);
            await Task.Delay(WaitingTime);
            Animator.FadeOut(Notice);
            await Task.Delay(1000);
            CloseNotice();
        }

        private void CloseNotice()
        {
            openNoticeCount--;
            AdjustMarginsForAll();
            Close();
        }

        private void AdjustMargin()
        {
            int bottomMargin = BaseMargin + (openNoticeCount * (NoticeHeight + Spacing));
            Notice.Margin = new Thickness(0, 0, 0, bottomMargin);
            Notice.Height = double.NaN;
        }

        private static void AdjustMarginsForAll()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window window in Application.Current.Windows.OfType<NoticeBar>())
                {
                    (window as NoticeBar)?.AdjustMargin();
                }
            });
        }
    }
}