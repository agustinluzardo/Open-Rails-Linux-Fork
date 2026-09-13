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

using System;
using System.IO;

namespace Orts.ActivityRunner.Viewer3D
{
    /// <summary>
    /// Windows counterpart of the OpenAL platform glue.
    /// </summary>
    /// <remarks>
    /// Nothing has to be resolved here: soft_oal.dll ships next to the game and Program.cs adds
    /// the architecture specific folder to the library search path before anything loads it.
    /// </remarks>
    internal static class OpenALLibrary
    {
        /// <summary>The OpenAL Soft configuration file in the user's roaming profile.</summary>
        internal static string ConfigurationFile => Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "alsoft.ini");
    }
}
