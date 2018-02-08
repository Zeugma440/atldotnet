using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;
using System.Text;

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
        public void Utils_FormatTime()
        {
            // Display s and ms
            Assert.AreEqual("00:02.2", Utils.EncodeTimecode_ms(2*1000+2));
            // Display m, s and ms
            Assert.AreEqual("01:02.2", Utils.EncodeTimecode_ms(62 * 1000 + 2));
            // Display h, m, s and ms
            Assert.AreEqual("01:01:00.0", Utils.EncodeTimecode_ms(60 * 60 * 1000 + 60 * 1000));
            // Display d, h, m, s and ms
            Assert.AreEqual("2d 01:01:00.0", Utils.EncodeTimecode_ms(48 * 60 * 60 * 1000 + 60 * 60 * 1000 + 60 * 1000));
        }

        [TestMethod]
        public void Utils_StrictLengthStringBytes()
        {
            byte[] data;
            byte[] testData;

            testData = new byte[] { 32, 32, 0, 0 };
            data = Utils.BuildStrictLengthStringBytes("  ", 4, 0, Encoding.UTF8);
            Assert.IsTrue(StreamUtils.ArrEqualsArr(testData, data));

            testData = new byte[] { 0, 0, 32, 32 };
            data = Utils.BuildStrictLengthStringBytes("  ", 4, 0, Encoding.UTF8, false);
            Assert.IsTrue(StreamUtils.ArrEqualsArr(testData, data));

            testData = new byte[] { 231, 136, 182, 0 };
            data = Utils.BuildStrictLengthStringBytes("父父", 4, 0, Encoding.UTF8);
            Assert.IsTrue(StreamUtils.ArrEqualsArr(testData, data));

            testData = new byte[] { 0, 231, 136, 182 };
            data = Utils.BuildStrictLengthStringBytes("父父", 4, 0, Encoding.UTF8, false);
            Assert.IsTrue(StreamUtils.ArrEqualsArr(testData, data));
        }

        [TestMethod]
        public void Utils_IsNumeric()
        {
            Assert.IsTrue(Utils.IsNumeric("123"));
            Assert.IsTrue(Utils.IsNumeric("123.456"));
            Assert.IsTrue(Utils.IsNumeric("123,456"));

            Assert.IsTrue(Utils.IsNumeric("123", true));
            Assert.IsFalse(Utils.IsNumeric("123,456", true));
            Assert.IsFalse(Utils.IsNumeric("123.456", true));
        }

    }
}
