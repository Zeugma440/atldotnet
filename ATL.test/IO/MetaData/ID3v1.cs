using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v1 : MetaIOTest
    {
        public ID3v1()
        {
            emptyFile = "MP3/empty.mp3";
            notEmptyFile = "MP3/id3v1.mp3";
            tagType = MetaDataIOFactory.TAG_ID3V1;

            supportsInternationalChars = false;

            // Initialize specific test data
            testData = new TagData();

            testData.Title = "Title";
            testData.Album = "?";
            testData.Artist = "Artist";
            testData.Comment = "Test!";
            testData.RecordingYear = "2017";
            testData.Genre = "Bluegrass";
            testData.TrackNumber = "22";
        }

        [TestMethod]
        public void TagIO_R_ID3v1()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v1_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_ID3v1_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, true);
        }

    }
}
