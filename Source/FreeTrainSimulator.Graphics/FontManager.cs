using System;
using System.Collections.Concurrent;
using System.Drawing;

namespace FreeTrainSimulator.Graphics
{
    public class FontManager
    {
        private static readonly ConcurrentDictionary<(string Name, FontStyle Style, float Scale), FontManagerInstance> fontManagerCache = new();

        public string FontName { get; }

        public FontFamily FontFamily { get; }

        public FontStyle FontStyle { get; }

        public static float ScalingFactor { get; set; } = 1.0f;

        public static FontManagerInstance Exact(string fontName, FontStyle style) => Get(fontName, style, 1f);

        public static FontManagerInstance Exact(FontFamily fontFamily, FontStyle style) =>
            Get((fontFamily ?? throw new ArgumentNullException(nameof(fontFamily))).Name, style, 1f);

        public static FontManagerInstance Scaled(string fontName, FontStyle style) => Get(fontName, style, ScalingFactor);

        public static FontManagerInstance Scaled(FontFamily fontFamily, FontStyle style) =>
            Get((fontFamily ?? throw new ArgumentNullException(nameof(fontFamily))).Name, style, ScalingFactor);

        private static FontManagerInstance Get(string fontName, FontStyle style, float scale)
        {
            // A second display may have a different DPI; do not reuse fonts from the first.
            return fontManagerCache.GetOrAdd((fontName, style, scale),
                key => new FontManagerInstance(key.Name, key.Style, key.Scale));
        }
    }

    public sealed class FontManagerInstance
    {
        private readonly ConcurrentDictionary<int, Font> fontCache = new();

        public string FontName { get; }

        public FontFamily FontFamily { get; }

        public FontStyle FontStyle { get; }

        public float DpiScale { get; }

        internal FontManagerInstance(string fontName, FontStyle fontStyle, float scale = 1.0f)
        {
            FontName = fontName;
            FontStyle = fontStyle;
            DpiScale = scale;
        }

        internal FontManagerInstance(FontFamily fontFamily, FontStyle fontStyle, float scale = 1.0f)
        {
            FontName = fontFamily.Name;
            FontFamily = fontFamily;
            FontStyle = fontStyle;
            DpiScale = scale;
        }

        public Font this[int size]
        {
            get
            {
                return fontCache.GetOrAdd(size, value => new Font(FontName,
                    Math.Max(1, (int)Math.Round(value * DpiScale)), FontStyle, GraphicsUnit.Pixel));
            }
        }

    }
}
