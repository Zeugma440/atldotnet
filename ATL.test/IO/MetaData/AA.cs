using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System;
using System.IO;
using static ATL.PictureInfo;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class AA : MetaIOTest
    {
        public AA()
        {
            notEmptyFile = "AA/aa.aa";
            tagType = MetaDataIOFactory.TAG_NATIVE;

            // Initialize specific test data
            testData = new TagData();

            testData.Title = "The New York Times Audio Digest, July 10, 2015";
            testData.Artist = "The New York Times";
            testData.Album = "The New York Times Audio Digest";
            testData.Comment = "It's the perfect listen for your morning commute! In the time it takes you to get to work, you'll hear a digest of the day's top stories, prepared by the editorial staff of The New York Times....";
            testData.PublishingDate = DateTime.Parse("10-JUL-2015").ToString();
            testData.Publisher = "The New York Times";
            testData.Composer = "The New York Times";
            testData.GeneralDescription = "It's the perfect listen for your morning commute! In the time it takes you to get to work, you'll hear a digest of the day's top stories, prepared by the editorial staff of The New York Times. Each edition includes articles from the front page, as well as the paper's international, national, business, sports, and editorial sections.";
            testData.Copyright = "(P) and &#169;2015 The New York Times News Services Division of The New York Times Company";

            // Initialize specific test data (Picture native codes are strings)
            testData.Pictures.Clear();
            PictureInfo pic = PictureInfo.fromBinaryData(
                File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "AA/aa.jpg"),
                PIC_TYPE.Generic,
                MetaDataIOFactory.TAG_ANY,
                11);
            pic.ComputePicHash();
            testData.Pictures.Add(pic);

            supportsInternationalChars = true;
        }

        [TestMethod]
        public void TagIO_R_AA_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile, 1);
        }

        [TestMethod]
        public void TagIO_RW_AA_Existing()
        {
            // Filesize comparison not possible due to the empty "user_alias" tag disappearing upon rewrite
            // Bit-per-bit comparison not possible yet due to field order differences
            test_RW_Existing(notEmptyFile, 1, true, false); 
        }
    }
}
