using System.ComponentModel;
using Aimmy2.AILogic;
using Aimmy2.Types;

namespace Aimmy2.Models;

public class RelativeRectModel : INotifyPropertyChanged
{
    private float _widthPercentage;
    private float _heightPercentage;
    private float _leftMarginPercentage;
    private float _topMarginPercentage;

    public RelativeRectModel(RelativeRect rect): this(rect.WidthPercentage, rect.HeightPercentage, rect.LeftMarginPercentage, rect.TopMarginPercentage)
    {}

    public RelativeRectModel(float widthPercentage, float heightPercentage, float leftMarginPercentage, float topMarginPercentage)
    {
        WidthPercentage = widthPercentage;
        HeightPercentage = heightPercentage;
        LeftMarginPercentage = leftMarginPercentage;
        TopMarginPercentage = topMarginPercentage;
    }

    public float WidthPercentage
    {
        get => _widthPercentage;
        set
        {
            _widthPercentage = value;
            OnPropertyChanged(nameof(WidthPercentage));
        }
    }

    public float HeightPercentage
    {
        get => _heightPercentage;
        set
        {
            _heightPercentage = value;
            OnPropertyChanged(nameof(HeightPercentage));
        }
    }

    public float LeftMarginPercentage
    {
        get => _leftMarginPercentage;
        set
        {
            _leftMarginPercentage = value;
            OnPropertyChanged(nameof(LeftMarginPercentage));
        }
    }

    public float TopMarginPercentage
    {
        get => _topMarginPercentage;
        set
        {
            _topMarginPercentage = value;
            OnPropertyChanged(nameof(TopMarginPercentage));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    internal RelativeRect ToRelativeRect()
    {
        return new RelativeRect(WidthPercentage, HeightPercentage, LeftMarginPercentage, TopMarginPercentage);
    }

    public override string ToString()
    {
        return ToRelativeRect().ToString();
    }

    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}