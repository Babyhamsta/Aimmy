using System.Drawing;
using System.Windows;
using Aimmy2;
using Aimmy2.AILogic;
using Aimmy2.AILogic.Actions;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Config;
using Aimmy2.InputLogic;
using Aimmy2.Models;
using Class;
using Nextended.Core.Extensions;
using Visuality;


public class AIManager : IDisposable
{
    public static AIManager Instance { get; private set; }
    private readonly IList<IAction> _actions;
    private bool _isAiLoopRunning;
    private Thread _aiLoopThread;
    private bool _pausedNotified = false;
    public bool IsRunning => _isAiLoopRunning;
    public bool IsModelLoaded { get; private set; }

    public IPredictionLogic PredictionLogic { get; private set; }
    public ICapture ImageCapture { get; private set; }

    public AIManager(string modelPath) : this(new ScreenCapture(), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }
    
    public AIManager(string modelPath, CaptureSource target) : this(CreateScreenCapture(target), new PredictionLogic(modelPath), BaseAction.AllActions())
    { }

    private static ICapture CreateScreenCapture(CaptureSource target)
    {
        try
        {
            return target.TargetType switch
            {
                CaptureTargetType.Screen => target.ProcessOrScreenId.HasValue ? new ScreenCapture(target.ProcessOrScreenId.Value) : new ScreenCapture(),
                CaptureTargetType.Process => new ProcessCapture(ProcessModel.FindProcessById(target.ProcessOrScreenId ?? 0) ?? ProcessModel.FindProcessByTitle(target.Title)),
                _ => throw new ArgumentException("Unsupported RecordTargetType"),
            };
        }
        catch (Exception e)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => new NoticeBar($"Error: {e.Message}", 5000).Show()));
            throw;
        }
    }

    public AIManager(ICapture screenCapture, IPredictionLogic predictionLogic, IList<IAction> actions)
    {
        Instance = this;
        ImageCapture = screenCapture;
        PredictionLogic = predictionLogic;
        _actions = actions.Apply(a =>
        {
            a.AIManager = this;
        }).ToList();

        NotifyLoaded(true);

        _isAiLoopRunning = true;
        _= SetActionsState(false);
        _aiLoopThread = new Thread(AiLoop);
        _aiLoopThread.Start();
    }

    private void NotifyLoaded(bool loaded)
    {
        IsModelLoaded = loaded;
        var w = Application.Current.MainWindow as MainWindow;
        w.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (AppConfig.Current.ToggleState.GlobalActive)
                w.SetActive(loaded);
            w.CallPropertyChanged(nameof(w.IsModelLoaded));
        }));
    }

    private async void AiLoop()
    {
        while (_isAiLoopRunning)
        {
            if (AppConfig.Current.ToggleState.GlobalActive)
            {
                if(_pausedNotified)
                {
                    _pausedNotified = false;
                    await SetActionsState(false);
                }
                var area = ImageCapture.GetCaptureArea();

                var cursorPosition = WinAPICaller.GetCursorPosition();

                var targetX = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.X - area.Left : area.Width / 2;
                var targetY = AppConfig.Current.DropdownState.DetectionAreaType == DetectionAreaType.ClosestToMouse ? cursorPosition.Y - area.Top : area.Height / 2;

                Rectangle detectionBox = new(targetX - Aimmy2.AILogic.PredictionLogic.IMAGE_SIZE / 2, targetY - Aimmy2.AILogic.PredictionLogic.IMAGE_SIZE / 2, Aimmy2.AILogic.PredictionLogic.IMAGE_SIZE, Aimmy2.AILogic.PredictionLogic.IMAGE_SIZE);
                var frame = ImageCapture.Capture(detectionBox);

                var predictions = (await PredictionLogic.Predict(frame, detectionBox)).ToArray();
                await Task.WhenAll(_actions.Select(a => a.Execute(predictions)));
            }
            else if (!_pausedNotified)
            {
                _pausedNotified = true;
                await SetActionsState(true);

            }
            await Task.Delay(1);
        }
    }

    private async Task SetActionsState(bool paused)
    {
        await Task.WhenAll(_actions.Select(a => paused ? a.OnPause() : a.OnResume()));
    }


    public void Dispose()
    {
        IsModelLoaded = false;
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