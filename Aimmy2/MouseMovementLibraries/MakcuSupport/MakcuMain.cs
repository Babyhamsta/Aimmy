using System;
using System.Threading.Tasks;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.MakcuSupport
{
    internal class MakcuMain
    {
        public static MakcuMouse MakcuInstance { get; private set; }

        private static bool _isMakcuLoaded = false;

        public static void ConfigureMakcuInstance(bool debugEnabled, bool sendInitCmds)
        {
            MakcuInstance?.Dispose();

            MakcuInstance = new MakcuMouse(debugEnabled, sendInitCmds);
            _isMakcuLoaded = false;
        }

        private static async Task<bool> InitializeMakcuDevice()
        {
            if (_isMakcuLoaded && MakcuInstance != null && MakcuInstance.IsInitializedAndConnected)
            {
                Console.WriteLine("MakcuMain: InitializeMakcuDevice called, but Makcu is already loaded and connected.");
                new NoticeBar($"MAKCU instance initialized for Port={MakcuInstance.PortName}", 5000).Show();
                return true;
            }

            if (MakcuInstance == null)
            {
                ConfigureMakcuInstance(false, false);
            }

            try
            {
                if (MakcuInstance == null || !MakcuInstance.Init())
                {
                    MessageBox.Show($"MAKCU initialization failed\n" +
                                    "Verify that the device is connected and not in use",
                                    "Makcu Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isMakcuLoaded = false;
                    return false;
                }

                string version = MakcuInstance.GetKmVersion();

                if (string.IsNullOrWhiteSpace(version))
                {
                    MessageBox.Show($"No version response received from the Makcu device on {MakcuInstance.PortName}. " +
                                      "Ensure the firmware is compatible and responds to the 'km.version()' command.\n" +
                                      "The connection might be unstable or the device is not responding as expected.",
                                      "Makcu Version Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _isMakcuLoaded = true;
                new NoticeBar($"MAKCU instance initialized for Port={MakcuInstance.PortName}", 5000).Show();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Catastrophic exception during Makcu initialization. Error: {ex.Message}\nStack Trace: {ex.StackTrace}",
                                "Makcu Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isMakcuLoaded = false;
                if (MakcuInstance != null && MakcuInstance.IsInitializedAndConnected)
                {
                    MakcuInstance.Close();
                }
                return false;
            }
        }

        public static async Task<bool> Load() => await InitializeMakcuDevice();

        public static void Unload()
        {
            if (MakcuInstance != null)
            {
                MakcuInstance.Close();
            }
            _isMakcuLoaded = false;
            Console.WriteLine("MakcuMain: Makcu device unloaded/closed.");
        }

        public static void DisposeInstance()
        {
            Unload();
            MakcuInstance?.Dispose();
            MakcuInstance = null;
            _isMakcuLoaded = false;
            Console.WriteLine("MakcuMain: MakcuMouse instance disposed (null).");
        }
    }
}