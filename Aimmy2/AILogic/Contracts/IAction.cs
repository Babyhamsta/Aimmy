
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aimmy2.AILogic.Contracts;

public interface IAction
{
    public AIManager AIManager { get; set; }
    Task Execute(IEnumerable<Prediction> predictions);
    Task OnPause();
    Task OnResume();
}
