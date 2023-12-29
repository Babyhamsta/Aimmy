using Class;
using SecondaryWindows;
using System;
using System.Net;
using System.Windows.Controls;

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
                if (DownloadButton.Content != "\xE895")
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
                }
            };
        }
    }
}