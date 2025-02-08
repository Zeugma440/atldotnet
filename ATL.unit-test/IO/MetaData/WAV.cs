using System.Globalization;
using ATL.AudioData;
using ATL.AudioData.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class WAV : MetaIOTest
    {
        private readonly string notEmptyFile_bext = "WAV/broadcastwave_bext.wav";
        private readonly string notEmptyFile_info = "WAV/broadcastwave_bext_info.wav";
        private readonly string notEmptyFile_ixml = "WAV/broadcastwave_bext_iXML.wav";
        private readonly string notEmptyFile_xmp = "WAV/xmp_partial.wav";
        private readonly string notEmptyFile_sample = "WAV/broadcastwave_bext_iXML.wav";
        private readonly string notEmptyFile_cue = "WAV/cue.wav";
        private readonly string notEmptyFile_cart = "WAV/cart.wav";

        public WAV()
        {
            emptyFile = "WAV/empty.wav";

            tagType = MetaDataIOFactory.TagType.NATIVE;
            supportsInternationalChars = false;
            supportsExtraEmbeddedPictures = false;
            titleFieldCode = "info.INAM";

            testData.TrackTotal = 0;
        }

        private void initBextTestData()
        {
            notEmptyFile = notEmptyFile_bext;

            testData = new TagHolder
            {
                GeneralDescription = "bext.description"
            };

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                {"bext.originator", "bext.originator"},
                {"bext.originatorReference", "bext.originatorReference"},
                {"bext.originationDate", "2018-01-09"},
                {"bext.originationTime", "01:23:45"},
                {"bext.timeReference", "110801250"},
                {"bext.version", "2"},
                {"bext.UMID", "060A2B3401010101010102101300000000000000000000800000000000000000"},
                {"bext.loudnessValue", 1.23.ToString()},
                {"bext.loudnessRange", 4.56.ToString()},
                {"bext.maxTruePeakLevel", 7.89.ToString()},
                {"bext.maxMomentaryLoudness", 3.33.ToString()},
                {"bext.maxShortTermLoudness", (-3.33).ToString()},
                {"bext.codingHistory", "A=MPEG1L3,F=22050,B=56,W=20,M=dual-mono,T=haha"}
            };
            testData.AdditionalFields = tags;
        }

        private void initListInfoTestData()
        {
            notEmptyFile = notEmptyFile_info;

            testData = new TagHolder
            {
                Artist = "info.IART",
                Title = "info.INAM",
                Album = "info.IPRD",
                Copyright = "info.ICOP",
                Genre = "info.IGNR",
                Comment = "info.ICMT",
                Date = DateTime.Parse("2018-01-09T01:23:45"),
                TrackNumber = "5",
                Popularity = 0.2f,
                EncodedBy = "info.ITCH",
                Encoder = "info.ISFT",
                Language = "info.ILNG"
            };

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                {"info.IARL", "info.IARL"},
                {"info.ICMS", "info.ICMS"},
                {"info.IENG", "info.IENG"},
                {"info.IKEY", "info.IKEY"},
                {"info.IMED", "info.IMED"},
                {"info.ISBJ", "info.ISBJ"},
                {"info.ISRC", "info.ISRC"},
                {"info.ISRF", "info.ISRF"}
            };
            testData.AdditionalFields = tags;
        }

        private void initDispTestData()
        {
            notEmptyFile = emptyFile;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                {"disp.entry[0].type", "CF_TEXT"},
                {"disp.entry[0].value", "blah"},
                {"disp.entry[1].type", "CF_BITMAP"},
                {"disp.entry[1].value", "YmxhaCBibGFo"},
                {"disp.entry[2].type", "CF_METAFILE"},
                {"disp.entry[2].value", "YmxlaCBibGVo"},
                {"disp.entry[3].type", "CF_DIB"},
                {"disp.entry[3].value", "Ymx1aCBibHVo"},
                {"disp.entry[4].type", "CF_PALETTE"},
                {"disp.entry[4].value", "YmzDvGggYmzDvGg="}
            };
            testData.AdditionalFields = tags;
        }

        private void initIXmlTestData()
        {
            notEmptyFile = notEmptyFile_ixml;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "ixml.PROJECT", "ANewMovie" },
                { "ixml.SPEED.NOTE", "camera overcranked" },
                { "ixml.SYNC_POINT_LIST.SYNC_POINT[1].SYNC_POINT_FUNCTION", "SLATE_GENERIC" },
                { "ixml.TRACK_LIST.TRACK[1].NAME", "Mid" },
                { "ixml.TRACK_LIST.TRACK[2].NAME", "Side" }
            };
            testData.AdditionalFields = tags;
        }

        private void initXmpTestData()
        {
            notEmptyFile = notEmptyFile_xmp;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "xmp.x:xmptk", "Adobe XMP Core 9.1-c002 79.2c0288b, 2024/01/23-06:33:24        " },
                { "xmp.rdf:RDF.rdf:Description.dc:title.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.dc:title.rdf:Alt.rdf:li[1]", "document title" },
                { "xmp.rdf:RDF.rdf:Description.dc:creator.rdf:Seq.rdf:li[1]", "author1" },
                { "xmp.rdf:RDF.rdf:Description.dc:creator.rdf:Seq.rdf:li[2]", "author2" },
                { "xmp.rdf:RDF.rdf:Description.dc:description.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.dc:description.rdf:Alt.rdf:li[1]", "author description" },
                { "xmp.rdf:RDF.rdf:Description.dc:subject.rdf:Bag.rdf:li[1]", "keyword11" },
                { "xmp.rdf:RDF.rdf:Description.dc:subject.rdf:Bag.rdf:li[2]", "keyword2" },
                { "xmp.rdf:RDF.rdf:Description.dc:rights.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.dc:rights.rdf:Alt.rdf:li[1]", "copyright notice" },
                { "xmp.rdf:RDF.rdf:Description.photoshop:Source", "Source" },
                { "xmp.rdf:RDF.rdf:Description.photoshop:Urgency", "5" },
                { "xmp.rdf:RDF.rdf:Description.xmp:Rating", "4" },
                { "xmp.rdf:RDF.rdf:Description.xmpRights:Marked", "True" },
                { "xmp.rdf:RDF.rdf:Description.xmpRights:WebStatement", "http://copyright/info.u.rl" },
                { "xmp.rdf:RDF.rdf:Description.xmpRights:UsageTerms.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.xmpRights:UsageTerms.rdf:Alt.rdf:li[1]", "right usage terms" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:CreatorContactInfo.rdf:parseType", "Resource" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:CreatorContactInfo.Iptc4xmpCore:CiAdrExtadr", "author address" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:CreatorContactInfo.Iptc4xmpCore:CiUrlWork", "author website1;author website2" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:IntellectualGenre", "intellectual genre" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:Scene.rdf:Bag.rdf:li[1]", "IPTC scene code 1" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:Scene.rdf:Bag.rdf:li[2]", "IPTC scene code 2" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:AltTextAccessibility.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:AltTextAccessibility.rdf:Alt.rdf:li[1]", "alt test" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:ExtDescrAccessibility.rdf:Alt.rdf:li[1].xml:lang", "x-default" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:ExtDescrAccessibility.rdf:Alt.rdf:li[1]", "extended description" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:SubjectCode.rdf:Bag.rdf:li[1]", "IPTC subject code 1" },
                { "xmp.rdf:RDF.rdf:Description.Iptc4xmpCore:SubjectCode.rdf:Bag.rdf:li[2]", "IPTC subject code 2" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:tapeName", "tape name" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:altTimecode.rdf:parseType", "Resource" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:altTimecode.xmpDM:timeValue", "alternate timecode" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:artist", "artist" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:album", "album" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:genre", "genre" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:trackNumber", "6" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:composer", "composer" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:engineer", "engineer" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:releaseDate", "2024-02-29T20:55:00" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:instrument", "instrument" },
                { "xmp.rdf:RDF.rdf:Description.xmp:CreateDate", "2024-03-29T00:00:01+01:00" },
                { "xmp.rdf:RDF.rdf:Description.xmp:CreatorTool", "creator tool" },
                { "xmp.rdf:RDF.rdf:Description.xmp:Label", "xmp label" },
                { "xmp.rdf:RDF.rdf:Description.xmp:MetadataDate", "2024-03-29T00:00:03+01:00" },
                { "xmp.rdf:RDF.rdf:Description.xmp:ModifyDate", "2024-03-29T00:00:02+01:00" },
                { "xmp.rdf:RDF.rdf:Description.dc:contributor.rdf:Bag.rdf:li[1]", "contributor" },
                { "xmp.rdf:RDF.rdf:Description.dc:coverage", "coverage" },
                { "xmp.rdf:RDF.rdf:Description.dc:date.rdf:Seq.rdf:li[1]", "2024-04-29T00:00:00+02:00" },
                { "xmp.rdf:RDF.rdf:Description.dc:format", "format" },
                { "xmp.rdf:RDF.rdf:Description.dc:identifier", "identifier" },
                { "xmp.rdf:RDF.rdf:Description.dc:publisher.rdf:Bag.rdf:li[1]", "publisher" },
                { "xmp.rdf:RDF.rdf:Description.dc:relation.rdf:Bag.rdf:li[1]", "relation" },
                { "xmp.rdf:RDF.rdf:Description.dc:source", "source" },
                { "xmp.rdf:RDF.rdf:Description.dc:type.rdf:Bag.rdf:li[1]", "type" },
                { "xmp.rdf:RDF.rdf:Description.adid:code", "1234" },
                { "xmp.rdf:RDF.rdf:Description.adid:brand", "brand" },
            };
            testData.AdditionalFields = tags;
        }

        private void initCartTestData()
        {
            notEmptyFile = notEmptyFile_cart;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "cart.version", "1.01" },
                { "cart.title", "title" },
                { "cart.artist", "artist" },
                { "cart.cutNumber", "cutId" },
                { "cart.clientId", "clientId" },
                { "cart.category", "category" },
                { "cart.classification", "classification" },
                { "cart.outCue", "outCue" },
                { "cart.startDate", "1900/01/01" },
                { "cart.startTime", "12:34:56" },
                { "cart.endDate", "2199/12/31" },
                { "cart.endTime", "21:43:56" },
                { "cart.producerAppId", "producerApplicationId" },
                { "cart.producerAppVersion", "producerApplicationVersion" },
                { "cart.userDef", "userDefined" },
                { "cart.dwLevelReference", "32768" },
                { "cart.postTimerUsageId[1]", "" },
                { "cart.postTimerValue[1]", "0" },
                { "cart.postTimerUsageId[2]", "" },
                { "cart.postTimerValue[2]", "0" },
                { "cart.postTimerUsageId[3]", "" },
                { "cart.postTimerValue[3]", "0" },
                { "cart.postTimerUsageId[4]", "" },
                { "cart.postTimerValue[4]", "0" },
                { "cart.postTimerUsageId[5]", "" },
                { "cart.postTimerValue[5]", "0" },
                { "cart.postTimerUsageId[6]", "" },
                { "cart.postTimerValue[6]", "0" },
                { "cart.postTimerUsageId[7]", "" },
                { "cart.postTimerValue[7]", "0" },
                { "cart.postTimerUsageId[8]", "" },
                { "cart.postTimerValue[8]", "0" },
                { "cart.url", "URL" },
                { "cart.tagText", "tagText" }
            };
            testData.AdditionalFields = tags;
        }

        private void initSampleTestData()
        {
            notEmptyFile = notEmptyFile_sample;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "sample.manufacturer", "1"},
                { "sample.product", "2"},
                { "sample.period", "3"},
                { "sample.MIDIUnityNote", "4"},
                { "sample.MIDIPitchFraction", "5"},
                { "sample.SMPTEFormat", "24"},
                { "sample.SMPTEOffset.Hours", "-1"},
                { "sample.SMPTEOffset.Minutes", "10"},
                { "sample.SMPTEOffset.Seconds", "20"},
                { "sample.SMPTEOffset.Frames", "30"},
                { "sample.SampleLoop[0].CuePointId", "11"},
                { "sample.SampleLoop[0].Type", "1"},
                { "sample.SampleLoop[0].Start", "123"},
                { "sample.SampleLoop[0].End", "456"},
                { "sample.SampleLoop[0].Fraction", "8"},
                { "sample.SampleLoop[0].PlayCount", "2"}
            };
            testData.AdditionalFields = tags;
        }

        private void initCueTestReadData()
        {
            notEmptyFile = notEmptyFile_cue;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                {"cue.NumCuePoints", "10"},
                {"cue.CuePoints[0].CuePointId", "1"},
                {"cue.CuePoints[0].Position", "88200"},
                {"cue.CuePoints[0].DataChunkId", "data"},
                {"cue.CuePoints[0].ChunkStart", "0"},
                {"cue.CuePoints[0].BlockStart", "0"},
                {"cue.CuePoints[0].SampleOffset", "88200"},
                {"cue.CuePoints[9].CuePointId", "10"},
                {"cue.CuePoints[9].Position", "1730925"},
                {"cue.CuePoints[9].DataChunkId", "data"},
                {"cue.CuePoints[9].ChunkStart", "0"},
                {"cue.CuePoints[9].BlockStart", "0"},
                {"cue.CuePoints[9].SampleOffset", "1730925"},
                {"adtl.Labels[0].Type", "labl"},
                {"adtl.Labels[0].CuePointId", "1"},
                {"adtl.Labels[0].Text", "MARKEURRRR 1"},
                {"adtl.Labels[9].Type", "labl"},
                {"adtl.Labels[9].CuePointId", "10"},
                {"adtl.Labels[9].Text", "MARKEURRRR 8"}
            };
            testData.AdditionalFields = tags;
        }

        private void initCueTestRWData()
        {
            notEmptyFile = notEmptyFile_cue;

            testData = new TagHolder();

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                {"cue.CuePoints[0].CuePointId", "1"},
                {"cue.CuePoints[0].Position", "88200"},
                {"cue.CuePoints[0].DataChunkId", "data"},
                {"cue.CuePoints[0].ChunkStart", "0"},
                {"cue.CuePoints[0].BlockStart", "0"},
                {"cue.CuePoints[0].SampleOffset", "88200"},
                {"cue.CuePoints[1].CuePointId", "10"},
                {"cue.CuePoints[1].Position", "1730925"},
                {"cue.CuePoints[1].DataChunkId", "data"},
                {"cue.CuePoints[1].ChunkStart", "0"},
                {"cue.CuePoints[1].BlockStart", "0"},
                {"cue.CuePoints[1].SampleOffset", "1730925"},
                {"adtl.Labels[0].Type", "labl"},
                {"adtl.Labels[0].CuePointId", "1"},
                {"adtl.Labels[0].Text", "MARKEURRRR 1"},
                {"adtl.Labels[1].Type", "note"},
                {"adtl.Labels[1].CuePointId", "10"},
                {"adtl.Labels[1].Text", "MARKEURRRR 8"},
                {"adtl.Labels[2].Type", "ltxt"},
                {"adtl.Labels[2].CuePointId", "11"},
                {"adtl.Labels[2].SampleLength", "1234"},
                {"adtl.Labels[2].PurposeId", "5678"},
                {"adtl.Labels[2].Country", "2"},
                {"adtl.Labels[2].Language", "4"},
                {"adtl.Labels[2].Dialect", "6"},
                {"adtl.Labels[2].CodePage", "8"},
                {"adtl.Labels[2].Text", "HEYHEY 10"}
            };
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
        public void TagIO_R_WAV_XMP_simple()
        {
            new ConsoleLogger();
            initXmpTestData();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 0);
        }

        [TestMethod]
        public void TagIO_R_WAV_Cart_simple()
        {
            new ConsoleLogger();
            initCartTestData();

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
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_LIST_INFO_Empty()
        {
            initListInfoTestData();
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_IXML_Empty()
        {
            initIXmlTestData();
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_XMP_Empty()
        {
            initXmpTestData();
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_Cart_Empty()
        {
            initCartTestData();
            test_RW_Empty(emptyFile, false, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_Sample_Empty()
        {
            initSampleTestData();
            test_RW_Empty(emptyFile, false, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_Cue_Empty()
        {
            initCueTestRWData();
            test_RW_Empty(emptyFile, false, true, true, true);
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
            // Size check impossible because of fields mapped to multiple sub-chunks
            // CRC check impossible because of field order
            test_RW_Existing(notEmptyFile, 0, true, false, false);
        }

        [TestMethod]
        public void TagIO_RW_WAV_DISP_Existing()
        {
            initDispTestData();
            test_RW_Empty(emptyFile, false, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_WAV_IXML_Existing()
        {
            initIXmlTestData();
            test_RW_Existing(notEmptyFile, 0, true, false, false); // length-check impossible because of parasite end-of-line characters and padding
        }

        [TestMethod]
        public void TagIO_RW_WAV_XMP_Existing()
        {
            initXmpTestData();
            test_RW_Existing(notEmptyFile, 0, true, false, false); // length-check impossible because of parasite end-of-line characters and padding
        }

        [TestMethod]
        public void TagIO_RW_WAV_Cart_Existing()
        {
            initCartTestData();
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
                Assert.IsTrue(theFile2.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.ID3V2).GetAwaiter().GetResult());
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
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.ID3V2).GetAwaiter().GetResult());

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
