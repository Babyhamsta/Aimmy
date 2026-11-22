namespace Aimmy2.AILogic
{
    internal static class AimProcessingDecisions
    {
        internal static bool ShouldRunPrediction(
            bool showDetectedPlayer,
            bool constantAiTracking,
            bool aimKeyHeld,
            bool secondAimKeyHeld) =>
            showDetectedPlayer ||
            constantAiTracking ||
            aimKeyHeld ||
            secondAimKeyHeld;

        internal static bool ShouldProcessFrame(
            bool aimAssist,
            bool showDetectedPlayer,
            bool autoTrigger) =>
            aimAssist ||
            showDetectedPlayer ||
            autoTrigger;

        internal static bool ShouldAttemptAutoTrigger(
            bool autoTrigger,
            bool aimKeyHeld,
            bool secondAimKeyHeld,
            bool constantAiTracking) =>
            autoTrigger &&
            aimKeyHeld &&
            !secondAimKeyHeld &&
            !constantAiTracking;
    }
}