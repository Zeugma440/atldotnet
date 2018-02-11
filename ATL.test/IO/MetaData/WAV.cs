 using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Collections.Generic;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class WAV : MetaIOTest
    {
        public WAV()
        {
            emptyFile = "WAV/empty.wav";
            notEmptyFile = "WAV/broadcastwave_bext_info.wav";

            tagType = MetaDataIOFactory.TAG_NATIVE;
            supportsInternationalChars = false;

            // Initialize specific test data
            testData = new TagData();

            testData.GeneralDescription = "bext.description";

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.originator", "bext.originator"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.originatorReference", "bext.originatorReference"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.originationDate", "2018-01-09"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.originationTime", "01:23:45"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.timeReference", "110801250"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.version", "2"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.UMID", "060A2B3401010101010102101300000000000000000000800000000000000000"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.loudnessValue", (1.23).ToString()));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.loudnessRange", (4.56).ToString()));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.maxTruePeakLevel", (7.89).ToString()));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.maxMomentaryLoudness", (3.33).ToString()));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.maxShortTermLoudness", (-3.33).ToString()));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "bext.codingHistory", "A=MPEG1L3,F=22050,B=56,W=20,M=dual-mono,T=haha"));
        }

        [TestMethod]
        public void TagIO_R_WAV_BEXT_simple()
        {
            ConsoleLogger log = new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location) );

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Existing()
        {
            test_RW_Existing(notEmptyFile, 0, true, true, true); // No comparison can be done because of padding spaces
        }
        
    }
}
