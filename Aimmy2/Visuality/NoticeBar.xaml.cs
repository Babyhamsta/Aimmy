using Aimmy2.Class;
using Aimmy2.Theme;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Visuality
{
    public partial class NoticeBar : Window
    {
        private static NoticeBar? _containerInstance;
        private static readonly object _instanceLock = new();
        private static ObservableCollection<NoticeItem>? _notices;
        private static DispatcherTimer? _cleanupTimer;
        private readonly bool _isContainerInstance;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);
        }

        public NoticeBar(string text, int waitingTime = 4000, NoticeType type = NoticeType.Info) : this(false)
        {
            CreateStandaloneNotice(text, waitingTime, type);
        }

        private NoticeBar(bool isContainer)
        {
            InitializeComponent();
            _isContainerInstance = isContainer;

            if (isContainer)
            {
                _notices ??= new ObservableCollection<NoticeItem>();
                NoticesContainer.ItemsSource = _notices;

                if (_cleanupTimer == null)
                {
                    _cleanupTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _cleanupTimer.Tick += CleanupExpiredNotices;
                }

                ThemeManager.ExcludeWindowFromBackground(this);
                ThemeManager.RegisterElement(this);
                ThemeManager.ThemeChanged += OnThemeChanged;
            }
        }

        private async void CreateStandaloneNotice(string text, int waitingTime, NoticeType type)
        {
            Application.Current.Dispatcher.Invoke(() => Show(text, waitingTime, type));
            Hide();
            await Task.Delay(100);
            Close();
        }

        private void OnThemeChanged(object? sender, Color newColor)
        {
            if (_isContainerInstance && _notices != null)
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (var notice in _notices)
                    {
                        notice.UpdateThemeColors();
                    }
                });
            }
        }

        private static void CleanupExpiredNotices(object? sender, EventArgs e)
        {
            if (_notices == null || _containerInstance == null) return;

            var now = DateTime.Now;
            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var notice = _notices[i];
                if (notice.IsExpired(now)) _containerInstance.AnimateRemoval(notice);
            }
        }

        private async void AnimateRemoval(NoticeItem notice)
        {
            notice.IsRemoving = true;

            var container = NoticesContainer?.ItemContainerGenerator.ContainerFromItem(notice) as ListBoxItem;
            if (container != null)
            {
                var fadeOut = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(150)
                };
                container.BeginAnimation(OpacityProperty, fadeOut);
                await Task.Delay(150);
            }

            _notices?.Remove(notice);

            if (_notices?.Count == 0)
            {
                _cleanupTimer?.Stop();
                Hide();
            }
        }

        public static void Show(string message, int duration = 4000, NoticeType type = NoticeType.Info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_instanceLock)
                {
                    if (_containerInstance == null)
                    {
                        _containerInstance = new NoticeBar(true);
                    }

                    if (!_containerInstance.IsVisible)
                    {
                        _containerInstance.Show();
                    }

                    var notice = new NoticeItem(message, duration, type);
                    _notices?.Add(notice);

                    while (_notices?.Count > 8)
                    {
                        _notices.RemoveAt(0);
                    }

                    _cleanupTimer?.Start();
                }
            });
        }

        private void ProgressBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not Border { DataContext: NoticeItem notice } bar) return;
            var remaining = notice.Remaining;
            double fraction = notice.Duration > 0 ? Math.Clamp(remaining.TotalMilliseconds / notice.Duration, 0, 1) : 0;
            var scale = new ScaleTransform(fraction, 1);
            bar.RenderTransform = scale;
            if (remaining > TimeSpan.Zero)
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(fraction, 0, remaining));
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_isContainerInstance)
            {
                _cleanupTimer?.Stop();
                ThemeManager.ThemeChanged -= OnThemeChanged;
                ThemeManager.UnregisterElement(this);

                lock (_instanceLock)
                {
                    _containerInstance = null;
                    _notices = null;
                    _cleanupTimer = null;
                }
            }

            base.OnClosed(e);
        }
    }

    public class NoticeItem : INotifyPropertyChanged
    {
        private readonly DateTime _expiryTime;
        private bool _isRemoving;

        public string Message { get; }
        public NoticeType Type { get; }
        public int Duration { get; }
        public string TypeLabel { get; }
        public string IconData { get; }
        public Brush IconColor { get; private set; }
        public Brush IconBackground { get; private set; }
        public Brush ProgressBrush { get; private set; }
        public Color ThemeColorMid { get; private set; }
        public Color ThemeColorEnd { get; private set; }

        public bool IsRemoving
        {
            get => _isRemoving;
            set
            {
                _isRemoving = value;
                OnPropertyChanged();
            }
        }

        public NoticeItem(string message, int duration, NoticeType type)
        {
            Message = message;
            Duration = duration;
            Type = type;
            _expiryTime = DateTime.Now.AddMilliseconds(duration);

            TypeLabel = type switch
            {
                NoticeType.Success => "SUCCESS",
                NoticeType.Warning => "WARNING",
                NoticeType.Error => "ERROR",
                _ => "INFO"
            };

            IconData = type switch
            {
                NoticeType.Success => "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41L9 16.17z",
                NoticeType.Warning => "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z",
                NoticeType.Error => "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12 19 6.41z",
                _ => "M11 7h2v6h-2zm0 8h2v2h-2z"
            };

            IconColor = new SolidColorBrush(Colors.White);
            IconBackground = new SolidColorBrush(Colors.Gray);
            ProgressBrush = new SolidColorBrush(Colors.Gray);

            UpdateThemeColors();
        }

        public void UpdateThemeColors()
        {
            Color typeColor = Type switch
            {
                NoticeType.Success => Color.FromRgb(34, 197, 94),
                NoticeType.Warning => Color.FromRgb(251, 191, 36),
                NoticeType.Error => Color.FromRgb(239, 68, 68),
                _ => ThemeManager.ThemeColor
            };

            IconColor = new SolidColorBrush(typeColor);
            IconBackground = new SolidColorBrush(Color.FromArgb(50, typeColor.R, typeColor.G, typeColor.B));
            ProgressBrush = new SolidColorBrush(typeColor);

            // Theme gradient colors (fully opaque)
            ThemeColorMid = Color.FromRgb(ThemeManager.ThemeColor.R,
                                          ThemeManager.ThemeColor.G,
                                          ThemeManager.ThemeColor.B);
            ThemeColorEnd = Color.FromRgb(ThemeManager.ThemeColorDark.R,
                                          ThemeManager.ThemeColorDark.G,
                                          ThemeManager.ThemeColorDark.B);

            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(IconBackground));
            OnPropertyChanged(nameof(ProgressBrush));
            OnPropertyChanged(nameof(ThemeColorMid));
            OnPropertyChanged(nameof(ThemeColorEnd));
        }

        internal TimeSpan Remaining => _expiryTime - DateTime.Now;

        public bool IsExpired(DateTime now) => now >= _expiryTime && !_isRemoving;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum NoticeType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
