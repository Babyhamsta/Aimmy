namespace Aimmy2.Config;

public class AntiRecoilSettings : BaseSettings
{
    private int _holdTime = 10;
    private int _fireRate = 200;
    private int _yRecoil = 10;
    private int _xRecoil = 0;

    public int HoldTime
    {
        get => _holdTime;
        set => SetField(ref _holdTime, value);
    }

    public int FireRate
    {
        get => _fireRate;
        set => SetField(ref _fireRate, value);
    }

    public int YRecoil
    {
        get => _yRecoil;
        set => SetField(ref _yRecoil, value);
    }

    public int XRecoil
    {
        get => _xRecoil;
        set => SetField(ref _xRecoil, value);
    }
}