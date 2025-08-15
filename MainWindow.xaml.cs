using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit;

// Убираем неоднозначность
using MessageBox = System.Windows.MessageBox;

namespace DrawingAppWPF
{
    public partial class MainWindow : Window
    {
        private DrawingAttributes drawingAttributes = new DrawingAttributes();
        private Point startPoint;
        private Shape currentShape;
        private bool isDrawingShape = false;

        private ObservableCollection<Color> paletteColors;

        public MainWindow()
        {
            InitializeComponent();
            LoadPalette();
            InitializeDrawing();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SavePalette();
            base.OnClosing(e);
        }

        private void LoadPalette()
        {
            var saved = Properties.Settings.Default.PaletteColors;
            if (saved != null && saved.Count == 10)
            {
                paletteColors = new ObservableCollection<Color>(saved);
            }
            else
            {
                paletteColors = new ObservableCollection<Color>
                {
                    Colors.Red,
                    Colors.Green,
                    Colors.Blue,
                    Colors.Yellow,
                    Colors.Purple,
                    Colors.Cyan,
                    Colors.Magenta,
                    Colors.Orange,
                    Colors.Brown,
                    Colors.Gray
                };
            }
        }

        private void SavePalette()
        {
            Properties.Settings.Default.PaletteColors = paletteColors;
            Properties.Settings.Default.Save();
        }

        private void ResetPalette()
        {
            paletteColors = new ObservableCollection<Color>
            {
                Colors.Red,
                Colors.Green,
                Colors.Blue,
                Colors.Yellow,
                Colors.Purple,
                Colors.Cyan,
                Colors.Magenta,
                Colors.Orange,
                Colors.Brown,
                Colors.Gray
            };
            SavePalette();
        }

        private void InitializeDrawing()
        {
            drawingAttributes.Color = Colors.Black;
            drawingAttributes.Width = 5;
            drawingAttributes.Height = 5;
            drawingAttributes.FitToCurve = true;
            drawingAttributes.IgnorePressure = true;

            drawingCanvas.DefaultDrawingAttributes = drawingAttributes;
            currentColorRect.Fill = new SolidColorBrush(drawingAttributes.Color);

            drawingCanvas.MouseLeftButtonDown += DrawingCanvas_MouseLeftButtonDown;
            drawingCanvas.MouseMove += DrawingCanvas_MouseMove;
            drawingCanvas.MouseLeftButtonUp += DrawingCanvas_MouseLeftButtonUp;

            shapeComboBox.SelectedIndex = 0;
            drawingCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            var popup = new Popup
            {
                Placement = PlacementMode.Bottom,
                PlacementTarget = btnColorPicker,
                StaysOpen = false,
                Width = 600,
                Height = 580
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(256) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(256) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border previewBorder = null;

            var hueWheel = new HueWheelControl
            {
                Width = 300,
                Height = 300,
                Color = drawingAttributes.Color
            };

            var satValBox = new SatValBox
            {
                Width = 256,
                Height = 256,
                SelectedColor = drawingAttributes.Color
            };

            var alphaSlider = new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Value = drawingAttributes.Color.A,
                Width = 200,
                Margin = new Thickness(10, 5, 10, 5),
                TickFrequency = 1,
                IsSnapToTickEnabled = true
            };

            var alphaText = new TextBox
            {
                Text = $"{drawingAttributes.Color.A}",
                Width = 60,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            alphaText.TextChanged += (s, e) =>
            {
                if (int.TryParse(alphaText.Text, out int value))
                {
                    value = Math.Max(0, Math.Min(255, value));
                    alphaSlider.Value = value;
                    alphaText.Text = value.ToString();
                    UpdateAlpha(value);
                }
            };

            var alphaStack = new StackPanel { Orientation = Orientation.Horizontal };
            alphaStack.Children.Add(new TextBlock { Text = "Прозрачность:", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            alphaStack.Children.Add(alphaSlider);
            alphaStack.Children.Add(alphaText);

            previewBorder = new Border
            {
                Background = new SolidColorBrush(drawingAttributes.Color),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Width = 50,
                Height = 30,
                Margin = new Thickness(10)
            };

            var paletteStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 10, 10, 0) };

            for (int i = 0; i < 10; i++)
            {
                var colorRect = new Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(paletteColors[i]),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = i
                };

                colorRect.MouseLeftButtonDown += (s, e) =>
                {
                    var index = (int)((Rectangle)s).Tag;

                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        paletteColors[index] = drawingAttributes.Color;
                        ((Rectangle)s).Fill = new SolidColorBrush(drawingAttributes.Color);
                        SavePalette();
                        MessageBox.Show($"Цвет сохранён в позицию {index + 1}", "Сохранено", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        var selectedColor = paletteColors[index];
                        drawingAttributes.Color = selectedColor;
                        currentColorRect.Fill = new SolidColorBrush(selectedColor);
                        UpdatePreview(previewBorder, selectedColor);
                        satValBox.SelectedColor = selectedColor;
                        hueWheel.Color = selectedColor;
                        alphaSlider.Value = selectedColor.A;
                        alphaText.Text = selectedColor.A.ToString();
                    }
                };

                paletteStack.Children.Add(colorRect);
            }

            var resetButton = new Button
            {
                Content = "Reset Palette",
                Margin = new Thickness(10),
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 120
            };

            resetButton.Click += (s, args) =>
            {
                ResetPalette();
                MessageBox.Show("Палитра сброшена к значениям по умолчанию.", "Сброс", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем отображение
                for (int i = 0; i < paletteStack.Children.Count; i++)
                {
                    if (paletteStack.Children[i] is Rectangle rect)
                    {
                        rect.Fill = new SolidColorBrush(paletteColors[i]);
                    }
                }
            };

            var paletteControls = new StackPanel();
            paletteControls.Children.Add(paletteStack);
            paletteControls.Children.Add(resetButton);

            hueWheel.ColorChanged += (s, args) =>
            {
                var color = args.Color;
                ColorUtils.RgbToHsv(color, out double h, out _, out _);
                satValBox.UpdateHue(h);
                satValBox.SelectedColor = Color.FromArgb((byte)alphaSlider.Value, color.R, color.G, color.B);
                UpdatePreview(previewBorder, satValBox.SelectedColor);
            };

            satValBox.ColorChanged += (s, args) =>
            {
                var color = args.Color;
                var newColor = Color.FromArgb((byte)alphaSlider.Value, color.R, color.G, color.B);
                drawingAttributes.Color = newColor;
                UpdatePreview(previewBorder, newColor);
            };

            alphaSlider.ValueChanged += (s, args) =>
            {
                var color = satValBox.SelectedColor;
                var newColor = Color.FromArgb((byte)alphaSlider.Value, color.R, color.G, color.B);
                drawingAttributes.Color = newColor;
                alphaText.Text = ((byte)alphaSlider.Value).ToString();
                UpdatePreview(previewBorder, newColor);
            };

            void UpdatePreview(Border border, Color color)
            {
                border.Background = new SolidColorBrush(color);
                currentColorRect.Fill = new SolidColorBrush(color);
            }

            void UpdateAlpha(int alpha)
            {
                var color = satValBox.SelectedColor;
                var newColor = Color.FromArgb((byte)alpha, color.R, color.G, color.B);
                drawingAttributes.Color = newColor;
                UpdatePreview(previewBorder, newColor);
            }

            Grid.SetColumn(hueWheel, 0);
            Grid.SetRow(hueWheel, 0);
            Grid.SetRowSpan(hueWheel, 2);

            Grid.SetColumn(satValBox, 1);
            Grid.SetRow(satValBox, 1);

            Grid.SetColumn(alphaStack, 0);
            Grid.SetColumnSpan(alphaStack, 2);
            Grid.SetRow(alphaStack, 2);

            Grid.SetColumn(paletteControls, 0);
            Grid.SetColumnSpan(paletteControls, 2);
            Grid.SetRow(paletteControls, 3);

            var closeButton = new Button
            {
                Content = "Закрыть",
                Margin = new Thickness(10),
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = 100
            };
            closeButton.Click += (s, args) => popup.IsOpen = false;
            Grid.SetColumn(closeButton, 1);
            Grid.SetRow(closeButton, 5);

            grid.Children.Add(hueWheel);
            grid.Children.Add(satValBox);
            grid.Children.Add(alphaStack);
            grid.Children.Add(paletteControls);
            grid.Children.Add(closeButton);

            popup.Child = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Child = grid,
                Padding = new Thickness(5)
            };

            popup.IsOpen = true;
        }

        private void ThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (drawingAttributes != null)
            {
                double value = thicknessSlider.Value;
                drawingAttributes.Width = value;
                drawingAttributes.Height = value;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.Strokes.Clear();
            drawingCanvas.Children.Clear();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Ink Format|*.isf",
                Title = "Save Drawing",
                AddExtension = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    if (dialog.FilterIndex == 3)
                    {
                        using (var fs = new FileStream(dialog.FileName, FileMode.Create))
                        {
                            drawingCanvas.Strokes.Save(fs);
                        }
                    }
                    else
                    {
                        int width = (int)drawingCanvas.ActualWidth;
                        int height = (int)drawingCanvas.ActualHeight;
                        if (width <= 0 || height <= 0)
                        {
                            MessageBox.Show("Canvas is empty or has invalid size.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(drawingCanvas);

                        BitmapEncoder encoder = dialog.FilterIndex == 1 ? new PngBitmapEncoder() : new JpegBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));

                        using (var stream = new FileStream(dialog.FileName, FileMode.Create))
                        {
                            encoder.Save(stream);
                        }

                        MessageBox.Show("Drawing saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ShapeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (drawingCanvas == null) return;

            drawingCanvas.EditingMode = shapeComboBox.SelectedIndex == 0
                ? InkCanvasEditingMode.Ink
                : InkCanvasEditingMode.None;
        }

        #region Рисование фигур
        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (shapeComboBox.SelectedIndex == 0) return;

            startPoint = e.GetPosition(drawingCanvas);
            isDrawingShape = true;
            drawingCanvas.CaptureMouse();

            double thickness = drawingAttributes.Width;
            currentShape = shapeComboBox.SelectedIndex switch
            {
                1 => new Line { Stroke = new SolidColorBrush(drawingAttributes.Color), StrokeThickness = thickness, X1 = startPoint.X, Y1 = startPoint.Y, X2 = startPoint.X, Y2 = startPoint.Y },
                2 => new Rectangle { Stroke = new SolidColorBrush(drawingAttributes.Color), StrokeThickness = thickness, Fill = Brushes.Transparent },
                3 => new Ellipse { Stroke = new SolidColorBrush(drawingAttributes.Color), StrokeThickness = thickness, Fill = Brushes.Transparent },
                _ => null
            };

            if (currentShape != null)
            {
                Canvas.SetLeft(currentShape, startPoint.X);
                Canvas.SetTop(currentShape, startPoint.Y);
                drawingCanvas.Children.Add(currentShape);
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isDrawingShape || currentShape == null) return;

            var point = e.GetPosition(drawingCanvas);
            switch (shapeComboBox.SelectedIndex)
            {
                case 1:
                    ((Line)currentShape).X2 = point.X;
                    ((Line)currentShape).Y2 = point.Y;
                    break;
                case 2:
                case 3:
                    double x = Math.Min(startPoint.X, point.X);
                    double y = Math.Min(startPoint.Y, point.Y);
                    Canvas.SetLeft(currentShape, x);
                    Canvas.SetTop(currentShape, y);
                    currentShape.Width = Math.Abs(point.X - startPoint.X);
                    currentShape.Height = Math.Abs(point.Y - startPoint.Y);
                    break;
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDrawingShape = false;
            drawingCanvas.ReleaseMouseCapture();
            currentShape = null;
        }
        #endregion
    }
}