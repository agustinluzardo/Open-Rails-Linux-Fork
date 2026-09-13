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
        private const int CancelButtonId = 1;

        private static MessageDialogResult ShowCore(string title, string message, MessageDialogButtons buttons, MessageDialogIcon icon)
        {
            // Return and Escape are wired to the obvious buttons so the dialog can be dismissed
            // from the keyboard, which matters when it appears over a full screen game.
            (uint Flags, int Id, string Text)[] choices = buttons == MessageDialogButtons.OkCancel
                ? new[]
                {
                    (SdlNative.ButtonReturnKeyDefault, 0, "OK"),
                    (SdlNative.ButtonEscapeKeyDefault, CancelButtonId, "Cancel"),
                }
                : new[]
                {
                    (SdlNative.ButtonReturnKeyDefault | SdlNative.ButtonEscapeKeyDefault, 0, "OK"),
                };

            IntPtr[] textPointers = new IntPtr[choices.Length];
            IntPtr buttonArray = IntPtr.Zero;
            try
            {
                int buttonSize = Marshal.SizeOf<SdlNative.MessageBoxButtonData>();
                buttonArray = Marshal.AllocHGlobal(buttonSize * choices.Length);
                for (int i = 0; i < choices.Length; i++)
                {
                    textPointers[i] = Marshal.StringToHGlobalAnsi(choices[i].Text);
                    Marshal.StructureToPtr(
                        new SdlNative.MessageBoxButtonData { Flags = choices[i].Flags, ButtonId = choices[i].Id, Text = textPointers[i] },
                        buttonArray + (i * buttonSize),
                        false);
                }

                SdlNative.MessageBoxData data = new SdlNative.MessageBoxData
                {
                    Flags = icon switch
                    {
                        MessageDialogIcon.Information => SdlNative.MessageBoxInformation,
                        MessageDialogIcon.Warning => SdlNative.MessageBoxWarning,
                        _ => SdlNative.MessageBoxError,
                    },
                    Window = IntPtr.Zero,
                    Title = title,
                    Message = message,
                    ButtonCount = choices.Length,
                    Buttons = buttonArray,
                    ColorScheme = IntPtr.Zero,
                };

                if (SdlNative.ShowMessageBox(ref data, out int pressed) != 0)
                    throw new InvalidOperationException("SDL could not show the message box.");

                return pressed == CancelButtonId ? MessageDialogResult.Cancel : MessageDialogResult.Ok;
            }
            finally
            {
                foreach (IntPtr text in textPointers)
                {
                    if (text != IntPtr.Zero)
                        Marshal.FreeHGlobal(text);
                }
                if (buttonArray != IntPtr.Zero)
                    Marshal.FreeHGlobal(buttonArray);
            }
        }
    }
}
