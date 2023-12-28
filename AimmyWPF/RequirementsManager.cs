using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

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
                if (key != null && key.GetValue("Installed") != null)
                {
                    object installedValue = key.GetValue("Installed");
                    return installedValue != null && (int)installedValue == 1;
                }
            }

            return false;
        }
        //public bool IsDotNetInstalled() { not working atm.. weird
        //    //Checking for 7.0 .NET
        //    string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        //    string dotnetPath = Path.Combine(programFilesPath, "dotnet");

        //    if (Directory.Exists(dotnetPath))
        //    {
        //        string dotnetExePath = Path.Combine(dotnetPath, "dotnet.exe");
        //        if (!File.Exists(dotnetExePath)) return false;
        //        var dotnetFileVersion = FileVersionInfo.GetVersionInfo(dotnetExePath);
        //        Version version = Version.Parse(dotnetFileVersion?.ProductVersion);
        //        if (version != null && version.Major >= 7) return true;
        //    }
        //    return false;
        //}
    }
}