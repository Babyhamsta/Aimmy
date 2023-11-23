using Microsoft.Win32;

namespace AimmyWPF
{
    internal class RequirementsManager
    {
        public bool IsVCRedistInstalled()
            {
                // Visual C++ Redistributable for Visual Studio 2015, 2017, and 2019 check
                string regKeyPath = @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64";

                using (var key = Registry.LocalMachine.OpenSubKey(regKeyPath))
                {
                    if (key != null)
                    {
                        object installedValue = key.GetValue("Installed");
                        return installedValue != null && (int)installedValue == 1;
                    }
                }

                return false;
            }

        }
}
