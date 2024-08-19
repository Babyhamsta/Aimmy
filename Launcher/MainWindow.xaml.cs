using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;

using System.Windows;
using System.Windows.Input;
using Core;
using Vestris.ResourceLib;


namespace Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseDialog
    {
        private static readonly Random _random = new Random();
        private string _status;
        private bool _isInstallerMode;
        private string _version;
        private string? _installDirectory;
        private string _subTitle = "Please wait...";
        private bool _installing;
        private bool _canClose = true;
        private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public Visibility InstallerVisibility => IsInstallerMode ? Visibility.Visible : Visibility.Collapsed;
        public bool IsInstallerMode
        {
            get => _isInstallerMode;
            set {
                if (SetField(ref _isInstallerMode, value))
                {
                    OnPropertyChanged(nameof(InstallerVisibility));
                }
            }
        }

        public bool CanClose
        {
            get => _canClose;
            set => SetField(ref _canClose, value);
        }

        public bool Installing
        {
            get => _installing;
            set => SetField(ref _installing, value);
        }

        public string SubTitle
        {
            get => _subTitle;
            set => SetField(ref _subTitle, value);
        }

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public string Version
        {
            get => _version;
            set => SetField(ref _version, value);
        }

        public string InstallDirectory
        {
            get => _installDirectory ?? Path.GetDirectoryName(Environment.ProcessPath);
            set => SetField(ref _installDirectory, value);
        }

        public MainWindow()
        {
            Title = "AI-M";
            InitializeComponent();
            
            DataContext = this;
            Task.Delay(400).ContinueWith(_ => Execute());
        }


        private async Task Execute()
        {
            Status = "Search executable...";
            await Task.Delay(100);
            var exe = FindExe();
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                IsInstallerMode = true;
                SubTitle = "Install";
                Status = string.Empty;
            }
            else
            {
                 ChangeResources(exe);
                await Task.Delay(200);
                await RenameExe(exe);
            }
        }

        private void ChangeResources(string exe)
        {
            try
            {
                var versionResource = new VersionResource();
                versionResource.LoadFrom(exe);
                Version = versionResource.FileVersion;
                //string newName = GenerateRandomString(15).ToUpper();
                //var resource = versionResource["StringFileInfo"];
                //var fi = resource as StringFileInfo;

                //foreach (var table in fi.Strings.Select(pair => pair.Value))
                //{
                //    table["CompanyName"] = newName;
                //    table["FileDescription"] = newName;
                //    table["InternalName"] = $"{newName}.dll";
                //    table["OriginalFilename"] = $"{newName}.dll";
                //    table["ProductName"] = newName;
                //}


                //versionResource.SaveTo(exe);

            }
            catch (Exception e)
            {
                Status = $"Error: {e.Message}";
            }
        }

        public static Assembly LoadAssemblyViaStream(string assemblyLocation)
        {
            byte[] file = null;
            int bufferSize = 1024;
            using (FileStream fileStream = File.Open(assemblyLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    byte[] buffer = new byte[bufferSize];
                    int readBytesCount = 0;
                    while ((readBytesCount = fileStream.Read(buffer, 0, bufferSize)) > 0)
                        memoryStream.Write(buffer, 0, readBytesCount);
                    file = memoryStream.ToArray();
                }
            }

            return Assembly.Load(file);
        }
        

        private string GenerateRandomString(int length = 8)
        {
            return new string(Enumerable.Repeat(_chars, length)
                .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        private async Task RenameExe(string exe)
        {
            string newName = $"{GenerateRandomString()}.exe";
            var newExe = Path.Combine(Path.GetDirectoryName(exe), newName);
            Status = $"Shuffle name to {newName}";
            await Task.Delay(100); 
            File.Move(exe, newExe);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(newExe)
                {
                    UseShellExecute = true
                }
            };

            process.Start();
            process.WaitForInputIdle();

            await Task.Delay(3000);

            await Dispatcher.Invoke(() =>
            {
                Close();
                System.Windows.Application.Current.Shutdown();
                return Task.CompletedTask;
            });
        }


        private string FindExe()
        {
            var launcherExe = Process.GetCurrentProcess().MainModule.FileName;
            var currentDir = Path.GetDirectoryName(launcherExe);

            var l = Directory.EnumerateFiles(currentDir, "*.exe").Where(x => x != launcherExe && Path.GetFileName(x) != "createdump.exe" && Path.GetFileName(x) != "Installer.exe").ToList();
            if(l.Count == 1)
                return l[0];
            l = l.Where(n => Path.GetFileNameWithoutExtension(n).Length == 8).ToList();
            if(l.Count == 1)
                return l[0];
            return l.FirstOrDefault();
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Exit_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Minimize_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            Installing = true;
            CanClose = false;
            FolderSelect.Visibility = Visibility.Collapsed;
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Visibility = Visibility.Visible;
           
            Status = "Installing (Check and create Directory)...";
            var dir = InstallDirectory;
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                await Task.Delay(500);
                Status = "Checking for latest version...";
                var installer = new UpdateManager(dir);
                var canInstall = await installer.CheckForUpdate(null, "fgilde", "AI-Ming");
                ProgressBar.IsIndeterminate = false;
                if (!canInstall)
                {
                    Status = "Can not install";
                    return;
                }

                await installer.DoUpdate(new Progress<double>(p =>
                {
                    ProgressBar.Value = p;
                    Status = $"Downloading... {p:0.00}%";
                }));

            }
            catch (Exception exception)
            {
                Status = $"Error: {exception.Message}";
                return;
            }
            finally
            {
                Installing = false;
            }
        }

        private void SelectDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            dlg.InitialDirectory = InstallDirectory;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallDirectory = dlg.SelectedPath;
            }
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            e.Cancel = !CanClose;
        }
    }
}