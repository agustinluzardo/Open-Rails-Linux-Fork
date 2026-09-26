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
        /// Where lines are broken. Narrow enough for the smallest screen the game supports at the
        /// default font, wide enough that a path or a URL usually fits on one line.
        /// </summary>
        internal const int WrapColumn = 90;

        /// <summary>
        /// Breaks each paragraph of <paramref name="text"/> at spaces so no line is longer than
        /// <paramref name="width"/>. Existing line breaks are kept; a single word longer than the
        /// width - a long path - is left whole rather than cut somewhere unreadable.
        /// </summary>
        internal static string Wrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            System.Text.StringBuilder result = new System.Text.StringBuilder(text.Length + 16);
            string[] paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    result.Append('\n');

                int lineLength = 0;
                foreach (string word in paragraphs[p].Split(' '))
                {
                    if (lineLength > 0 && lineLength + 1 + word.Length > width)
                    {
                        result.Append('\n');
                        lineLength = 0;
                    }
                    else if (lineLength > 0)
                    {
                        result.Append(' ');
                        lineLength++;
                    }
                    result.Append(word);
                    lineLength += word.Length;
                }
            }
            return result.ToString();
        }

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
