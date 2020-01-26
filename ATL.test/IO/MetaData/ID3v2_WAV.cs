 using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.Collections.Generic;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v2_WAV : MetaIOTest
    {
        public ID3v2_WAV()
        {
            emptyFile = "WAV/empty.wav";
            notEmptyFile = "WAV/audacityTags.wav";
            tagType = MetaDataIOFactory.TAG_ID3V2;

            // Initialize specific test data
            testData = new TagData();

            testData.Title = "yeah";
            testData.Artist = "artist";
            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "TES2", "Test父"));
        }

        [TestMethod]
        public void TagIO_R_WAV_ID3v2()
        {
            String location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }
        
        [TestMethod]
        public void TagIO_RW_WAV_ID3v2_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, false, false); // Not same size after edit because original ID3v2.3 is remplaced by ATL ID3v2.4
        }

        [TestMethod]
        public void TagIO_RW_AIF_ID3v2_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }
    }
}
