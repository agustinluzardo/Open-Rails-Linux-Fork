using Riel.Common.Input;

using Microsoft.Xna.Framework;

namespace Riel.Graphics.MapView
{
    internal interface IMapHostSessionFactory
    {
        IMapHostSession Create(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent);
    }
}
