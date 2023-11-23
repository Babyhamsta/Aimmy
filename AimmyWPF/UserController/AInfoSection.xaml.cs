using Newtonsoft.Json.Linq;
using SecondaryWindows;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AimmyWPF.UserController
{
    /// <summary>
    /// Interaction logic for AInfoSection.xaml
    /// </summary>
    public partial class AInfoSection : UserControl
    {
        static async Task<(string, string)> GetLatestReleaseInfo()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Aimmy"); // GitHub API requires a user-agent

                var response = await client.GetStringAsync("https://api.github.com/repos/Babyhamsta/Aimmy/releases");
                JArray releases = JArray.Parse(response);

                string tagName = releases[0]["tag_name"].ToString();
                string downloadUrl = releases[0]["assets"][0]["browser_download_url"].ToString();

                return (tagName, downloadUrl);
            }
        }

        static async Task DoUpdate(string currentVersion)
        {
            // Download the newest release of Aimmy to %temp%
            string envTempPath = Path.GetTempPath();
            string localZipPath = Path.Combine(envTempPath, "AimmyUpdate.zip");
            var (latestVersion, latestZipUrl) = await GetLatestReleaseInfo();

            if (string.IsNullOrEmpty(latestVersion) || string.IsNullOrEmpty(latestZipUrl))
            {
                new NoticeBar("Failed to get latest release information from github.").Show();
                return;
            }
            else
            {
                if (latestVersion == currentVersion)
                {
                    new NoticeBar("You are up to date.").Show();
                    return;
                }

                new NoticeBar("Update was found, downloading update.").Show();
            }

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(new Uri(latestZipUrl), HttpCompletionOption.ResponseHeadersRead);
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(localZipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }

            // Extract update to %temp%
            new NoticeBar("Extracting update zip in %temp%.").Show();
            string extractPath = Path.Combine(envTempPath, "AimmyUpdate");
            await Task.Run(() => // Run extraction in a separate task
            {
                ZipFile.ExtractToDirectory(localZipPath, extractPath, true);
            });

            // Create a batch script to move the files and restart Aimmy
            string mainAppPath = Process.GetCurrentProcess().MainModule.FileName;
            string mainAppDir = Path.GetDirectoryName(mainAppPath);
            string batchScriptPath = Path.Combine(mainAppDir, "update.bat");

            using (StreamWriter sw = new StreamWriter(batchScriptPath))
            {
                sw.WriteLine("@echo off");
                sw.WriteLine("timeout /t 3 /nobreak");
                sw.WriteLine($"xcopy /Y \"{extractPath}\\*\" \"{mainAppDir}\"");
                sw.WriteLine($"start \"\" \"{mainAppPath}\"");
                sw.WriteLine($"del /f \"{localZipPath}\"");
                sw.WriteLine($"rd /s /q \"{extractPath}\"");
                sw.WriteLine($"del /f \"{batchScriptPath}\"");
                sw.WriteLine($"( del /F /Q \"%~f0\" >nul 2>&1 & exit ) >nul");
            }

            Process.Start(batchScriptPath);
            Environment.Exit(0);
        }

        public AInfoSection()
        {
            InitializeComponent();
            CheckForUpdates.Click += async (s, e) => {
                new NoticeBar("Checking for updates, please wait!").Show();
                await DoUpdate(VersionNumber.Content.ToString());
            };
        }
    }
}
