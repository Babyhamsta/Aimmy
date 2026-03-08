using Aimmy2.Theme;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Visuality;

namespace Aimmy2.UILibrary
{
    public partial class AColorWheel : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(AColorWheel),
            new PropertyMetadata("Theme Color", OnTitleChanged));

        public static readonly DependencyProperty ShowArrowProperty =
        DependencyProperty.Register(
        "ShowArrow",
        typeof(bool),
        typeof(AColorWheel),
        new PropertyMetadata(false));

        public bool ShowArrow
        {
            get => (bool)GetValue(ShowArrowProperty);
            set => SetValue(ShowArrowProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AColorWheel control && e.NewValue is string newTitle)
            {
                control.ColorWheelTitle.Content = newTitle;
            }
        }
        //--
        private Color _initialColor = Colors.Transparent; // Transparent means "not set"
        private bool _hasInitialColor = false;
        public bool SuppressThemeApply { get; set; } = false;
        private bool _isShowingDragDrop = false;
        private readonly TimeSpan _animationDuration = TimeSpan.FromMilliseconds(200);
        private double _mediaBrightness = 1.0;
        private bool _isUpdatingMediaFromCode = false;
        //--
        private bool _isMouseDown = false;
        private WriteableBitmap? _colorWheelBitmap;
        private Color _selectedColor = Color.FromRgb(114, 46, 209); // Default purple
        private Color _previewColor = Color.FromRgb(114, 46, 209); // For live preview
        private double _brightness = 1.0;
        private double _currentHue = 0;
        private double _currentSaturation = 0;
        private bool _isUpdatingFromCode = false;
        private bool _isTextBoxLoadedAlready = false;

        public AColorWheel()
        {
            InitializeComponent();
            Loaded += AColorWheel_Loaded;
        }

        private void AColorWheel_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the color wheel bitmap
            CreateColorWheel();

            // Use initial color if set, otherwise use theme color
            if (_hasInitialColor)
            {
                _selectedColor = _initialColor;
            }
            else
            {
                _selectedColor = ThemeManager.ThemeColor;
            }
            _previewColor = _selectedColor;
            UpdateColorPreview(_previewColor);

            // Position selector based on current color
            PositionSelectorForColor(_selectedColor);

            // Update brightness gradient
            UpdateBrightnessGradient();

            // Allow TextBox to load Hex Values
            _isTextBoxLoadedAlready = true;
        }

        private void CreateColorWheel()
        {
            int size = 200;
            _colorWheelBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);

            byte[] pixels = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Calculate distance from center
                    double dx = x - size / 2.0;
                    double dy = y - size / 2.0;
                    double distance = Math.Sqrt(dx * dx + dy * dy);

                    // Only draw within the circle
                    if (distance <= size / 2.0)
                    {
                        // Calculate angle for hue (0-360)
                        double angle = Math.Atan2(dy, dx);
                        double hue = (angle + Math.PI) / (2 * Math.PI) * 360;

                        // Calculate saturation based on distance from center
                        double saturation = distance / (size / 2.0);

                        // Convert HSV to RGB with full brightness for the wheel
                        Color color = HsvToRgb(hue, saturation, 1.0);

                        int pixelOffset = (y * size + x) * 4;
                        pixels[pixelOffset] = color.B;
                        pixels[pixelOffset + 1] = color.G;
                        pixels[pixelOffset + 2] = color.R;
                        pixels[pixelOffset + 3] = 255;
                    }
                    else
                    {
                        // Transparent outside the circle
                        int pixelOffset = (y * size + x) * 4;
                        pixels[pixelOffset + 3] = 0;
                    }
                }
            }

            _colorWheelBitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            ColorWheelEllipse.Fill = new ImageBrush(_colorWheelBitmap);
        }

        private void ColorWheel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            UpdateColorFromPosition(e.GetPosition(ColorWheelCanvas));
            ColorWheelCanvas.CaptureMouse();
        }

        private void ColorWheel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                UpdateColorFromPosition(e.GetPosition(ColorWheelCanvas));
            }
        }

        private void ColorWheel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            ColorWheelCanvas.ReleaseMouseCapture();

            // Only save theme color if we're not suppressing theme changes
            if (!SuppressThemeApply)
            {
                SaveThemeColor(_previewColor);
            }
        }

        private void UpdateColorFromPosition(Point position)
        {
            double centerX = ColorWheelCanvas.Width / 2;
            double centerY = ColorWheelCanvas.Height / 2;

            double dx = position.X - centerX;
            double dy = position.Y - centerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            // Constrain to circle
            if (distance > centerX)
            {
                double angle = Math.Atan2(dy, dx);
                dx = Math.Cos(angle) * centerX;
                dy = Math.Sin(angle) * centerX;
                distance = centerX;
            }

            // Update selector position
            Canvas.SetLeft(ColorSelector, centerX + dx - 10); // Adjusted for new selector size
            Canvas.SetTop(ColorSelector, centerY + dy - 10);

            // Calculate color
            _currentHue = (Math.Atan2(dy, dx) + Math.PI) / (2 * Math.PI) * 360;
            _currentSaturation = distance / centerX;

            _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);

            // Update preview
            UpdateColorPreview(_previewColor);

            // Update brightness gradient
            UpdateBrightnessGradient();

            // Update the selector dot color
            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(_previewColor);
            }
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessSlider != null && !_isUpdatingFromCode)
            {
                _brightness = BrightnessSlider.Value;

                // Recalculate color with new brightness
                _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);

                // Update preview
                UpdateColorPreview(_previewColor);

                // Update the selector dot color
                if (ColorDot != null)
                {
                    ColorDot.Fill = new SolidColorBrush(_previewColor);
                }
            }
        }

        private void BrightnessSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // Change BrightnessValue Text
            BrightnessValue.Text = (BrightnessSlider.Value * 100).ToString();

            // Only save theme color if we're not suppressing theme changes
            if (!SuppressThemeApply)
            {
                SaveThemeColor(_previewColor);
            }
        }

        private void UpdateColorPreview(Color color)
        {
            // Update the preview circle
            if (ColorPreview != null)
            {
                ColorPreview.Fill = new SolidColorBrush(color);
            }

            // Update hex value
            if (HexValue != null)
            {
                HexValue.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }

        private void UpdateBrightnessGradient()
        {
            // Update the brightness slider gradient to show the color range
            if (BrightnessGradientStart != null && BrightnessGradientEnd != null)
            {
                // Start with black
                BrightnessGradientStart.Color = Color.FromRgb(0, 0, 0);

                // End with the full brightness color
                BrightnessGradientEnd.Color = HsvToRgb(_currentHue, _currentSaturation, 1.0);

                // Change Brightness Value Text
                BrightnessValue.Text = (BrightnessSlider.Value * 100).ToString();
            }
        }

        private void SaveThemeColor(Color color)
        {
            // Update selected color
            _selectedColor = color;

            // Only set theme color if we're not suppressing theme changes
            if (!SuppressThemeApply)
            {
                ThemeManager.SetThemeColor(color);
            }

            // Save to settings (implement your settings save logic here)
            string hexColor = ThemeManager.GetThemeColorHex();
            // Settings.SaveThemeColor(hexColor);
        }

        private void PositionSelectorForColor(Color color)
        {
            _isUpdatingFromCode = true;

            // Convert RGB to HSV to find position
            double h, s, v;
            RgbToHsv(color, out h, out s, out v);

            // Store current values
            _currentHue = h;
            _currentSaturation = s;
            _brightness = v;

            // Calculate position from HSV
            double angle = h * Math.PI / 180.0 - Math.PI;
            double radius = s * (ColorWheelCanvas.Width / 2);

            double x = ColorWheelCanvas.Width / 2 + Math.Cos(angle) * radius;
            double y = ColorWheelCanvas.Height / 2 + Math.Sin(angle) * radius;

            Canvas.SetLeft(ColorSelector, x - 10);
            Canvas.SetTop(ColorSelector, y - 10);

            // Set brightness slider
            BrightnessSlider.Value = v;

            // Update selector dot color
            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(color);
            }

            // Update brightness gradient
            UpdateBrightnessGradient();

            _isUpdatingFromCode = false;
        }

        public void LoadSavedThemeColor(string hexColor)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hexColor);
                _selectedColor = color;
                _previewColor = color;

                // Update UI
                UpdateColorPreview(color);
                PositionSelectorForColor(color);

                // Apply theme
                ThemeManager.SetThemeColor(color);
            }
            catch
            {
                // If parsing fails, keep default color
            }
        }

        #region Color Conversion Methods

        private Color HsvToRgb(double hue, double saturation, double value)
        {
            int hi = (int)(hue / 60) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte)value;
            byte p = (byte)(value * (1 - saturation));
            byte q = (byte)(value * (1 - f * saturation));
            byte t = (byte)(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return Color.FromRgb(v, t, p);
                case 1: return Color.FromRgb(q, v, p);
                case 2: return Color.FromRgb(p, v, t);
                case 3: return Color.FromRgb(p, q, v);
                case 4: return Color.FromRgb(t, p, v);
                default: return Color.FromRgb(v, p, q);
            }
        }

        private void RgbToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            // Value
            value = max;

            // Saturation
            if (max == 0)
                saturation = 0;
            else
                saturation = delta / max;

            // Hue
            if (delta == 0)
            {
                hue = 0;
            }
            else if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }

            if (hue < 0)
                hue += 360;
        }

        #endregion

        #region Hex Changer through TextBox

        bool isValidHex(string Hex)
        {
            // Reference: https://www.w3resource.com/csharp-exercises/re/csharp-re-exercise-1.php
            return Regex.IsMatch(Hex, @"[#][0-9A-Fa-f]{6}\b");
        }

        private void HexValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Reference: https://stackoverflow.com/questions/2109756/how-do-i-get-the-color-from-a-hexadecimal-color-code-using-net
            if (isValidHex(HexValue.Text) && _isTextBoxLoadedAlready == true)
            {
                SaveThemeColor((Color)ColorConverter.ConvertFromString(HexValue.Text));
                UpdateColorPreview((Color)ColorConverter.ConvertFromString(HexValue.Text));
                PositionSelectorForColor((Color)ColorConverter.ConvertFromString(HexValue.Text));
            }
        }

        #endregion

        #region Brightness Changer through TextBox

        private void BrightnessValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Reference: https://stackoverflow.com/questions/463299/how-do-i-make-a-textbox-that-only-accepts-numbers
            if (System.Text.RegularExpressions.Regex.IsMatch(BrightnessValue.Text, "[^0-9]") || (BrightnessValue.Text.Length > 2 && BrightnessValue.Text != "100"))
                BrightnessValue.Text = BrightnessValue.Text.Remove(BrightnessValue.Text.Length - 1);
            if (BrightnessValue.Text.Length < 1)
                BrightnessValue.Text = "0";
        }

        private void BrightnessValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                BrightnessSlider.Value = (double)Convert.ToInt32(BrightnessValue.Text) / 100;
                e.Handled = true;
            }
        }

        #endregion
        //--
        public Color GetCurrentPreviewColor()
        {
            if (ColorPreview.Fill is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Colors.Transparent;
        }
        public void SetInitialColor(Color color)
        {
            _initialColor = color;
            _hasInitialColor = true;
        }
        //--

        #region Drag & Drop & Image Support, etc.

        private void DragDropView_DragEnter(object sender, DragEventArgs e)
        {
            if (IsValidDragData(e.Data))
            {
                DropZone.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3FFFFFFF"));
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DragDropView_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AFFFFFF"));
            e.Handled = true;
        }

        private void DragDropView_Drop(object sender, DragEventArgs e)
        {
            DropZone.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1AFFFFFF"));

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files?.Length > 0)
                {
                    string filePath = files[0];
                    string? extension = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();

                    // Only allow image formats
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                    {
                        LoadMediaPreview(filePath);
                    }
                    else
                    {
                        new NoticeBar("Error: Only PNG/JPG/JPEG images are supported", 3000).Show();
                    }

                    e.Handled = true;
                    return;
                }
            }

            e.Handled = true;
        }

        private bool IsValidDragData(IDataObject data)
        {
            return data.GetDataPresent(DataFormats.FileDrop)
                   && ((string[])data.GetData(DataFormats.FileDrop))[0].ToLower()
                   is var file
                   && (file.EndsWith(".png")
                      || file.EndsWith(".jpg")
                      || file.EndsWith(".jpeg"));

        }

        private void LoadMediaPreview(string filePath)
        {
            if (DropHintText == null || MediaPreviewImage == null || MediaPreviewPlayer == null)
                return;
            try
            {
                DropZone.Background = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
                DropHintText.Visibility = Visibility.Collapsed;
                string mediaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "media");
                Directory.CreateDirectory(mediaDir);
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(mediaDir, fileName);
                if (!filePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Copy(filePath, destPath);
                }
                if (filePath.ToLower().EndsWith(".png") ||
                    filePath.ToLower().EndsWith(".jpg") ||
                    filePath.ToLower().EndsWith(".jpeg"))
                {
                    MediaPreviewImage.Visibility = Visibility.Visible;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(destPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    MediaPreviewImage.Source = bitmap;
                    MediaPreviewImage.Stretch = Stretch.Uniform;

                }
                _mediaBrightness = ThemeManager.MediaBrightness;
                InitializeMediaBrightnessControls();
                ThemeManager.SetMediaBackground(destPath, _mediaBrightness);
            }
            catch
            {
                DropHintText.Visibility = Visibility.Visible;
                DropHintText.Text = "Invalid Media File";
                DropZone.Background = Brushes.Transparent;
            }
        }

        private void InitializeMediaBrightnessControls()
        {
            _isUpdatingMediaFromCode = true;
            _mediaBrightness = ThemeManager.MediaBrightness;
            if (BrightnessMediaSlider != null)
            {
                BrightnessMediaSlider.Value = _mediaBrightness;
            }
            if (BrightnessMediaValue != null)
            {
                BrightnessMediaValue.Text = (_mediaBrightness * 100).ToString("F0");
            }
            _isUpdatingMediaFromCode = false;
        }

        public void SyncWithThemeManager()
        {
            if (ThemeManager.IsMediaBackground)
            {
                _isUpdatingMediaFromCode = true;
                _mediaBrightness = ThemeManager.MediaBrightness;
                if (BrightnessMediaSlider != null)
                {
                    BrightnessMediaSlider.Value = _mediaBrightness;
                }
                if (BrightnessMediaValue != null)
                {
                    BrightnessMediaValue.Text = (_mediaBrightness * 100).ToString("F0");
                }
                _isUpdatingMediaFromCode = false;
                ApplyMediaBrightness();
            }
        }

        public void ClearMediaPreview()
        {
            if (MediaPreviewImage.Parent is Grid imageParent)
            {
                var overlays = imageParent.Children.OfType<Border>()
                    .Where(b => b.Tag?.ToString() == "BrightnessOverlay")
                    .ToList();
                foreach (var overlay in overlays)
                    imageParent.Children.Remove(overlay);
            }
            MediaPreviewImage.Source = null;
            MediaPreviewImage.OpacityMask = null;
            MediaPreviewImage.Visibility = Visibility.Collapsed;
            DropHintText.Visibility = Visibility.Visible;
            DropZone.Background = Brushes.Transparent;
            ThemeManager.ClearMediaBackground();
            _mediaBrightness = 1.0;
            InitializeMediaBrightnessControls();
        }

        #endregion

        #region XAML Buttons - Media

        private void ArrowButton_Click(object sender, RoutedEventArgs e)
        {
            _isShowingDragDrop = !_isShowingDragDrop;
            ArrowButton.Content = _isShowingDragDrop ? "<" : ">";
            if (_isShowingDragDrop)
            {
                DragDropView.Visibility = Visibility.Visible;
                DragDropView.Opacity = 0;
                DragDropView.IsHitTestVisible = true;
                ColorWheelView.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(0, _animationDuration));
                DragDropView.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1, _animationDuration));
                var timer = new DispatcherTimer { Interval = _animationDuration };
                timer.Tick += (s, args) =>
                {
                    ColorWheelView.Visibility = Visibility.Collapsed;
                    ColorWheelView.IsHitTestVisible = false;
                    timer.Stop();
                };
                timer.Start();
            }
            else
            {
                ColorWheelView.Visibility = Visibility.Visible;
                ColorWheelView.Opacity = 0;
                ColorWheelView.IsHitTestVisible = true;
                DragDropView.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, _animationDuration));
                ColorWheelView.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, _animationDuration));
                var timer = new DispatcherTimer { Interval = _animationDuration };
                timer.Tick += (s, args) =>
                {
                    DragDropView.Visibility = Visibility.Collapsed;
                    DragDropView.IsHitTestVisible = false;
                    timer.Stop();
                };
                timer.Start();
            }
        }

        private void BrightnessMediaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessMediaSlider == null || _isUpdatingMediaFromCode)
                return;
            _mediaBrightness = BrightnessMediaSlider.Value;
            if (BrightnessMediaValue != null)
            {
                _isUpdatingMediaFromCode = true;
                BrightnessMediaValue.Text = Math.Round(_mediaBrightness * 100).ToString("F0");
                _isUpdatingMediaFromCode = false;
            }
            ApplyMediaBrightness();
            ThemeManager.UpdateMediaBrightness(_mediaBrightness);
        }

        private void BrightnessMediaSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (BrightnessMediaValue != null)
            {
                BrightnessMediaValue.Text = (_mediaBrightness * 100).ToString("F0");
            }
            ThemeManager.UpdateMediaBrightness(_mediaBrightness);
        }

        private void BrightnessMediaValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(BrightnessMediaValue.Text, "[^0-9]") ||
                (BrightnessMediaValue.Text.Length > 2 && BrightnessMediaValue.Text != "100"))
            {
                BrightnessMediaValue.Text = BrightnessMediaValue.Text.Remove(BrightnessMediaValue.Text.Length - 1);
            }
            if (BrightnessMediaValue.Text.Length < 1)
            {
                BrightnessMediaValue.Text = "0";
            }
        }

        private void BrightnessMediaValue_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                try
                {
                    double value = Convert.ToDouble(BrightnessMediaValue.Text) / 100.0;
                    value = Math.Max(0, Math.Min(2, value));

                    _isUpdatingMediaFromCode = true;
                    BrightnessMediaSlider.Value = value;
                    _isUpdatingMediaFromCode = false;

                    _mediaBrightness = value;
                    ApplyMediaBrightness();
                    ThemeManager.UpdateMediaBrightness(_mediaBrightness);
                }
                catch
                {
                    BrightnessMediaValue.Text = (_mediaBrightness * 100).ToString("F0");
                }
                e.Handled = true;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MediaPreviewImage != null)
                {
                    MediaPreviewImage.Source = null;
                    MediaPreviewImage.Visibility = Visibility.Collapsed;
                }

                if (MediaPreviewPlayer != null)
                {
                    MediaPreviewPlayer.Source = null;
                    MediaPreviewPlayer.Stop();
                    MediaPreviewPlayer.Visibility = Visibility.Collapsed;
                }
                if (DropHintText != null)
                {
                    DropHintText.Visibility = Visibility.Visible;
                    DropHintText.Text = "Drag & Drop Media Here";
                }
                _mediaBrightness = 1.0;
                if (BrightnessMediaSlider != null) BrightnessMediaSlider.Value = 1.0;
                if (BrightnessMediaValue != null) BrightnessMediaValue.Text = "100";
                ThemeManager.ClearMediaBackground();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing media: {ex.Message}");
            }
        }

        #endregion

        #region Media Brightness Logic - KMP

        private void ApplyMediaBrightness()
        {
            if (MediaPreviewImage.Visibility == Visibility.Visible && MediaPreviewImage.Source != null)
            {
                ApplyImageBrightness();
            }

        }

        private void ApplyImageBrightness()
        {
            var brightnessEffect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.White,
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 0,
                Opacity = 0
            };
            var scaleTransform = new ScaleTransform();
            var rotateTransform = new RotateTransform();
            var transformGroup = new TransformGroup();
            if (_mediaBrightness < 1.0)
            {
                MediaPreviewImage.OpacityMask = new SolidColorBrush(Color.FromArgb(
                    (byte)(255 * _mediaBrightness), 255, 255, 255));
            }
            else
            {
                MediaPreviewImage.OpacityMask = null;
                if (MediaPreviewImage.Parent is Grid parentGrid)
                {
                    var existingOverlay = parentGrid.Children.OfType<Border>()
                        .FirstOrDefault(b => b.Tag?.ToString() == "BrightnessOverlay");
                    if (existingOverlay != null)
                        parentGrid.Children.Remove(existingOverlay);

                    if (_mediaBrightness > 1.0)
                    {
                        var overlay = new Border
                        {
                            Tag = "BrightnessOverlay",
                            Background = new SolidColorBrush(Color.FromArgb(
                                (byte)(255 * (_mediaBrightness - 1.0) * 0.3), 255, 255, 255)),
                            IsHitTestVisible = false
                        };

                        Grid.SetRow(overlay, Grid.GetRow(MediaPreviewImage));
                        Grid.SetColumn(overlay, Grid.GetColumn(MediaPreviewImage));
                        parentGrid.Children.Add(overlay);
                    }
                }
            }
        }

        public void SyncBrightnessWithThemeManager()
        {
            if (ThemeManager.IsMediaBackground)
            {
                _mediaBrightness = ThemeManager.MediaBrightness;

                _isUpdatingMediaFromCode = true;

                if (BrightnessMediaSlider != null)
                {
                    BrightnessMediaSlider.Value = _mediaBrightness;
                }

                if (BrightnessMediaValue != null)
                {
                    BrightnessMediaValue.Text = (_mediaBrightness * 100).ToString("F0");
                }

                _isUpdatingMediaFromCode = false;

                ApplyMediaBrightness();
            }
        }

        #endregion

    }
}