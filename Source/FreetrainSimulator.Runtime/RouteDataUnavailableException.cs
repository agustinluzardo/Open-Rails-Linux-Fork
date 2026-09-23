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

using System;
using System.IO;

namespace FreeTrainSimulator.Runtime
{
    /// <summary>
    /// A route was started whose track data the content scan could not read.
    /// </summary>
    /// <remarks>
    /// The scan records the reason - the file, and what was wrong with it - and carries on with
    /// the other routes. Starting the route afterwards used to fail on the missing data with a
    /// null reference, which names neither the route nor the file. This names both halves of what
    /// the user needs: which route, and where to look for why.
    /// </remarks>
    public sealed class RouteDataUnavailableException : IOException
    {
        public RouteDataUnavailableException(string message) : base(message)
        {
        }

        public RouteDataUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public RouteDataUnavailableException()
        {
        }
    }
}
