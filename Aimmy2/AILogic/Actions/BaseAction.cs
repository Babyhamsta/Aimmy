using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Nextended.Core.Helper;

namespace Aimmy2.AILogic.Actions;

public abstract class BaseAction: IAction
{
    public AIManager AIManager { get; set; }
    public Task Execute(IEnumerable<Prediction> predictions) => Task.Run(() => ExecuteAsync(predictions.ToArray()));
    public virtual Task OnPause() => Task.CompletedTask;

    public virtual Task OnResume() => Task.CompletedTask;

    public abstract Task ExecuteAsync(Prediction[] predictions);

    protected virtual bool Active => AppConfig.Current.ToggleState.GlobalActive;
    public IPredictionLogic PredictionLogic => AIManager.PredictionLogic;
    public ICapture ImageCapture => AIManager.ImageCapture;

    public static IList<IAction> AllActions()
    {
        return typeof(BaseAction).Assembly.GetTypes()
            .Where(t => t.ImplementsInterface(typeof(IAction)) && !t.IsAbstract)
            .Select(t => (IAction)Activator.CreateInstance(t)).ToList();
    }
}