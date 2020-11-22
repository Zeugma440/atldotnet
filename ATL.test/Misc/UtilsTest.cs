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

            Assert.AreEqual("abc\0def", Utils.StripEndingZeroChars(s));
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
        public void Utils_DecodeTimecodeToMs()
        {
            // Display s and ms
            Assert.AreEqual(2 * 1000 + 2, Utils.DecodeTimecodeToMs("00:02.2"));
            // Display m, s and ms
            Assert.AreEqual(62 * 1000 + 2, Utils.DecodeTimecodeToMs("01:02.2"));
            // Display h, m, s and ms
            Assert.AreEqual(60 * 60 * 1000 + 60 * 1000, Utils.DecodeTimecodeToMs("01:01:00.0"));
            // Display d, h, m, s and ms
            Assert.AreEqual(48 * 60 * 60 * 1000 + 60 * 60 * 1000 + 60 * 1000, Utils.DecodeTimecodeToMs("2d 01:01:00.0"));
        }

        [TestMethod]
        public void Utils_StrictLengthString()
        {
            Assert.AreEqual("0000", Utils.BuildStrictLengthString(null, 4, '0', false));
            Assert.AreEqual("0001", Utils.BuildStrictLengthString("1", 4, '0', false));
            Assert.AreEqual("1000", Utils.BuildStrictLengthString("1", 4, '0'));
            Assert.AreEqual("1234", Utils.BuildStrictLengthString("12345", 4, '0'));
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
        public void Utils_ToBoolean()
        {
            Assert.IsTrue(Utils.ToBoolean("1"));
            Assert.IsTrue(Utils.ToBoolean("1,0"));
            Assert.IsTrue(Utils.ToBoolean("true"));
            Assert.IsTrue(Utils.ToBoolean("True"));

            Assert.IsFalse(Utils.ToBoolean(null));
            Assert.IsFalse(Utils.ToBoolean(""));
        }

        [TestMethod]
        public void Utils_Exceptions()
        {
            try
            {
                Utils.DecodeFrom64(new byte[9]);
                Assert.Fail();
            }
            catch { }
        }

        [TestMethod]
        public void Utils_IsNumeric()
        {
            Assert.IsTrue(Utils.IsNumeric("123"));
            Assert.IsTrue(Utils.IsNumeric("123.456"));
            Assert.IsTrue(Utils.IsNumeric("123,456"));
            Assert.IsTrue(Utils.IsNumeric("123", true));
            Assert.IsTrue(Utils.IsNumeric("-123.456"));
            Assert.IsTrue(Utils.IsNumeric("-123"));

            Assert.IsFalse(Utils.IsNumeric("a"));
            Assert.IsFalse(Utils.IsNumeric("123,456", true));
            Assert.IsFalse(Utils.IsNumeric("123.456", true));
        }

        [TestMethod]
        public void Utils_IsHex()
        {
            Assert.IsTrue(Utils.IsHex("11"));
            Assert.IsTrue(Utils.IsHex("1A"));
            Assert.IsTrue(Utils.IsHex("AA"));

            Assert.IsFalse(Utils.IsHex(null));
            Assert.IsFalse(Utils.IsHex(""));
            Assert.IsFalse(Utils.IsHex("AAA"));
            Assert.IsFalse(Utils.IsHex("GG"));
            Assert.IsFalse(Utils.IsHex("1§"));
        }

        [TestMethod]
        public void Utils_ParseDouble()
        {
            Assert.AreEqual(111, Utils.ParseDouble("111"));
            Assert.AreEqual(1.11, Utils.ParseDouble("1.11"));
            Assert.AreEqual(1.11, Utils.ParseDouble("1,11"));
            Assert.AreEqual(-111, Utils.ParseDouble("-111"));
            Assert.AreEqual(-1.11, Utils.ParseDouble("-1,11"));
            Assert.AreEqual(0.5, Utils.ParseDouble("0.5"));

            Assert.AreEqual(0, Utils.ParseDouble("a"));
            Assert.AreEqual(0, Utils.ParseDouble("1.11.1"));
        }
    }
}
