using AimmyWPF.Class;
using SecondaryWindows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for ADownloadGateway.xaml
    /// </summary>
    public partial class ADownloadGateway : UserControl
    {

        public ADownloadGateway(string Text)
        {
            InitializeComponent();
            Title.Content = Text;

            DownloadButton.Click += async (s, e) =>
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.DownloadFileAsync(new Uri($"https://github.com/Babyhamsta/Aimmy/raw/master/models/{Text}"), $"bin\\models\\{Text}");
                    while (webClient.IsBusy)
                    {
                        await Task.Delay(1000);
                    }
                    webClient.Dispose();
                }
                new NoticeBar("The file has been completed.").Show();
                //await Task.Delay(1000);
                (this.Parent as StackPanel).Children.Remove(this);
            };
        }
    }
}
