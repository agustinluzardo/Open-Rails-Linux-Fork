// Native Linux compatibility for the small non-windowing WinForms API surface
// still used by the Open Rails engine. GPL-3.0-or-later.

using System;
using System.Drawing;

using ORTS.Common;

namespace System.Windows.Forms
{
    public enum DialogResult { None = 0, OK = 1, Cancel = 2, Abort = 3, Retry = 4, Ignore = 5, Yes = 6, No = 7 }
    public enum MessageBoxButtons { OK = 0, OKCancel = 1, AbortRetryIgnore = 2, YesNoCancel = 3, YesNo = 4, RetryCancel = 5 }
    public enum MessageBoxIcon { None = 0, Error = 16, Hand = 16, Stop = 16, Question = 32, Exclamation = 48, Warning = 48, Information = 64, Asterisk = 64 }

    public static class MessageBox
    {
        public static DialogResult Show(string text) => Show(text, Application.ProductName, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(string.IsNullOrEmpty(caption) ? Application.ProductName : caption);
            Console.Error.WriteLine(text);
            // There is no modal desktop dependency in the native renderer. Error messages are
            // also written to the Open Rails log, so default to the non-destructive choice.
            return buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Cancel;
        }
    }

    public static class Application
    {
        public static string ProductName => ApplicationInfo.ProductName;
        public static void EnableVisualStyles() { }
        public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }
    }

    [Flags]
    public enum TextFormatFlags
    {
        Default = 0,
        NoPadding = 0x10000000,
        NoPrefix = 0x00000800,
        SingleLine = 0x00000020,
        Top = 0x00000000,
    }

    public static class TextRenderer
    {
        public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags)
        {
            using Bitmap bitmap = new Bitmap(1, 1);
            using Graphics graphics = Graphics.FromImage(bitmap);
            SizeF size = graphics.MeasureString(text ?? string.Empty, font);
            return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
        }

        public static void DrawText(Graphics graphics, string text, Font font, Point point, Color color, TextFormatFlags flags)
        {
            using SolidBrush brush = new SolidBrush(color);
            graphics.DrawString(text ?? string.Empty, font, brush, point);
        }
    }
}
