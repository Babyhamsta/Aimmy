using Aimmy2.Class;
using Aimmy2.Resources;
using Aimmy2.Theme;
using Class;
using Other;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for LGDownloader.xaml
    /// </summary>
    public partial class LGDownloader : Window
    {
        private const string CorrectHash = "33-DF-A8-5A-63-22-40-F8-73-F9-B8-E5-D9-8A-0C-A6";
        private const long CorrectFileSize = 41131424;

        private string FilePath = $"{Path.GetTempPath()}\\lghub.exe";

        public LGDownloader()
        {
            InitializeComponent();

            LGDownloaderTitleLabel.Content = LocalizationManager.GetString("LG_LGDownloader");
            DownloadInstructionLabel.Content = LocalizationManager.GetString("LG_ClickToDownload");
            LGDirectButton.Content = LocalizationManager.GetString("LG_LGDirect");
            SupercometButton.Content = LocalizationManager.GetString("LG_SupercometCDN");
            DeveloperGitHubButton.Content = LocalizationManager.GetString("LG_DeveloperGitHub");

            // Initialize theme colors
            UpdateThemeColors();

            // Subscribe to theme changes
            ThemeManager.RegisterElement(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, System.Windows.Media.Color newColor)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateThemeColors();
            });
        }

        private void UpdateThemeColors()
        {
            // Update gradient colors
            ThemeGradientStop.Color = ThemeManager.ThemeColorDark;
        }

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private double currentGradientAngle = 0;

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var CurrentMousePos = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(CurrentMousePos.X, CurrentMousePos.Y));
                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
                if (angleDifference > 180)
                {
                    angleDifference -= 360;
                }

                angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        #endregion Window Controls

        /// <summary>
        /// Reference 1: https://stackoverflow.com/questions/1380839/how-do-you-get-the-file-size-in-c
        /// Reference 2: https://stackoverflow.com/questions/10520048/calculate-md5-checksum-for-a-file
        /// -Nori
        /// </summary>
        private bool CheckFileValidity()
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(FilePath);
            var currentHash = BitConverter.ToString(md5.ComputeHash(stream));
            var currentFileSize = new FileInfo(FilePath).Length;

            return currentHash == CorrectHash && currentFileSize == CorrectFileSize;
        }

        private async void DownloadFile(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                LogManager.Log(LogManager.LogLevel.Info, LocalizationManager.GetString("Msg_LGHubDownloading"), true);

                using HttpClient httpClient = new();

                string? tagString = clickedButton.Tag?.ToString();
                if (!string.IsNullOrEmpty(tagString))
                {
                    var response = await httpClient.GetAsync(new Uri(tagString));
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(FilePath, content);
                    }
                    LogManager.Log(LogManager.LogLevel.Info, LocalizationManager.GetString("Msg_LGHubVerifying"), true);
                }

                if (CheckFileValidity())
                {
                    LogManager.Log(LogManager.LogLevel.Info, LocalizationManager.GetString("Msg_LGHubVerified"), true);
                    LogManager.Log(LogManager.LogLevel.Warning, LocalizationManager.GetString("Msg_LGHubAutoUpdateWarning"), true, 20000);
                    Process.Start(new ProcessStartInfo
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        FileName = "cmd.exe",
                        Arguments = "/C start lghub.exe",
                        WorkingDirectory = Path.GetTempPath()
                    });
                    Close();
                }
                else
                {
                    LogManager.Log(LogManager.LogLevel.Error, LocalizationManager.GetString("Msg_LGHubFileError_Simple"), true);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unregister from theme manager
            ThemeManager.ThemeChanged -= OnThemeChanged;
            ThemeManager.UnregisterElement(this);
            base.OnClosed(e);
        }
    }
}