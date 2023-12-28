using AimmyWPF.Class;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SecondaryWindows
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

        public NoticeBar(string text)
        {
            InitializeComponent();
            ContentText.Content = text;
            Loaded += OnLoaded;
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
            await Task.Delay(4000);
            Animator.FadeOut(Notice);
            await Task.Delay(1000);
            CloseNotice();
        }

        private void CloseNotice()
        {
            openNoticeCount--;
            AdjustMarginsForAll();
            this.Close();
        }

        private void AdjustMargin()
        {
            int bottomMargin = BaseMargin + (openNoticeCount * (NoticeHeight + Spacing));
            Notice.Margin = new Thickness(0, 0, 0, bottomMargin);
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