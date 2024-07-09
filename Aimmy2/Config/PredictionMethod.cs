using System.ComponentModel;

namespace Aimmy2.Config;

public enum PredictionMethod
{
    [Description("Kalman Filter")]
    KalmanFilter,

    [Description("Shall0e's Prediction")]
    Shall0,

    [Description("wisethef0x's EMA Prediction")]
    WiseThef0x,
}