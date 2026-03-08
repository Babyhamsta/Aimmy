using Other;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

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

                var encodedName = Uri.EscapeDataString(Name);
                var downloadUri = new Uri($"https://github.com/BabyHamsta/Aimmy/raw/Aimmy-V2/{Path}/{encodedName}");
                var downloadResult = await DownloadFileAsync(downloadUri, Path, Name);

                if (downloadResult)
                {
                    LogManager.Log(LogManager.LogLevel.Info, $"Downloaded {Name} to bin/{Path}/{Name}", true);
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
                LogManager.Log(LogManager.LogLevel.Error, $"Failed to download {name} from {uri}. Status: {response.StatusCode} - {response.ReasonPhrase}", true);
                return false;
            }

            var content = await response.Content.ReadAsByteArrayAsync();
            var filePath = Path.Combine("bin", path, name);

            string? dirPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath))
            {
                Directory.CreateDirectory(dirPath); // just in case
            }
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