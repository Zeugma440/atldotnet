using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.Collections.Generic;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class APE : MetaIOTest
    {
        public APE()
        {
            emptyFile = "MP3/empty.mp3";
            notEmptyFile = "MP3/APE.mp3";
            tagType = MetaDataIOFactory.TAG_APE;

            // Initialize specific test data (Publisher and Description fields not supported in APE tag)
            testData = new TagData();

            testData.Title = "Title";
            testData.Album = "父";
            testData.Artist = "Artist";
            testData.AlbumArtist = "Bob";
            testData.Comment = "Test!";
            testData.RecordingYear = "2017";
            testData.RecordingDate = ""; // Empty string means "supported, but not valued in test sample"
            testData.Genre = "Test";
            testData.Rating = "0";
            testData.TrackNumber = "22";
            testData.Composer = "Me";
            testData.Conductor = "";
            testData.DiscNumber = "2";
            testData.Copyright = "";

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "TEST", "xxx"));
        }


        [TestMethod]
        public void TagIO_R_APE() // My deepest apologies for this dubious method name
        {
            String location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location) );

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_APE_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_APE_Existing()
        {
            // Final size and hash checks NOT POSSIBLE YET mainly due to tag order and tag name (e.g. "DISC" becoming "DISCNUMBER") differences
            test_RW_Existing(notEmptyFile, 2, false, false);
        }

        [TestMethod]
        public void TagIO_RW_APE_Unsupported_Empty()
        {
            test_RW_Unsupported_Empty(emptyFile);
        }

        [TestMethod]
        public void TagIO_RW_APE_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_APE, MetaDataIOFactory.TAG_ID3V1);
        }

        [TestMethod]
        public void TagIO_RW_APE_ID3v2()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_APE, MetaDataIOFactory.TAG_ID3V2);
        }
    }
}
