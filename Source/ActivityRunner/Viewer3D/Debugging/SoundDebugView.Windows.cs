// COPYRIGHT 2026 by the Open Rails Linux Fork project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Orts.ActivityRunner.Viewer3D;

namespace Orts.ActivityRunner.Viewer3D.Debugging
{
    /// <summary>
    /// Owns the Windows Forms sound debug window.
    /// </summary>
    /// <remarks>
    /// The window must be created on the render thread or it does not pump its messages.
    /// </remarks>
    internal static class SoundDebugView
    {
        private static SoundDebugForm form;

        internal static bool Available => true;

        internal static void Create(Viewer viewer)
        {
            form = new SoundDebugForm(viewer);
            form.Hide();
        }

        internal static void SetVisible(bool visible)
        {
            if (form == null)
                return;
            if (visible)
                form.Show();
            else
                form.Hide();
        }

        internal static void Close()
        {
            form?.Dispose();
            form = null;
        }
    }
}
