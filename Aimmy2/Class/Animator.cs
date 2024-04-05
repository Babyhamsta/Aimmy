using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace AimmyWPF.Class
{
    public static class Animator
    {
        public static Storyboard StoryBoard = new();
        private static TimeSpan duration = TimeSpan.FromMilliseconds(500);
        //private static TimeSpan duration2 = TimeSpan.FromMilliseconds(1000);

        private static readonly IEasingFunction Smooth = new QuarticEase
        {
            EasingMode = EasingMode.EaseInOut
        };

        public static void Fade(DependencyObject Object)
        {
            DoubleAnimation FadeIn = new()
            {
                From = 0.0,
                To = 1.0,
                Duration = new Duration(duration),
            };
            Storyboard.SetTarget(FadeIn, Object);
            Storyboard.SetTargetProperty(FadeIn, new PropertyPath("Opacity", 1));
            StoryBoard.Children.Add(FadeIn);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(FadeIn);
        }

        public static void FadeOut(DependencyObject Object)
        {
            DoubleAnimation Fade = new()
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(duration),
            };
            Storyboard.SetTarget(Fade, Object);
            Storyboard.SetTargetProperty(Fade, new PropertyPath("Opacity", 1));
            StoryBoard.Children.Add(Fade);
            StoryBoard.Begin();
            StoryBoard.Children.Remove(Fade);
        }

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

        public static void WidthShift(Duration speed, Ellipse Circle, double OriginalSize, double NewSize)
        {
            DoubleAnimation doubleanimation = new DoubleAnimation();
            doubleanimation.From = new double?(OriginalSize);
            doubleanimation.To = new double?(NewSize);
            doubleanimation.Duration = speed;
            doubleanimation.EasingFunction = new QuarticEase();
            Circle.BeginAnimation(FrameworkElement.WidthProperty, doubleanimation); ;
        }

        public static void HeightShift(Duration speed, Ellipse Circle, double OriginalSize, double NewSize)
        {
            DoubleAnimation doubleanimation = new DoubleAnimation();
            doubleanimation.From = new double?(OriginalSize);
            doubleanimation.To = new double?(NewSize);
            doubleanimation.Duration = speed;
            doubleanimation.EasingFunction = new QuarticEase();
            Circle.BeginAnimation(FrameworkElement.HeightProperty, doubleanimation); ;
        }
    }
}