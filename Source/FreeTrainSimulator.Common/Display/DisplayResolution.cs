using System;
using System.Drawing;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>Chooses physical pixels for the selected monitor and screen mode.</summary>
    public static class DisplayResolution
    {
        public static (int Width, int Height) SizeFor(ScreenMode mode, Rectangle bounds, Rectangle workingArea)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return (1024, 768);
            if (mode != ScreenMode.Windowed)
                return (bounds.Width, bounds.Height);

            // Leave room for desktop panels and the window frame in ordinary windowed mode.
            Rectangle available = workingArea.Width > 0 && workingArea.Height > 0 ? workingArea : bounds;
            return (Math.Min(available.Width, Math.Max(640, (int)(available.Width * 0.9))),
                Math.Min(available.Height, Math.Max(360, (int)(available.Height * 0.9))));
        }
    }
}
