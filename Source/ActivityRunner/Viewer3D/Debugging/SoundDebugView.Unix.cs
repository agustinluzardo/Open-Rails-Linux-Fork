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

using System.Diagnostics;

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    /// <summary>
    /// The sound debug window is a Windows Forms tool and has no counterpart here yet.
    /// </summary>
    /// <remarks>
    /// It lists the active sound sources and their state in a separate top level window. Nothing
    /// else depends on it, so the Linux build keeps the key binding working and says why nothing
    /// appeared rather than pretending the window opened. The in-game debug overlays, which are
    /// drawn by the engine's own window system, are available on both platforms.
    /// </remarks>
    internal static class SoundDebugView
    {
        internal static bool Available => false;

        internal static void Create(Viewer viewer)
        {
            _ = viewer;
        }

        internal static void SetVisible(bool visible)
        {
            if (visible)
                Trace.TraceInformation("The sound debug window is only available in the Windows build.");
        }

        internal static void Close()
        {
        }
    }
}
