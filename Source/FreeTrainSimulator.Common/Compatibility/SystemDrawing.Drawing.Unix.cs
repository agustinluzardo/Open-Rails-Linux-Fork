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

using System.Collections.Generic;
using System.Drawing.Drawing2D;

using SkiaSharp;

namespace System.Drawing
{
    /// <summary>How an area is filled.</summary>
    public abstract class Brush : IDisposable
    {
        internal abstract SKColor SkiaColor { get; }

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>A brush of a single colour, the only kind the engine uses.</summary>
    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color)
        {
            Color = color;
        }

        public Color Color { get; set; }

        internal override SKColor SkiaColor => SkiaConvert.ToSkia(Color);

        public override int GetHashCode() => Color.GetHashCode();

        public override bool Equals(object obj) => obj is SolidBrush other && other.Color == Color;
    }

    /// <summary>Brushes for the standard colours.</summary>
    public static class Brushes
    {
        public static Brush Black { get; } = new SolidBrush(Color.Black);
        public static Brush White { get; } = new SolidBrush(Color.White);
        public static Brush Red { get; } = new SolidBrush(Color.Red);
        public static Brush Green { get; } = new SolidBrush(Color.Green);
        public static Brush Blue { get; } = new SolidBrush(Color.Blue);
        public static Brush Gray { get; } = new SolidBrush(Color.Gray);
        public static Brush Yellow { get; } = new SolidBrush(Color.Yellow);
        public static Brush Transparent { get; } = new SolidBrush(Color.Transparent);
    }

    /// <summary>How a line or an outline is stroked.</summary>
    public sealed class Pen : IDisposable
    {
        public Pen(Color color)
            : this(color, 1f)
        {
        }

        public Pen(Color color, float width)
        {
            Color = color;
            Width = width;
        }

        public Color Color { get; set; }

        public float Width { get; set; }

        public LineJoin LineJoin { get; set; } = LineJoin.Miter;

        internal SKColor SkiaColor => SkiaConvert.ToSkia(Color);

        internal SKStrokeJoin SkiaJoin => LineJoin switch
        {
            LineJoin.Round => SKStrokeJoin.Round,
            LineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter,
        };

        public override int GetHashCode() => HashCode.Combine(Color, Width, LineJoin);

        public override bool Equals(object obj) => obj is Pen other && other.Color == Color && other.Width == Width && other.LineJoin == LineJoin;

        public void Dispose()
        {
        }
    }

    /// <summary>Pens for the standard colours.</summary>
    public static class Pens
    {
        public static Pen Black { get; } = new Pen(Color.Black);
        public static Pen White { get; } = new Pen(Color.White);
        public static Pen Red { get; } = new Pen(Color.Red);
        public static Pen Gray { get; } = new Pen(Color.Gray);
    }

    /// <summary>A span of characters within a string, used to measure part of it.</summary>
    public readonly struct CharacterRange : IEquatable<CharacterRange>
    {
        public CharacterRange(int first, int length)
        {
            First = first;
            Length = length;
        }

        public int First { get; }

        public int Length { get; }

        public bool Equals(CharacterRange other) => other.First == First && other.Length == Length;

        public override bool Equals(object obj) => obj is CharacterRange other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(First, Length);

        public static bool operator ==(CharacterRange left, CharacterRange right) => left.Equals(right);

        public static bool operator !=(CharacterRange left, CharacterRange right) => !left.Equals(right);
    }

    [Flags]
    public enum StringFormatFlags
    {
        DirectionRightToLeft = 0x0001,
        DirectionVertical = 0x0002,
        FitBlackBox = 0x0004,
        DisplayFormatControl = 0x0020,
        NoFontFallback = 0x0400,
        MeasureTrailingSpaces = 0x0800,
        NoWrap = 0x1000,
        LineLimit = 0x2000,
        NoClip = 0x4000,
    }

    public enum StringAlignment
    {
        Near = 0,
        Center = 1,
        Far = 2,
    }

    /// <summary>Text layout options.</summary>
    public sealed class StringFormat : IDisposable
    {
        private CharacterRange[] ranges = Array.Empty<CharacterRange>();

        public StringFormat()
        {
        }

        public StringFormat(StringFormat source)
        {
            if (source != null)
            {
                FormatFlags = source.FormatFlags;
                Alignment = source.Alignment;
                LineAlignment = source.LineAlignment;
                ranges = source.ranges;
            }
        }

        public StringFormat(StringFormatFlags options)
        {
            FormatFlags = options;
        }

        public static StringFormat GenericDefault { get; } = new StringFormat();

        public static StringFormat GenericTypographic { get; } = new StringFormat(StringFormatFlags.NoClip);

        public StringFormatFlags FormatFlags { get; set; }

        public StringAlignment Alignment { get; set; } = StringAlignment.Near;

        public StringAlignment LineAlignment { get; set; } = StringAlignment.Near;

        public void SetMeasurableCharacterRanges(CharacterRange[] characterRanges)
        {
            ranges = characterRanges ?? Array.Empty<CharacterRange>();
        }

        internal IReadOnlyList<CharacterRange> MeasurableCharacterRanges => ranges;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// An area of a drawing surface. The engine only asks a region for its bounds, so this holds
    /// a rectangle rather than a general path.
    /// </summary>
    public sealed class Region : IDisposable
    {
        private readonly RectangleF bounds;

        public Region()
        {
            bounds = RectangleF.Empty;
        }

        public Region(RectangleF rectangle)
        {
            bounds = rectangle;
        }

        public RectangleF GetBounds(Graphics graphics)
        {
            _ = graphics;
            return bounds;
        }

        public void Dispose()
        {
        }
    }

    internal static class SkiaConvert
    {
        internal static SKColor ToSkia(Color color) => new SKColor(color.R, color.G, color.B, color.A);
    }
}

namespace System.Drawing.Drawing2D
{
    public enum LineJoin
    {
        Miter = 0,
        Bevel = 1,
        Round = 2,
        MiterClipped = 3,
    }

    public enum SmoothingMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        AntiAlias = 4,
    }

    public enum CompositingQuality
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        GammaCorrected = 3,
        AssumeLinear = 4,
    }

    public enum InterpolationMode
    {
        Invalid = -1,
        Default = 0,
        Low = 1,
        High = 2,
        Bilinear = 3,
        Bicubic = 4,
        NearestNeighbor = 5,
        HighQualityBilinear = 6,
        HighQualityBicubic = 7,
    }

    public enum PixelOffsetMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        Half = 4,
    }

    public enum FillMode
    {
        Alternate = 0,
        Winding = 1,
    }
}

namespace System.Drawing.Text
{
    public enum TextRenderingHint
    {
        SystemDefault = 0,
        SingleBitPerPixelGridFit = 1,
        SingleBitPerPixel = 2,
        AntiAliasGridFit = 3,
        AntiAlias = 4,
        ClearTypeGridFit = 5,
    }
}
