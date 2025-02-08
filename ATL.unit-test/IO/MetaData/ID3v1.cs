using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using ATL.AudioData.IO;
using System;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v1 : MetaIOTest
    {
        public ID3v1()
        {
            emptyFile = "MP3/empty.mp3";
            notEmptyFile = "MP3/id3v1.mp3";
            tagType = MetaDataIOFactory.TagType.ID3V1;

            supportsInternationalChars = false;
            supportsExtraEmbeddedPictures = false;

            // Initialize specific test data
            testData = new TagHolder();

            testData.Title = "Title";
            testData.Album = "?";
            testData.Artist = "Artist";
            testData.Comment = "Test!";
            testData.Date = DateTime.Parse("01/01/2017");
            testData.Genre = "Bluegrass";
            testData.TrackNumber = "22";
        }

        [TestMethod]
        public void TagIO_R_ID3v1()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v1_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_ID3v1_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, true);
        }

    }
}
