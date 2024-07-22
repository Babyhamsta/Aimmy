using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using Visuality;

namespace Other
{
    internal class RequirementsManager
    {
        public static bool IsVCRedistInstalled()
        {
            // Visual C++ Redistributable for Visual Studio 2015, 2017, and 2019 check
            string regKeyPath = @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

            using (var key = Registry.LocalMachine.OpenSubKey(regKeyPath))
            {
                if (key != null && key.GetValue("Installed") != null)
                {
                    object? installedValue = key.GetValue("Installed");
                    return installedValue != null && (int)installedValue == 1;
                }
            }

            return false;
        }

        public static bool IsMemoryIntegrityEnabled() // false if enabled true if disabled, you want it disabled
        {
            //credits to Themida
            string keyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforceCodeIntegrity";
            string valueName = "Enabled";
            object? value = Registry.GetValue(keyPath, valueName, null);
            if (value != null && Convert.ToInt32(value) == 1)
            {
                new NoticeBar("You have Memory Integrity enabled, please disable it to use Logitech Driver", 7000).Show();
                return false;
            }
            else return true;
        }

        public static bool CheckForGhub()
        {
            try
            {
                Process? process = Process.GetProcessesByName("lghub").FirstOrDefault(); //gets the first process named "lghub"
                if (process == null)
                {
                    ShowLGHubNotRunningMessage();
                    return false;
                }

                string ghubfilepath = process.MainModule.FileName;
                if (ghubfilepath == null)
                {
                    new NoticeBar($"An error occurred. Run as admin and try again.", 6000).Show();
                    return false;
                }

                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(ghubfilepath);

                if (!versionInfo.ProductVersion.Contains("2021"))
                {
                    ShowLGHubImproperInstallMessage();
                    return false;
                }

                return true;
            }
            catch (AccessViolationException ex)
            {
                new NoticeBar($"An error occured: {ex.Message}\nRun as admin and try again.", 6000).Show();
                return false;
            }
        }

        private static void ShowLGHubNotRunningMessage()
        {
            if (MessageBox.Show("LG HUB is not running, is it installed?", "Aimmy - LG HUB Mouse Movement", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
            {
                if (MessageBox.Show("Would you like to install it?", "Aimmy - LG HUB Mouse Movement", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    new LGDownloader().Show();
                }
            }
        }

        private static void ShowLGHubImproperInstallMessage()
        {
            if (MessageBox.Show("LG HUB install is improper, would you like to install it?", "Aimmy - LG HUB Mouse Movement", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                new LGDownloader().Show();
            }
        }
    }
}