using Aimmy2.Theme;
using Class;
using System.Windows;
using Aimmy2.Other;
using Aimmy2.Resources;

namespace Aimmy2
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            InitializeLanguage();
            InitializeTheme();

            // Set shutdown mode to prevent app from closing when startup window closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;



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
                MessageBox.Show(LocalizationManager.GetString("Msg_StartupFailed", ex.Message));

                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();

                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
#endif
        }

        private void InitializeLanguage()
        {
            try
            {
                SaveDictionary.LoadJSON(Class.Dictionary.languageState, "bin\\language.cfg");
                var lang = Class.Dictionary.languageState.GetValueOrDefault("Language", "en-US")?.ToString() ?? "en-US";
                LocalizationManager.SetLanguage(lang);
            }
            catch
            {
                LocalizationManager.SetLanguage("en-US");
            }
        }

        private void InitializeTheme()
        {
            try
            {
                var colorState = new Dictionary<string, dynamic>
                {
                    { "Theme Color", "#FF722ED1" }
                };

                SaveDictionary.LoadJSON(colorState, "bin\\colors.cfg");

                if (colorState.TryGetValue("Theme Color", out var themeColor) && themeColor is string colorString)
                {
                    ThemeManager.SetThemeColor(colorString);
                }
                else
                {
                    ThemeManager.SetThemeColor("#FF722ED1");
                }
            }
            catch
            {
                ThemeManager.SetThemeColor("#FF722ED1");
            }
        }
    }
}