using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using FreeTrainSimulator.Graphics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Graphics
{
    [TestClass]
    [DoNotParallelize]
    public class TextMetricsTests
    {
        [TestMethod]
        public void FontCacheTracksDisplayScale()
        {
            float original = FontManager.ScalingFactor;
            try
            {
                FontManager.ScalingFactor = 1;
                Font small = FontManager.Scaled("Arial", FontStyle.Regular)[13];
                FontManager.ScalingFactor = 2;
                Font large = FontManager.Scaled("Arial", FontStyle.Regular)[13];
                Assert.AreEqual(13f, small.Size);
                Assert.AreEqual(26f, large.Size);
                Assert.IsGreaterThan(small.Height, large.Height);
                Assert.AreEqual(13f, FontManager.Exact("Arial", FontStyle.Regular)[13].Size);
                FontManager.ScalingFactor = 1;
                Assert.AreSame(small, FontManager.Scaled("Arial", FontStyle.Regular)[13]);
            }
            finally { FontManager.ScalingFactor = original; }
        }

        [TestMethod]
        [DataRow("\n")]
        [DataRow("\r\n")]
        [DataRow("\r")]
        public void MultilineMeasurementAndOutlinesUseTheSameLineSpacing(string newline)
        {
            using Font font = new Font("sans-serif", 20, GraphicsUnit.Pixel);
            using Bitmap bitmap = new Bitmap(128, 128);
            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);
            SizeF single = graphics.MeasureString("Hello", font);
            SizeF multiple = graphics.MeasureString("Hello" + newline + "Hello", font);
            Assert.AreEqual(single.Width, multiple.Width, 0.01f);
            Assert.AreEqual(single.Height * 2, multiple.Height, 0.01f);
            using GraphicsPath first = new GraphicsPath();
            using GraphicsPath both = new GraphicsPath();
            first.AddString("Hello", font.FontFamily, 0, font.Size, Point.Empty, null);
            both.AddString("Hello" + newline + "Hello", font.FontFamily, 0, font.Size, Point.Empty, null);
            Assert.AreEqual(first.GetBounds().Height + font.GetHeight(), both.GetBounds().Height, 0.01f);

            using StringFormat format = new StringFormat();
            format.SetMeasurableCharacterRanges(new[] { new CharacterRange(0, 10 + newline.Length) });
            Region[] ranges = graphics.MeasureCharacterRanges("Hello" + newline + "Hello", font, new RectangleF(0, 0, 128, 128), format);
            using Region region = ranges[0];
            Assert.AreEqual(multiple.Height, region.GetBounds(graphics).Height, 0.01f);
        }
    }
}
