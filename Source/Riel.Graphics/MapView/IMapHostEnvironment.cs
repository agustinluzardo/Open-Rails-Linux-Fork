using System;

using Riel.Common.Input;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    public interface IMapHostEnvironment
    {
        Point ClientSize { get; }

        Point PointerPosition { get; }

        event EventHandler ClientSizeChanged;

        void RegisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);

        void UnregisterMouseMove(MouseInputGameComponent.MouseMoveEvent handler);
    }
}
