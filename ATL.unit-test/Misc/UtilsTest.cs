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
            Assert.AreEqual("00:02.2", Utils.EncodeTimecode_ms(2 * 1000 + 2));
            // Display m, s and ms
            Assert.AreEqual("01:02.2", Utils.EncodeTimecode_ms(62 * 1000 + 2));
            // Display h, m, s and ms
            Assert.AreEqual("01:01:00.0", Utils.EncodeTimecode_ms(60 * 60 * 1000 + 60 * 1000));
            // Display d, h, m, s and ms
            Assert.AreEqual("2d 01:01:00.0", Utils.EncodeTimecode_ms(48 * 60 * 60 * 1000 + 60 * 60 * 1000 + 60 * 1000));
            // Display m, s and ms for very long durations in MM:SS.UUUU format
            Assert.AreEqual("2941:01.0", Utils.EncodeTimecode_ms(48 * 60 * 60 * 1000 + 60 * 60 * 1000 + 60 * 1000 + 1000, true));
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
        public void Utils_CheckDateFormat()
        {
            Assert.IsFalse(Utils.CheckDateFormat("sds"));
            Assert.IsFalse(Utils.CheckDateFormat("31-44"));
            Assert.IsFalse(Utils.CheckDateFormat("31-44-55"));
            Assert.IsFalse(Utils.CheckDateFormat("1131-4-55"));
            Assert.IsFalse(Utils.CheckDateFormat("1131-44-5"));
            Assert.IsFalse(Utils.CheckDateFormat("1800-01-01"));
            Assert.IsFalse(Utils.CheckDateFormat("1900-13-01"));
            Assert.IsFalse(Utils.CheckDateFormat("1900-01-32"));
            Assert.IsFalse(Utils.CheckDateFormat("1900-01/99"));

            Assert.IsTrue(Utils.CheckDateFormat("1900-01-01"));
            Assert.IsTrue(Utils.CheckDateFormat("1900-12-01"));
            Assert.IsTrue(Utils.CheckDateFormat("1900-01-31"));
            Assert.IsTrue(Utils.CheckDateFormat("3000-12-31"));
            Assert.IsTrue(Utils.CheckDateFormat("3000/12/31"));
        }

        [TestMethod]
        public void Utils_CheckTimeFormat()
        {
            Assert.IsFalse(Utils.CheckTimeFormat("sds"));
            Assert.IsFalse(Utils.CheckTimeFormat("31:44"));
            Assert.IsFalse(Utils.CheckTimeFormat("3:44:55"));
            Assert.IsFalse(Utils.CheckTimeFormat("33:4:55"));
            Assert.IsFalse(Utils.CheckTimeFormat("33-44-5"));
            Assert.IsFalse(Utils.CheckTimeFormat("24:01:01"));
            Assert.IsFalse(Utils.CheckTimeFormat("01:60:01"));
            Assert.IsFalse(Utils.CheckTimeFormat("01:01:60"));

            Assert.IsTrue(Utils.CheckTimeFormat("00:00:00"));
            Assert.IsTrue(Utils.CheckTimeFormat("23:00:00"));
            Assert.IsTrue(Utils.CheckTimeFormat("00:59:00"));
            Assert.IsTrue(Utils.CheckTimeFormat("00:00:59"));
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
            Assert.IsTrue(testData.SequenceEqual(data));

            testData = new byte[] { 0, 0, 32, 32 };
            data = Utils.BuildStrictLengthStringBytes("  ", 4, 0, Encoding.UTF8, false);
            Assert.IsTrue(testData.SequenceEqual(data));

            testData = new byte[] { 231, 136, 182, 0 };
            data = Utils.BuildStrictLengthStringBytes("父父", 4, 0, Encoding.UTF8);
            Assert.IsTrue(testData.SequenceEqual(data));

            testData = new byte[] { 0, 231, 136, 182 };
            data = Utils.BuildStrictLengthStringBytes("父父", 4, 0, Encoding.UTF8, false);
            Assert.IsTrue(testData.SequenceEqual(data));
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
            catch
            {
                // Nothing
            }
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
            Assert.IsFalse(Utils.IsNumeric("a", true));
            Assert.IsFalse(Utils.IsNumeric("a", true, false));
            Assert.IsFalse(Utils.IsNumeric("123,456", true));
            Assert.IsFalse(Utils.IsNumeric("123.456", true));
            Assert.IsFalse(Utils.IsNumeric("-123", true, false));
            Assert.IsFalse(Utils.IsNumeric("-123.456", false, false));
        }

        [TestMethod]
        public void Utils_IsHex()
        {
            Assert.IsTrue(Utils.IsHex("11"));
            Assert.IsTrue(Utils.IsHex("1A"));
            Assert.IsTrue(Utils.IsHex("AA"));
            Assert.IsTrue(Utils.IsHex("1a"));
            Assert.IsTrue(Utils.IsHex("aa"));

            Assert.IsFalse(Utils.IsHex(null));
            Assert.IsFalse(Utils.IsHex(""));
            Assert.IsFalse(Utils.IsHex("AAA"));
            Assert.IsFalse(Utils.IsHex("GG"));
            Assert.IsFalse(Utils.IsHex("1§"));
        }

        [TestMethod]
        public void Utils_ParseHex()
        {
            Assert.AreEqual(83, Utils.ParseHex("53")[0]);
            Assert.AreEqual(90, Utils.ParseHex("5A")[0]);
            Assert.AreEqual(90, Utils.ParseHex("5a")[0]);
            Assert.AreEqual(190, Utils.ParseHex("BE")[0]);
            Assert.AreEqual(190, Utils.ParseHex("be")[0]);
            Assert.AreEqual(227, Utils.ParseHex("E3")[0]);
            Assert.AreEqual(227, Utils.ParseHex("e3")[0]);

            Assert.AreEqual(227, Utils.ParseHex("e3af")[0]);
            Assert.AreEqual(175, Utils.ParseHex("e3af")[1]);

            Assert.AreEqual(0, Utils.ParseHex(null).Length);
            Assert.AreEqual(0, Utils.ParseHex("").Length);
            Assert.AreEqual(0, Utils.ParseHex("aaa").Length);
            Assert.AreEqual(0, Utils.ParseHex("GG").Length);
            Assert.AreEqual(0, Utils.ParseHex("1§").Length);
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

            Assert.AreEqual(double.NaN, Utils.ParseDouble("a"));
            Assert.AreEqual(double.NaN, Utils.ParseDouble("1.11.1"));
        }
    }
}
