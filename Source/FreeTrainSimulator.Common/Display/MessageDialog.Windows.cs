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
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Common.Display
{
    public static partial class MessageDialog
    {
        private static MessageDialogResult ShowCore(string title, string message, MessageDialogButtons buttons, MessageDialogIcon icon)
        {
            uint style = buttons == MessageDialogButtons.OkCancel ? MbOkCancel : MbOk;
            style |= icon switch
            {
                MessageDialogIcon.Information => MbIconInformation,
                MessageDialogIcon.Warning => MbIconWarning,
                _ => MbIconError,
            };

            // Topmost keeps the dialog in front of a full screen game window.
            return MessageBox(IntPtr.Zero, message, title, style | MbTopmost) == IdCancel
                ? MessageDialogResult.Cancel
                : MessageDialogResult.Ok;
        }

        private const uint MbOk = 0x00000000;
        private const uint MbOkCancel = 0x00000001;
        private const uint MbIconError = 0x00000010;
        private const uint MbIconWarning = 0x00000030;
        private const uint MbIconInformation = 0x00000040;
        private const uint MbTopmost = 0x00040000;
        private const int IdCancel = 2;

        [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
    }
}
