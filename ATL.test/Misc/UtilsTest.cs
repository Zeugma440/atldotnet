using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;

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

    }
}
