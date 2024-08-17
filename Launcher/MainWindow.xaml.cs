using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Aimmy2;
using dnlib.DotNet;
using Visuality;


namespace Launcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : BaseDialog
    {
        private static readonly Random _random = new Random();
        private string _status;
        private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public string Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        public MainWindow()
        {
            Title = ApplicationConstants.ApplicationName;
            InitializeComponent();
            DataContext = this;
            Activated += (s, e) => _ = Task.Run(Execute);
        }

        private async Task Execute()
        {
            Status = "Search executable...";
            await Task.Delay(100);
            var exe = FindExe();
            //ChangeAssemblyTitle(exe, ApplicationConstants.ApplicationName);
            await RenameExe(exe);
        }

        private void ChangeAssemblyTitle(string assemblyPath, string newTitle)
        {
            Status = $"Change assembly title to {newTitle}";
            var module = ModuleDefMD.Load(assemblyPath);

            var assemblyTitleAttribute = module.CorLibTypes.GetTypeRef("System.Reflection", "AssemblyTitleAttribute");
            var constructor = new MemberRefUser(module, ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String), assemblyTitleAttribute);

            var newAttribute = new CustomAttribute(constructor);
            newAttribute.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, new UTF8String(newTitle)));

            var existingTitleAttribute = module.Assembly.CustomAttributes.Find("System.Reflection.AssemblyTitleAttribute");
            if (existingTitleAttribute != null)
            {
                module.Assembly.CustomAttributes.Remove(existingTitleAttribute);
            }
            module.Assembly.CustomAttributes.Add(newAttribute);

            module.Write(assemblyPath);
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
                Application.Current.Shutdown();
                return Task.CompletedTask;
            });
        }


        private string FindExe()
        {
            var launcherExe = Process.GetCurrentProcess().MainModule.FileName;
            var currentDir = Path.GetDirectoryName(launcherExe);

            var l = Directory.EnumerateFiles(currentDir, "*.exe").Where(x => x != launcherExe).ToList();
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
    }
}