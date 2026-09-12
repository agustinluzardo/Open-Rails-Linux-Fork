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

// A GDI+ compatible drawing surface for the Linux build.
//
// Every piece of text the simulator shows - cab labels, the HUD, the popup windows, the track
// monitor - is rasterized into a bitmap with System.Drawing and uploaded as a texture. That
// works on Windows because System.Drawing.Common wraps GDI+, but .NET 7 removed the Unix
// implementation of that library, so on Linux the type simply is not there.
//
// Rather than rewriting the dozens of call sites, this provides the small slice of the GDI+ API
// the engine actually uses, in the namespaces the engine already imports, backed by Skia. The
// engine code then compiles unchanged on both platforms and upstream changes merge without
// conflicts. Geometry types (Color, Point, Size, Rectangle and their float variants) are not
// redefined here: those live in System.Drawing.Primitives, which is part of the shared framework
// and works everywhere.
//
// These files are only compiled for the Linux build - see Directory.Build.targets.

using System.Collections.Concurrent;
using System.Globalization;

using SkiaSharp;

namespace System.Drawing
{
    /// <summary>Font styles, matching the GDI+ flag values.</summary>
    [Flags]
    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8,
    }

    /// <summary>Unit a font size or a coordinate is expressed in.</summary>
    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6,
    }

    /// <summary>
    /// A font family, resolved through the platform's font configuration.
    /// </summary>
    public sealed class FontFamily : IDisposable
    {
        private static readonly ConcurrentDictionary<string, FontFamily> families = new ConcurrentDictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);

        public FontFamily(string name)
        {
            Name = string.IsNullOrEmpty(name) ? SkiaFonts.DefaultFamilyName : name;
        }

        public string Name { get; }

        /// <summary>The generic sans serif family, whatever the desktop resolves it to.</summary>
        public static FontFamily GenericSansSerif { get; } = new FontFamily(SkiaFonts.DefaultFamilyName);

        public static FontFamily GenericMonospace { get; } = new FontFamily("monospace");

        public static FontFamily GenericSerif { get; } = new FontFamily("serif");

        internal static FontFamily Get(string name)
        {
            return families.GetOrAdd(name ?? SkiaFonts.DefaultFamilyName, static key => new FontFamily(key));
        }

        public override string ToString() => $"[FontFamily: Name={Name}]";

        public override bool Equals(object obj) => obj is FontFamily other && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

        public void Dispose()
        {
            // Typefaces are shared and cached; a family holds no unmanaged state of its own.
        }
    }

    /// <summary>
    /// A font at a concrete size, the unit of text rendering throughout the engine.
    /// </summary>
    /// <remarks>
    /// GDI+ expresses a size in points by default and converts using the device resolution.
    /// Everything the engine renders goes to an offscreen bitmap, for which GDI+ assumes 96 dpi,
    /// so that is the resolution used here as well - see <see cref="Graphics.DefaultDpi"/>.
    /// </remarks>
    public sealed class Font : IDisposable
    {
        private SKFont skiaFont;

        public Font(string familyName, float emSize)
            : this(FontFamily.Get(familyName), emSize, FontStyle.Regular, GraphicsUnit.Point)
        {
        }

        public Font(string familyName, float emSize, FontStyle style)
            : this(FontFamily.Get(familyName), emSize, style, GraphicsUnit.Point)
        {
        }

        public Font(string familyName, float emSize, GraphicsUnit unit)
            : this(FontFamily.Get(familyName), emSize, FontStyle.Regular, unit)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
            : this(FontFamily.Get(familyName), emSize, style, unit)
        {
        }

        public Font(FontFamily family, float emSize)
            : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style)
            : this(family, emSize, style, GraphicsUnit.Point)
        {
        }

        public Font(FontFamily family, float emSize, GraphicsUnit unit)
            : this(family, emSize, FontStyle.Regular, unit)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
        {
            ArgumentNullException.ThrowIfNull(family);
            if (emSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(emSize), emSize, "Font size must be greater than zero.");

            FontFamily = family;
            Size = emSize;
            Style = style;
            Unit = unit;
        }

        public FontFamily FontFamily { get; }

        public string Name => FontFamily.Name;

        /// <summary>The size as given to the constructor, in <see cref="Unit"/>.</summary>
        public float Size { get; }

        public GraphicsUnit Unit { get; }

        public FontStyle Style { get; }

        public bool Bold => (Style & FontStyle.Bold) != 0;

        public bool Italic => (Style & FontStyle.Italic) != 0;

        public bool Underline => (Style & FontStyle.Underline) != 0;

        public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

        /// <summary>The em size in points, whatever unit the font was created with.</summary>
        public float SizeInPoints => Unit switch
        {
            GraphicsUnit.Point => Size,
            GraphicsUnit.Inch => Size * 72,
            GraphicsUnit.Millimeter => Size * 72 / 25.4f,
            GraphicsUnit.Document => Size * 72 / 300,
            _ => Size * 72 / Graphics.DefaultDpi,
        };

        /// <summary>The em size in pixels, which is what Skia works in.</summary>
        internal float SizeInPixels => Unit switch
        {
            GraphicsUnit.Pixel or GraphicsUnit.World or GraphicsUnit.Display => Size,
            _ => SizeInPoints * Graphics.DefaultDpi / 72,
        };

        /// <summary>Line spacing in pixels, rounded up, as <c>Font.Height</c> is in GDI+.</summary>
        public int Height => (int)Math.Ceiling(GetHeight());

        /// <summary>Line spacing in pixels.</summary>
        public float GetHeight() => SkiaFont.Spacing;

        public float GetHeight(float dpi) => SkiaFont.Spacing * dpi / Graphics.DefaultDpi;

        public float GetHeight(Graphics graphics) => GetHeight();

        internal SKFont SkiaFont => skiaFont ??= SkiaFonts.CreateFont(FontFamily.Name, SizeInPixels, Style);

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, Style={Style}]");

        public void Dispose()
        {
            skiaFont?.Dispose();
            skiaFont = null;
        }
    }

    /// <summary>
    /// The fonts the desktop uses for its own interface.
    /// </summary>
    public static class SystemFonts
    {
        /// <summary>
        /// The dialog font. There is no cross-desktop way to ask for it, so this is the
        /// platform's default sans serif family at the customary 9 point size.
        /// </summary>
        public static Font MessageBoxFont { get; } = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular, GraphicsUnit.Point);

        public static Font DefaultFont => MessageBoxFont;

        public static Font DialogFont => MessageBoxFont;

        public static Font MenuFont => MessageBoxFont;

        public static Font StatusFont => MessageBoxFont;
    }

    /// <summary>
    /// Typeface lookup and caching.
    /// </summary>
    /// <remarks>
    /// Resolving a family name goes through fontconfig, which costs milliseconds, and the engine
    /// asks for the same handful of fonts thousands of times while a route loads, so both the
    /// typefaces and the sized fonts are cached.
    /// </remarks>
    internal static class SkiaFonts
    {
        private static readonly ConcurrentDictionary<(string Family, FontStyle Style), SKTypeface> typefaces =
            new ConcurrentDictionary<(string, FontStyle), SKTypeface>();

        /// <summary>
        /// The family used when content asks for a font the system does not have. Skia resolves
        /// "sans-serif" through fontconfig, which every desktop configures.
        /// </summary>
        internal const string DefaultFamilyName = "sans-serif";

        internal static SKFont CreateFont(string familyName, float sizeInPixels, FontStyle style)
        {
            SKFont font = new SKFont(GetTypeface(familyName, style), sizeInPixels)
            {
                // Grid fitting keeps small HUD text crisp, matching what GDI+ does with the
                // SingleBitPerPixelGridFit hint the renderer asks for.
                Subpixel = true,
                Edging = SKFontEdging.SubpixelAntialias,
            };

            // Synthesise bold when the family has no bold face of its own, as GDI+ does.
            if ((style & FontStyle.Bold) != 0 && !font.Typeface.IsBold)
                font.Embolden = true;
            if ((style & FontStyle.Italic) != 0 && !font.Typeface.IsItalic)
                font.SkewX = -0.25f;

            return font;
        }

        internal static SKTypeface GetTypeface(string familyName, FontStyle style)
        {
            return typefaces.GetOrAdd((familyName ?? DefaultFamilyName, style), static key =>
            {
                SKFontStyle skiaStyle = new SKFontStyle(
                    (key.Style & FontStyle.Bold) != 0 ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                    SKFontStyleWidth.Normal,
                    (key.Style & FontStyle.Italic) != 0 ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

                return SKFontManager.Default.MatchFamily(key.Family, skiaStyle)
                    ?? SKFontManager.Default.MatchFamily(DefaultFamilyName, skiaStyle)
                    ?? SKTypeface.Default;
            });
        }
    }
}
