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
        private const string rzctlDownloadUrl = "https://github.com/MarsQQ/rzctl/releases/download/1.0.0/rzctl.dll";

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool init();

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_move(int x, int y, bool starting_point);

        [DllImport(rzctlpath, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mouse_click(int up_down);

        private static List<string> Razer_HID = [];

        #endregion Razer Variables

        public static bool CheckForRazerDevices()
        {
            Razer_HID.Clear();
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Manufacturer LIKE 'Razer%'");
            var razerDevices = searcher.Get().Cast<ManagementBaseObject>();

            Razer_HID.AddRange(razerDevices.Select(device => device["DeviceID"]?.ToString() ?? string.Empty));

            return Razer_HID.Count != 0;
        }

        public static async Task<bool> CheckRazerSynapseInstall() // returns true if running/installed and false if not installed/running
        {
            bool isSynapseRunning = Process.GetProcessesByName("RazerAppEngine").Any();

            if (isSynapseRunning) return true;

            var result = MessageBox.Show("Razer Synapse is not running, do you have it installed?",
                                         "Aimmy - Razer Synapse", MessageBoxButton.YesNo);
            Debug.WriteLine("Couldn't Find Process");
            if (result == MessageBoxResult.No)
            {
                await InstallRazerSynapse();
                return false;
            }

            bool isSynapseInstalled = Directory.Exists(@"C:\Program Files\Razer") ||
                                      Directory.Exists(@"C:\Program Files (x86)\Razer") ||
                                      CheckRazerRegistryKey();

            if (!isSynapseInstalled)
            {
                var installConfirmation = MessageBox.Show("Razer Synapse is not installed, would you like to install it?",
                                                          "Aimmy - Razer Synapse", MessageBoxButton.YesNo);

                if (installConfirmation == MessageBoxResult.Yes)
                {
                    await InstallRazerSynapse();
                    return false;
                }
            }

            return isSynapseInstalled;
        }

        private static bool CheckRazerRegistryKey()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Razer"))
            {
                return key != null ? true : false;
            }
        }

        private static async Task InstallRazerSynapse()
        {
            using HttpClient httpClient = new();
            var response = await httpClient.GetAsync(new Uri("https://rzr.to/synapse-new-pc-download-beta"));

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync($"{Path.GetTempPath()}\\rz.exe", content);

                Process.Start(new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Arguments = "/C start rz.exe",
                    WorkingDirectory = Path.GetTempPath()
                });

                new NoticeBar("Razer Synapse downloaded, please look for UAC prompt and install Razer Synapse.", 4000).Show();
            }
        }

        private static async Task downloadrzctl()
        {
            try
            {
                new NoticeBar($"{rzctlpath} is missing, attempting to download {rzctlpath}.", 4000).Show();

                using HttpClient httpClient = new();
                using var response = await httpClient.GetAsync(new Uri(rzctlDownloadUrl), HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(rzctlpath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                    await contentStream.CopyToAsync(fileStream);
                    new NoticeBar($"{rzctlpath} has downloaded successfully, please re-select Razer Synapse to load the DLL.", 4000).Show();
                }
            }
            catch
            {
                new NoticeBar($"{rzctlpath} has failed to install, please try a different Mouse Movement Method.", 4000).Show();
            }
        }

        public static async Task<bool> Load()
        {
            if (!await CheckRazerSynapseInstall())
            {
                return false;
            }

            if (!File.Exists(rzctlpath))
            {
                await downloadrzctl();
                return false;
            }

            if (!CheckForRazerDevices())
            {
                MessageBox.Show("No Razer Peripheral is detected, this Mouse Movement Method is unusable.", "Aimmy");
                return false;
            }

            try
            {
                return init();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unfortunately, Razer Synapse mode cannot be ran sufficiently.\n{ex}", "Aimmy");
                return false;
            }
        }
    }
}