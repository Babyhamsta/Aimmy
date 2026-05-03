using System.Globalization;
using System.Resources;
using System.Reflection;
using Other;

namespace Aimmy2.Resources
{
    public static class LocalizationManager
    {
        private static readonly ResourceManager _resourceManager;

        public static string CurrentLanguage { get; private set; } = "en-US";

        static LocalizationManager()
        {
            _resourceManager = new ResourceManager(
                "Aimmy2.Resources.Strings",
                Assembly.GetExecutingAssembly());
        }

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                var value = _resourceManager.GetString(key);
                if (value != null)
                    return value;
            }
            catch (MissingManifestResourceException)
            {
            }

            LogManager.Log(LogManager.LogLevel.Warning,
                $"[Localization] Missing translation key: '{key}'");

            return key;
        }

        public static string GetString(string key, params object[] args)
        {
            var format = GetString(key);
            try
            {
                return string.Format(format, args);
            }
            catch
            {
                return format;
            }
        }

        public static void SetLanguage(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
            {
                cultureName = "en-US";
            }

            var supported = new[] { "en-US", "zh-CN" };
            if (!supported.Contains(cultureName))
            {
                cultureName = "en-US";
            }

            try
            {
                var culture = new CultureInfo(cultureName);
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;

                CurrentLanguage = cultureName;
            }
            catch (CultureNotFoundException)
            {
                SetLanguage("en-US");
            }
        }

        public static bool IsChinese => CurrentLanguage == "zh-CN";
    }
}
