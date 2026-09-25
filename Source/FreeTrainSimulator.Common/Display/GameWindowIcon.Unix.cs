// COPYRIGHT 2026 by the Riel project.
// This file is part of Riel, a fork of Open Rails, under the GPL version 3 or later.

using System;
using System.IO;
using System.Runtime.InteropServices;

using SkiaSharp;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>Sets the SDL window icon from the same PNG used by the Riel launcher.</summary>
    public static class GameWindowIcon
    {
        public static void Set(IntPtr window, Stream iconPng)
        {
            if (window == IntPtr.Zero || iconPng == null)
                return;

            using SKBitmap bitmap = SKBitmap.Decode(iconPng);
            if (bitmap == null)
                return;

            int pitch = checked(bitmap.Width * 4);
            byte[] pixels = new byte[checked(pitch * bitmap.Height)];
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor color = bitmap.GetPixel(x, y);
                    int index = y * pitch + x * 4;
                    pixels[index] = color.Blue;
                    pixels[index + 1] = color.Green;
                    pixels[index + 2] = color.Red;
                    pixels[index + 3] = color.Alpha;
                }

            GCHandle pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                IntPtr surface = SdlNative.CreateRgbSurfaceFrom(pinned.AddrOfPinnedObject(),
                    bitmap.Width, bitmap.Height, 32, pitch,
                    0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);
                if (surface == IntPtr.Zero)
                    return;
                try
                {
                    SdlNative.SetWindowIcon(window, surface);
                }
                finally
                {
                    SdlNative.FreeSurface(surface);
                }
            }
            finally
            {
                pinned.Free();
            }
        }
    }
}
