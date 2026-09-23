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

namespace Riel.Launcher
{
    /// <summary>
    /// A failure worth showing the user as it is - a sentence, not a stack trace.
    /// </summary>
    public sealed class LauncherException : Exception
    {
        public LauncherException(string message) : base(message)
        {
        }

        public LauncherException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public LauncherException()
        {
        }
    }
}
