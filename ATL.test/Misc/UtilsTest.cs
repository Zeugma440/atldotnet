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
        public void Utils_StripZeroChars()
        {
            string s = "abc\0def\0\0";

            Assert.AreEqual(Utils.StripZeroChars(s), "abcdef");
        }

        [TestMethod]
        public void Utils_StripEndingZeroChars()
        {
            string s = "abc\0def\0\0";

            Assert.AreEqual(Utils.StripEndingZeroChars(s), "abc\0def");
        }

        [TestMethod]
        public void Utils_ColorFromCodeColor()
        {
            Assert.AreEqual("#000000", Utils.GetColorCodeFromColor(Color.Black));
            Assert.AreEqual("#FFFFFF", Utils.GetColorCodeFromColor(Color.White));
            Assert.AreEqual("#A52A2A", Utils.GetColorCodeFromColor(Color.Brown));
            Assert.AreEqual("#DC143C", Utils.GetColorCodeFromColor(Color.Crimson));
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
        public void Utils_MD5()
        {
            string test = "this is a test string !";
            Assert.AreEqual("b5a1c8176ec92291ff3595e4104c3759", Utils.GetStrMD5Hash(test) );
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
