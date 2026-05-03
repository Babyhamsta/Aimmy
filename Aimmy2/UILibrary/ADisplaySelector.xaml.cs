using Aimmy2.Class;
using Aimmy2.Resources;
using Aimmy2.Theme;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Aimmy2.UILibrary
{
    /// <summary>
    /// Interaction logic for ADisplaySelector.xaml
    /// </summary>
    public partial class ADisplaySelector : UserControl
    {
        private List<DisplayInfo> _displays = new List<DisplayInfo>();
        private int _selectedDisplayIndex = 0;

        public ADisplaySelector()
        {
            InitializeComponent();
            ApplyLocalization();
            Loaded += ADisplaySelector_Loaded;

            // Subscribe to theme and display changes
            ThemeManager.ThemeChanged += OnThemeChanged;
            DisplayManager.DisplayChanged += OnDisplayManagerChanged;
        }

        private void ApplyLocalization()
        {
            DisplaySelectorTitle.Content = LocalizationManager.GetString("Display_FocusDisplay");
            CurrentDisplayInfo.Content = LocalizationManager.GetString("Display_PrimaryDisplaySelected");
        }

        private void ADisplaySelector_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDisplays();
        }

        private void OnThemeChanged(object? sender, Color newThemeColor)
        {
            // Update all display visuals when theme changes
            UpdateUI();
        }

        private void OnDisplayManagerChanged(object? sender, DisplayChangedEventArgs e)
        {
            // Update selection when DisplayManager changes (external change)
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (e.DisplayIndex != _selectedDisplayIndex)
                {
                    _selectedDisplayIndex = e.DisplayIndex;
                    RefreshDisplays(); // This will update the UI
                }
            });
        }

        public void RefreshDisplays()
        {
            _displays = DisplayManager.GetAllDisplays();
            _selectedDisplayIndex = DisplayManager.CurrentDisplayIndex;

            DisplayGrid.Children.Clear();
            CreateDisplayVisuals();
            UpdateGridLayout();
            UpdateUI();
        }

        private void CreateDisplayVisuals()
        {
            for (int i = 0; i < _displays.Count; i++)
            {
                var display = _displays[i];
                CreateDisplayVisual(display);
            }
        }

        private void CreateDisplayVisual(DisplayInfo display)
        {
            // Create container
            var container = new Border
            {
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Cursor = Cursors.Hand,
                Tag = display.Index
            };

            // Create inner grid
            var grid = new Grid();

            // Monitor visual
            var monitorBorder = new Border
            {
                Width = 50,
                Height = 35,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 25)
            };

            // Add a stand for the monitor
            var stand = new Rectangle
            {
                Width = 20,
                Height = 8,
                Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 35, 0, 0)
            };

            // Display number
            var displayNumber = new TextBlock
            {
                Text = (display.Index + 1).ToString(),
                FontFamily = (FontFamily)FindResource("Atkinson Hyperlegible"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(221, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Primary indicator
            if (display.IsPrimary)
            {
                var primaryBadge = new Border
                {
                    Background = new SolidColorBrush(ThemeManager.ThemeColor),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 2, 4, 2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 5),
                    Tag = "PrimaryBadge" // Tag for identification during updates
                };

                var primaryText = new TextBlock
                {
                    Text = LocalizationManager.GetString("Display_Primary"),
                    FontFamily = (FontFamily)FindResource("Atkinson Hyperlegible"),
                    FontSize = 9,
                    Foreground = Brushes.White
                };

                primaryBadge.Child = primaryText;
                grid.Children.Add(primaryBadge);
            }

            // Add elements to grid
            grid.Children.Add(monitorBorder);
            grid.Children.Add(stand);
            grid.Children.Add(displayNumber);

            container.Child = grid;

            // Event handlers
            container.MouseEnter += DisplayVisual_MouseEnter;
            container.MouseLeave += DisplayVisual_MouseLeave;
            container.MouseLeftButtonDown += DisplayVisual_MouseLeftButtonDown;

            DisplayGrid.Children.Add(container);
        }

        private void UpdateGridLayout()
        {
            // Adjust grid layout based on number of displays
            switch (_displays.Count)
            {
                case 0:
                    DisplayGrid.Rows = 1;
                    DisplayGrid.Columns = 1;
                    break;
                case 1:
                    DisplayGrid.Rows = 1;
                    DisplayGrid.Columns = 1;
                    break;
                case 2:
                    DisplayGrid.Rows = 1;
                    DisplayGrid.Columns = 2;
                    break;
                case 3:
                case 4:
                    DisplayGrid.Rows = 2;
                    DisplayGrid.Columns = 2;
                    break;
                default:
                    DisplayGrid.Rows = 2;
                    DisplayGrid.Columns = Math.Min(4, (_displays.Count + 1) / 2);
                    break;
            }
        }

        private void DisplayVisual_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && (int)border.Tag != _selectedDisplayIndex)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(77, 60, 60, 60));
            }
        }

        private void DisplayVisual_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && (int)border.Tag != _selectedDisplayIndex)
            {
                border.Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60));
            }
        }

        private void DisplayVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                int newIndex = (int)border.Tag;
                SelectDisplay(newIndex);
            }
        }

        private void SelectDisplay(int index)
        {
            if (index == _selectedDisplayIndex || index >= _displays.Count) return;

            // Use DisplayManager to change display - this will notify all subscribers
            if (DisplayManager.SetDisplay(index))
            {
                _selectedDisplayIndex = index;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (_displays.Count == 0)
            {
                CurrentDisplayInfo.Content = LocalizationManager.GetString("Common_NoDisplaysDetected");
                return;
            }

            // Update visual states
            for (int i = 0; i < DisplayGrid.Children.Count; i++)
            {
                if (DisplayGrid.Children[i] is Border border)
                {
                    bool isSelected = (int)border.Tag == _selectedDisplayIndex;

                    if (isSelected)
                    {
                        // Use current theme color
                        border.Background = new SolidColorBrush(ThemeManager.ThemeColor);
                        border.BorderBrush = new SolidColorBrush(Colors.White);
                        border.BorderThickness = new Thickness(2);
                    }
                    else
                    {
                        border.Background = new SolidColorBrush(Color.FromArgb(51, 60, 60, 60));
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
                        border.BorderThickness = new Thickness(1);
                    }

                    // Update primary badge color if exists
                    if (border.Child is Grid grid)
                    {
                        foreach (var child in grid.Children)
                        {
                            if (child is Border badge && badge.Tag as string == "PrimaryBadge")
                            {
                                badge.Background = new SolidColorBrush(ThemeManager.ThemeColor);
                            }
                        }
                    }
                }
            }

            // Update info label
            if (_selectedDisplayIndex < _displays.Count)
            {
                var display = _displays[_selectedDisplayIndex];
                string info = string.Format(LocalizationManager.GetString("Common_DisplaySelected"), display.Index + 1);
                if (display.IsPrimary) info += LocalizationManager.GetString("Common_Primary");
                info += $" - {display.Bounds.Width}x{display.Bounds.Height}";
                CurrentDisplayInfo.Content = info;
            }
        }

        public int GetSelectedDisplayIndex() => _selectedDisplayIndex;

        public DisplayInfo? GetSelectedDisplay() => _selectedDisplayIndex < _displays.Count ? _displays[_selectedDisplayIndex] : null;

        public Rect GetSelectedDisplayBounds() => GetSelectedDisplay()?.Bounds ?? new Rect();

        // Clean up event subscriptions
        public void Dispose()
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            DisplayManager.DisplayChanged -= OnDisplayManagerChanged;
        }
    }
}