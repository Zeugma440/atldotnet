using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class AIF : MetaIOTest
    {
        public AIF()
        {
            emptyFile = "AIF/empty.aif";
            notEmptyFile = "AIF/aiff.aiff";
            tagType = MetaDataIOFactory.TAG_NATIVE;

            // Initialize specific test data
            testData = new TagData();

            testData.Title = "woodblock";
            testData.Artist = "Prosonus";
            testData.Comment = "Popyright 0000, Prosonus";
            testData.Copyright = "Copyright 1991, Prosonus";

            supportsInternationalChars = false;
        }

        [TestMethod]
        public void TagIO_R_AIF_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_RW_AIF_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, false); // Bit-per-bit comparison not possible yet due to field order differences
        }

        [TestMethod]
        public void TagIO_RW_AIF_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }
    }
}
