using AimmyWPF.Class;
using Class;
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

        public ADownloadGateway(string Text, string Path)
        {
            InitializeComponent();
            Title.Content = Text;

            DownloadButton.Click += async (s, e) =>
            {
                using (WebClient webClient = new WebClient())
                {
                    DownloadButton.Content = "\xE895";
                    new NoticeBar("The download is being parsed.").Show();
                    webClient.DownloadFileAsync(new Uri($"https://github.com/{RetrieveGithubFiles.RepoOwner}/{RetrieveGithubFiles.RepoName}/raw/master/{Path}/{Text}"), $"bin\\{Path}\\{Text}");
                    webClient.DownloadProgressChanged += (s, e) => DownloadProgress.Value = e.ProgressPercentage;

                    webClient.DownloadFileCompleted += (s, e) =>
                    {
                        webClient.Dispose();
                        new NoticeBar("The file has been completed.").Show();
                        (this.Parent as StackPanel).Children.Remove(this);
                    };
                }
            };
        }
    }
}
