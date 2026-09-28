// COPYRIGHT 2026 by the Riel project.
//
// This file is part of Riel, a fork of Open Rails.
//
// Riel is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Riel is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Riel.  If not, see <http://www.gnu.org/licenses/>.

// Part of the GDI+ compatibility layer; see SystemDrawing.Fonts.Unix.cs for the rationale.

using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

using SkiaSharp;

namespace System.Drawing
{
    /// <summary>A raster image.</summary>
    public abstract class Image : IDisposable
    {
        private bool disposed;

        public abstract int Width { get; }

        public abstract int Height { get; }

        public Size Size => new Size(Width, Height);

        public static Image FromFile(string filename)
        {
            return Bitmap.Decode(SKBitmap.Decode(filename), filename);
        }

        public static Image FromStream(Stream stream)
        {
            return Bitmap.Decode(SKBitmap.Decode(stream), "<stream>");
        }

        public static Image FromStream(Stream stream, bool useEmbeddedColorManagement)
        {
            _ = useEmbeddedColorManagement;
            return FromStream(stream);
        }

        public abstract void Save(string filename, ImageFormat format);

        public abstract void Save(Stream stream, ImageFormat format);

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
    }

    /// <summary>
    /// A bitmap the engine can draw into and then hand to the graphics device.
    /// </summary>
    /// <remarks>
    /// The pixels are held in Skia's BGRA order with premultiplied alpha, which is what a Skia
    /// canvas can draw into. GDI+ instead hands out straight (non premultiplied) alpha for
    /// <see cref="PixelFormat.Format32bppArgb"/>, and the engine relies on that when it copies
    /// the locked bytes straight into a texture, so <see cref="LockBits"/> converts.
    /// </remarks>
    public sealed class Bitmap : Image
    {
        private readonly SKBitmap bitmap;
        private IntPtr lockedBuffer;
        private BitmapData lockedData;
        private ImageLockMode lockedMode;

        public Bitmap(int width, int height)
            : this(width, height, PixelFormat.Format32bppArgb)
        {
        }

        public Bitmap(int width, int height, PixelFormat format)
        {
            _ = format;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        private Bitmap(SKBitmap source)
        {
            bitmap = source;
        }

        /// <summary>
        /// Creates a scaled copy of <paramref name="source"/>, used to make screenshot thumbnails.
        /// </summary>
        public Bitmap(Image source, Size size)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (source is not Bitmap original)
                throw new ArgumentException("Only a bitmap can be resized.", nameof(source));

            bitmap = new SKBitmap(new SKImageInfo(Math.Max(1, size.Width), Math.Max(1, size.Height), SKColorType.Bgra8888, SKAlphaType.Premul));
            using SKCanvas canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            using SKPaint paint = new SKPaint { IsAntialias = true };
            canvas.DrawBitmap(original.bitmap, new SKRect(0, 0, bitmap.Width, bitmap.Height), paint);
        }

        public override int Width => bitmap.Width;

        public override int Height => bitmap.Height;

        internal SKBitmap SkiaBitmap => bitmap;

        /// <summary>The pixel layout, always 32 bit straight-alpha BGRA in this layer.</summary>
        public PixelFormat PixelFormat => PixelFormat.Format32bppArgb;

        internal static Bitmap Decode(SKBitmap decoded, string source)
        {
            if (decoded == null)
                throw new ArgumentException($"Unable to decode image from {source}.", nameof(source));

            // Normalise whatever the decoder produced to the layout the rest of this layer
            // assumes, so callers see one consistent pixel format.
            if (decoded.ColorType != SKColorType.Bgra8888 || decoded.AlphaType != SKAlphaType.Premul)
            {
                SKBitmap converted = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using (SKCanvas canvas = new SKCanvas(converted))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawBitmap(decoded, 0, 0);
                }
                decoded.Dispose();
                decoded = converted;
            }
            return new Bitmap(decoded);
        }

        public Color GetPixel(int x, int y)
        {
            SKColor color = bitmap.GetPixel(x, y);
            return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
        }

        public void SetPixel(int x, int y, Color color)
        {
            bitmap.SetPixel(x, y, new SKColor(color.R, color.G, color.B, color.A));
        }

        /// <summary>
        /// Exposes the pixels as a straight-alpha BGRA buffer, matching
        /// <see cref="PixelFormat.Format32bppArgb"/> on a little endian machine.
        /// </summary>
        public BitmapData LockBits(Rectangle rectangle, ImageLockMode mode, PixelFormat format)
        {
            _ = format;
            if (lockedData != null)
                throw new InvalidOperationException("The bitmap is already locked.");

            int stride = bitmap.Width * 4;
            int size = stride * bitmap.Height;
            lockedBuffer = Marshal.AllocHGlobal(size);
            lockedMode = mode;

            if (mode != ImageLockMode.WriteOnly)
            {
                using SKPixmap pixels = bitmap.PeekPixels();
                // Reading into an Unpremul destination is what undoes the premultiplication.
                if (pixels == null || !pixels.ReadPixels(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul), lockedBuffer, stride))
                {
                    Marshal.FreeHGlobal(lockedBuffer);
                    lockedBuffer = IntPtr.Zero;
                    throw new InvalidOperationException("Unable to read the bitmap pixels.");
                }
            }

            lockedData = new BitmapData
            {
                Scan0 = lockedBuffer,
                Stride = stride,
                Width = rectangle.Width == 0 ? bitmap.Width : rectangle.Width,
                Height = rectangle.Height == 0 ? bitmap.Height : rectangle.Height,
                PixelFormat = PixelFormat.Format32bppArgb,
            };
            return lockedData;
        }

        public void UnlockBits(BitmapData data)
        {
            if (lockedData == null || data != lockedData)
                return;

            if (lockedMode != ImageLockMode.ReadOnly)
            {
                using SKPixmap source = new SKPixmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul), lockedBuffer, lockedData.Stride);
                using SKPixmap destination = bitmap.PeekPixels();
                _ = destination != null && source.ReadPixels(destination);
            }

            Marshal.FreeHGlobal(lockedBuffer);
            lockedBuffer = IntPtr.Zero;
            lockedData = null;
        }

        public override void Save(string filename, ImageFormat format)
        {
            ArgumentNullException.ThrowIfNull(format);

            using SKData encoded = bitmap.Encode(format.EncodedFormat, 100);
            using FileStream output = File.Create(filename);
            encoded.SaveTo(output);
        }

        public override void Save(Stream stream, ImageFormat format)
        {
            ArgumentNullException.ThrowIfNull(format);
            ArgumentNullException.ThrowIfNull(stream);

            using SKData encoded = bitmap.Encode(format.EncodedFormat, 100);
            encoded.SaveTo(stream);
        }

        public void Save(string filename)
        {
            Save(filename, ImageFormat.Png);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (lockedBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(lockedBuffer);
                    lockedBuffer = IntPtr.Zero;
                    lockedData = null;
                }
                bitmap?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

namespace System.Drawing.Imaging
{
    /// <summary>
    /// Pixel layouts. Only the 32 bit ones are meaningful in this layer; the others exist so
    /// code that names them still compiles.
    /// </summary>
    public enum PixelFormat
    {
        Undefined = 0,
        Format24bppRgb = 137224,
        Format32bppRgb = 139273,
        Format32bppArgb = 2498570,
        Format32bppPArgb = 925707,
    }

    public enum ImageLockMode
    {
        ReadOnly = 1,
        WriteOnly = 2,
        ReadWrite = 3,
        UserInputBuffer = 4,
    }

    /// <summary>A view onto the pixels of a locked bitmap.</summary>
    public sealed class BitmapData
    {
        public IntPtr Scan0 { get; set; }

        public int Stride { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public PixelFormat PixelFormat { get; set; }
    }

    /// <summary>An image file format.</summary>
    public sealed class ImageFormat
    {
        private readonly string name;

        private ImageFormat(string name, SKEncodedImageFormat encodedFormat)
        {
            this.name = name;
            EncodedFormat = encodedFormat;
        }

        internal SKEncodedImageFormat EncodedFormat { get; }

        public static ImageFormat Png { get; } = new ImageFormat(nameof(Png), SKEncodedImageFormat.Png);

        public static ImageFormat Jpeg { get; } = new ImageFormat(nameof(Jpeg), SKEncodedImageFormat.Jpeg);

        public static ImageFormat Bmp { get; } = new ImageFormat(nameof(Bmp), SKEncodedImageFormat.Bmp);

        public static ImageFormat Gif { get; } = new ImageFormat(nameof(Gif), SKEncodedImageFormat.Gif);

        public override string ToString() => name;
    }
}
