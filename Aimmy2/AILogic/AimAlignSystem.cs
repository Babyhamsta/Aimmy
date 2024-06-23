using System;
using System.Collections.Generic;
using System.Linq;

namespace AILogic
{
    public class AimAlignmentSystem
    {
        private readonly TargetManager targetManager = new TargetManager();

        public void UpdateDetections(List<Target> detections)
        {
            targetManager.UpdateTargets(detections);
        }

        public void AlignAim()
        {
            var target = targetManager.GetBestTarget();
            if (target != null)
            {
                // Logic to align aim with the target
                WinAPICaller.SetCursorPosition(target.X, target.Y);
            }
        }
    }
}
