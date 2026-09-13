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
    public static partial class GraphicsCapabilities
    {
        /// <summary>
        /// Direct3D creates the swap chain itself and the adapter has already been asked whether
        /// it supports the sample count, so there is nothing further to probe.
        /// </summary>
        private static partial bool WindowSupportsMultiSample(int samples) => true;
    }
}
