using Aimmy2.Class;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Visuality;

namespace Other
{
    internal class LogManager
    {
        private static readonly object FileLock = new();

        public enum LogLevel
        {
            Info,
            Warning,
            Error
        }

        public static void Log(LogLevel lvl, string message, bool notifyUser = false, int waitingTime = 4000)
        {
            if (notifyUser)
            {
                var dispatcher = Application.Current.Dispatcher;
                if (dispatcher.CheckAccess()) NoticeBar.Show(message, waitingTime);
                else dispatcher.BeginInvoke(new Action(() => NoticeBar.Show(message, waitingTime)));
            }
#if DEBUG
            Debug.WriteLine(message);
#endif
            if (Dictionary.toggleState["Debug Mode"])
            {
                lock (FileLock)
                {
                    using StreamWriter writer = new("debug.txt", true);
                    writer.WriteLine($"[{DateTime.Now}] [{lvl.ToString().ToUpperInvariant()}]: {message}");
                }
            }
        }
    }
}
