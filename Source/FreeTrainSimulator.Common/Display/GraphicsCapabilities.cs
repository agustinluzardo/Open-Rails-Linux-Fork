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

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>
    /// What the window system will actually grant, as opposed to what the adapter reports.
    /// </summary>
    public static partial class GraphicsCapabilities
    {
        /// <summary>
        /// Returns the largest antialiasing sample count no greater than <paramref name="requested"/>
        /// that a window can actually be created with, or 0 for none.
        /// </summary>
        /// <remarks>
        /// The graphics adapter answering that it supports a sample count is not the same question.
        /// On OpenGL the back buffer is a window, and the window needs a visual with that many
        /// samples; where one does not exist - a remote or virtual display, an unusual driver -
        /// creating the window fails and the game dies before it draws anything, with an error
        /// that names neither antialiasing nor the window. Asking first costs one hidden window.
        /// </remarks>
        public static int SupportedMultiSampleCount(int requested)
        {
            if (requested < 2)
                return 0;

            for (int samples = requested; samples > 1; samples /= 2)
            {
                if (WindowSupportsMultiSample(samples))
                    return samples;
            }
            return 0;
        }

        /// <summary>
        /// Whether a window with <paramref name="samples"/> samples can be created. Platforms
        /// where the question does not arise answer yes.
        /// </summary>
        private static partial bool WindowSupportsMultiSample(int samples);
    }
}
