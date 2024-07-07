using System;
using System.Collections.Generic;
using System.Linq;

namespace AILogic
{
    public class TargetManager
    {
        private readonly Dictionary<int, Target> targets = new Dictionary<int, Target>();

        public void UpdateTargets(List<Target> newTargets)
        {
            foreach (var target in newTargets)
            {
                if (targets.ContainsKey(target.Id))
                {
                    targets[target.Id].UpdatePosition(target.X, target.Y);
                }
                else
                {
                    targets[target.Id] = target;
                }
            }

            // Remove targets that are no longer detected
            var targetIdsToRemove = targets.Keys.Except(newTargets.Select(t => t.Id)).ToList();
            foreach (var targetId in targetIdsToRemove)
            {
                targets.Remove(targetId);
            }
        }

        public List<Target> GetAllTargets()
        {
            return targets.Values.ToList();
        }

        public Target GetBestTarget()
        {
            return targets.Values.OrderBy(t => GetDistanceFromCursor(t)).FirstOrDefault();
        }

        private double GetDistanceFromCursor(Target target)
        {
            var cursorPos = WinAPICaller.GetCursorPosition();
            return Math.Sqrt(Math.Pow(cursorPos.X - target.X, 2) + Math.Pow(cursorPos.Y - target.Y, 2));
        }
    }

    public class Target
    {
        public int Id { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public DateTime LastUpdated { get; private set; }

        public Target(int id, int x, int y)
        {
            Id = id;
            X = x;
            Y = y;
            LastUpdated = DateTime.UtcNow;
        }

        public void UpdatePosition(int x, int y)
        {
            X = x;
            Y = y;
            LastUpdated = DateTime.UtcNow;
        }
    }
}
