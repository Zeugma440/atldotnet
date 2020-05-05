using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.Collections.Generic;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class WAV : MetaIOTest
    {
        private string notEmptyFile_bext = "WAV/broadcastwave_bext.wav";
        private string notEmptyFile_info = "WAV/broadcastwave_bext_info.wav";
        private string notEmptyFile_ixml = "WAV/broadcastwave_bext_iXML.wav";
        private string notEmptyFile_sample = "WAV/broadcastwave_bext_iXML.wav";

        public WAV()
        {
            emptyFile = "WAV/empty.wav";

            tagType = MetaDataIOFactory.TAG_NATIVE;
            supportsInternationalChars = false;
        }

        private void initBextTestData()
        {
            notEmptyFile = notEmptyFile_bext;

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

        private void initInfoTestData()
        {
            notEmptyFile = notEmptyFile_info;

            testData = new TagData();

            testData.Artist = "info.IART";
            testData.Title = "info.INAM";
            testData.Copyright = "info.ICOP";
            testData.Genre = "info.IGNR";
            testData.Comment = "info.ICMT";

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.IARL", "info.IARL"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ICMS", "info.ICMS"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ICRD", "2018-01-09 01:23:45"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.IENG", "info.IENG"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.IKEY", "info.IKEY"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.IMED", "info.IMED"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.IPRD", "info.IPRD"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ISBJ", "info.ISBJ"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ISFT", "info.ISFT"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ISRC", "info.ISRC"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ISRF", "info.ISRF"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "info.ITCH", "info.ITCH"));
        }

        private void initIXmlTestData()
        {
            notEmptyFile = notEmptyFile_ixml;

            testData = new TagData();

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "ixml.PROJECT", "ANewMovie"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "ixml.SPEED.NOTE", "camera overcranked"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "ixml.SYNC_POINT_LIST.SYNC_POINT[1].SYNC_POINT_FUNCTION", "SLATE_GENERIC"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "ixml.TRACK_LIST.TRACK[1].NAME", "Mid"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "ixml.TRACK_LIST.TRACK[2].NAME", "Side"));
        }

        private void initSampleTestData()
        {
            notEmptyFile = notEmptyFile_sample;

            testData = new TagData();

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.manufacturer", "1"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.product", "2"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.period", "3"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.MIDIUnityNote", "4"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.MIDIPitchFraction", "5"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SMPTEFormat", "24"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SMPTEOffset.Hours", "-1"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SMPTEOffset.Minutes", "10"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SMPTEOffset.Seconds", "20"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SMPTEOffset.Frames", "30"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].CuePointId", "11"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].Type", "1"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].Start", "123"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].End", "456"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].Fraction", "8"));
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "sample.SampleLoop[0].PlayCount", "2"));
        }

        [TestMethod]
        public void TagIO_R_WAV_BEXT_simple()
        {
            ConsoleLogger log = new ConsoleLogger();
            initBextTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_WAV_INFO_simple()
        {
            ConsoleLogger log = new ConsoleLogger();
            initInfoTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_WAV_IXML_simple()
        {
            ConsoleLogger log = new ConsoleLogger();
            initIXmlTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Empty()
        {
            initBextTestData();
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_INFO_Empty()
        {
            initInfoTestData();
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_IXML_Empty()
        {
            initIXmlTestData();
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_Sample_Empty()
        {
            initSampleTestData();
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Existing()
        {
            initBextTestData();
            test_RW_Existing(notEmptyFile, 0, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_INFO_Existing()
        {
            initInfoTestData();
            test_RW_Existing(notEmptyFile, 0, true, true, false); // CRC check impossible because of field order
        }

        [TestMethod]
        public void TagIO_RW_WAV_IXML_Existing()
        {
            initIXmlTestData();
            test_RW_Existing(notEmptyFile, 0, true, false, false); // length-check impossible because of parasite end-of-line characters and padding
        }

        /* Should find a tool to edit all these informations manually to create a test file
        [TestMethod]
        public void TagIO_RW_WAV_Sample_Existing()
        {
            initSampleTestData();
            test_RW_Existing(notEmptyFile, 0, true, false, false); // length-check impossible because of parasite end-of-line characters and padding
        }
        */

    }
}
