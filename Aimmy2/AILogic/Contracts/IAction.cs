
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aimmy2.AILogic.Contracts;

public interface IAction
{
    Task Execute(IEnumerable<Prediction> predictions);
}
