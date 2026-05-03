using Aimmy2.Resources;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.RazerSupport
{
    internal class RZMouse
    {
        #region Razer Variables

        private const string rzctlpath = "rzctl.dll";
        private const string rzctlDownloadUrl_Debug = "https://github.com/MarsQQ/rzctl/releases/download/1.0.0/rzctl.dll";
        private const string rzctlDownloadUrl_Release = "https://github.com/camilia2o7/rzctl/releases/download/Release/rzctl.dll";

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool init();

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_move(int x, int y, bool starting_point);

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_click(int up_down);

        private static readonly List<string> Razer_HID = [];

        private static bool vcRedistPromptRejected = false;

        #endregion
        public static async Task<bool> Load()
        {
            if (!await EnsureRazerSynapseInstalled())
                return false;

            if (!File.Exists(rzctlpath))
            {
                await DownloadAppropriateRzctl();
                return false;
            }

            if (!DetectRazerDevices())
            {
                new NoticeBar(LocalizationManager.GetString("Msg_RazerNoDevice"), 5000).Show();
                return false;
            }
            try
            {
                return init();
            }
            catch (BadImageFormatException)
            {
                new NoticeBar(LocalizationManager.GetString("Msg_rzctlIncompatible"), 4000).Show();
                await DownloadRzctl(rzctlDownloadUrl_Release);
                return false;
            }
            // Hopefully this method will solve the issue for the users who have/don't have the drivers
            // If the user gets this error, Failed to initialize Razer mode, then it will attempt to download the Release version.
            // And if that still doesn't work, then it will error again, which in this case would mean they don't have the driver for vs &&|| vc 2015–2022
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.GetString("Msg_rzctlInitFailed", ex.Message),
                                LocalizationManager.GetString("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                try
                {
                    if (File.Exists(rzctlpath))
                        File.Delete(rzctlpath);

                    await DownloadRzctl(rzctlDownloadUrl_Release);

                    new NoticeBar(LocalizationManager.GetString("Msg_rzctlReplaced"), 5000).Show();
                }
                catch (Exception innerEx)
                {
                    MessageBox.Show(LocalizationManager.GetString("Msg_rzctlRecoveryFailed", innerEx.Message),
                            LocalizationManager.GetString("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return false;
            }
            /* Commenting this method out to replace with a more enhanced version
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize Razer mode.\n{ex.Message}",
                        "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            */
        }
        #region Razer Synapse & Device Checks

        private static bool DetectRazerDevices()
        {
            Razer_HID.Clear();
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE 'Razer%'");
            var devices = searcher.Get().Cast<ManagementBaseObject>();

            Razer_HID.AddRange(devices.Select(d => d["DeviceID"]?.ToString() ?? string.Empty));
            return Razer_HID.Count > 0;
        }

        private static async Task<bool> EnsureRazerSynapseInstalled()
        {
            if (Process.GetProcessesByName("RazerAppEngine").Any())
                return true;

            var response = MessageBox.Show(LocalizationManager.GetString("Msg_RazerNotRunning"),
                                           "Aimmy - Razer Synapse", MessageBoxButton.YesNo);
            if (response == MessageBoxResult.No)
            {
                await DownloadAndInstallRazerSynapse();
                return false;
            }

            if (!IsRazerSynapseInstalled())
            {
                var install = MessageBox.Show(LocalizationManager.GetString("Msg_RazerNotInstalled"),
                                              "Aimmy - Razer Synapse", MessageBoxButton.YesNo);
                if (install == MessageBoxResult.Yes)
                {
                    await DownloadAndInstallRazerSynapse();
                    return false;
                }
                return false;
            }

            return true;
        }

        private static bool IsRazerSynapseInstalled()
        {
            return Directory.Exists(@"C:\Program Files\Razer") ||
                   Directory.Exists(@"C:\Program Files (x86)\Razer") ||
                   Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Razer") != null;
        }

        private static async Task DownloadAndInstallRazerSynapse()
        {
            try
            {
                using HttpClient client = new();
                var response = await client.GetAsync("https://rzr.to/synapse-new-pc-download-beta");
                if (!response.IsSuccessStatusCode)
                {
                    new NoticeBar(LocalizationManager.GetString("Msg_RazerSynapseDownloadFailed"), 4000).Show();
                    return;
                }

                string path = Path.Combine(Path.GetTempPath(), "rz.exe");
                var content = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(path, content);

                Process.Start(new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C start rz.exe",
                    WorkingDirectory = Path.GetTempPath()
                });

                new NoticeBar(LocalizationManager.GetString("Msg_RazerDownloaded"), 4000).Show();
            }
            catch
            {
                new NoticeBar(LocalizationManager.GetString("Msg_RazerInstallError"), 4000).Show();
            }
        }

        #endregion

        #region Visual Studio & Redist Checks

        private static bool IsVcRedistInstalled()
        {
            string[] keys =
            {
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                @"SOFTWARE\Microsoft\VisualStudio\17.0\VC\Runtimes\x64"
            };

            foreach (string path in keys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(path);
                if (key != null && Convert.ToInt32(key.GetValue("Installed", 0)) == 1)
                    return true;
            }

            return false;
        }

        private static bool IsVisualStudioInstalled()
        {
            string[] uninstallRoots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string root in uninstallRoots)
            {
                using var key = Registry.LocalMachine.OpenSubKey(root);
                if (key == null) continue;

                foreach (string subKey in key.GetSubKeyNames())
                {
                    using var sub = key.OpenSubKey(subKey);
                    string name = sub?.GetValue("DisplayName") as string ?? "";

                    if (name.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region rzctl.dll Download Logic

        private static async Task DownloadAppropriateRzctl()
        {
            if (IsVisualStudioInstalled())
            {
                if (!await DownloadRzctl(rzctlDownloadUrl_Debug))
                {
                    await DownloadRzctl(rzctlDownloadUrl_Release);
                }
            }
            else
            {
                if (!IsVcRedistInstalled())
                {
                    if (!vcRedistPromptRejected)
                    {
                        var prompt = MessageBox.Show(LocalizationManager.GetString("Msg_RedistMissing"),
                                                     LocalizationManager.GetString("Msg_ErrorTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (prompt == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            vcRedistPromptRejected = true;
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                await DownloadRzctl(rzctlDownloadUrl_Release);

            }
        }


        private static async Task<bool> DownloadRzctl(string url)
        {
            try
            {
                new NoticeBar(LocalizationManager.GetString("Msg_rzctlMissing"), 4000).Show();

                using HttpClient client = new();
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    new NoticeBar(LocalizationManager.GetString("Msg_rzctlDownloadFailed_Simple"), 4000).Show();
                    return false;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = new FileStream(rzctlpath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await stream.CopyToAsync(file);

                new NoticeBar(LocalizationManager.GetString("Msg_rzctlDownloaded"), 5000).Show();
                return true;
            }
            catch
            {
                new NoticeBar(LocalizationManager.GetString("Msg_rzctlDownloadError_Simple"), 4000).Show();
                return false;
            }
        }
        #endregion
    }
}
