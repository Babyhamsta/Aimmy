using System.Drawing;
using System.Windows;
using Aimmy2;
using Aimmy2.AILogic;
using Aimmy2.AILogic.Actions;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.Models;
using Class;
using Nextended.Core.Extensions;
using Visuality;

public class AIManager : IDisposable
{
    private readonly ICapture _screenCapture;
    private readonly IPredictionLogic _predictionLogic;
    private readonly IList<IAction> _actions;
    private bool _isAiLoopRunning;
    private Thread _aiLoopThread;

    public bool IsModelLoaded { get; private set; }

    //public AIManager(string modelPath) : this(new ScreenCapture(), new PredictionLogic(modelPath), BaseAction.AllActions())
    //{ }

    public AIManager(string modelPath) : 
        this(CreateScreenCapture(RecordTarget.Process(new ProcessModel { Title = AppConfig.Current.DropdownState.GamepadProcess })), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }


    public AIManager(string modelPath, RecordTarget target) : this(CreateScreenCapture(target), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }

    private static ICapture CreateScreenCapture(RecordTarget target)
    {
        return target.TargetType switch
        {
            RecordTargetType.Screen => target.ProcessOrScreenId.HasValue ? new ScreenCapture(target.ProcessOrScreenId.Value) : new ScreenCapture(),
            RecordTargetType.Process => new ProcessCapture(target.ProcessOrScreenId.Value),
            _ => throw new ArgumentException("Unsupported RecordTargetType"),
        };
    }

    public AIManager(ICapture screenCapture, IPredictionLogic predictionLogic, IList<IAction> actions)
    {
        _screenCapture = screenCapture;
        _predictionLogic = predictionLogic;
        _actions = actions.Apply(a =>
        {
            a.PredictionLogic = predictionLogic;
            a.ImageCapture = screenCapture;
        }).ToList();

        NotifyLoaded();

        _isAiLoopRunning = true;
        _aiLoopThread = new Thread(AiLoop);
        _aiLoopThread.Start();
    }

    private void NotifyLoaded()
    {
        IsModelLoaded = true;
        var w = Application.Current.MainWindow as MainWindow;
        w.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AppConfig.Current.ToggleState.GlobalActive)
                w.SetActive(true);
            w.CallPropertyChanged(nameof(w.IsModelLoaded));
        }));
    }

    private async void AiLoop()
    {
        while (_isAiLoopRunning)
        {
            if (AppConfig.Current.ToggleState.GlobalActive)
            {
                var area = _screenCapture.GetCaptureArea();
              
                var cursorPosition = WinAPICaller.GetCursorPosition();
                
                var targetX = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.X - area.Left : area.Width / 2;
                var targetY = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.Y - area.Top : area.Height / 2;

                Rectangle detectionBox = new(targetX - PredictionLogic.IMAGE_SIZE / 2, targetY - PredictionLogic.IMAGE_SIZE / 2, PredictionLogic.IMAGE_SIZE, PredictionLogic.IMAGE_SIZE);
                var frame = _screenCapture.Capture(detectionBox);

                var predictions = (await _predictionLogic.Predict(frame, detectionBox)).ToArray();
                await Task.WhenAll(_actions.Select(a => a.Execute(predictions)));
            }
            await Task.Delay(1);
        }
    }


    public void Dispose()
    {
        _isAiLoopRunning = false;
        if (_aiLoopThread is { IsAlive: true })
        {
            if (!_aiLoopThread.Join(TimeSpan.FromSeconds(1)))
            {
                _aiLoopThread.Interrupt();
            }
        }
    }
}