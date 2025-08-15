using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DrawingAppWPF
{
    public class HueWheelControl : FrameworkElement
    {
        private const double OuterRadius = 100;
        private const double InnerRadius = 60;

        private VisualCollection _visuals;
        private double _hue = 0; // 0–360
        private Point? _currentPoint;

        public HueWheelControl()
        {
            _visuals = new VisualCollection(this);
            RenderWheel();
        }

        // Событие изменения цвета
        public event EventHandler<ColorEventArgs> ColorChanged;

        // Свойство Color — двусторонняя привязка
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register(
                "Color",
                typeof(Color),
                typeof(HueWheelControl),
                new PropertyMetadata(Colors.Red, OnColorChanged, OnCoerceColor));

        public Color Color
        {
            get => (Color)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        private static object OnCoerceColor(DependencyObject d, object baseValue)
        {
            if (baseValue is Color color)
                return color;
            return Colors.Red;
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (HueWheelControl)d;
            var color = (Color)e.NewValue;
            ctrl.UpdateFromColor(color);

            // Вызываем событие
            ctrl.ColorChanged?.Invoke(ctrl, new ColorEventArgs(color));
        }

        private void UpdateFromColor(Color color)
        {
            ColorUtils.RgbToHsv(color, out _hue, out _, out _);
            InvalidateVisual();
        }

        protected override int VisualChildrenCount => _visuals.Count;

        protected override Visual GetVisualChild(int index) => _visuals[index];

        private void InvalidateVisual()
        {
            _visuals.Clear();
            RenderWheel();
        }

        private void RenderWheel()
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                for (double angle = 0; angle < 360; angle += 0.5)
                {
                    var brush = new SolidColorBrush(GetColorFromHue(angle));
                    var geometry = CreateArcSegment(angle, angle + 0.5, OuterRadius, InnerRadius);
                    context.DrawGeometry(brush, null, geometry);
                }
            }

            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawDrawing(drawing);
            }

            _visuals.Add(visual);

            // Нарисовать маркер
            DrawMarker();
        }

        private Geometry CreateArcSegment(double startAngle, double endAngle, double outer, double inner)
        {
            var startPoint1 = PolarToCartesian(startAngle, inner);
            var endPoint1 = PolarToCartesian(startAngle, outer);
            var startPoint2 = PolarToCartesian(endAngle, outer);
            var endPoint2 = PolarToCartesian(endAngle, inner);

            var figure = new PathFigure
            {
                StartPoint = new Point(startPoint1.X + 150, startPoint1.Y + 150)
            };

            figure.Segments.Add(new LineSegment(new Point(endPoint1.X + 150, endPoint1.Y + 150), true));

            var arcOuter = new ArcSegment(
                new Point(startPoint2.X + 150, startPoint2.Y + 150),
                new Size(outer, outer),
                0,
                endAngle - startAngle > 180,
                SweepDirection.Clockwise,
                true)
            {
                IsStroked = true
            };
            figure.Segments.Add(arcOuter);

            figure.Segments.Add(new LineSegment(new Point(endPoint2.X + 150, endPoint2.Y + 150), true));

            var arcInner = new ArcSegment(
                new Point(startPoint1.X + 150, startPoint1.Y + 150),
                new Size(inner, inner),
                0,
                false,
                SweepDirection.Counterclockwise,
                true)
            {
                IsStroked = true
            };
            figure.Segments.Add(arcInner);

            figure.IsClosed = true;

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }

        private (double X, double Y) PolarToCartesian(double angle, double radius)
        {
            var rad = angle * Math.PI / 180.0;
            return (radius * Math.Cos(rad), radius * Math.Sin(rad));
        }

        private Color GetColorFromHue(double hue)
        {
            var (r, g, b) = ColorUtils.HsvToRgb(hue, 1.0, 1.0);
            return Color.FromArgb(255, r, g, b);
        }

        private void DrawMarker()
        {
            var (x, y) = PolarToCartesian(_hue, (InnerRadius + OuterRadius) / 2);
            var point = new Point(x + 150, y + 150);

            var ellipse = new EllipseGeometry(point, 6, 6);
            var drawing = new GeometryDrawing(null, new Pen(Brushes.White, 2), ellipse);

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
            var dx = point.X - 150;
            var dy = point.Y - 150;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance >= InnerRadius && distance <= OuterRadius)
            {
                var angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                if (angle < 0) angle += 360;
                _hue = angle;
                OnColorChanged();
                InvalidateVisual();
            }
        }

        private void OnColorChanged()
        {
            var (r, g, b) = ColorUtils.HsvToRgb(_hue, 1.0, 1.0);
            Color = Color.FromArgb(255, r, g, b);
        }
    }

    // Вспомогательный класс для передачи цвета в событии
    public class ColorEventArgs : EventArgs
    {
        public Color Color { get; }

        public ColorEventArgs(Color color)
        {
            Color = color;
        }
    }

    // Вспомогательный класс для HSV ↔ RGB
    public static class ColorUtils
    {
        public static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
        {
            int hi = (int)Math.Floor(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            byte v1 = (byte)(v * 255);
            byte p = (byte)(v * (1 - s) * 255);
            byte q = (byte)(v * (1 - f * s) * 255);
            byte t = (byte)(v * (1 - (1 - f) * s) * 255);

            return hi switch
            {
                0 => (v1, t, p),
                1 => (q, v1, p),
                2 => (p, v1, t),
                3 => (p, q, v1),
                4 => (t, p, v1),
                5 => (v1, p, q),
                _ => (0, 0, 0)
            };
        }

        public static void RgbToHsv(Color color, out double h, out double s, out double v)
        {
            byte r = color.R, g = color.G, b = color.B;
            double r_ = r / 255.0, g_ = g / 255.0, b_ = b / 255.0;
            double max = Math.Max(r_, Math.Max(g_, b_));
            double min = Math.Min(r_, Math.Min(g_, b_));
            v = max;

            if (max == 0) { s = 0; h = 0; return; }
            s = (max - min) / max;

            if (s == 0) { h = 0; return; }

            if (max == r_) h = 60 * ((g_ - b_) / (max - min) % 6);
            else if (max == g_) h = 60 * ((b_ - r_) / (max - min) + 2);
            else h = 60 * ((r_ - g_) / (max - min) + 4);

            if (h < 0) h += 360;
        }
    }
}