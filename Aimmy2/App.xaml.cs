using Aimmy2.Theme;
using Class;
using System.Windows;
using Aimmy2.Other;

namespace Aimmy2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Initialize the application theme from saved settings
            InitializeTheme();

            // Set shutdown mode to prevent app from closing when startup window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize anti-detection system BEFORE creating any windows
            InitializeAntiDetection();
            InitializeIdentityRandomizer();

#if DEBUG
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
#else
            // code IS reachable, only in release though
            try
            {
                // Create and show startup window
                var startupWindow = new StartupWindow();
                startupWindow.Show();

                // Reset shutdown mode after startup window is shown
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                // If startup window fails, launch main window directly
                MessageBox.Show($"Startup animation failed: {ex.Message}\nLaunching main application...",
                              "Aimmy AI", MessageBoxButton.OK, MessageBoxImage.Information);

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
#endif
        }

        /// <summary>
        /// 初始化反检测系统 - 在程序启动时自动执行特征随机化
        /// </summary>
        private void InitializeAntiDetection()
        {
            try
            {
                // 加载配置以检查反检测是否启用
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FF722ED1" }
                };
                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                // 检查反检测功能是否启用
                bool antiDetectionEnabled = false;
                try
                {
                    var toggleStatePath = "bin\\configs\\Default.cfg";
                    if (System.IO.File.Exists(toggleStatePath))
                    {
                        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(
                            System.IO.File.ReadAllText(toggleStatePath));
                        if (config != null && config.ContainsKey("Anti-Detection"))
                        {
                            antiDetectionEnabled = config["Anti-Detection"];
                        }
                    }
                }
                catch
                {
                    // 如果读取配置失败，使用默认值
                }

                if (antiDetectionEnabled)
                {
                    // 初始化反检测管理器
                    var antiDetectionManager = AntiDetectionManager.Instance;
                    antiDetectionManager.Initialize();
                }
            }
            catch (Exception ex)
            {
                // 反检测初始化失败不应阻止程序启动
                System.Diagnostics.Debug.WriteLine($"[AntiDetection] Startup initialization error: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化身份随机化系统 - 在程序启动时自动执行
        /// </summary>
        private void InitializeIdentityRandomizer()
        {
            try
            {
                // 检查身份随机化功能是否启用
                bool identityRandomizerEnabled = false;
                try
                {
                    var toggleStatePath = "bin\\configs\\Default.cfg";
                    if (System.IO.File.Exists(toggleStatePath))
                    {
                        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(
                            System.IO.File.ReadAllText(toggleStatePath));
                        if (config != null && config.ContainsKey("Randomize Identity"))
                        {
                            identityRandomizerEnabled = config["Randomize Identity"];
                        }
                    }
                }
                catch
                {
                    // 如果读取配置失败，使用默认值
                }

                if (identityRandomizerEnabled)
                {
                    // 初始化身份随机化管理器
                    var identityRandomizer = IdentityRandomizer.Instance;
                    identityRandomizer.Initialize();
                }
            }
            catch (Exception ex)
            {
                // 身份随机化初始化失败不应阻止程序启动
                System.Diagnostics.Debug.WriteLine($"[IdentityRandomizer] Startup initialization error: {ex.Message}");
            }
        }

        private void InitializeTheme()
        {
            try
            {
                // Load the color state configuration
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FF722ED1" }
                };

                // Load saved colors
                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                // Apply theme color if found
                if (colorState.TryGetValue("Theme Color", out var themeColor) && themeColor is string colorString)
                {
                    ThemeManager.SetThemeColor(colorString);
                }
                else
                {
                    // Use default purple if no saved color
                    ThemeManager.SetThemeColor("#FF722ED1");
                }
            }
            catch
            {
                // Log error and use default color
                ThemeManager.SetThemeColor("#FF722ED1");
            }
        }
    }
}