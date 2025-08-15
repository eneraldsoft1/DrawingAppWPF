using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DrawingAppWPF
{
    public class SatValBox : FrameworkElement
    {
        private VisualCollection _visuals;
        private Point _markerPosition = new Point(128, 128); // Начальная позиция
        private double _hue = 0; // Устанавливается извне

        public SatValBox()
        {
            _visuals = new VisualCollection(this);
            Render();
        }

        // Событие изменения цвета
        public event EventHandler<ColorEventArgs> ColorChanged;

        // Свойство Hue — передаётся извне (например, от HueWheel)
        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register("Hue", typeof(double), typeof(SatValBox),
                new PropertyMetadata(0.0, OnHueChanged));

        public double Hue
        {
            get => (double)GetValue(HueProperty);
            set => SetValue(HueProperty, value);
        }

        private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (SatValBox)d;
            ctrl.Render();
        }

        // Свойство SelectedColor — для привязки
        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register("SelectedColor", typeof(Color), typeof(SatValBox),
                new PropertyMetadata(Colors.Red, OnColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (SatValBox)d;
            var color = (Color)e.NewValue;
            ColorUtils.RgbToHsv(color, out double h, out double s, out double v);
            ctrl._hue = h;
            ctrl._markerPosition = new Point(s * 255, (1 - v) * 255);
            ctrl.Render();
        }

        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index) => _visuals[index];

        private void Render()
        {
            _visuals.Clear();

            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                var size = new Size(256, 256);
                var gradient = new LinearGradientBrush();

                // Вертикальный градиент: от чистого цвета (Value=1) до чёрного (Value=0)
                gradient.StartPoint = new Point(0, 0);
                gradient.EndPoint = new Point(0, 1);

                var hueColor = ColorUtils.HsvToRgb(_hue, 1.0, 1.0);
                gradient.GradientStops.Add(new GradientStop(Color.FromArgb(255, hueColor.R, hueColor.G, hueColor.B), 0));
                gradient.GradientStops.Add(new GradientStop(Colors.Black, 1));

                var rect = new RectangleGeometry(new Rect(0, 0, size.Width, size.Height));
                context.DrawGeometry(gradient, null, rect);

                // Горизонтальный белый градиент для насыщенности
                var whiteGradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 0)
                };
                whiteGradient.GradientStops.Add(new GradientStop(Colors.White, 0));
                whiteGradient.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

                context.DrawGeometry(whiteGradient, null, rect);
            }

            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawDrawing(drawing);
            }

            _visuals.Add(visual);
            DrawMarker();
        }

        private void DrawMarker()
        {
            var marker = new EllipseGeometry(_markerPosition, 6, 6);
            var pen = new Pen(Brushes.White, 2);
            pen.Freeze();

            var drawing = new GeometryDrawing(null, pen, marker);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawDrawing(drawing);
            }

            _visuals.Add(visual);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                UpdateFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            UpdateFromPoint(e.GetPosition(this));
            CaptureMouse();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
        }

        private void UpdateFromPoint(Point point)
        {
            if (point.X < 0 || point.X > 256 || point.Y < 0 || point.Y > 256) return;

            _markerPosition = point;
            var saturation = point.X / 256.0;
            var value = 1.0 - (point.Y / 256.0);

            var (r, g, b) = ColorUtils.HsvToRgb(_hue, saturation, value);
            var color = Color.FromArgb(255, r, g, b);

            SelectedColor = color;
            ColorChanged?.Invoke(this, new ColorEventArgs(color));

            Render();
        }

        public void UpdateHue(double hue)
        {
            _hue = hue;
            Render();
        }
    }
}