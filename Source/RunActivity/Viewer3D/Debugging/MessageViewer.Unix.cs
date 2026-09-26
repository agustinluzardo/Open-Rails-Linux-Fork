// Native Linux multiplayer-message bridge. Messages stay available through the in-game/web UI.
using System.Diagnostics;

namespace Orts.Viewer3D.Debugging
{
    public sealed class MessageViewer
    {
        public bool Visible { get; set; }
        public bool AddNewMessage(double time, string msg)
        {
            Trace.WriteLine($"Multiplayer: {msg}");
            Visible = true;
            return true;
        }
        public void Show() => Visible = true;
        public void Hide() => Visible = false;
        public void BringToFront() { }
        public void Dispose() { }
    }
}
