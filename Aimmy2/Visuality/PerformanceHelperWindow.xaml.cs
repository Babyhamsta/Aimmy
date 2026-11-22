using Aimmy2;
using Aimmy2.AILogic;
using Aimmy2.Class;
using Aimmy2.Theme;
using Other;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Visuality
{
    public partial class PerformanceHelperWindow : Window
    {
        private const double CompactWidth = 460;
        private const double CompactHeight = 178;
        private const double ExpandedWidth = 700;
        private const double ExpandedHeight = 474;
        private const int ExpandDurationMilliseconds = 860;
        private const int AutoExpandDelayMilliseconds = 260;

        internal enum CompactPromptAction
        {
            StayCompact,
            ExpandToHelper,
            SaveSkipAndClose
        }

        internal enum LaunchMode
        {
            CompactPrompt,
            FullHelper
        }

        internal readonly record struct LaunchBehavior(
            bool StartCompact,
            bool ShowCompactPrompt,
            bool AutoExpand);

        internal readonly record struct ExpansionAnimationPlan(
            double WindowLeft,
            double WindowTop,
            double WindowWidth,
            double WindowHeight,
            double SurfaceStartWidth,
            double SurfaceStartHeight,
            double SurfaceEndWidth,
            double SurfaceEndHeight,
            bool AnimateWindowBounds);

        private readonly MainWindow _mainWindow;
        private readonly AIManager _aiManager;
        private CancellationTokenSource? _benchmarkCancellation;
        private PerformanceBenchmarkReport? _report;
        private PerformanceBenchmarkSizeResult? _selectedResult;
        private PerformanceGoal _selectedGoal = PerformanceGoal.Balanced;
        private bool _benchmarkRunning;
        private bool _closeAfterBenchmarkCancel;
        private readonly LaunchMode _launchMode;

        internal PerformanceHelperWindow(MainWindow mainWindow, AIManager aiManager, LaunchMode launchMode = LaunchMode.CompactPrompt)
        {
            InitializeComponent();

            _mainWindow = mainWindow;
            _aiManager = aiManager;
            _launchMode = launchMode;

            if (!ResolveLaunchBehavior(launchMode).StartCompact)
            {
                Width = ExpandedWidth;
                Height = ExpandedHeight;
            }

            ThemeManager.TrackWindow(this);
            UpdateThemeColors();
            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateGoalButtons();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var behavior = ResolveLaunchBehavior(_launchMode);
            if (behavior.StartCompact)
            {
                PrepareCompactNotice(behavior.ShowCompactPrompt);
            }
            else
            {
                PrepareFullHelper();
            }

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

            if (behavior.AutoExpand)
            {
                AutoExpandAfterLoad();
            }
        }

        internal static LaunchBehavior ResolveLaunchBehavior(LaunchMode launchMode)
        {
            return launchMode switch
            {
                LaunchMode.CompactPrompt => new LaunchBehavior(true, true, false),
                LaunchMode.FullHelper => new LaunchBehavior(true, false, true),
                _ => new LaunchBehavior(false, false, false)
            };
        }

        private void PrepareCompactNotice(bool showPrompt)
        {
            Opacity = 0;
            FullContent.Visibility = Visibility.Hidden;
            FullContent.IsHitTestVisible = false;
            FullContent.Opacity = 0;
            MiniPanel.Visibility = Visibility.Visible;
            MiniPanel.IsHitTestVisible = true;
            MiniPanel.Opacity = 1;
            MiniOpenButton.IsEnabled = true;
            MiniSkipButton.IsEnabled = true;
            MiniCloseButton.IsEnabled = true;
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.HorizontalAlignment = HorizontalAlignment.Center;
            MainBorder.VerticalAlignment = VerticalAlignment.Center;
            MainBorder.Width = CompactWidth;
            MainBorder.Height = CompactHeight;

            if (showPrompt)
            {
                MiniPromptTitle.Text = "Run Performance Helper?";
                MiniPromptSubtitle.Text = "Benchmark Aimmy once and get a size + FPS cap for this PC.";
                MiniFooterText.Text = "Closing will remind you later.";
                MiniFooter.Visibility = Visibility.Visible;
                MiniOpenButton.Visibility = Visibility.Visible;
                MiniSkipButton.Visibility = Visibility.Visible;
                return;
            }

            MiniPromptTitle.Text = "Performance Helper";
            MiniPromptSubtitle.Text = "Opening benchmark panel.";
            MiniFooterText.Text = string.Empty;
            MiniFooter.Visibility = Visibility.Visible;
            MiniOpenButton.Visibility = Visibility.Collapsed;
            MiniSkipButton.Visibility = Visibility.Collapsed;
            MiniOpenButton.IsEnabled = false;
            MiniSkipButton.IsEnabled = false;
        }

        private async void AutoExpandAfterLoad()
        {
            await Task.Delay(AutoExpandDelayMilliseconds);

            if (!IsVisible)
                return;

            MiniOpenButton.IsEnabled = false;
            MiniSkipButton.IsEnabled = false;
            MiniCloseButton.IsEnabled = false;
            BeginExpandAnimation();
        }

        private void PrepareFullHelper()
        {
            Opacity = 0;
            Width = ExpandedWidth;
            Height = ExpandedHeight;

            MiniPanel.BeginAnimation(OpacityProperty, null);
            MiniPanel.Visibility = Visibility.Collapsed;
            MiniPanel.IsHitTestVisible = false;
            MiniPanel.Opacity = 0;

            FullContent.BeginAnimation(OpacityProperty, null);
            FullContent.Visibility = Visibility.Visible;
            FullContent.IsHitTestVisible = true;
            FullContent.Opacity = 1;

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            MainBorder.VerticalAlignment = VerticalAlignment.Stretch;
            MainBorder.Width = double.NaN;
            MainBorder.Height = double.NaN;
        }

        internal static CompactPromptAction ResolveCompactPromptAction(bool openRequested, bool skipRequested)
        {
            if (skipRequested)
                return CompactPromptAction.SaveSkipAndClose;

            return openRequested
                ? CompactPromptAction.ExpandToHelper
                : CompactPromptAction.StayCompact;
        }

        private void MiniOpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveCompactPromptAction(openRequested: true, skipRequested: false) != CompactPromptAction.ExpandToHelper)
                return;

            MiniOpenButton.IsEnabled = false;
            MiniSkipButton.IsEnabled = false;
            MiniCloseButton.IsEnabled = false;
            BeginExpandAnimation();
        }

        private void MiniSkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResolveCompactPromptAction(openRequested: false, skipRequested: true) == CompactPromptAction.SaveSkipAndClose)
            {
                PerformanceHelperState.SaveChoice("Skipped");
                Close();
            }
        }

        private void MiniCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BeginExpandAnimation()
        {
            var plan = CreateExpansionAnimationPlan(
                Left,
                Top,
                ActualWidth > 0 ? ActualWidth : Width,
                ActualHeight > 0 ? ActualHeight : Height,
                CompactWidth,
                CompactHeight,
                ExpandedWidth,
                ExpandedHeight,
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);

            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.HorizontalAlignment = HorizontalAlignment.Center;
            MainBorder.VerticalAlignment = VerticalAlignment.Center;
            MainBorder.Width = plan.SurfaceStartWidth;
            MainBorder.Height = plan.SurfaceStartHeight;

            Width = plan.WindowWidth;
            Height = plan.WindowHeight;
            Left = plan.WindowLeft;
            Top = plan.WindowTop;
            UpdateLayout();

            FullContent.Visibility = Visibility.Visible;
            FullContent.IsHitTestVisible = false;
            FullContent.Opacity = 0;

            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            var duration = TimeSpan.FromMilliseconds(ExpandDurationMilliseconds);

            var heightAnimation = CreateAnimation(plan.SurfaceStartHeight, plan.SurfaceEndHeight, duration, ease);
            heightAnimation.Completed += (_, _) => CompleteExpandAnimation();
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, CreateAnimation(plan.SurfaceStartWidth, plan.SurfaceEndWidth, duration, ease));
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, heightAnimation);

            MiniPanel.BeginAnimation(OpacityProperty, CreateOpacityAnimation(1, 0, 220, 360));
            FullContent.BeginAnimation(OpacityProperty, CreateOpacityAnimation(0, 1, 320, 500));
        }

        internal static ExpansionAnimationPlan CreateExpansionAnimationPlan(
            double currentLeft,
            double currentTop,
            double currentWidth,
            double currentHeight,
            double compactWidth,
            double compactHeight,
            double expandedWidth,
            double expandedHeight,
            double? workAreaLeft = null,
            double? workAreaTop = null,
            double? workAreaWidth = null,
            double? workAreaHeight = null)
        {
            double centerX = currentLeft + currentWidth / 2.0;
            double centerY = currentTop + currentHeight / 2.0;
            double targetLeft = centerX - expandedWidth / 2.0;
            double targetTop = centerY - expandedHeight / 2.0;

            if (workAreaLeft.HasValue &&
                workAreaTop.HasValue &&
                workAreaWidth.HasValue &&
                workAreaHeight.HasValue)
            {
                double minLeft = workAreaLeft.Value;
                double minTop = workAreaTop.Value;
                double maxLeft = minLeft + workAreaWidth.Value - expandedWidth;
                double maxTop = minTop + workAreaHeight.Value - expandedHeight;

                targetLeft = maxLeft < minLeft
                    ? minLeft
                    : Math.Clamp(targetLeft, minLeft, maxLeft);
                targetTop = maxTop < minTop
                    ? minTop
                    : Math.Clamp(targetTop, minTop, maxTop);
            }

            return new ExpansionAnimationPlan(
                targetLeft,
                targetTop,
                expandedWidth,
                expandedHeight,
                compactWidth,
                compactHeight,
                expandedWidth,
                expandedHeight,
                false);
        }

        private static DoubleAnimation CreateAnimation(double from, double to, TimeSpan duration, IEasingFunction ease)
        {
            return new DoubleAnimation(from, to, duration)
            {
                EasingFunction = ease
            };
        }

        private static DoubleAnimation CreateOpacityAnimation(
            double from,
            double to,
            int durationMilliseconds,
            int beginMilliseconds)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(durationMilliseconds))
            {
                BeginTime = TimeSpan.FromMilliseconds(beginMilliseconds),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }

        private void CompleteExpandAnimation()
        {
            MainBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            MainBorder.BeginAnimation(FrameworkElement.HeightProperty, null);
            MainBorder.HorizontalAlignment = HorizontalAlignment.Stretch;
            MainBorder.VerticalAlignment = VerticalAlignment.Stretch;
            MainBorder.Width = double.NaN;
            MainBorder.Height = double.NaN;

            MiniPanel.BeginAnimation(OpacityProperty, null);
            MiniPanel.Opacity = 0;
            MiniPanel.Visibility = Visibility.Collapsed;

            FullContent.BeginAnimation(OpacityProperty, null);
            FullContent.Visibility = Visibility.Visible;
            FullContent.IsHitTestVisible = true;
            FullContent.Opacity = 1;
        }

        private void OnThemeChanged(object? sender, Color newColor)
        {
            Dispatcher.Invoke(UpdateThemeColors);
        }

        private void UpdateThemeColors()
        {
            ThemeGradientStop.Color = ThemeManager.ThemeColorDark;
            var themeBrush = new SolidColorBrush(ThemeManager.ThemeColor);
            MiniOpenButton.Background = themeBrush;
            RunTestButton.Background = themeBrush;
            ApplyButton.Background = new SolidColorBrush(ThemeManager.ThemeColor);
            BenchmarkProgressBar.Foreground = new SolidColorBrush(ThemeManager.ThemeColorLight);
            UpdateGoalButtons();
            RefreshResultRows();
        }

        private void GoalButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.Tag is not string goalName ||
                !Enum.TryParse(goalName, out PerformanceGoal goal))
            {
                return;
            }

            _selectedGoal = goal;
            UpdateGoalButtons();
        }

        private void UpdateGoalButtons()
        {
            if (FastestGoalButton == null ||
                BalancedGoalButton == null ||
                LowestGoalButton == null)
            {
                return;
            }

            UpdateGoalButton(FastestGoalButton, _selectedGoal == PerformanceGoal.FastestDetections);
            UpdateGoalButton(BalancedGoalButton, _selectedGoal == PerformanceGoal.Balanced);
            UpdateGoalButton(LowestGoalButton, _selectedGoal == PerformanceGoal.LowestResources);
        }

        private static void UpdateGoalButton(Button button, bool selected)
        {
            Color color = selected ? ThemeManager.ThemeColor : Color.FromArgb(36, 255, 255, 255);
            Color border = selected ? ThemeManager.ThemeColorLight : Color.FromArgb(52, 255, 255, 255);
            button.Background = new SolidColorBrush(Color.FromArgb(selected ? (byte)150 : (byte)24, color.R, color.G, color.B));
            button.BorderBrush = new SolidColorBrush(border);
        }

        private async void RunTestButton_Click(object sender, RoutedEventArgs e)
        {
            _benchmarkRunning = true;
            _closeAfterBenchmarkCancel = false;
            _benchmarkCancellation = new CancellationTokenSource();
            IntroPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            ApplyButton.Visibility = Visibility.Collapsed;
            RunTestButton.Visibility = Visibility.Visible;
            NoThanksButton.IsEnabled = false;
            RunTestButton.IsEnabled = false;
            RunTestButton.Content = "Running";
            FooterText.Text = string.Empty;

            var progress = new Progress<PerformanceBenchmarkProgress>(UpdateProgress);

            try
            {
                _report = await Task.Run(
                    () => _aiManager.RunPerformanceBenchmarkAsync(progress, _benchmarkCancellation.Token, _selectedGoal),
                    _benchmarkCancellation.Token);

                ShowResults(_report);
                PerformanceHelperState.SaveChoice("Run");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _closeAfterBenchmarkCancel = false;
                ProgressTitle.Text = "Benchmark stopped";
                ProgressSubtitle.Text = ex.Message;
                FooterText.Text = "Close this helper or try again after loading a model.";
                RunTestButton.Content = "Try Again";
                RunTestButton.IsEnabled = true;
                NoThanksButton.IsEnabled = true;
            }
            finally
            {
                bool closeAfterCancel = _closeAfterBenchmarkCancel;
                _benchmarkRunning = false;
                _closeAfterBenchmarkCancel = false;
                _benchmarkCancellation?.Dispose();
                _benchmarkCancellation = null;

                if (closeAfterCancel && IsVisible)
                {
                    Close();
                }
            }
        }

        private void UpdateProgress(PerformanceBenchmarkProgress progress)
        {
            int remaining = Math.Max(0, progress.TotalSteps - progress.StepIndex);
            double percent = progress.TotalSteps == 0
                ? 0
                : ((progress.StepIndex - 1 + Math.Clamp(progress.StepProgress, 0, 1)) * 100.0) / progress.TotalSteps;

            if (progress.Status.Equals("Complete", StringComparison.OrdinalIgnoreCase))
            {
                percent = 100;
            }

            ProgressTitle.Text = $"Step {progress.StepIndex} of {progress.TotalSteps}";
            ProgressSubtitle.Text = $"{progress.Status}. {remaining} size checks left.";
            BenchmarkProgressBar.Value = percent;
        }

        private void ShowResults(PerformanceBenchmarkReport report)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ResultsPanel.Visibility = Visibility.Visible;
            NoThanksButton.Content = "Close";
            NoThanksButton.IsEnabled = true;
            FooterText.Text = string.Empty;

            ResultsStack.Children.Clear();

            _selectedResult = report.Results
                .FirstOrDefault(result =>
                    result.ImageSize == report.Recommendation.SuggestedImageSize &&
                    result.FrameCount > 0 &&
                    result.AverageFps > 0) ??
                report.Results.FirstOrDefault(result => result.FrameCount > 0 && result.AverageFps > 0) ??
                report.Results.FirstOrDefault();

            foreach (var result in report.Results.OrderByDescending(result => result.ImageSize))
            {
                AddResultRow(result, result == _selectedResult);
            }

            UpdateSelectedResultSummary();
            UpdateCompletedRunButtons(report);

            FixedModelNote.Text = report.IsFixedSizeModel
                ? "Fixed-size model: image size cannot be changed without loading a different model."
                : string.Empty;
        }

        private void UpdateCompletedRunButtons(PerformanceBenchmarkReport report)
        {
            bool hasStableResult = report.Results.Any(result => result.FrameCount > 0 && result.AverageFps > 0);
            ApplyButton.Visibility = hasStableResult ? Visibility.Visible : Visibility.Collapsed;
            RunTestButton.Visibility = hasStableResult ? Visibility.Collapsed : Visibility.Visible;
            RunTestButton.Content = hasStableResult ? "Run Test" : "Run Again";
            RunTestButton.IsEnabled = !hasStableResult;
        }

        private void AddResultRow(PerformanceBenchmarkSizeResult result, bool selected)
        {
            bool usable = result.FrameCount > 0 && result.AverageFps > 0;
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(selected ? ThemeManager.ThemeColorLight : Color.FromArgb(38, 255, 255, 255)),
                Background = new SolidColorBrush(selected ? Color.FromArgb(34, ThemeManager.ThemeColor.R, ThemeManager.ThemeColor.G, ThemeManager.ThemeColor.B) : Color.FromArgb(14, 255, 255, 255)),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = usable ? Cursors.Hand : Cursors.Arrow,
                Opacity = usable ? 1 : 0.58
            };

            if (usable)
            {
                border.PreviewMouseLeftButtonDown += (_, _) => SelectResult(result);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(154) });

            AddCell(grid, $"{result.ImageSize}px", 0, true);
            AddPairCell(grid, result, 1);
            AddBadgeCell(grid, result, selected, 2);

            border.Child = grid;
            ResultsStack.Children.Add(border);
        }

        private void SelectResult(PerformanceBenchmarkSizeResult result)
        {
            if (_report == null || result.FrameCount <= 0 || result.AverageFps <= 0)
                return;

            _selectedResult = result;
            ResultsStack.Children.Clear();

            foreach (var item in _report.Results.OrderByDescending(item => item.ImageSize))
            {
                AddResultRow(item, item == _selectedResult);
            }

            UpdateSelectedResultSummary();
        }

        private void RefreshResultRows()
        {
            if (_report == null ||
                ResultsStack == null ||
                ResultsPanel.Visibility != Visibility.Visible)
            {
                return;
            }

            ResultsStack.Children.Clear();

            foreach (var item in _report.Results.OrderByDescending(item => item.ImageSize))
            {
                AddResultRow(item, item == _selectedResult);
            }
        }

        private void UpdateSelectedResultSummary()
        {
            if (_report == null || _selectedResult == null || _selectedResult.FrameCount <= 0 || _selectedResult.AverageFps <= 0)
            {
                MaxFpsValue.Text = "-";
                CpuValue.Text = "-";
                GpuValue.Text = "-";
                CapValue.Text = "-";
                ApplyButton.IsEnabled = false;
                ApplyButton.Visibility = Visibility.Collapsed;
                FooterText.Text = "Close this helper or run the test again after the model settles.";
                RecommendationText.Text = "No stable AI-loop sample was captured.";
                WarningText.Text = _report?.Recommendations.HasLimitedHardwareWarning == true
                    ? $"Hardware notice: {_report.Recommendations.HardwareWarning}"
                    : string.Empty;
                WarningText.Visibility = _report?.Recommendations.HasLimitedHardwareWarning == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                return;
            }

            MaxFpsValue.Text = $"{_selectedResult.AverageFps:F0}";
            CpuValue.Text = FormatPercent(_selectedResult.AverageCpuPercent);
            GpuValue.Text = _selectedResult.GpuAvailable ? FormatPercent(_selectedResult.AverageGpuPercent) : "N/A";
            int selectedCap = GetSuggestedCap(_selectedResult);
            CapValue.Text = $"{selectedCap} FPS";
            ApplyButton.IsEnabled = _selectedResult.FrameCount > 0 && _selectedResult.AverageFps > 0;
            ApplyButton.Visibility = Visibility.Visible;
            FooterText.Text = "Select Fastest, Balanced, or Lowest Resource, then apply that pair.";

            string prefix = GetSelectionTitle(_selectedResult, _report);
            string summary = CreateSelectionRecommendation(_selectedResult, _report).Summary;
            RecommendationText.Text =
                $"{prefix}: {_selectedResult.ImageSize}px + {selectedCap} FPS cap. Full-speed AI-loop average was {_selectedResult.AverageFps:F0} FPS. {summary}";

            WarningText.Text = _report.Recommendations.HasLimitedHardwareWarning
                ? $"Hardware notice: {_report.Recommendations.HardwareWarning}"
                : string.Empty;
            WarningText.Visibility = _report.Recommendations.HasLimitedHardwareWarning
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AddPairCell(Grid grid, PerformanceBenchmarkSizeResult result, int column)
        {
            int cap = GetSuggestedCap(result);
            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(column == 0 ? 0 : 18, 0, 0, 0)
            };

            stack.Children.Add(new TextBlock
            {
                Text = result.FrameCount > 0 ? $"Full speed: {result.AverageFps:F0} avg FPS -> {cap} FPS cap" : "No stable sample",
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Atkinson Hyperlegible"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            stack.Children.Add(new TextBlock
            {
                Text = result.FrameCount > 0
                    ? $"Pair: {result.ImageSize}px + {cap} FPS cap   CPU {FormatPercent(result.AverageCpuPercent)}   GPU {(result.GpuAvailable ? FormatPercent(result.AverageGpuPercent) : "N/A")}"
                    : "Try again after the model finishes settling.",
                Foreground = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)),
                FontFamily = new FontFamily("Atkinson Hyperlegible"),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
        }

        private int GetSuggestedCap(PerformanceBenchmarkSizeResult result)
        {
            return _report == null
                ? result.SuggestedFpsLimit
                : CreateSelectionRecommendation(result, _report).SuggestedFpsLimit;
        }

        private readonly record struct ResultBadge(string Text, Color Color);

        private void AddBadgeCell(Grid grid, PerformanceBenchmarkSizeResult result, bool selected, int column)
        {
            var panel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };

            foreach (var badge in GetResultBadges(result, selected))
            {
                panel.Children.Add(CreateBadge(badge));
            }

            Grid.SetColumn(panel, column);
            grid.Children.Add(panel);
        }

        private IEnumerable<ResultBadge> GetResultBadges(PerformanceBenchmarkSizeResult result, bool selected)
        {
            var badges = new List<ResultBadge>();
            if (_report == null || result.FrameCount <= 0 || result.AverageFps <= 0)
                return badges;

            if (selected)
                badges.Add(new ResultBadge("Selected", ThemeManager.ThemeColorLight));

            if (_report.IsFixedSizeModel && MatchesRecommendation(result, _report.Recommendation))
            {
                badges.Add(new ResultBadge("Fixed Size - All Picks", Color.FromRgb(196, 181, 253)));
                return badges;
            }

            if (AllRecommendationModesUseSameResult(_report) && MatchesRecommendation(result, _report.Recommendations.Primary))
            {
                badges.Add(new ResultBadge("All Picks", Color.FromRgb(255, 210, 122)));
                return badges;
            }

            var recommendation = GetMatchingRecommendation(result, _report);
            if (recommendation != null)
                badges.Add(CreateModeBadge(recommendation.Mode));

            if (MatchesRecommendation(result, _report.Recommendations.Primary))
                badges.Add(new ResultBadge("Suggested", ThemeManager.ThemeColor));

            return badges;
        }

        private static ResultBadge CreateModeBadge(PerformanceRecommendationMode mode)
        {
            return mode switch
            {
                PerformanceRecommendationMode.FastestDetections => new ResultBadge("Fastest", Color.FromRgb(124, 199, 255)),
                PerformanceRecommendationMode.Quality => new ResultBadge("Quality", Color.FromRgb(124, 199, 255)),
                PerformanceRecommendationMode.LowestResources => new ResultBadge("Lowest", Color.FromRgb(94, 234, 212)),
                PerformanceRecommendationMode.HighSpeed => new ResultBadge("High Speed", Color.FromRgb(94, 234, 212)),
                _ => new ResultBadge("Balanced", Color.FromRgb(167, 139, 250))
            };
        }

        private static Border CreateBadge(ResultBadge badge)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(145, badge.Color.R, badge.Color.G, badge.Color.B)),
                Background = new SolidColorBrush(Color.FromArgb(34, badge.Color.R, badge.Color.G, badge.Color.B)),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 2, 0, 2),
                Child = new TextBlock
                {
                    Text = badge.Text,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontFamily = new FontFamily("Atkinson Hyperlegible"),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.NoWrap
                }
            };
        }

        private static void AddCell(Grid grid, string text, int column, bool strong)
        {
            var block = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(strong ? Colors.White : Color.FromArgb(190, 255, 255, 255)),
                FontFamily = new FontFamily("Atkinson Hyperlegible"),
                FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = strong ? 14 : 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(column == 0 ? 0 : 18, 0, 0, 0)
            };

            Grid.SetColumn(block, column);
            grid.Children.Add(block);
        }

        private static string GetSelectionTitle(PerformanceBenchmarkSizeResult result, PerformanceBenchmarkReport report)
        {
            if (AllRecommendationModesUseSameResult(report) && MatchesRecommendation(result, report.Recommendations.Primary))
                return "All picks";

            var recommendation = GetMatchingRecommendation(result, report);
            if (recommendation != null)
                return $"{recommendation.Label} pick";

            return "Selected pair";
        }

        private static PerformanceRecommendation? GetMatchingRecommendation(
            PerformanceBenchmarkSizeResult result,
            PerformanceBenchmarkReport report)
        {
            if (MatchesRecommendation(result, report.Recommendations.Primary))
                return report.Recommendations.Primary;

            if (MatchesRecommendation(result, report.Recommendations.FastestDetections))
                return report.Recommendations.FastestDetections;

            if (MatchesRecommendation(result, report.Recommendations.Balanced))
                return report.Recommendations.Balanced;

            if (MatchesRecommendation(result, report.Recommendations.LowestResources))
                return report.Recommendations.LowestResources;

            if (MatchesRecommendation(result, report.Recommendations.Quality))
                return report.Recommendations.Quality;

            if (MatchesRecommendation(result, report.Recommendations.HighSpeed))
                return report.Recommendations.HighSpeed;

            return null;
        }

        private static bool AllRecommendationModesUseSameResult(PerformanceBenchmarkReport report)
        {
            return SameRecommendationPair(report.Recommendations.FastestDetections, report.Recommendations.Balanced) &&
                   SameRecommendationPair(report.Recommendations.Balanced, report.Recommendations.LowestResources);
        }

        private static bool SameRecommendationPair(
            PerformanceRecommendation left,
            PerformanceRecommendation right)
        {
            return left.SuggestedImageSize == right.SuggestedImageSize &&
                   left.SuggestedFpsLimit == right.SuggestedFpsLimit;
        }

        private static bool MatchesRecommendation(
            PerformanceBenchmarkSizeResult result,
            PerformanceRecommendation recommendation)
        {
            return result.ImageSize == recommendation.SuggestedImageSize &&
                   PerformanceRecommendationBuilder.BuildFpsCap(result, GetGoalForMode(recommendation.Mode)) == recommendation.SuggestedFpsLimit;
        }

        private static PerformanceGoal GetGoalForMode(PerformanceRecommendationMode mode)
        {
            return mode switch
            {
                PerformanceRecommendationMode.FastestDetections => PerformanceGoal.FastestDetections,
                PerformanceRecommendationMode.LowestResources => PerformanceGoal.LowestResources,
                _ => PerformanceGoal.Balanced
            };
        }

        private static string FormatPercent(double value)
        {
            if (value > 0 && value < 1)
                return "<1%";

            if (value < 10)
                return $"{value:F1}%";

            return $"{value:F0}%";
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null || _selectedResult == null)
                return;

            ApplyButton.IsEnabled = false;
            FooterText.Text = "Applying selected size and FPS cap...";

            bool applied = await _mainWindow.ApplyPerformanceRecommendationAsync(CreateSelectionRecommendation(_selectedResult, _report));
            if (!applied)
            {
                FooterText.Text = "Could not apply the selected pair. Model settings were left unchanged.";
                ApplyButton.IsEnabled = true;
                return;
            }

            PerformanceHelperState.SaveChoice("Applied");
            Close();
        }

        private static PerformanceRecommendation CreateSelectionRecommendation(
            PerformanceBenchmarkSizeResult result,
            PerformanceBenchmarkReport report)
        {
            var knownRecommendation = GetMatchingRecommendation(result, report);
            if (knownRecommendation != null)
            {
                return knownRecommendation with
                {
                    CanChangeImageSize = !report.IsFixedSizeModel && result.ImageSize != AimSettings.ImageSize
                };
            }

            bool canChangeImageSize = !report.IsFixedSizeModel && result.ImageSize != AimSettings.ImageSize;
            string summary = report.IsFixedSizeModel
                ? "Fixed model locks image size. FPS cap still cuts heat."
                : result.ImageSize < AimSettings.ImageSize
                    ? "Lower image size cuts load. Small or far targets can lose detail."
                    : result.ImageSize > AimSettings.ImageSize
                        ? "Higher image size can improve detail but costs more load."
                        : "Keep image size. FPS cap cuts extra CPU/GPU work.";

            return new PerformanceRecommendation(
                result.ImageSize,
                PerformanceRecommendationBuilder.BuildFpsCap(result, report.Goal),
                canChangeImageSize,
                summary,
                "Selected",
                GetSelectedMode(report.Goal));
        }

        private static PerformanceRecommendationMode GetSelectedMode(PerformanceGoal goal)
        {
            return goal switch
            {
                PerformanceGoal.FastestDetections => PerformanceRecommendationMode.FastestDetections,
                PerformanceGoal.LowestResources => PerformanceRecommendationMode.LowestResources,
                _ => PerformanceRecommendationMode.Balanced
            };
        }

        private void NoThanksButton_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null)
            {
                PerformanceHelperState.SaveChoice("Skipped");
            }

            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RequestBenchmarkCancelAndClose();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                e.OriginalSource is DependencyObject source &&
                !IsInsideButton(source))
            {
                DragMove();
            }
        }

        private static bool IsInsideButton(DependencyObject source)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is Button)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        protected override void OnClosed(EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            if (_benchmarkRunning)
            {
                _benchmarkCancellation?.Cancel();
            }

            base.OnClosed(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_benchmarkRunning)
            {
                e.Cancel = true;
                RequestBenchmarkCancelAndClose();
                return;
            }

            base.OnClosing(e);
        }

        private void RequestBenchmarkCancelAndClose()
        {
            if (!_benchmarkRunning)
            {
                Close();
                return;
            }

            if (_closeAfterBenchmarkCancel)
                return;

            _closeAfterBenchmarkCancel = true;
            CloseButton.IsEnabled = false;
            NoThanksButton.IsEnabled = false;
            RunTestButton.IsEnabled = false;
            ApplyButton.IsEnabled = false;
            ProgressTitle.Text = "Stopping benchmark";
            ProgressSubtitle.Text = "Restoring the loaded model before closing.";
            FooterText.Text = string.Empty;
            _benchmarkCancellation?.Cancel();
        }
    }
}
