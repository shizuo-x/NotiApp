using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NotiApp
{
    public partial class MainWindow : Window
    {
        // State Management Variables
        private bool _isPinned = false;
        private bool _isHidden = false;
        private bool _isAnimating = false;
        private Size _lastNoteSize; // The single source of truth for the note's dimensions

        // Dragging Logic Variables
        private bool _isIconMouseDown = false;
        private bool _isDraggingIcon = false;
        private Point _dragStartPoint;

        // UI and Resources
        private Storyboard _jiggleAnimation = null!;
        private readonly List<SolidColorBrush> _noteColors;
        private int _currentColorIndex = 0;
        private const int TitleCharacterLimit = 50;
        private const double AbsoluteMinWidth = 150;

        public MainWindow()
        {
            InitializeComponent();
            _noteColors = new List<SolidColorBrush>
            {
                new(Color.FromRgb(0xFF, 0xF5, 0xE1)), new(Color.FromRgb(0xE1, 0xF5, 0xFF)),
                new(Color.FromRgb(0xE1, 0xFF, 0xE1)), new(Color.FromRgb(0xFF, 0xE1, 0xE1)),
                new(Color.FromRgb(0xF5, 0xE1, 0xFF)), new(Color.FromRgb(0x40, 0x40, 0x40))
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 50;
            this.Top = 50;
            _jiggleAnimation = (Storyboard)this.Resources["JiggleAnimation"];
            _lastNoteSize = new Size(this.Width, this.Height); // Initialize with startup size
            UpdateMinWidth();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Persistently update the size when the note is visible and not animating.
            if (!_isHidden && !_isAnimating)
            {
                _lastNoteSize = e.NewSize;
            }
        }

        // --- Hiding and Showing: The New Core Logic ---

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!_isPinned && !_isHidden)
            {
                HideNote();
            }
        }

        private void HideNote()
        {
            if (_isAnimating || _isHidden) return;
            
            // The window's Left/Top is now the single source of truth for position.
            // Size is updated by Window_SizeChanged, no need to set it here.
            
            _isAnimating = true;

            var fadeOutAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            fadeOutAnim.Completed += (s, a) =>
            {
                MainBorder.Visibility = Visibility.Collapsed;
                this.ResizeMode = ResizeMode.NoResize;

                double iconContainerSize = 54.0;
                var animDuration = TimeSpan.FromSeconds(0.3);
                var ease = new PowerEase { EasingMode = EasingMode.EaseInOut };

                this.BeginAnimation(WidthProperty, new DoubleAnimation(iconContainerSize, animDuration) { EasingFunction = ease });
                var heightAnim = new DoubleAnimation(iconContainerSize, animDuration) { EasingFunction = ease };
                
                heightAnim.Completed += (s2, a2) =>
                {
                    _isHidden = true;
                    _isAnimating = false;
                    IconView.Visibility = Visibility.Visible;
                    _jiggleAnimation.Begin(IconView);
                };
                this.BeginAnimation(HeightProperty, heightAnim);
            };
            MainBorder.BeginAnimation(UIElement.OpacityProperty, fadeOutAnim);
        }

        private void ShowNote()
        {
            if (_isAnimating || !_isHidden) return;
            _isAnimating = true;
            
            IconView.Visibility = Visibility.Collapsed;
            
            // The window is already at the correct Left/Top (the bubble's position).
            // We just need to animate its size back to the last known note size.
            var animDuration = TimeSpan.FromSeconds(0.3);
            var ease = new PowerEase { EasingMode = EasingMode.EaseOut };
            
            this.BeginAnimation(WidthProperty, new DoubleAnimation(_lastNoteSize.Width, animDuration) { EasingFunction = ease });
            var heightAnim = new DoubleAnimation(_lastNoteSize.Height, animDuration) { EasingFunction = ease };

            heightAnim.Completed += (s, a) =>
            {
                // Stop animations to allow manual setting of final size without conflict.
                this.BeginAnimation(WidthProperty, null);
                this.BeginAnimation(HeightProperty, null);

                // Restore the exact last known size. Position is already correct.
                this.Width = _lastNoteSize.Width;
                this.Height = _lastNoteSize.Height;

                MainBorder.Visibility = Visibility.Visible;
                MainBorder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.2)));
                
                this.ResizeMode = ResizeMode.CanResizeWithGrip;

                // Mark as not hidden and not animating AFTER all visual changes are set.
                _isHidden = false;
                _isAnimating = false;
            };
            this.BeginAnimation(HeightProperty, heightAnim);
        }

        // --- UI and Dragging Logic (Unchanged but Confirmed Stable) ---

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            HideNote();
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;
            if (textBox.Text.Length > TitleCharacterLimit)
            {
                textBox.Text = textBox.Text.Substring(0, TitleCharacterLimit);
                textBox.CaretIndex = TitleCharacterLimit;
            }
            UpdateMinWidth();
        }

        private void UpdateMinWidth()
        {
            var formattedText = new FormattedText(TitleTextBox.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(TitleTextBox.FontFamily, TitleTextBox.FontStyle, TitleTextBox.FontWeight, TitleTextBox.FontStretch),
                TitleTextBox.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double requiredWidth = formattedText.Width + 90 + 30;
            this.MinWidth = Math.Max(AbsoluteMinWidth, requiredWidth);
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            _currentColorIndex = (_currentColorIndex + 1) % _noteColors.Count;
            var newColor = _noteColors[_currentColorIndex];
            HeaderBorder.Background = BodyBorder.Background = FooterBorder.Background = IconBackground.Fill = newColor;
            var textColor = (_currentColorIndex == _noteColors.Count - 1) ? Brushes.WhiteSmoke : Brushes.Black;
            
            // FIXED: Changed BodyBorder.Foreground to BodyTextBox.Foreground
            TitleTextBox.Foreground = BodyTextBox.Foreground = textColor;
        }
        
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
        private void IconView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isIconMouseDown = true;
            _dragStartPoint = e.GetPosition(Application.Current.MainWindow);
            IconView.CaptureMouse();
        }
        private void IconView_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isIconMouseDown)
            {
                Point currentPos = e.GetPosition(Application.Current.MainWindow);
                if (Math.Abs(currentPos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDraggingIcon = true;
                    this.Left += currentPos.X - _dragStartPoint.X;
                    this.Top += currentPos.Y - _dragStartPoint.Y;
                }
            }
        }
        private void IconView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            IconView.ReleaseMouseCapture();
            if (!_isDraggingIcon) { ShowNote(); }
            _isIconMouseDown = false;
            _isDraggingIcon = false;
        }
        
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            PinButton.Content = _isPinned ? "📍" : "📌";
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}