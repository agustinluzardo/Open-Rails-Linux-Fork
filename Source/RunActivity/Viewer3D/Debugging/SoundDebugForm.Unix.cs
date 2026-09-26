// Linux bridge for the WinForms sound-debug window.
using System;

namespace Orts.Viewer3D.Debugging
{
    public sealed class SoundDebugForm : IDisposable
    {
        public bool Enabled { get; private set; }
        public SoundDebugForm(Viewer viewer) { }
        public void Show() => Enabled = true;
        public void Hide() => Enabled = false;
        public void Close() => Enabled = false;
        public void Dispose() => Enabled = false;
    }
}
