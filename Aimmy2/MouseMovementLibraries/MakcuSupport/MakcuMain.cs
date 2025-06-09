using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using Visuality;


namespace MouseMovementLibraries.MakcuSupport
{
    internal class MakcuMain
    {
        public static MakcuMouse MakcuInstance { get; private set; }

        private static bool _isMakcuLoaded = false;
        private static bool _isSubscribedToButtonEvents = false;


        private const bool DefaultDebugLoggingForInternalCreation = false;
        private const bool DefaultSendInitCommandsForInternalCreation = true;

        public static void ConfigureMakcuInstance(bool debugEnabled, bool sendInitCmds)
        {
            Console.WriteLine($"MakcuMain: Configuring MakcuInstance. Debug: {debugEnabled}, SendInitCmds: {sendInitCmds}");
            UnsubscribeFromButtonEvents();
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
                ConfigureMakcuInstance(DefaultDebugLoggingForInternalCreation, DefaultSendInitCommandsForInternalCreation);
            }


            try
            {
                if (MakcuInstance == null || !MakcuInstance.Init())
                {
                    MessageBox.Show($"MAKCU initialization failed.\n" +
                                    "Verify that the device is connected and not in use by another application.",
                                    "Makcu Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    _isMakcuLoaded = false;
                    return false;
                }

                string version = MakcuInstance.GetKmVersion();

                if (string.IsNullOrWhiteSpace(version))
                {
                    MessageBox.Show($"No version response received from the Makcu device on {MakcuInstance.PortName}.\n" +
                                      "Ensure the firmware is compatible and responds to the 'km.version()' command.\n" +
                                      "The connection might be unstable or the device is not responding as expected.",
                                      "Makcu Version Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _isMakcuLoaded = true;

                SubscribeToButtonEvents();

                new NoticeBar($"MAKCU instance initialized for Port={MakcuInstance.PortName}", 5000).Show();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Catastrophic exception during Makcu initialization. Error: {ex.Message}\nStack Trace: {ex.StackTrace}",
                                "Makcu Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _isMakcuLoaded = false;
                MakcuInstance?.Close();
                return false;
            }
        }

        public static async Task<bool> Load() => await InitializeMakcuDevice();

        public static void Unload()
        {
            UnsubscribeFromButtonEvents();
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
            Console.WriteLine("MakcuMain: MakcuMouse instance disposed (null).");
        }

        private static void SubscribeToButtonEvents()
        {
            if (MakcuInstance != null && !_isSubscribedToButtonEvents)
            {
                MakcuInstance.ButtonStateChanged += OnMakcuButtonStateChanged;
                _isSubscribedToButtonEvents = true;
                Debug.WriteLine("MakcuMain: Subscribed to MakcuInstance.ButtonStateChanged events.");
            }
        }

        private static void UnsubscribeFromButtonEvents()
        {
            if (MakcuInstance != null && _isSubscribedToButtonEvents)
            {
                MakcuInstance.ButtonStateChanged -= OnMakcuButtonStateChanged;
                _isSubscribedToButtonEvents = false;
                Debug.WriteLine("MakcuMain: Unsubscribed from MakcuInstance.ButtonStateChanged events.");
            }
        }

        private static void OnMakcuButtonStateChanged(MakcuMouseButton button, bool isPressed)
        {

            string state = isPressed ? "Presionado" : "Liberado";
            Debug.WriteLine($"{button} físico {state}!");


        }


    }
}