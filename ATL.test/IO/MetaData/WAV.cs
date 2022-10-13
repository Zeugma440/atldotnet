using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.Collections.Generic;
using ATL.AudioData.IO;
using System.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class WAV : MetaIOTest
    {
        readonly private string notEmptyFile_bext = "WAV/broadcastwave_bext.wav";
        readonly private string notEmptyFile_info = "WAV/broadcastwave_bext_info.wav";
        readonly private string notEmptyFile_ixml = "WAV/broadcastwave_bext_iXML.wav";
        readonly private string notEmptyFile_sample = "WAV/broadcastwave_bext_iXML.wav";
        readonly private string notEmptyFile_cue = "WAV/cue.wav";

        public WAV()
        {
            emptyFile = "WAV/empty.wav";

            tagType = MetaDataIOFactory.TagType.NATIVE;
            supportsInternationalChars = false;

            testData.TrackTotal = 0;
        }

        private void initBextTestData()
        {
            notEmptyFile = notEmptyFile_bext;

            testData = new TagHolder();

            testData.GeneralDescription = "bext.description";

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("bext.originator", "bext.originator"));
            tags.Add(new KeyValuePair<string, string>("bext.originatorReference", "bext.originatorReference"));
            tags.Add(new KeyValuePair<string, string>("bext.originationDate", "2018-01-09"));
            tags.Add(new KeyValuePair<string, string>("bext.originationTime", "01:23:45"));
            tags.Add(new KeyValuePair<string, string>("bext.timeReference", "110801250"));
            tags.Add(new KeyValuePair<string, string>("bext.version", "2"));
            tags.Add(new KeyValuePair<string, string>("bext.UMID", "060A2B3401010101010102101300000000000000000000800000000000000000"));
            tags.Add(new KeyValuePair<string, string>("bext.loudnessValue", (1.23).ToString()));
            tags.Add(new KeyValuePair<string, string>("bext.loudnessRange", (4.56).ToString()));
            tags.Add(new KeyValuePair<string, string>("bext.maxTruePeakLevel", (7.89).ToString()));
            tags.Add(new KeyValuePair<string, string>("bext.maxMomentaryLoudness", (3.33).ToString()));
            tags.Add(new KeyValuePair<string, string>("bext.maxShortTermLoudness", (-3.33).ToString()));
            tags.Add(new KeyValuePair<string, string>("bext.codingHistory", "A=MPEG1L3,F=22050,B=56,W=20,M=dual-mono,T=haha"));
            testData.AdditionalFields = tags;
        }

        private void initListInfoTestData()
        {
            notEmptyFile = notEmptyFile_info;

            testData = new TagHolder();

            testData.Artist = "info.IART";
            testData.Title = "info.INAM";
            testData.Copyright = "info.ICOP";
            testData.Genre = "info.IGNR";
            testData.Comment = "info.ICMT";
            testData.Date = System.DateTime.Parse("2018-01-09T01:23:45");
            testData.TrackNumber = 5;
            testData.Popularity = 0.2f;

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("info.IARL", "info.IARL"));
            tags.Add(new KeyValuePair<string, string>("info.ICMS", "info.ICMS"));
            tags.Add(new KeyValuePair<string, string>("info.IENG", "info.IENG"));
            tags.Add(new KeyValuePair<string, string>("info.IKEY", "info.IKEY"));
            tags.Add(new KeyValuePair<string, string>("info.IMED", "info.IMED"));
            tags.Add(new KeyValuePair<string, string>("info.IPRD", "info.IPRD"));
            tags.Add(new KeyValuePair<string, string>("info.ISBJ", "info.ISBJ"));
            tags.Add(new KeyValuePair<string, string>("info.ISFT", "info.ISFT"));
            tags.Add(new KeyValuePair<string, string>("info.ISRC", "info.ISRC"));
            tags.Add(new KeyValuePair<string, string>("info.ISRF", "info.ISRF"));
            tags.Add(new KeyValuePair<string, string>("info.ITCH", "info.ITCH"));
            testData.AdditionalFields = tags;
        }

        private void initDispTestData()
        {
            notEmptyFile = emptyFile;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("disp[0].type", "CF_TEXT"));
            tags.Add(new KeyValuePair<string, string>("disp[0].value", "blah"));
            tags.Add(new KeyValuePair<string, string>("disp[1].type", "CF_BITMAP"));
            tags.Add(new KeyValuePair<string, string>("disp[1].value", "YmxhaCBibGFo"));
            tags.Add(new KeyValuePair<string, string>("disp[2].type", "CF_METAFILE"));
            tags.Add(new KeyValuePair<string, string>("disp[2].value", "YmxlaCBibGVo"));
            tags.Add(new KeyValuePair<string, string>("disp[3].type", "CF_DIB"));
            tags.Add(new KeyValuePair<string, string>("disp[3].value", "Ymx1aCBibHVo"));
            tags.Add(new KeyValuePair<string, string>("disp[4].type", "CF_PALETTE"));
            tags.Add(new KeyValuePair<string, string>("disp[4].value", "YmzDvGggYmzDvGg="));
            testData.AdditionalFields = tags;
        }

        private void initIXmlTestData()
        {
            notEmptyFile = notEmptyFile_ixml;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("ixml.PROJECT", "ANewMovie"));
            tags.Add(new KeyValuePair<string, string>("ixml.SPEED.NOTE", "camera overcranked"));
            tags.Add(new KeyValuePair<string, string>("ixml.SYNC_POINT_LIST.SYNC_POINT[1].SYNC_POINT_FUNCTION", "SLATE_GENERIC"));
            tags.Add(new KeyValuePair<string, string>("ixml.TRACK_LIST.TRACK[1].NAME", "Mid"));
            tags.Add(new KeyValuePair<string, string>("ixml.TRACK_LIST.TRACK[2].NAME", "Side"));
            testData.AdditionalFields = tags;
        }

        private void initSampleTestData()
        {
            notEmptyFile = notEmptyFile_sample;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("sample.manufacturer", "1"));
            tags.Add(new KeyValuePair<string, string>("sample.product", "2"));
            tags.Add(new KeyValuePair<string, string>("sample.period", "3"));
            tags.Add(new KeyValuePair<string, string>("sample.MIDIUnityNote", "4"));
            tags.Add(new KeyValuePair<string, string>("sample.MIDIPitchFraction", "5"));
            tags.Add(new KeyValuePair<string, string>("sample.SMPTEFormat", "24"));
            tags.Add(new KeyValuePair<string, string>("sample.SMPTEOffset.Hours", "-1"));
            tags.Add(new KeyValuePair<string, string>("sample.SMPTEOffset.Minutes", "10"));
            tags.Add(new KeyValuePair<string, string>("sample.SMPTEOffset.Seconds", "20"));
            tags.Add(new KeyValuePair<string, string>("sample.SMPTEOffset.Frames", "30"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].CuePointId", "11"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].Type", "1"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].Start", "123"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].End", "456"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].Fraction", "8"));
            tags.Add(new KeyValuePair<string, string>("sample.SampleLoop[0].PlayCount", "2"));
            testData.AdditionalFields = tags;
        }

        private void initCueTestReadData()
        {
            notEmptyFile = notEmptyFile_cue;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("cue.NumCuePoints", "10"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].CuePointId", "1"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].Position", "88200"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].DataChunkId", "data"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].ChunkStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].BlockStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].SampleOffset", "88200"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].CuePointId", "10"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].Position", "1730925"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].DataChunkId", "data"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].ChunkStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].BlockStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[9].SampleOffset", "1730925"));

            tags.Add(new KeyValuePair<string, string>("info.Labels[0].Type", "labl"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[0].CuePointId", "1"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[0].Text", "MARKEURRRR 1"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[9].Type", "labl"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[9].CuePointId", "10"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[9].Text", "MARKEURRRR 8"));
            testData.AdditionalFields = tags;
        }

        private void initCueTestRWData()
        {
            notEmptyFile = notEmptyFile_cue;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>();
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].CuePointId", "1"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].Position", "88200"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].DataChunkId", "data"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].ChunkStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].BlockStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[0].SampleOffset", "88200"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].CuePointId", "10"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].Position", "1730925"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].DataChunkId", "data"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].ChunkStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].BlockStart", "0"));
            tags.Add(new KeyValuePair<string, string>("cue.CuePoints[1].SampleOffset", "1730925"));

            tags.Add(new KeyValuePair<string, string>("info.Labels[0].Type", "labl"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[0].CuePointId", "1"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[0].Text", "MARKEURRRR 1"));

            tags.Add(new KeyValuePair<string, string>("info.Labels[1].Type", "note"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[1].CuePointId", "10"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[1].Text", "MARKEURRRR 8"));

            tags.Add(new KeyValuePair<string, string>("info.Labels[2].Type", "ltxt"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].CuePointId", "11"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].SampleLength", "1234"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].PurposeId", "5678"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].Country", "2"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].Language", "4"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].Dialect", "6"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].CodePage", "8"));
            tags.Add(new KeyValuePair<string, string>("info.Labels[2].Text", "HEYHEY 10"));
            testData.AdditionalFields = tags;
        }

        [TestMethod]
        public void TagIO_R_WAV_BEXT_simple()
        {
            new ConsoleLogger();
            initBextTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_WAV_INFO_simple()
        {
            new ConsoleLogger();
            initListInfoTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_WAV_IXML_simple()
        {
            new ConsoleLogger();
            initIXmlTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_Cue_simple()
        {
            new ConsoleLogger();
            initCueTestReadData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

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
            initListInfoTestData();
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
        public void TagIO_RW_WAV_Cue_Empty()
        {
            initCueTestRWData();
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_BEXT_Existing()
        {
            initBextTestData();
            test_RW_Existing(notEmptyFile, 0, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_LIST_INFO_Existing()
        {
            initListInfoTestData();
            test_RW_Existing(notEmptyFile, 0, true, true, false); // CRC check impossible because of field order
        }

        [TestMethod]
        public void TagIO_RW_WAV_DISP_Existing()
        {
            initDispTestData();
            test_RW_Empty(emptyFile, true, true, true);
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

        [TestMethod]
        public void TagIO_RW_WAV_Cue_Existing()
        {
            initCueTestRWData();
            test_RW_Existing(notEmptyFile, 0, true, false, false); // length-check impossible because of parasite end-of-line characters and padding
        }

        [TestMethod]
        public void TagIO_RW_WAV_GEOB_Existing()
        {
            new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string testFileLocation = TestUtils.CopyAsTempTestFile("WAV/id3v2_geob.wav");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            byte initialSubversion = ATL.Settings.ID3v2_tagSubVersion;
            ATL.Settings.ID3v2_tagSubVersion = 3; // Write ID3v2.3

            try
            {
                Assert.IsTrue(theFile.ReadFromFile(true, true));

                AudioDataManager theFile2 = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
                TagHolder theTag = new TagHolder();
                theTag.Comment = "something";
                Assert.IsTrue(theFile2.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.ID3V2));
                Assert.IsTrue(theFile2.ReadFromFile(true, true));

                Assert.AreEqual(theFile.ID3v2.AdditionalFields.Count, theFile2.ID3v2.AdditionalFields.Count);
                foreach (var v in theFile.ID3v2.AdditionalFields)
                {
                    Assert.IsTrue(theFile2.ID3v2.AdditionalFields.ContainsKey(v.Key));
                    Assert.AreEqual(theFile2.ID3v2.AdditionalFields[v.Key], v.Value);
                }
            }
            finally
            {
                ATL.Settings.ID3v2_tagSubVersion = initialSubversion;
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_WAV_Even_ID3()
        {
            new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string testFileLocation = TestUtils.CopyAsTempTestFile("WAV/id3v2_geob.wav");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            TagHolder theTag = new TagHolder();
            theTag.Comment = "somethin";
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.ID3V2));

            // Chunk must have an even size (actual size, not declared size)
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                StreamUtils.FindSequence(s, Commons.Utils.Latin1Encoding.GetBytes("id3 "));
                byte[] intBytes = new byte[4];
                s.Read(intBytes, 0, intBytes.Length);
                int chunkSize = StreamUtils.DecodeInt32(intBytes);
                Assert.IsTrue(chunkSize % 2 > 0); // Odd declared chunk size...
                s.Seek(chunkSize, SeekOrigin.Current);
                Assert.IsTrue(0 == s.ReadByte()); // ...word-aligned with the spec-compliant padding byte
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }
    }
}
