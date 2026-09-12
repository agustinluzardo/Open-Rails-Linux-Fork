// COPYRIGHT 2026 by the Open Rails Linux Fork project.
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

// Part of the GDI+ compatibility layer; see SystemDrawing.Fonts.Unix.cs for the rationale.

using System.Drawing.Drawing2D;
using System.Drawing.Text;

using SkiaSharp;

namespace System.Drawing
{
    /// <summary>
    /// A drawing surface, the GDI+ device context.
    /// </summary>
    /// <remarks>
    /// GDI+ places text by its top left corner while Skia places it on the baseline, so every
    /// text operation here shifts by the font's ascent. Everything the engine draws goes to an
    /// offscreen bitmap, which GDI+ treats as 96 dpi; <see cref="DefaultDpi"/> keeps the
    /// point-to-pixel conversions consistent with that.
    /// </remarks>
    public sealed class Graphics : IDisposable
    {
        /// <summary>Resolution GDI+ assumes for an offscreen bitmap.</summary>
        internal const float DefaultDpi = 96f;

        private readonly SKCanvas canvas;
        private readonly bool ownsCanvas;

        private Graphics(SKCanvas canvas, bool ownsCanvas)
        {
            this.canvas = canvas;
            this.ownsCanvas = ownsCanvas;
        }

        public static Graphics FromImage(Image image)
        {
            if (image is not Bitmap bitmap)
                throw new ArgumentException("A drawing surface can only be created from a bitmap.", nameof(image));
            return new Graphics(new SKCanvas(bitmap.SkiaBitmap), true);
        }

        /// <summary>
        /// Returns a surface with no backing pixels. On Windows this reaches the screen; the
        /// only caller on this platform uses it to ask for the display resolution, which
        /// <see cref="DpiX"/> answers.
        /// </summary>
        public static Graphics FromHwnd(IntPtr handle)
        {
            _ = handle;
            return new Graphics(null, false);
        }

        public float DpiX => DefaultDpi;

        public float DpiY => DefaultDpi;

        public SmoothingMode SmoothingMode { get; set; } = SmoothingMode.Default;

        public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;

        public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;

        public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;

        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;

        /// <summary>Antialiasing follows the quality hints the caller set.</summary>
        private bool AntiAlias => SmoothingMode is SmoothingMode.AntiAlias or SmoothingMode.HighQuality or SmoothingMode.Default;

        private bool AntiAliasText => TextRenderingHint is not (TextRenderingHint.SingleBitPerPixel or TextRenderingHint.SingleBitPerPixelGridFit);

        public IntPtr GetHdc() => IntPtr.Zero;

        public void ReleaseHdc() { }

        public void ReleaseHdc(IntPtr hdc) { }

        public void Clear(Color color)
        {
            canvas?.Clear(SkiaConvert.ToSkia(color));
        }

        public void DrawString(string text, Font font, Brush brush, PointF point)
        {
            DrawString(text, font, brush, point.X, point.Y);
        }

        public void DrawString(string text, Font font, Brush brush, Point point)
        {
            DrawString(text, font, brush, point.X, point.Y);
        }

        public void DrawString(string text, Font font, Brush brush, RectangleF layoutRectangle)
        {
            DrawString(text, font, brush, layoutRectangle.X, layoutRectangle.Y);
        }

        public void DrawString(string text, Font font, Brush brush, float x, float y)
        {
            if (canvas == null || string.IsNullOrEmpty(text) || font == null || brush == null)
                return;

            SKFont skiaFont = font.SkiaFont;
            using SKPaint paint = new SKPaint
            {
                Color = brush.SkiaColor,
                IsAntialias = AntiAliasText,
                Style = SKPaintStyle.Fill,
            };

            // GDI+ takes the top of the line; Skia takes the baseline.
            canvas.DrawText(text, x, y - skiaFont.Metrics.Ascent, skiaFont, paint);
            DrawTextDecorations(text, font, brush, x, y);
        }

        public SizeF MeasureString(string text, Font font)
        {
            if (string.IsNullOrEmpty(text) || font == null)
                return SizeF.Empty;

            SKFont skiaFont = font.SkiaFont;
            // The advance width is the right measure for laying text out; the ink bounds would
            // clip the side bearings and make consecutive strings overlap.
            return new SizeF(skiaFont.MeasureText(text), skiaFont.Spacing);
        }

        public SizeF MeasureString(string text, Font font, int width) => MeasureString(text, font);

        public SizeF MeasureString(string text, Font font, SizeF layoutArea) => MeasureString(text, font);

        public SizeF MeasureString(string text, Font font, PointF origin, StringFormat format) => MeasureString(text, font);

        /// <summary>
        /// Measures the ranges <paramref name="format"/> was given, returning one region each.
        /// </summary>
        public Region[] MeasureCharacterRanges(string text, Font font, RectangleF layoutRect, StringFormat format)
        {
            _ = layoutRect;

            if (font == null)
                return Array.Empty<Region>();

            System.Collections.Generic.IReadOnlyList<CharacterRange> ranges =
                format?.MeasurableCharacterRanges ?? Array.Empty<CharacterRange>();
            if (ranges.Count == 0)
                ranges = new[] { new CharacterRange(0, text?.Length ?? 0) };

            SKFont skiaFont = font.SkiaFont;
            Region[] regions = new Region[ranges.Count];
            for (int i = 0; i < ranges.Count; i++)
            {
                CharacterRange range = ranges[i];
                if (string.IsNullOrEmpty(text) || range.Length <= 0 || range.First >= text.Length)
                {
                    regions[i] = new Region();
                    continue;
                }

                int length = Math.Min(range.Length, text.Length - range.First);
                string part = text.Substring(range.First, length);
                float offset = range.First == 0 ? 0 : skiaFont.MeasureText(text.Substring(0, range.First));
                regions[i] = new Region(new RectangleF(offset, 0, skiaFont.MeasureText(part), skiaFont.Spacing));
            }
            return regions;
        }

        public void FillRectangle(Brush brush, Rectangle rectangle)
        {
            FillRectangle(brush, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        public void FillRectangle(Brush brush, float x, float y, float width, float height)
        {
            if (canvas == null || brush == null)
                return;

            using SKPaint paint = new SKPaint { Color = brush.SkiaColor, IsAntialias = AntiAlias, Style = SKPaintStyle.Fill };
            canvas.DrawRect(x, y, width, height, paint);
        }

        public void DrawRectangle(Pen pen, Rectangle rectangle)
        {
            DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }

        public void DrawRectangle(Pen pen, float x, float y, float width, float height)
        {
            if (canvas == null || pen == null)
                return;

            using SKPaint paint = CreateStrokePaint(pen);
            canvas.DrawRect(x, y, width, height, paint);
        }

        public void DrawLine(Pen pen, Point start, Point end)
        {
            DrawLine(pen, start.X, start.Y, end.X, end.Y);
        }

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            if (canvas == null || pen == null)
                return;

            using SKPaint paint = CreateStrokePaint(pen);
            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        public void DrawPath(Pen pen, GraphicsPath path)
        {
            if (canvas == null || pen == null || path?.SkiaPath == null)
                return;

            using SKPaint paint = CreateStrokePaint(pen);
            canvas.DrawPath(path.SkiaPath, paint);
        }

        public void FillPath(Brush brush, GraphicsPath path)
        {
            if (canvas == null || brush == null || path?.SkiaPath == null)
                return;

            using SKPaint paint = new SKPaint { Color = brush.SkiaColor, IsAntialias = AntiAlias, Style = SKPaintStyle.Fill };
            canvas.DrawPath(path.SkiaPath, paint);
        }

        public void DrawImage(Image image, int x, int y)
        {
            if (canvas == null || image is not Bitmap bitmap)
                return;
            canvas.DrawBitmap(bitmap.SkiaBitmap, x, y);
        }

        public void DrawImage(Image image, Rectangle destination)
        {
            if (canvas == null || image is not Bitmap bitmap)
                return;
            canvas.DrawBitmap(bitmap.SkiaBitmap, new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom));
        }

        private SKPaint CreateStrokePaint(Pen pen)
        {
            return new SKPaint
            {
                Color = pen.SkiaColor,
                IsAntialias = AntiAlias,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = pen.Width,
                StrokeJoin = pen.SkiaJoin,
            };
        }

        /// <summary>
        /// Draws the underline and strikeout a styled font asks for; Skia leaves those to the
        /// caller while GDI+ draws them as part of the text.
        /// </summary>
        private void DrawTextDecorations(string text, Font font, Brush brush, float x, float y)
        {
            if (!font.Underline && !font.Strikeout)
                return;

            SKFont skiaFont = font.SkiaFont;
            SKFontMetrics metrics = skiaFont.Metrics;
            float baseline = y - metrics.Ascent;
            float width = skiaFont.MeasureText(text);
            float thickness = metrics.UnderlineThickness ?? Math.Max(1, skiaFont.Size / 14);

            using SKPaint paint = new SKPaint
            {
                Color = brush.SkiaColor,
                IsAntialias = AntiAliasText,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = thickness,
            };

            if (font.Underline)
            {
                float offset = metrics.UnderlinePosition ?? thickness * 2;
                canvas.DrawLine(x, baseline + offset, x + width, baseline + offset, paint);
            }
            if (font.Strikeout)
            {
                float offset = metrics.StrikeoutPosition ?? metrics.Ascent / 2;
                canvas.DrawLine(x, baseline + offset, x + width, baseline + offset, paint);
            }
        }

        public void Dispose()
        {
            if (ownsCanvas)
                canvas?.Dispose();
        }
    }
}

namespace System.Drawing.Drawing2D
{
    /// <summary>
    /// A geometric path. The engine builds one from a string to draw outlined text.
    /// </summary>
    public sealed class GraphicsPath : IDisposable
    {
        private SKPath path = new SKPath();

        public GraphicsPath()
        {
        }

        public GraphicsPath(FillMode fillMode)
        {
            FillMode = fillMode;
        }

        public FillMode FillMode { get; set; } = FillMode.Alternate;

        internal SKPath SkiaPath => path;

        /// <summary>
        /// Appends the glyph outlines of <paramref name="text"/>.
        /// </summary>
        /// <param name="emSize">The em size in pixels, as GDI+ expects for this overload.</param>
        /// <param name="origin">The top left of the text, not its baseline.</param>
        public void AddString(string text, FontFamily family, int style, float emSize, Point origin, StringFormat format)
        {
            AddString(text, family, style, emSize, new PointF(origin.X, origin.Y), format);
        }

        public void AddString(string text, FontFamily family, int style, float emSize, PointF origin, StringFormat format)
        {
            _ = format;
            if (string.IsNullOrEmpty(text) || family == null)
                return;

            using SKFont font = SkiaFonts.CreateFont(family.Name, emSize, (FontStyle)style);
            using SKPath text_path = font.GetTextPath(text, new SKPoint(origin.X, origin.Y - font.Metrics.Ascent));
            path.AddPath(text_path, SKPathAddMode.Append);
        }

        public RectangleF GetBounds()
        {
            SKRect bounds = path.Bounds;
            return new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }

        public void Reset()
        {
            path.Dispose();
            path = new SKPath();
        }

        public void Dispose()
        {
            path?.Dispose();
            path = null;
        }
    }
}
