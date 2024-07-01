namespace InputLogic
{
    internal class Program
    {
        static void Main(string[] args)
        {
            InputBindingManager inputBindingManager = new();
            inputBindingManager.OnToggleStateChanged += (bindingId, state) =>
            {
                if (bindingId == "Auto Trigger")
                {
                    MouseManager.AutoTriggerEnabled = state;
                }
            };

            // Setup default keybindings and actions
            inputBindingManager.SetupDefault("AutoTriggerToggle", "F1"); // Key to toggle auto trigger
            inputBindingManager.SetupDefaultAction("TargetDetectedAction", "LButton", "SinglePress"); // Default action for target detected

            // Simulating user enabling the auto trigger
            inputBindingManager.ToggleAutoTrigger(true);

            // Start the application logic that detects targets and moves the crosshair
            while (true)
            {
                // Replace with your target detection logic
                bool targetDetected = DetectTarget(out int targetX, out int targetY);
                if (targetDetected)
                {
                    MouseManager.MoveCrosshair(targetX, targetY);
                }

                Thread.Sleep(100); // Adjust sleep time as necessary
            }
        }

        private static bool DetectTarget(out int targetX, out int targetY)
        {
            // Replace with actual detection logic
            targetX = 0;
            targetY = 0;
            return false;
        }
    }
}
