// Linux bridge for the legacy WinForms dispatcher window.
// The native in-game dispatcher is wired separately; this keeps Open Rails core references neutral.
using System;
using Orts.Simulation.Physics;

namespace Orts.Viewer3D.Debugging
{
    public sealed class MapViewer : IDisposable
    {
        public dynamic switchPickedItem;
        public dynamic signalPickedItem;
        public bool ClickedTrain;
        public Train PickedTrain;
        public bool Enabled { get; private set; }
        public bool Visible => Enabled;

        public MapViewer(Orts.Simulation.Simulator simulator, Viewer viewer)
        {
            PickedTrain = simulator?.PlayerLocomotive?.Train;
        }

        public void Show() => Enabled = true;
        public void Hide() => Enabled = false;
        public void Close() => Enabled = false;
        public void Dispose() => Enabled = false;
    }
}
