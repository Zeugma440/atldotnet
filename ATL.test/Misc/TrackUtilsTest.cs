using ATL.AudioData;
using ATL.AudioData.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATL.test
{
    [TestClass]
    public class TrackUtilsTest
    {
        [TestMethod]
        public void TrackUtils_ExtractTrackNumber()
        {
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber("15"));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber(" 15"));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber(" 15 "));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber("15 "));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber("15.1"));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber("15,1"));
            Assert.AreEqual(15, TrackUtils.ExtractTrackNumber("a15a"));

            Assert.AreEqual(0, TrackUtils.ExtractTrackNumber(""));
            Assert.AreEqual(0, TrackUtils.ExtractTrackNumber(null));
            Assert.AreEqual(0, TrackUtils.ExtractTrackNumber("aaa"));
            Assert.AreEqual(0, TrackUtils.ExtractTrackNumber("99999999"));
        }

        [TestMethod]
        public void TrackUtils_DecodePopularity()
        {
            // Classic behaviour (more cases in metadata-specific test classes such as ID3v2, APE...)
            Assert.AreEqual(0.1, TrackUtils.DecodePopularity(((char)15).ToString(), MetaDataIO.RC_ID3v2));
            Assert.AreEqual(0.4, TrackUtils.DecodePopularity(((char)117).ToString(), MetaDataIO.RC_ID3v2));

            // Star ratings (very rare)
            // Assert.AreEqual((float)1.0 /5,  TrackUtils.DecodePopularity("*", MetaDataIO.RC_ID3v2));  <-- case not handled (see comments in code)
            Assert.AreEqual(2.0 /5,  TrackUtils.DecodePopularity("**", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(3.0 /5,  TrackUtils.DecodePopularity("***", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(4.0 /5,  TrackUtils.DecodePopularity("****", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(1,              TrackUtils.DecodePopularity("*****", MetaDataIO.RC_ID3v2));

            // Fringe cases not covered by test data in metadata-specific test classes
            Assert.AreEqual(0,              TrackUtils.DecodePopularity("0", MetaDataIO.RC_ASF));
            Assert.AreEqual(0,              TrackUtils.DecodePopularity("7", MetaDataIO.RC_APE));
            Assert.AreEqual(6.0 /10, TrackUtils.DecodePopularity("6", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(1,              TrackUtils.DecodePopularity("10", MetaDataIO.RC_ID3v2));

            // Error cases
            Assert.AreEqual(0, TrackUtils.DecodePopularity("", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(0, TrackUtils.DecodePopularity(null, MetaDataIO.RC_ID3v2));
            Assert.AreEqual(0, TrackUtils.DecodePopularity("aaa", MetaDataIO.RC_ID3v2));
        }

        [TestMethod]
        public void TrackUtils_EncodePopularity()
        {
            // Cases not covered by metadata-specific test classes
            Assert.AreEqual(0, TrackUtils.EncodePopularity("0.5", MetaDataIO.RC_ASF));
            Assert.AreEqual(1, TrackUtils.EncodePopularity("1.5", MetaDataIO.RC_ASF));
            Assert.AreEqual(1, TrackUtils.EncodePopularity("1,5", MetaDataIO.RC_ASF));
            Assert.AreEqual(25, TrackUtils.EncodePopularity("2.5", MetaDataIO.RC_ASF));
            Assert.AreEqual(50, TrackUtils.EncodePopularity("3.5", MetaDataIO.RC_ASF));
            Assert.AreEqual(75, TrackUtils.EncodePopularity("4.5", MetaDataIO.RC_ASF));
            Assert.AreEqual(99, TrackUtils.EncodePopularity("5", MetaDataIO.RC_ASF));

            Assert.AreEqual(0, TrackUtils.EncodePopularity("0.25", MetaDataIO.RC_APE));
            Assert.AreEqual(10, TrackUtils.EncodePopularity("0.5", MetaDataIO.RC_APE));
            Assert.AreEqual(20, TrackUtils.EncodePopularity("1", MetaDataIO.RC_APE));
            Assert.AreEqual(30, TrackUtils.EncodePopularity("1.5", MetaDataIO.RC_APE));
            Assert.AreEqual(40, TrackUtils.EncodePopularity("2", MetaDataIO.RC_APE));
            Assert.AreEqual(50, TrackUtils.EncodePopularity("2.5", MetaDataIO.RC_APE));
            Assert.AreEqual(60, TrackUtils.EncodePopularity("3", MetaDataIO.RC_APE));
            Assert.AreEqual(70, TrackUtils.EncodePopularity("3.5", MetaDataIO.RC_APE));
            Assert.AreEqual(80, TrackUtils.EncodePopularity("4", MetaDataIO.RC_APE));
            Assert.AreEqual(90, TrackUtils.EncodePopularity("4.5", MetaDataIO.RC_APE));
            Assert.AreEqual(100, TrackUtils.EncodePopularity("5", MetaDataIO.RC_APE));

            Assert.AreEqual(0, TrackUtils.EncodePopularity("0.25", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(13, TrackUtils.EncodePopularity("0.5", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(1, TrackUtils.EncodePopularity("1", MetaDataIO.RC_ID3v2)); // <-- yes, 1
            Assert.AreEqual(54, TrackUtils.EncodePopularity("1.5", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(64, TrackUtils.EncodePopularity("2", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(118, TrackUtils.EncodePopularity("2.5", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(128, TrackUtils.EncodePopularity("3", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(186, TrackUtils.EncodePopularity("3.5", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(196, TrackUtils.EncodePopularity("4", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(242, TrackUtils.EncodePopularity("4.5", MetaDataIO.RC_ID3v2));
            Assert.AreEqual(255, TrackUtils.EncodePopularity("5", MetaDataIO.RC_ID3v2));
        }

        [TestMethod]
        public void TrackUtils_ExtractStrYear()
        {
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("1952"));
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("1952.1"));
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("1952,1"));
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("1952aaa"));
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("aaa1952"));
            Assert.AreEqual("1952", TrackUtils.ExtractStrYear("aa1952aa"));

            Assert.AreEqual("", TrackUtils.ExtractStrYear(""));
            Assert.AreEqual("", TrackUtils.ExtractStrYear(null));
            Assert.AreEqual("", TrackUtils.ExtractStrYear("aaaa"));
            Assert.AreEqual("", TrackUtils.ExtractStrYear("999"));
        }

        [TestMethod]
        public void TrackUtils_ApplyLeadingZeroes()
        {
            // Use existing track format
            Assert.AreEqual("1", TrackUtils.ApplyLeadingZeroes("1", "", 1, false, false));
            Assert.AreEqual("1", TrackUtils.ApplyLeadingZeroes("1", "10", 1, false, false));
            Assert.AreEqual("1", TrackUtils.ApplyLeadingZeroes("1", "10", 1, true, false));
            Assert.AreEqual("01", TrackUtils.ApplyLeadingZeroes("1", "1", 2, true, false));
            Assert.AreEqual("01", TrackUtils.ApplyLeadingZeroes("1", "1", 2, false, false));
            Assert.AreEqual("01/01", TrackUtils.ApplyLeadingZeroes("1/1", "1", 2, false, false));
            Assert.AreEqual("01/01", TrackUtils.ApplyLeadingZeroes("01/1", "1", 2, false, false));

            // Override existing track format
            Assert.AreEqual("1", TrackUtils.ApplyLeadingZeroes("1", "10", 2, false, true));
            Assert.AreEqual("1", TrackUtils.ApplyLeadingZeroes("1", "1", 2, false, true));
            Assert.AreEqual("01", TrackUtils.ApplyLeadingZeroes("1", "1", 2, true, true));
            Assert.AreEqual("01", TrackUtils.ApplyLeadingZeroes("1", "10", 2, true, true));
            Assert.AreEqual("01", TrackUtils.ApplyLeadingZeroes("1", "1", 0, true, true));
            Assert.AreEqual("01/01", TrackUtils.ApplyLeadingZeroes("1/1", "1", 0, true, true));
            Assert.AreEqual("01/01", TrackUtils.ApplyLeadingZeroes("1/01", "1", 0, true, true));
            Assert.AreEqual("001", TrackUtils.ApplyLeadingZeroes("1", "100", 0, true, true));
        }
    }
}
