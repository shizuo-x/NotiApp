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
        private bool _isPinned = false;
        private bool _isHidden = false;
        private bool _isAnimating = false;
        
        private Point _lastNotePosition;
        private Size _lastNoteSize;
        private bool _isIconMouseDown = false;
        private bool _isDraggingIcon = false;
        private Point _dragStartPoint;
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
                new SolidColorBrush(Color.FromRgb(0xFF, 0xF5, 0xE1)), new SolidColorBrush(Color.FromRgb(0xE1, 0xF5, 0xFF)),
                new SolidColorBrush(Color.FromRgb(0xE1, 0xFF, 0xE1)), new SolidColorBrush(Color.FromRgb(0xFF, 0xE1, 0xE1)),
                new SolidColorBrush(Color.FromRgb(0xF5, 0xE1, 0xFF)), new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40))
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 50;
            this.Top = 50;
            _jiggleAnimation = (Storyboard)this.Resources["JiggleAnimation"];
            UpdateMinWidth(); // Set initial min width based on default title
        }

        // --- NEW: Dynamic Width and Title Limit Logic ---
        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 1. Enforce character limit
            if (textBox.Text.Length > TitleCharacterLimit)
            {
                textBox.Text = textBox.Text.Substring(0, TitleCharacterLimit);
                textBox.CaretIndex = TitleCharacterLimit; // Move cursor to end
            }
            
            // 2. Update the window's minimum width
            UpdateMinWidth();
        }

        private void UpdateMinWidth()
        {
            // Measure the exact size of the text
            var formattedText = new FormattedText(
                TitleTextBox.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(TitleTextBox.FontFamily, TitleTextBox.FontStyle, TitleTextBox.FontWeight, TitleTextBox.FontStretch),
                TitleTextBox.FontSize,
                Brushes.Black, // Brush doesn't matter for size measurement
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double titleWidth = formattedText.Width;
            double buttonsWidth = 90; // 3 buttons * 30px each
            double horizontalPadding = 30; // Margins and space between title and buttons

            double requiredWidth = titleWidth + buttonsWidth + horizontalPadding;
            
            // Set the MinWidth to be the larger of our absolute minimum or the calculated required width
            this.MinWidth = Math.Max(AbsoluteMinWidth, requiredWidth);
        }

        // --- UI Interaction Logic ---
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            _currentColorIndex = (_currentColorIndex + 1) % _noteColors.Count;
            var newColor = _noteColors[_currentColorIndex];
            HeaderBorder.Background = BodyBorder.Background = FooterBorder.Background = IconBackground.Fill = newColor;
            var textColor = (_currentColorIndex == _noteColors.Count - 1) ? Brushes.WhiteSmoke : Brushes.Black;
            TitleTextBox.Foreground = BodyTextBox.Foreground = textColor;
        }
        
        // --- Dragging Logic ---
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
        
        // --- Hiding and Showing Logic ---
        private void Window_Deactivated(object sender, EventArgs e) { if (!_isPinned && !_isHidden) HideNote(); }

        private void HideNote()
        {
            if (_isAnimating || _isHidden) return;
            _isAnimating = true;

            if (!_isHidden)
            {
                _lastNotePosition = new Point(this.Left, this.Top);
                _lastNoteSize = new Size(this.ActualWidth, this.ActualHeight);
            }
            
            var fadeOutAnim = new DoubleAnimation(0, TimeSpan.FromSeconds(0.2));
            fadeOutAnim.Completed += (s, a) =>
            {
                MainBorder.Visibility = Visibility.Collapsed;
                this.ResizeMode = ResizeMode.NoResize;

                double iconContainerSize = 54.0; 
                var anim = new Duration(TimeSpan.FromSeconds(0.3));
                var ease = new PowerEase { EasingMode = EasingMode.EaseInOut };

                this.BeginAnimation(WidthProperty, new DoubleAnimation(iconContainerSize, anim) { EasingFunction = ease });
                var heightAnim = new DoubleAnimation(iconContainerSize, anim) { EasingFunction = ease };
                
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
            _lastNotePosition = new Point(this.Left, this.Top);
            
            IconView.Visibility = Visibility.Collapsed;
            
            var anim = new Duration(TimeSpan.FromSeconds(0.3));
            var ease = new PowerEase { EasingMode = EasingMode.EaseOut };
            
            this.BeginAnimation(WidthProperty, new DoubleAnimation(_lastNoteSize.Width, anim) { EasingFunction = ease });
            var heightAnim = new DoubleAnimation(_lastNoteSize.Height, anim) { EasingFunction = ease };

            heightAnim.Completed += (s, a) =>
            {
                MainBorder.Visibility = Visibility.Visible;
                MainBorder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.2)));

                _isHidden = false;
                _isAnimating = false;
                this.ResizeMode = ResizeMode.CanResizeWithGrip;
                this.Width = _lastNoteSize.Width;
                this.Height = _lastNoteSize.Height;
                this.Left = _lastNotePosition.X;
                this.Top = _lastNotePosition.Y;
            };
            this.BeginAnimation(HeightProperty, heightAnim);
        }
        
        // --- Other Logic ---
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _isPinned = !_isPinned;
            PinButton.Content = _isPinned ? "📍" : "📌";
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}