// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// A small subset of the System.Drawing (GDI+) drawing API, implemented on top of Avalonia's DrawingContext.
// System.Drawing.Common is Windows-only on .NET 7+, so the route view (Viewer2D) draws through these
// look-alike types instead. Only what the activity editor uses is implemented.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using SColor = System.Drawing.Color;
using SPoint = System.Drawing.Point;
using SPointF = System.Drawing.PointF;
using SRectangle = System.Drawing.Rectangle;

namespace ActivityEditor.Drawing
{
    public enum FontStyle { Regular = 0, Bold = 1, Italic = 2 }

    public enum MatrixOrder { Prepend = 0, Append = 1 }

    static class ColorConvert
    {
        public static Color ToAvalonia(this SColor color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public static Point ToAvalonia(this SPointF point)
        {
            return new Point(point.X, point.Y);
        }
    }

    public class Pen
    {
        public SColor Color { get; set; }
        public float Width { get; set; } = 1;

        public Pen(SColor color)
        {
            Color = color;
        }

        public Pen(SColor color, float width)
        {
            Color = color;
            Width = width;
        }

        internal IPen ToAvalonia()
        {
            return new ImmutablePen(new ImmutableSolidColorBrush(Color.ToAvalonia()), Math.Max(Width, 1));
        }
    }

    public static class Pens
    {
        public static Pen Red => new Pen(SColor.Red);
        public static Pen Blue => new Pen(SColor.Blue);
        public static Pen DarkBlue => new Pen(SColor.DarkBlue);
        public static Pen Brown => new Pen(SColor.Brown);
        public static Pen Green => new Pen(SColor.Green);
        public static Pen Black => new Pen(SColor.Black);
    }

    public class SolidBrush : IDisposable
    {
        public SColor Color { get; set; }

        public SolidBrush(SColor color)
        {
            Color = color;
        }

        internal IBrush ToAvalonia()
        {
            return new ImmutableSolidColorBrush(Color.ToAvalonia());
        }

        public void Dispose()
        {
        }
    }

    public static class Brushes
    {
        public static SolidBrush Green => new SolidBrush(SColor.Green);
        public static SolidBrush Orange => new SolidBrush(SColor.Orange);
        public static SolidBrush Red => new SolidBrush(SColor.Red);
        public static SolidBrush Black => new SolidBrush(SColor.Black);
    }

    public class Font
    {
        public string FamilyName { get; }
        /// <summary>Size in points, as in GDI+.</summary>
        public float Size { get; }
        public FontStyle Style { get; }
        internal Typeface Typeface { get; }
        internal double PixelSize => Size * 96.0 / 72.0;

        public Font(string familyName, float emSize, FontStyle style = FontStyle.Regular)
        {
            FamilyName = familyName;
            Size = emSize;
            Style = style;
            // Arial / Microsoft Sans Serif are usually not installed on Linux; fall back to the default sans-serif font.
            Typeface = new Typeface(new FontFamily(familyName + ", Liberation Sans, DejaVu Sans, sans-serif"),
                (style & FontStyle.Italic) != 0 ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal,
                (style & FontStyle.Bold) != 0 ? FontWeight.Bold : FontWeight.Normal);
        }
    }

    /// <summary>An image (bitmap or icon) that can be drawn with <see cref="Graphics.DrawIcon"/> or <see cref="Graphics.DrawImage"/>.</summary>
    public class Image : IDisposable
    {
        internal Bitmap Bitmap { get; }

        public Image(Bitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public static Image FromStream(Stream stream)
        {
            return stream == null ? null : new Image(new Bitmap(stream));
        }

        public void Dispose()
        {
            Bitmap?.Dispose();
        }
    }

    /// <summary>Icons are decoded like any other bitmap (Skia can decode .ico files).</summary>
    public class Icon : Image
    {
        public Icon(Stream stream)
            : base(new Bitmap(stream))
        {
        }
    }

    /// <summary>2D affine transform with the GDI+ RotateAt semantics.</summary>
    public class Matrix
    {
        internal Avalonia.Matrix Value = Avalonia.Matrix.Identity;

        public void RotateAt(float angle, SPointF point, MatrixOrder order = MatrixOrder.Prepend)
        {
            var rotation = Avalonia.Matrix.CreateTranslation(-point.X, -point.Y)
                * Avalonia.Matrix.CreateRotation(angle * Math.PI / 180.0)
                * Avalonia.Matrix.CreateTranslation(point.X, point.Y);
            // Avalonia matrices use row vectors: A * B applies A first.
            Value = order == MatrixOrder.Append ? Value * rotation : rotation * Value;
        }

        public void RotateAt(float angle, SPoint point, MatrixOrder order = MatrixOrder.Prepend)
        {
            RotateAt(angle, new SPointF(point.X, point.Y), order);
        }
    }

    /// <summary>
    /// GDI+-like drawing surface over an Avalonia <see cref="DrawingContext"/>.
    /// </summary>
    public sealed class Graphics : IDisposable
    {
        readonly DrawingContext Context;
        readonly Size Size;
        DrawingContext.PushedState? TransformState;
        Matrix CurrentTransform;

        public Graphics(DrawingContext context, Size size)
        {
            Context = context;
            Size = size;
        }

        public Matrix Transform
        {
            get { return CurrentTransform ?? new Matrix(); }
            set
            {
                ResetTransform();
                CurrentTransform = value;
                if (value != null)
                    TransformState = Context.PushTransform(value.Value);
            }
        }

        public void ResetTransform()
        {
            TransformState?.Dispose();
            TransformState = null;
            CurrentTransform = null;
        }

        public void Clear(SColor color)
        {
            Context.FillRectangle(new ImmutableSolidColorBrush(color.ToAvalonia()), new Rect(Size));
        }

        public void DrawLine(Pen pen, SPointF a, SPointF b)
        {
            Context.DrawLine(pen.ToAvalonia(), a.ToAvalonia(), b.ToAvalonia());
        }

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            Context.DrawLine(pen.ToAvalonia(), new Point(x1, y1), new Point(x2, y2));
        }

        public void DrawRectangle(Pen pen, float x, float y, float width, float height)
        {
            Context.DrawRectangle(null, pen.ToAvalonia(), new Rect(x, y, Math.Max(width, 0), Math.Max(height, 0)));
        }

        public void DrawEllipse(Pen pen, float x, float y, float width, float height)
        {
            Context.DrawEllipse(null, pen.ToAvalonia(), new Rect(x, y, Math.Max(width, 0), Math.Max(height, 0)));
        }

        public void FillEllipse(SolidBrush brush, float x, float y, float width, float height)
        {
            Context.DrawEllipse(brush.ToAvalonia(), null, new Rect(x, y, Math.Max(width, 0), Math.Max(height, 0)));
        }

        public void DrawPolygon(Pen pen, SPoint[] points)
        {
            DrawPolygon(null, pen, points);
        }

        public void FillPolygon(SolidBrush brush, SPoint[] points)
        {
            DrawPolygon(brush, null, points);
        }

        void DrawPolygon(SolidBrush brush, Pen pen, SPoint[] points)
        {
            if (points.Length < 2)
                return;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(points[0].X, points[0].Y), brush != null);
                foreach (var p in points.Skip(1))
                    g.LineTo(new Point(p.X, p.Y));
                g.EndFigure(true);
            }
            Context.DrawGeometry(brush?.ToAvalonia(), pen?.ToAvalonia(), geometry);
        }

        /// <summary>
        /// Draw a cardinal spline (tension 0.5, as GDI+ DrawCurve) through the given points.
        /// </summary>
        public void DrawCurve(Pen pen, SPointF[] points)
        {
            if (points.Length < 2)
                return;
            const double tension = 0.5;
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(points[0].ToAvalonia(), false);
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var p0 = points[Math.Max(i - 1, 0)].ToAvalonia();
                    var p1 = points[i].ToAvalonia();
                    var p2 = points[i + 1].ToAvalonia();
                    var p3 = points[Math.Min(i + 2, points.Length - 1)].ToAvalonia();
                    var c1 = p1 + (p2 - p0) * (tension / 3);
                    var c2 = p2 - (p3 - p1) * (tension / 3);
                    g.CubicBezierTo(c1, c2, p2);
                }
                g.EndFigure(false);
            }
            Context.DrawGeometry(null, pen.ToAvalonia(), geometry);
        }

        public void DrawString(string text, Font font, SolidBrush brush, float x, float y)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, font.Typeface, font.PixelSize, brush.ToAvalonia());
            Context.DrawText(formatted, new Point(x, y));
        }

        public void DrawString(string text, Font font, SolidBrush brush, SPointF point)
        {
            DrawString(text, font, brush, point.X, point.Y);
        }

        public void DrawString(string text, Font font, SolidBrush brush, SPoint point)
        {
            DrawString(text, font, brush, point.X, point.Y);
        }

        public void DrawIcon(Image icon, SRectangle rect)
        {
            DrawImage(icon, rect);
        }

        public void DrawImage(Image image, SRectangle rect)
        {
            if (image?.Bitmap == null || rect.Width <= 0 || rect.Height <= 0)
                return;
            Context.DrawImage(image.Bitmap, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        public void Dispose()
        {
            ResetTransform();
        }
    }
}
