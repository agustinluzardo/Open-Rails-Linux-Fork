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

using System.Collections.Generic;
using System.Globalization;

namespace FreeTrainSimulator.Common.Native
{
    /// <summary>
    /// PC/AT set 1 scan codes to virtual key codes and display names.
    /// </summary>
    /// <remarks>
    /// Open Rails stores key bindings as scan codes, which describe the physical key and so
    /// survive a keyboard layout change; the names and virtual key codes below are the US layout
    /// answers Windows gives through <c>MapVirtualKey</c> and <c>GetKeyNameText</c>. Keeping the
    /// same answers on Linux matters because the shipped default bindings, the saved user
    /// bindings and the in-game keyboard map are all expressed in those terms.
    ///
    /// Extended keys carry a 0xE0 prefix on the wire. Windows also accepts 0xE1 (only the Pause
    /// key uses it) and resolves it through the same table, which is why the caller's comment
    /// notes that Pause reports itself as Right Ctrl - reproduced here rather than corrected, so
    /// bindings behave identically on both platforms.
    /// </remarks>
    internal static class ScanCodeMap
    {
        // Virtual key codes, matching Microsoft.Xna.Framework.Input.Keys values.
        private const int VkBack = 0x08, VkTab = 0x09, VkReturn = 0x0D, VkPause = 0x13, VkCapital = 0x14;
        private const int VkEscape = 0x1B, VkSpace = 0x20, VkPrior = 0x21, VkNext = 0x22, VkEnd = 0x23, VkHome = 0x24;
        private const int VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28;
        private const int VkSnapshot = 0x2C, VkInsert = 0x2D, VkDelete = 0x2E;
        private const int VkLWin = 0x5B, VkRWin = 0x5C, VkApps = 0x5D;
        private const int VkMultiply = 0x6A, VkAdd = 0x6B, VkSubtract = 0x6D, VkDecimal = 0x6E, VkDivide = 0x6F;
        private const int VkNumLock = 0x90, VkScroll = 0x91;
        private const int VkLShift = 0xA0, VkRShift = 0xA1, VkLControl = 0xA2, VkRControl = 0xA3, VkLMenu = 0xA4, VkRMenu = 0xA5;
        private const int VkOem1 = 0xBA, VkOemPlus = 0xBB, VkOemComma = 0xBC, VkOemMinus = 0xBD, VkOemPeriod = 0xBE;
        private const int VkOem2 = 0xBF, VkOem3 = 0xC0, VkOem4 = 0xDB, VkOem5 = 0xDC, VkOem6 = 0xDD, VkOem7 = 0xDE, VkOem102 = 0xE2;

        /// <summary>Scan code (0x00-0x58) to virtual key, for keys sent without a prefix.</summary>
        private static readonly int[] baseKeys = BuildBaseKeys();

        /// <summary>Scan code to virtual key, for keys sent with the 0xE0 (or 0xE1) prefix.</summary>
        private static readonly Dictionary<int, int> extendedKeys = new Dictionary<int, int>
        {
            [0x1C] = VkReturn,      // numeric keypad Enter
            [0x1D] = VkRControl,
            [0x35] = VkDivide,
            [0x37] = VkSnapshot,
            [0x38] = VkRMenu,
            [0x45] = VkNumLock,
            [0x46] = VkPause,       // Ctrl+Break
            [0x47] = VkHome,
            [0x48] = VkUp,
            [0x49] = VkPrior,
            [0x4B] = VkLeft,
            [0x4D] = VkRight,
            [0x4F] = VkEnd,
            [0x50] = VkDown,
            [0x51] = VkNext,
            [0x52] = VkInsert,
            [0x53] = VkDelete,
            [0x5B] = VkLWin,
            [0x5C] = VkRWin,
            [0x5D] = VkApps,
        };

        private static readonly string[] baseNames = BuildBaseNames();

        private static readonly Dictionary<int, string> extendedNames = new Dictionary<int, string>
        {
            [0x1C] = "Num Enter",
            [0x1D] = "Right Ctrl",
            [0x35] = "Num /",
            [0x37] = "Prnt Scrn",
            [0x38] = "Right Alt",
            [0x45] = "Num Lock",
            [0x46] = "Break",
            [0x47] = "Home",
            [0x48] = "Up",
            [0x49] = "Page Up",
            [0x4B] = "Left",
            [0x4D] = "Right",
            [0x4F] = "End",
            [0x50] = "Down",
            [0x51] = "Page Down",
            [0x52] = "Insert",
            [0x53] = "Delete",
            [0x5B] = "Left Windows",
            [0x5C] = "Right Windows",
            [0x5D] = "Application",
        };

        /// <summary>
        /// Maps a scan code, optionally carrying a 0xE0 or 0xE1 prefix in its high byte, to a
        /// virtual key code. Returns 0 for a key this table does not know.
        /// </summary>
        internal static int ToVirtualKey(int code)
        {
            int scanCode = code & 0xFF;
            if ((code & 0xFF00) != 0)
                return extendedKeys.TryGetValue(scanCode, out int extended) ? extended : 0;
            return scanCode < baseKeys.Length ? baseKeys[scanCode] : 0;
        }

        /// <summary>
        /// Maps a virtual key code back to a scan code, prefixed with 0xE0 when the key is an
        /// extended one. Returns 0 for a key this table does not know.
        /// </summary>
        internal static int ToScanCode(int virtualKey)
        {
            for (int scanCode = 0; scanCode < baseKeys.Length; scanCode++)
            {
                if (baseKeys[scanCode] == virtualKey && virtualKey != 0)
                    return scanCode;
            }
            foreach (KeyValuePair<int, int> entry in extendedKeys)
            {
                if (entry.Value == virtualKey)
                    return 0xE000 | entry.Key;
            }
            return 0;
        }

        /// <summary>
        /// Returns the display name Windows would give a key, or a hexadecimal placeholder for
        /// one this table does not know.
        /// </summary>
        internal static string GetKeyName(int scanCode, bool extended)
        {
            if (extended)
                return extendedNames.TryGetValue(scanCode, out string extendedName) ? extendedName : string.Empty;
            if (scanCode < baseNames.Length && baseNames[scanCode] != null)
                return baseNames[scanCode];
            return string.Empty;
        }

        private static int[] BuildBaseKeys()
        {
            int[] keys = new int[0x59];

            keys[0x01] = VkEscape;
            // 1 2 3 4 5 6 7 8 9 0 on the number row; '0' sits after '9', not before '1'.
            for (int i = 0; i < 9; i++)
                keys[0x02 + i] = '1' + i;
            keys[0x0B] = '0';
            keys[0x0C] = VkOemMinus;
            keys[0x0D] = VkOemPlus;
            keys[0x0E] = VkBack;
            keys[0x0F] = VkTab;

            const string topRow = "QWERTYUIOP";
            for (int i = 0; i < topRow.Length; i++)
                keys[0x10 + i] = topRow[i];
            keys[0x1A] = VkOem4;
            keys[0x1B] = VkOem6;
            keys[0x1C] = VkReturn;
            keys[0x1D] = VkLControl;

            const string homeRow = "ASDFGHJKL";
            for (int i = 0; i < homeRow.Length; i++)
                keys[0x1E + i] = homeRow[i];
            keys[0x27] = VkOem1;
            keys[0x28] = VkOem7;
            keys[0x29] = VkOem3;
            keys[0x2A] = VkLShift;
            keys[0x2B] = VkOem5;

            const string bottomRow = "ZXCVBNM";
            for (int i = 0; i < bottomRow.Length; i++)
                keys[0x2C + i] = bottomRow[i];
            keys[0x33] = VkOemComma;
            keys[0x34] = VkOemPeriod;
            keys[0x35] = VkOem2;
            keys[0x36] = VkRShift;
            keys[0x37] = VkMultiply;
            keys[0x38] = VkLMenu;
            keys[0x39] = VkSpace;
            keys[0x3A] = VkCapital;

            // F1 to F10 are contiguous; F11 and F12 were added later and sit past the keypad.
            for (int i = 0; i < 10; i++)
                keys[0x3B + i] = 0x70 + i;
            keys[0x45] = VkNumLock;
            keys[0x46] = VkScroll;

            keys[0x47] = 0x67;  // Num 7
            keys[0x48] = 0x68;  // Num 8
            keys[0x49] = 0x69;  // Num 9
            keys[0x4A] = VkSubtract;
            keys[0x4B] = 0x64;  // Num 4
            keys[0x4C] = 0x65;  // Num 5
            keys[0x4D] = 0x66;  // Num 6
            keys[0x4E] = VkAdd;
            keys[0x4F] = 0x61;  // Num 1
            keys[0x50] = 0x62;  // Num 2
            keys[0x51] = 0x63;  // Num 3
            keys[0x52] = 0x60;  // Num 0
            keys[0x53] = VkDecimal;

            keys[0x56] = VkOem102;
            keys[0x57] = 0x7A;  // F11
            keys[0x58] = 0x7B;  // F12

            return keys;
        }

        private static string[] BuildBaseNames()
        {
            string[] names = new string[0x59];

            names[0x01] = "Esc";
            for (int i = 0; i < 9; i++)
                names[0x02 + i] = (i + 1).ToString(CultureInfo.InvariantCulture);
            names[0x0B] = "0";
            names[0x0C] = "-";
            names[0x0D] = "=";
            names[0x0E] = "Backspace";
            names[0x0F] = "Tab";

            const string topRow = "QWERTYUIOP";
            for (int i = 0; i < topRow.Length; i++)
                names[0x10 + i] = topRow[i].ToString();
            names[0x1A] = "[";
            names[0x1B] = "]";
            names[0x1C] = "Enter";
            names[0x1D] = "Ctrl";

            const string homeRow = "ASDFGHJKL";
            for (int i = 0; i < homeRow.Length; i++)
                names[0x1E + i] = homeRow[i].ToString();
            names[0x27] = ";";
            names[0x28] = "'";
            names[0x29] = "`";
            names[0x2A] = "Shift";
            names[0x2B] = "\\";

            const string bottomRow = "ZXCVBNM";
            for (int i = 0; i < bottomRow.Length; i++)
                names[0x2C + i] = bottomRow[i].ToString();
            names[0x33] = ",";
            names[0x34] = ".";
            names[0x35] = "/";
            names[0x36] = "Right Shift";
            names[0x37] = "Num *";
            names[0x38] = "Alt";
            names[0x39] = "Space";
            names[0x3A] = "Caps Lock";

            for (int i = 0; i < 10; i++)
                names[0x3B + i] = string.Create(CultureInfo.InvariantCulture, $"F{i + 1}");
            names[0x45] = "Pause";
            names[0x46] = "Scroll Lock";

            names[0x47] = "Num 7";
            names[0x48] = "Num 8";
            names[0x49] = "Num 9";
            names[0x4A] = "Num -";
            names[0x4B] = "Num 4";
            names[0x4C] = "Num 5";
            names[0x4D] = "Num 6";
            names[0x4E] = "Num +";
            names[0x4F] = "Num 1";
            names[0x50] = "Num 2";
            names[0x51] = "Num 3";
            names[0x52] = "Num 0";
            names[0x53] = "Num Del";

            names[0x56] = "\\";
            names[0x57] = "F11";
            names[0x58] = "F12";

            return names;
        }
    }
}
