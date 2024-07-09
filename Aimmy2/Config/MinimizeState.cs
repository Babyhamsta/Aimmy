namespace Aimmy2.Config;

// TODO: Remove and just store hashed values for minimized boxes
public class MinimizeState: BaseSettings
{
    private List<string> _minimizedBoxes = new();
    public List<string> Minimized
    {
        get => _minimizedBoxes;
        set => SetField(ref _minimizedBoxes, value);
    }

    public bool IsMinimized(string boxName) => _minimizedBoxes.Contains(PrepareName(boxName));
    public void SetMinimized(string boxName, bool minimized)
    {
        boxName = PrepareName(boxName);
        switch (minimized)
        {
            case true when !_minimizedBoxes.Contains(boxName):
                _minimizedBoxes.Add(boxName);
                break;
            case false when _minimizedBoxes.Contains(boxName):
                _minimizedBoxes.Remove(boxName);
                break;
        }
    }
}