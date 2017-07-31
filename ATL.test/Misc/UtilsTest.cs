using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;
using System.Drawing;

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
            Assert.AreEqual("#000000",Utils.GetColorCodeFromColor(Color.Black));
            Assert.AreEqual("#FFFFFF", Utils.GetColorCodeFromColor(Color.White));
            Assert.AreEqual("#A52A2A", Utils.GetColorCodeFromColor(Color.Brown));
            Assert.AreEqual("#DC143C", Utils.GetColorCodeFromColor(Color.Crimson));
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
