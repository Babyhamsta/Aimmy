using System.Windows;
using System.Windows.Media.Animation;

namespace AimmyWPF.Class
{
    public static class Animator
    {
        public static Storyboard StoryBoard = new();
        private static readonly IEasingFunction Smooth = new QuarticEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        public static void ObjectShift(Duration speed, DependencyObject Object, Thickness Get, Thickness Set)
        {
            ThicknessAnimation Animation = new()
            {
                From = Get,
                To = Set,
                Duration = speed,
                EasingFunction = Smooth,
            };
            Storyboard.SetTarget(Animation, Object);
            Storyboard.SetTargetProperty(Animation, new PropertyPath("(Panel.Margin)"));
            StoryBoard.Children.Add(Animation);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(Animation);
        }

        public static void WidthShift(Duration speed, FrameworkElement element, double originalSize, double newSize)
        {
            var animation = new DoubleAnimation
            {
                From = originalSize,
                To = newSize,
                Duration = speed,
                EasingFunction = new QuarticEase()
            };

            element.BeginAnimation(FrameworkElement.WidthProperty, animation);
        }

        public static void HeightShift(Duration speed, FrameworkElement element, double originalSize, double newSize)
        {
            var animation = new DoubleAnimation
            {
                From = originalSize,
                To = newSize,
                Duration = speed,
                EasingFunction = new QuarticEase()
            };

            element.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
    }
}