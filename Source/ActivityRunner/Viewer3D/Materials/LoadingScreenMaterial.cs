using System.IO;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Native;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Materials
{
    internal sealed class LoadingScreenMaterial : LoadingMaterial
    {
        public LoadingScreenMaterial(GameHost game)
            : base(game, LoadingTexturePath(game))
        {
        }

        private static string LoadingTexturePath(Game game)
        {
            string texturePath = (game.GraphicsDevice.Adapter.IsWideScreen && !string.IsNullOrEmpty(Simulator.Instance.RouteModel.Graphics[GraphicType.WideScreen])) ?
                Simulator.Instance.RouteModel.Graphics[GraphicType.WideScreen] : Simulator.Instance.RouteModel.Graphics[GraphicType.Screen];
            if (string.IsNullOrEmpty(texturePath) || !ContentIO.FileExists(texturePath = Path.Combine(Simulator.Instance.RouteFolder.CurrentFolder,texturePath)))
            {
//                if (string.IsNullOrEmpty(texturePath = Simulator.Instance.Route.Thumbnail) || !ContentIO.FileExists(texturePath = Path.Combine(Simulator.Instance.RouteFolder.CurrentFolder, texturePath)))
                    texturePath = Path.Combine(Simulator.Instance.RouteFolder.CurrentFolder, "load.ace");
            }
            return texturePath;
        }
    }
}
