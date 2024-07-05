using Class;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Class;

namespace Visuality
{
    public partial class EditHeadArea : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static double ContainerWidth = 300;
        public static double ContainerHeight = 400;

        private bool isDragging;
        private bool isResizing;
        private Point clickPosition;

        private RelativeRectModel _relativeRect = new RelativeRectModel(0.5f, 0.33f, 0.25f, 0.25f);

        public RelativeRectModel RelativeRect
        {
            get { return _relativeRect; }
            set
            {
                _relativeRect = value;
                OnPropertyChanged(nameof(RelativeRect));
                UpdateGreenRectangle();
            }
        }

        private double _rectWidth;
        public double RectWidth
        {
            get { return _rectWidth; }
            set
            {
                _rectWidth = value;
                OnPropertyChanged(nameof(RectWidth));
            }
        }

        private double _rectHeight;
        public double RectHeight
        {
            get { return _rectHeight; }
            set
            {
                _rectHeight = value;
                OnPropertyChanged(nameof(RectHeight));
            }
        }

        private double _rectLeft;
        public double RectLeft
        {
            get { return _rectLeft; }
            set
            {
                _rectLeft = value;
                OnPropertyChanged(nameof(RectLeft));
            }
        }

        private double _rectTop;
        public double RectTop
        {
            get { return _rectTop; }
            set
            {
                _rectTop = value;
                OnPropertyChanged(nameof(RectTop));
            }
        }

        public EditHeadArea()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void UpdateGreenRectangle()
        {
            RectWidth = ContainerWidth * RelativeRect.WidthPercentage;
            RectHeight = ContainerHeight * RelativeRect.HeightPercentage;
            RectLeft = ContainerWidth * RelativeRect.LeftMarginPercentage;
            RectTop = ContainerHeight * RelativeRect.TopMarginPercentage;
        }

        private void EditHeadArea_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue as bool? == true)
            {
                Task.Delay(1000).ContinueWith(task => Dispatcher.BeginInvoke(() => { UpdateGreenRectangle(); }));
            }
        }

        private void GreenRectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            isResizing = false;
            clickPosition = e.GetPosition(MainArea);
            Mouse.Capture(MainArea);
        }

        private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isResizing = true;
            isDragging = false;
            clickPosition = e.GetPosition(MainArea);
            Mouse.Capture(MainArea);
        }

        private void MainArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDragging)
            {
                var mousePos = e.GetPosition(MainArea);
                var offsetX = mousePos.X - clickPosition.X;
                var offsetY = mousePos.Y - clickPosition.Y;

                RectLeft = Math.Max(0, Math.Min(ContainerWidth - RectWidth, RectLeft + offsetX));
                RectTop = Math.Max(0, Math.Min(ContainerHeight - RectHeight, RectTop + offsetY));

                clickPosition = mousePos;

                // Update the relative model
                RelativeRect.LeftMarginPercentage = (float)(RectLeft / ContainerWidth);
                RelativeRect.TopMarginPercentage = (float)(RectTop / ContainerHeight);
            }
            else if (isResizing)
            {
                var mousePos = e.GetPosition(MainArea);
                var offsetX = mousePos.X - clickPosition.X;
                var offsetY = mousePos.Y - clickPosition.Y;

                RectWidth = Math.Max(0, Math.Min(ContainerWidth - RectLeft, RectWidth + offsetX));
                RectHeight = Math.Max(0, Math.Min(ContainerHeight - RectTop, RectHeight + offsetY));

                clickPosition = mousePos;

                // Update the relative model
                RelativeRect.WidthPercentage = (float)(RectWidth / ContainerWidth);
                RelativeRect.HeightPercentage = (float)(RectHeight / ContainerHeight);
            }
        }

        private void MainArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            isResizing = false;
            Mouse.Capture(null);
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isDragging || isResizing) return;
            DragMove();
        }

        private double currentGradientAngle = 0;

        private void Main_Background_Gradient(object sender, MouseEventArgs e)
        {
            if (Dictionary.toggleState["Mouse Background Effect"])
            {
                var CurrentMousePos = WinAPICaller.GetCursorPosition();
                var translatedMousePos = PointFromScreen(new Point(CurrentMousePos.X, CurrentMousePos.Y));
                double targetAngle = Math.Atan2(translatedMousePos.Y - (MainBorder.ActualHeight * 0.5), translatedMousePos.X - (MainBorder.ActualWidth * 0.5)) * (180 / Math.PI);

                double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
                if (angleDifference > 180)
                {
                    angleDifference -= 360;
                }

                angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
                currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
                RotaryGradient.Angle = currentGradientAngle;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save logic here
            Console.WriteLine("Saved");
        }

        private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging || isResizing) return;
            UpdateGreenRectangle();
        }
    }

    public class RelativeRectModel : INotifyPropertyChanged
    {
        private float _widthPercentage;
        private float _heightPercentage;
        private float _leftMarginPercentage;
        private float _topMarginPercentage;

        public RelativeRectModel(float widthPercentage, float heightPercentage, float leftMarginPercentage, float topMarginPercentage)
        {
            WidthPercentage = widthPercentage;
            HeightPercentage = heightPercentage;
            LeftMarginPercentage = leftMarginPercentage;
            TopMarginPercentage = topMarginPercentage;
        }

        public float WidthPercentage
        {
            get { return _widthPercentage; }
            set
            {
                _widthPercentage = value;
                OnPropertyChanged(nameof(WidthPercentage));
            }
        }

        public float HeightPercentage
        {
            get { return _heightPercentage; }
            set
            {
                _heightPercentage = value;
                OnPropertyChanged(nameof(HeightPercentage));
            }
        }

        public float LeftMarginPercentage
        {
            get { return _leftMarginPercentage; }
            set
            {
                _leftMarginPercentage = value;
                OnPropertyChanged(nameof(LeftMarginPercentage));
            }
        }

        public float TopMarginPercentage
        {
            get { return _topMarginPercentage; }
            set
            {
                _topMarginPercentage = value;
                OnPropertyChanged(nameof(TopMarginPercentage));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
