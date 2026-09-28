using System;

namespace Riel.Graphics.MapView
{
    internal interface IMapHostSession : IDisposable
    {
        IMapViewStateAdapter ViewStateAdapter { get; }

        IMapRenderAdapter RenderAdapter { get; }

        IMapInteractionAdapter InteractionAdapter { get; }

        IMapOverlayShapeAdapter OverlayShapeAdapter { get; }
    }
}
