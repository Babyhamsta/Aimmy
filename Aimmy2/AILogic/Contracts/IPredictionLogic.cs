using System.Collections.Generic;
using System.Drawing;

namespace Aimmy2.AILogic.Contracts;

public interface IPredictionLogic
{
    Task<IEnumerable<Prediction>> Predict(Bitmap frame, Rectangle detectionBox);
}