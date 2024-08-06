using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Nextended.Core.Helper;

namespace Aimmy2.AILogic.Actions;

public abstract class BaseAction: IAction
{
    public Task Execute(IEnumerable<Prediction> predictions) => Task.Run(() => ExecuteAsync(predictions.ToArray()));

    public abstract Task ExecuteAsync(Prediction[] predictions);

    protected virtual bool Active => AppConfig.Current.ToggleState.GlobalActive;
    public IPredictionLogic PredictionLogic { get; set; }
    public ICapture ImageCapture { get; set; }

    public static IList<IAction> AllActions()
    {
        return typeof(BaseAction).Assembly.GetTypes()
            .Where(t => t.ImplementsInterface(typeof(IAction)) && !t.IsAbstract)
            .Select(t => (IAction)Activator.CreateInstance(t)).ToList();
    }
}