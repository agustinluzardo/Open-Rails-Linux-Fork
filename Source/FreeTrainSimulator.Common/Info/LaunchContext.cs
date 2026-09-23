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

namespace FreeTrainSimulator.Common.Info
{
    /// <summary>
    /// What started the simulator, where that changes how it should behave.
    /// </summary>
    public static class LaunchContext
    {
        /// <summary>
        /// Set by a launcher that watches the simulator and shows its failures itself.
        /// </summary>
        public const string LauncherReportsErrorsVariable = "RIEL_LAUNCHER_REPORTS_ERRORS";

        /// <summary>
        /// Whether the launcher that started this run will explain a failure itself, in which
        /// case the simulator's own error dialog would only be a second box to click through
        /// before the better one appears. The failure is still logged either way; the log is
        /// what the launcher reads.
        /// </summary>
        public static bool LauncherReportsErrors { get; } =
            string.Equals(Environment.GetEnvironmentVariable(LauncherReportsErrorsVariable), "1", StringComparison.Ordinal);
    }
}
