
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aimmy2.AILogic.Contracts;

public interface IAction
{
    public IPredictionLogic PredictionLogic { get; set; }   
    public ICapture ImageCapture { get; set; }   

    Task Execute(IEnumerable<Prediction> predictions);
}
