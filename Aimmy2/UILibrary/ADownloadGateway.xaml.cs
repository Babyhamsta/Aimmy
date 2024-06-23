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
        public ADownloadGateway(string APIurl, string Name, string Path)
        {
            InitializeComponent();
            Title.Content = Name;

            DownloadButton.Click += async (s, e) =>
            {
                if ((string)DownloadButton.Content != "\xE895")
                {
                    using HttpClient httpClient = new();

                    DownloadButton.Content = "\xE895";
                    var response = await httpClient.GetAsync(new Uri(APIurl));
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync($"bin\\{Path}\\{Name}", content);
                        new NoticeBar("The file has been completed.", 4000).Show();
                        if (Parent is StackPanel stackPanel)
                        {
                            Application.Current.Dispatcher.Invoke(() => stackPanel.Children.Remove(this));
                        }
                    }
                }
            };
        }
    }
}