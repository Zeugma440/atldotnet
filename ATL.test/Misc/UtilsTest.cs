using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;
using System.Drawing;
using System;

namespace ATL.test
{
    [TestClass]
    public class UtilsTest
    {
        [TestMethod]
        public void Utils_StripEndingZeroChars()
        {
            string s = "abc\0def\0\0";

            Assert.AreEqual(Utils.StripEndingZeroChars(s), "abc\0def");
        }

        [TestMethod]
        public void Utils_ResizePic()
        {
            Image image = Image.FromFile(TestUtils.GetResourceLocationRoot() + "pic1.jpg");
            Assert.AreEqual(900, image.Width);
            Assert.AreEqual(600, image.Height);

            Image resizedImage = Utils.ResizeImage(image, new Size(50, 50), false);
            Assert.AreEqual(50, resizedImage.Width);
            Assert.AreEqual(50, resizedImage.Height);

            resizedImage = Utils.ResizeImage(image, new Size(50, 50), true);
            Assert.AreEqual(50, resizedImage.Width);
            Assert.AreEqual(33, resizedImage.Height);
        }

        [TestMethod]
        public void Utils_FormatTime()
        {
            // Display s and ms
            Assert.AreEqual("00:02.2", Utils.FormatTime_ms(2*1000+2));
            // Display m, s and ms
            Assert.AreEqual("01:02.2", Utils.FormatTime_ms(62 * 1000 + 2));
            // Display h, m, s and ms
            Assert.AreEqual("01:01:00.0", Utils.FormatTime_ms(60 * 60 * 1000 + 60 * 1000));
            // Display d, h, m, s and ms
            Assert.AreEqual("2d 01:01:00.0", Utils.FormatTime_ms(48 * 60 * 60 * 1000 + 60 * 60 * 1000 + 60 * 1000));
        }

    }
}
