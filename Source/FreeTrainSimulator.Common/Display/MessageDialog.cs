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
using System.Diagnostics;

namespace FreeTrainSimulator.Common.Display
{
    /// <summary>What the dialog offers the user.</summary>
    public enum MessageDialogButtons
    {
        Ok,
        OkCancel,
    }

    /// <summary>How the dialog presents itself.</summary>
    public enum MessageDialogIcon
    {
        Information,
        Warning,
        Error,
    }

    /// <summary>Which button the user pressed.</summary>
    public enum MessageDialogResult
    {
        Ok,
        Cancel,
    }

    /// <summary>
    /// A modal message box, for the handful of failures the game cannot continue past.
    /// </summary>
    /// <remarks>
    /// The engine used WinForms' MessageBox, which does not exist outside Windows. The Linux
    /// implementation uses SDL's own dialog: it needs no toolkit, works before the game window
    /// exists and after the graphics device is gone, and falls back to standard error when the
    /// session has no display at all - a headless run under a test harness, for instance.
    /// </remarks>
    public static partial class MessageDialog
    {
        /// <summary>
        /// Shows <paramref name="message"/> and waits for the user. Also written to the log, so
        /// a report contains the failure even when the user dismisses the dialog.
        /// </summary>
        public static MessageDialogResult Show(string title, string message, MessageDialogButtons buttons = MessageDialogButtons.Ok, MessageDialogIcon icon = MessageDialogIcon.Error)
        {
            Trace.WriteLine($"{icon}: {title}{Environment.NewLine}{message}");
            try
            {
                return ShowCore(title, message, buttons, icon);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is InvalidOperationException)
            {
                Console.Error.WriteLine($"{title}{Environment.NewLine}{message}");
                return MessageDialogResult.Ok;
            }
        }
    }
}
