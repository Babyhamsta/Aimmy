using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using Visuality;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ADownloadGateway.xaml
    /// </summary>
    public partial class ADownloadGateway : UserControl
    {
        private static readonly HttpClient httpClient = new();

        public ADownloadGateway(string Name, string Path)
        {
            InitializeComponent();
            Title.Content = Name;

            DownloadButton.Click += async (s, e) =>
            {
                if ((string)DownloadButton.Content == "\xE895") return;

                DownloadButton.Content = "\xE895";
                SetupHttpClientHeaders();

                var downloadUri = new Uri($"https://github.com/BabyHamsta/Aimmy/raw/master/{Path}/{Name}");
                var downloadResult = await DownloadFileAsync(downloadUri, Path, Name);

                if (downloadResult)
                {
                    new NoticeBar("The download has been completed.", 4000).Show();
                    RemoveFromParent();
                }
                else
                {
                    DownloadButton.Content = "\xE896"; // Consider resetting this in both cases for consistency
                }
            };
        }

        private static void SetupHttpClientHeaders()
        {
            if (!httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Aimmy2");
            }
            if (!httpClient.DefaultRequestHeaders.Contains("Accept"))
            {
                httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            }
        }

        private static async Task<bool> DownloadFileAsync(Uri uri, string path, string name)
        {
            var response = await httpClient.GetAsync(uri);

            if (!response.IsSuccessStatusCode)
            {
                new NoticeBar($"Download Failed, {response.StatusCode}, {response.ReasonPhrase}", 4000).Show();
                return false;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var filePath = Path.Combine("bin", path, name);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)); // just in case
            await File.WriteAllBytesAsync(filePath, content);
            return true;
        }

        private void RemoveFromParent() // lol
        {
            if (Parent is StackPanel stackPanel)
            {
                Application.Current.Dispatcher.Invoke(() => stackPanel.Children.Remove(this));
            }
        }
    }
}