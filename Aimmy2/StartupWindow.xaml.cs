using Aimmy2.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Aimmy2
{
    public partial class StartupWindow : Window
    {
        #region Constants
        private const double PARTICLE_MIN_SIZE = 2;
        private const double PARTICLE_MAX_SIZE = 4;
        private const double PARTICLE_MIN_OPACITY = 0.2;
        private const double PARTICLE_MAX_OPACITY = 0.4;
        private const double PARTICLE_ANIMATION_CHANCE = 0.7;
        private const double PARTICLE_FLOAT_RANGE = 15;
        private const int PARTICLE_MIN_COUNT = 12;
        private const int PARTICLE_MAX_COUNT = 20;

        private const double VISIBLE_WINDOW_SIZE = 560;
        private const double EXCLUSION_WIDTH = 140;
        private const double EXCLUSION_HEIGHT = 160;

        private const double TRANSITION_DURATION_MS = 2000;
        private const double FADE_START_PROGRESS = 0.25;
        private const double MAIN_WINDOW_FADE_DELAY = 0.3;
        #endregion

        #region Fields
        private MainWindow? _mainWindow;
        private DateTime _animationStartTime;
        private bool _isTransitioning;
        private readonly object _lockObject = new();
        private double _targetWidth = 670;
        private double _targetHeight = 444;
        private readonly Random _random = new();
        #endregion

        public StartupWindow()
        {
            InitializeComponent();
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        }

        #region Window Lifecycle
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyStartupTheme();
                GenerateParticles();
                StartupAnimations();

                await Task.Delay(1500);
                await PreloadMainWindowAsync();
                await Task.Delay(2000);

                await Dispatcher.InvokeAsync(() => LoadingText.Text = "LAUNCHING INTERFACE");
                await Task.Delay(500);
                await StartSmoothTransition();
            }
            catch
            {
                ShowMainWindowDirect();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
            base.OnClosed(e);
        }
        #endregion

        #region Theme Application
        private void ApplyStartupTheme()
        {
            try
            {
                var baseColor = ThemeManager.ThemeColor;

                UpdateStartupGradients(baseColor);
                UpdateLoadingDots(baseColor);
                UpdateDynamicResources(baseColor);
                UpdateParticleColors(baseColor);
            }
            catch
            {
            }
        }

        private void UpdateDynamicResources(Color baseColor)
        {
            Resources["ThemeColor"] = new SolidColorBrush(baseColor);
            Resources["ThemeColorDark"] = new SolidColorBrush(ThemeManager.ThemeColorDark);
            Resources["ThemeColorLight"] = new SolidColorBrush(ThemeManager.ThemeColorLight);
            Resources["ThemeGradientDark"] = new SolidColorBrush(ThemeManager.ThemeGradientDark);
        }

        private void UpdateStartupGradients(Color baseColor)
        {
            var midColor = DarkenColor(baseColor, 0.5);
            var outerColor = DarkenColor(baseColor, 0.3);
            var darkBackground = Color.FromRgb(10, 10, 10);

            UpdateGradientStop("StartupGradientMid", BlendColors(midColor, darkBackground, 0.4));
            UpdateGradientStop("StartupGradientOuter", BlendColors(outerColor, darkBackground, 0.6));
            UpdateGradientStop("GlowGradient1", baseColor);
            UpdateGradientStop("GlowGradient2", baseColor);
        }

        private void UpdateGradientStop(string name, Color color)
        {
            if (FindName(name) is GradientStop gradientStop)
                gradientStop.Color = color;
        }

        private void UpdateLoadingDots(Color baseColor)
        {
            UpdateDropShadow("Dot1Shadow", baseColor);
            UpdateDropShadow("Dot2Shadow", baseColor);
            UpdateDropShadow("Dot3Shadow", baseColor);
        }

        private void UpdateDropShadow(string name, Color color)
        {
            if (FindName(name) is DropShadowEffect shadow)
                shadow.Color = color;
        }

        private void UpdateParticleColors(Color baseColor)
        {
            // Update pulse ring
            if (FindName("PulseRing") is Ellipse pulseRing)
                pulseRing.Stroke = new SolidColorBrush(baseColor);

            // Update central glow - simplified search
            UpdateCentralGlow(baseColor);

            // Keep white shadow for logo
            if (FindName("AimmyLogo") is Path logo && logo.Effect is DropShadowEffect shadowEffect)
                shadowEffect.Color = Colors.White;
        }

        private void UpdateCentralGlow(Color baseColor)
        {
            if (!(LogicalTreeHelper.FindLogicalNode(this, "LoadingIndicator") is Grid loadingGrid))
                return;

            var parent = loadingGrid.Parent as StackPanel;
            var mainGrid = parent?.Parent as Grid;
            if (mainGrid == null) return;

            foreach (var child in mainGrid.Children)
            {
                if (child is Ellipse { Width: 40, Height: 40 } ellipse &&
                    ellipse.Effect is BlurEffect)
                {
                    ellipse.Fill = new SolidColorBrush(baseColor);
                    break;
                }
            }
        }
        #endregion

        #region Particle System
        private void GenerateParticles()
        {
            var canvas = ParticleCanvas;
            if (canvas == null) return;

            canvas.Children.Clear();
            int particleCount = _random.Next(PARTICLE_MIN_COUNT, PARTICLE_MAX_COUNT + 1);

            for (int i = 0; i < particleCount; i++)
            {
                CreateRandomParticle(canvas);
            }
        }

        private void CreateRandomParticle(Canvas canvas)
        {
            var particle = new Ellipse
            {
                Width = _random.NextDouble() * (PARTICLE_MAX_SIZE - PARTICLE_MIN_SIZE) + PARTICLE_MIN_SIZE,
                Height = _random.NextDouble() * (PARTICLE_MAX_SIZE - PARTICLE_MIN_SIZE) + PARTICLE_MIN_SIZE,
                Fill = new SolidColorBrush(GetRandomParticleColor()),
                Opacity = _random.NextDouble() * (PARTICLE_MAX_OPACITY - PARTICLE_MIN_OPACITY) + PARTICLE_MIN_OPACITY,
                Effect = new BlurEffect { Radius = 1.5 }
            };

            var position = GenerateParticlePosition();
            Canvas.SetLeft(particle, position.X);
            Canvas.SetTop(particle, position.Y);

            if (_random.NextDouble() < PARTICLE_ANIMATION_CHANCE)
                AnimateParticle(particle);

            canvas.Children.Add(particle);
        }

        private Point GenerateParticlePosition()
        {
            double containerWidth = ActualWidth > 0 ? ActualWidth : _targetWidth;
            double containerHeight = ActualHeight > 0 ? ActualHeight : _targetHeight;
            double offsetX = (containerWidth - VISIBLE_WINDOW_SIZE) / 2;
            double offsetY = (containerHeight - VISIBLE_WINDOW_SIZE) / 2;

            var center = new Point(
                offsetX + VISIBLE_WINDOW_SIZE / 2,
                offsetY + VISIBLE_WINDOW_SIZE / 2
            );

            // Try to find position outside exclusion zone
            for (int attempts = 0; attempts < 30; attempts++)
            {
                var x = offsetX + _random.NextDouble() * (VISIBLE_WINDOW_SIZE - 40) + 20;
                var y = offsetY + _random.NextDouble() * (VISIBLE_WINDOW_SIZE - 40) + 20;

                if (!IsInExclusionZone(x, y, center))
                    return new Point(x, y);
            }

            // Fallback to corner placement
            return GetCornerPosition(offsetX, offsetY);
        }

        private bool IsInExclusionZone(double x, double y, Point center)
        {
            return x > center.X - EXCLUSION_WIDTH / 2 &&
                   x < center.X + EXCLUSION_WIDTH / 2 &&
                   y > center.Y - EXCLUSION_HEIGHT / 2 &&
                   y < center.Y + EXCLUSION_HEIGHT / 2;
        }

        private Point GetCornerPosition(double offsetX, double offsetY)
        {
            return _random.Next(4) switch
            {
                0 => new Point(offsetX + _random.NextDouble() * 60 + 20, offsetY + _random.NextDouble() * 60 + 20),
                1 => new Point(offsetX + VISIBLE_WINDOW_SIZE - _random.NextDouble() * 60 - 20, offsetY + _random.NextDouble() * 60 + 20),
                2 => new Point(offsetX + _random.NextDouble() * 60 + 20, offsetY + VISIBLE_WINDOW_SIZE - _random.NextDouble() * 60 - 20),
                _ => new Point(offsetX + VISIBLE_WINDOW_SIZE - _random.NextDouble() * 60 - 20, offsetY + VISIBLE_WINDOW_SIZE - _random.NextDouble() * 60 - 20)
            };
        }

        private Color GetRandomParticleColor()
        {
            var baseColor = ThemeManager.ThemeColor;

            return _random.Next(3) switch
            {
                0 => baseColor, // Base theme color (33%)
                1 => LightenColor(baseColor, 40), // Lighter variant (33%)
                _ => DarkenColor(baseColor, 0.3) // Darker variant (33%)
            };
        }

        private void AnimateParticle(Ellipse particle)
        {
            var transform = new TranslateTransform();
            particle.RenderTransform = transform;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = _random.NextDouble() * PARTICLE_FLOAT_RANGE - PARTICLE_FLOAT_RANGE / 2,
                Duration = TimeSpan.FromSeconds(_random.NextDouble() * 3 + 4),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
                BeginTime = TimeSpan.FromSeconds(_random.NextDouble() * 3)
            };

            var property = _random.Next(2) == 0 ?
                TranslateTransform.XProperty :
                TranslateTransform.YProperty;

            transform.BeginAnimation(property, animation);
        }
        #endregion

        #region Animations
        private void StartupAnimations()
        {
            var storyboards = new[] { "LogoRevealAnimation", "PulseAnimation", "TextRevealAnimation", "LoadingDotsAnimation" };
            foreach (var name in storyboards)
            {
                if (FindResource(name) is Storyboard storyboard)
                    storyboard.Begin();
            }
        }

        private async Task PreloadMainWindowAsync()
        {
            var tcs = new TaskCompletionSource<bool>();

            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    _mainWindow = new MainWindow
                    {
                        Opacity = 0,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left = -10000,
                        Top = -10000
                    };

                    _mainWindow.Loaded += (s, e) =>
                    {
                        try
                        {
                            _mainWindow.Measure(new Size(_targetWidth, _targetHeight));
                            _mainWindow.Arrange(new Rect(0, 0, _targetWidth, _targetHeight));
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    };

                    _mainWindow.Show();
                    _mainWindow.Hide();
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }

        private async Task StartSmoothTransition()
        {
            if (_mainWindow == null || _isTransitioning)
                return;

            lock (_lockObject)
            {
                if (_isTransitioning) return;
                _isTransitioning = true;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                SetupTransitionGeometry();
                _animationStartTime = DateTime.Now;
                CompositionTarget.Rendering += OnRendering;
            });
        }

        private void SetupTransitionGeometry()
        {
            if (_mainWindow == null) return;

            _mainWindow.Left = Left;
            _mainWindow.Top = Top;
            _mainWindow.Show();
            _mainWindow.Opacity = 0;

            _targetWidth = _mainWindow.ActualWidth > 0 ? _mainWindow.ActualWidth : _mainWindow.Width;
            _targetHeight = _mainWindow.ActualHeight > 0 ? _mainWindow.ActualHeight : _mainWindow.Height;

            RevealContainer.Width = _targetWidth;
            RevealContainer.Height = _targetHeight;
            MainBorder.Width = _targetWidth;
            MainBorder.Height = _targetHeight;

            double startWidth = ActualWidth;
            double startHeight = ActualHeight;
            double offsetX = (_targetWidth - startWidth) / 2;
            double offsetY = (_targetHeight - startHeight) / 2;

            RevealClip.Rect = new Rect(offsetX, offsetY, startWidth, startHeight);

            Left = _mainWindow.Left - offsetX;
            Top = _mainWindow.Top - offsetY;
            Width = _targetWidth;
            Height = _targetHeight;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _animationStartTime).TotalMilliseconds;
            var progress = Math.Min(elapsed / TRANSITION_DURATION_MS, 1.0);
            var eased = EaseOutExpo(progress);

            UpdateRevealClip(eased);
            UpdateFadeTransition(progress);

            if (progress >= 1.0)
            {
                CompositionTarget.Rendering -= OnRendering;
                CompleteTransition();
            }
        }

        private void UpdateRevealClip(double eased)
        {
            const double startSize = 280;
            double offsetX = (_targetWidth - startSize) / 2;
            double offsetY = (_targetHeight - startSize) / 2;

            double currentWidth = startSize + (_targetWidth - startSize) * eased;
            double currentHeight = startSize + (_targetHeight - startSize) * eased;
            double currentX = Math.Max(0, offsetX * (1 - eased));
            double currentY = Math.Max(0, offsetY * (1 - eased));

            RevealClip.Rect = new Rect(currentX, currentY, currentWidth, currentHeight);
        }

        private void UpdateFadeTransition(double progress)
        {
            if (progress <= FADE_START_PROGRESS) return;

            double fadeProgress = Math.Min((progress - FADE_START_PROGRESS) / (1 - FADE_START_PROGRESS), 1.0);
            double smoothedFade = EaseInOutQuart(fadeProgress);

            // Fade out startup window
            double startupOpacity = Math.Max(0, 1 - Math.Pow(smoothedFade, 3));
            ContentContainer.Opacity = startupOpacity;
            Opacity = startupOpacity;

            // Fade in main window
            if (_mainWindow != null && fadeProgress > MAIN_WINDOW_FADE_DELAY)
            {
                double mainProgress = (fadeProgress - MAIN_WINDOW_FADE_DELAY) / (1 - MAIN_WINDOW_FADE_DELAY);
                _mainWindow.Opacity = EaseOutQuint(mainProgress);
            }
        }

        private void CompleteTransition()
        {
            if (_mainWindow == null) return;

            try
            {
                _mainWindow.Opacity = 1.0;
                Application.Current.MainWindow = _mainWindow;
                ContentContainer.CacheMode = null;
                Close();
            }
            catch
            {
            }
        }

        private void ShowMainWindowDirect()
        {
            try
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Opacity = 1.0;
                    _mainWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    _mainWindow.Left = Left + (Width - _mainWindow.Width) / 2;
                    _mainWindow.Top = Top + (Height - _mainWindow.Height) / 2;
                    Application.Current.MainWindow = _mainWindow;
                    _mainWindow.Show();
                }
                else
                {
                    var mainWindow = new MainWindow
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    Application.Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                Close();
            }
            catch
            {
                Application.Current.Shutdown();
            }
        }
        #endregion

        #region Input Handlers
        protected override void OnPreviewMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_isTransitioning && _mainWindow != null)
                _ = StartSmoothTransition();
            base.OnPreviewMouseLeftButtonDown(e);
        }

        protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            var triggerKeys = new[] { System.Windows.Input.Key.Space, System.Windows.Input.Key.Enter, System.Windows.Input.Key.Escape };
            if (triggerKeys.Contains(e.Key) && !_isTransitioning && _mainWindow != null)
                _ = StartSmoothTransition();
            base.OnPreviewKeyDown(e);
        }
        #endregion

        #region Helper Methods
        private static Color DarkenColor(Color color, double factor)
        {
            return Color.FromRgb(
                (byte)(color.R * (1 - factor)),
                (byte)(color.G * (1 - factor)),
                (byte)(color.B * (1 - factor))
            );
        }

        private static Color LightenColor(Color color, int amount)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, color.R + amount),
                (byte)Math.Min(255, color.G + amount),
                (byte)Math.Min(255, color.B + amount)
            );
        }

        private static Color BlendColors(Color color1, Color color2, double ratio)
        {
            return Color.FromRgb(
                (byte)(color1.R * ratio + color2.R * (1 - ratio)),
                (byte)(color1.G * ratio + color2.G * (1 - ratio)),
                (byte)(color1.B * ratio + color2.B * (1 - ratio))
            );
        }

        private static double EaseOutExpo(double t) => t == 1 ? 1 : 1 - Math.Pow(2, -10 * t);
        private static double EaseInOutQuart(double t) => t < 0.5 ? 8 * t * t * t * t : 1 - Math.Pow(-2 * t + 2, 4) / 2;
        private static double EaseOutQuint(double t) => 1 - Math.Pow(1 - t, 5);
        #endregion
    }
}