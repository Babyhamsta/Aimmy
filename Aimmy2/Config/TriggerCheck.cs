using System.ComponentModel;

namespace Aimmy2.Config;

public enum TriggerCheck
{
    [Description("None")]
    None,
    [Description("Intersecting Center")]
    IntersectingCenter,
    [Description("Head Intersecting Center")]
    HeadIntersectingCenter
}