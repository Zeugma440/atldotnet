using ATL.AudioData;
using System.Drawing;
using static ATL.PictureInfo;
using ATL.AudioData.IO;
using static ATL.Logging.Log;
using ATL.Logging;
using Commons;
using System.Text;

namespace ATL.test.IO.MetaData
{
    /*
     * IMPLEMENTED USE CASES
     *  
     *  1. Single metadata fields
     *                                Read  | Add   | Remove
     *  Supported textual field     |   x   |  x    | x
     *  Unsupported textual field   |   x   |  x    | x
     *  Supported picture           |   x   |  x    | x
     *  Unsupported picture         |   x   |  x    | x
     *  
     *  2. General behaviour
     *  
     *  Whole tag removal
     *  
     *  Conservation of unmodified tag items after tag editing
     *  Conservation of unsupported tag field after tag editing
     *  Conservation of supported pictures after tag editing
     *  Conservation of unsupported pictures after tag editing
     *  
     *  3. Specific behaviour
     *  
     *  Remove single supported picture (from normalized type and index)
     *  Remove single unsupported picture (with multiple pictures; checking if removing pic 2 correctly keeps pics 1 and 3)
     *  
     *  4. Technical
     *  
     *  Cohabitation with ID3v1/ID3v2/APE (=> no impact on MP4 internals)
     *
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Individual picture removal (from index > 1)
     * 
     * 
     * TECHNICAL
     * 
     * Exact picture data conservation after tag editing
     * Files with mdat located before moov
     * 
    */


    [TestClass]
    public class MP4 : MetaIOTest
    {
        public MP4()
        {
            emptyFile = "MP4/empty.m4a"; // Has empty udta/meta tags
            notEmptyFile = "MP4/mp4.m4a";
            tagType = MetaDataIOFactory.TagType.NATIVE;
            titleFieldCode = "©nam";

            // MP4 does not support leading zeroes
            testData.TrackNumber = "1";
            testData.TrackTotal = 2;
            testData.DiscNumber = 3;
            testData.DiscTotal = 4;
            /*
            testData.RecordingYear = "1997";
            testData.RecordingDate = null;
            */
            testData.Date = DateTime.Parse("01/01/1997");
            testData.Conductor = "John Williams";
            testData.Publisher = null;
            testData.Genre = "Household"; // "House" was generating a 'gnre' numeric field whereas ATL standard way of tagging is '(c)gen' string field => Start with a non-standard Genre
            testData.ProductId = "THIS IS A GOOD ID";
            testData.SortAlbum = "SortAlbum";
            testData.SortAlbumArtist = "SortAlbumArtist";
            testData.SortArtist = "SortArtist";
            testData.SortTitle = "SortTitle";
            testData.Group = "Group";
            testData.SeriesTitle = "SeriesTitle";
            testData.SeriesPart = "2";
            testData.LongDescription = "LongDescription";
            testData.Lyricist = "John D. Applebottom";

            testData.AdditionalFields = new Dictionary<string, string>
            {
                { "TESTFIELD", "xxx" }
            };

            PictureInfo pic = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TagType.ANY, 13);
            pic.ComputePicHash();
            testData.EmbeddedPictures = new List<PictureInfo> { pic };

            supportsDateOrYear = true;
        }


        [TestMethod]
        public void TagIO_R_MP4()
        {
            new ConsoleLogger();

            // Source : M4A with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (TESTFIELD)
            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));
            readExistingTagsOnFile(theFile, 1);

            string pid = testData.ProductId;
            float? bpm = testData.BPM;
            string lyricist = testData.Lyricist;
            // Test reading complete recording date
            try
            {
                testData.Date = DateTime.Parse("1997-06-20T00:00:00"); // No timestamp in MP4 date format
                testData.ProductId = null;
                testData.BPM = 0;
                testData.Lyricist = "";

                location = TestUtils.GetResourceLocationRoot() + "MP4/mp4_date_in_©day.m4a";
                theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));
                readExistingTagsOnFile(theFile, 1);
            }
            finally
            {
                testData.Date = DateTime.MinValue;
                testData.ProductId = pid;
                testData.BPM = bpm;
                testData.Lyricist = lyricist;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Empty_no_udta()
        {
            test_RW_Empty("MP4/no_udta.m4a", true, true, false, false); // ATL leaves an empty udta/meta structure, which is more "standard" than wiping the entire udta branch
        }

        [TestMethod]
        public void TagIO_RW_MP4_Existing()
        {
            new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (TESTFIELD)
            String testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagHolder theTag = new TagHolder();
            theTag.Publisher = "John Jackman";

            byte[] data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.png");
            PictureInfo picInfo = PictureInfo.fromBinaryData(data, PictureInfo.PIC_TYPE.Generic, MetaDataIOFactory.TagType.ANY, 13);
            theTag.EmbeddedPictures = new List<PictureInfo> { picInfo };

            var testChaps = theFile.NativeTag.Chapters;
            testChaps.Add(new ChapterInfo(3000, "Chapter 2"));
            theTag.Chapters = testChaps;

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());


            try
            {
                // Read Quicktime chapters specifically
                ATL.Settings.MP4_readChaptersFormat = 1;
                Assert.IsTrue(theFile.ReadFromFile(true, true));
                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);
                Assert.AreEqual((uint)0, theFile.NativeTag.Chapters[0].StartTime); // 1st Quicktime chapter can't start at position > 0
                Assert.AreEqual("aa父bb", theFile.NativeTag.Chapters[0].Title);
                Assert.AreEqual((uint)2945, theFile.NativeTag.Chapters[1].StartTime); // Approximate due to the way timecodes are formatted in the MP4 format
                Assert.AreEqual("Chapter 2", theFile.NativeTag.Chapters[1].Title);

                // Read Nero chapters specifically
                ATL.Settings.MP4_readChaptersFormat = 3;
                Assert.IsTrue(theFile.ReadFromFile(true, true));
                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);
                Assert.AreEqual((uint)55, theFile.NativeTag.Chapters[0].StartTime);
                Assert.AreEqual("aa父bb", theFile.NativeTag.Chapters[0].Title);
                Assert.AreEqual((uint)3000, theFile.NativeTag.Chapters[1].StartTime);
                Assert.AreEqual("Chapter 2", theFile.NativeTag.Chapters[1].Title);
            }
            finally
            {
                ATL.Settings.MP4_readChaptersFormat = 0;
            }


            // Read the rest of supported fields
            readExistingTagsOnFile(theFile, 2);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.NativeTag.Publisher);

#pragma warning disable CA1416
            byte nbFound = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Generic) && (1 == nbFound))
                {
                    using Image picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Png, picture.RawFormat);
                    Assert.AreEqual(175, picture.Width);
                    Assert.AreEqual(168, picture.Height);
                }
                nbFound++;
            }
            Assert.AreEqual(2, nbFound);
#pragma warning restore CA1416

            // Remove the additional supported field
            theTag = new TagHolder();
            theTag.Publisher = "";

            // Remove additional picture
            picInfo = new PictureInfo(PIC_TYPE.Back);
            picInfo.MarkedForDeletion = true;
            theTag.EmbeddedPictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            readExistingTagsOnFile(theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.NativeTag.Publisher);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)

            /* NOT POSSIBLE YET mainly due to tag order and tag naming (e.g. "gnre" becoming "©gen") differences
                        FileInfo originalFileInfo = new FileInfo(location);
                        FileInfo testFileInfo = new FileInfo(testFileLocation);

                        Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                        string originalMD5 = TestUtils.GetFileMD5Hash(location);
                        string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                        Assert.IsTrue(originalMD5.Equals(testMD5));
            */
            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Remove()
        {
            test_RW_Remove(notEmptyFile);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Unsupported_Empty()
        {
            // Source : tag-free M4A
            String testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TES2", "This is another test 父"));

            // Add new unsupported pictures
            PictureInfo picInfo = PictureInfo.fromBinaryData(
                File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"),
                PIC_TYPE.Unsupported,
                MetaDataIOFactory.TagType.NATIVE,
                "1234");
            theTag.Pictures.Add(picInfo);
            picInfo = PictureInfo.fromBinaryData(
                File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"),
                PIC_TYPE.Unsupported,
                MetaDataIOFactory.TagType.NATIVE,
                "5678");
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult();

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TES2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);

#pragma warning disable CA1416
            byte found = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Generic) && (0 == found)) // No custom nor categorized picture type in MP4
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
                else if (pic.PicType.Equals(PIC_TYPE.Generic) && (1 == found))  // No custom nor categorized picture type in MP4
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(290, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
            }
            Assert.AreEqual(2, found);
#pragma warning restore CA1416

            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new PictureInfo(PIC_TYPE.Generic, 1);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TES2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

#pragma warning disable CA1416
            found = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Generic) && (0 == found))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(290, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
            }
            Assert.AreEqual(1, found);
#pragma warning restore CA1416

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_NonStandard_WM()
        {
            new ConsoleLogger();
            ArrayLogger log = new ArrayLogger();

            // Source : tag-free M4A
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            // Add a field outside Microsoft standards
            TagData theTag = new TagData();
            theTag.AdditionalFields = new List<MetaFieldInfo>();
            MetaFieldInfo infoOK = new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "WM/ParentalRating", "M for Mature");
            theTag.AdditionalFields.Add(infoOK);

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/ParentalRating"));
            Assert.AreEqual("M for Mature", theFile.NativeTag.AdditionalFields["WM/ParentalRating"]);

            // Check that it has indeed been added to the Xtra atom
            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("Xtra")));
                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("WM/ParentalRating")));
                Assert.AreEqual(true, StreamUtils.FindSequence(fs, Encoding.Unicode.GetBytes("M for Mature")));
            }

            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_Nero_Edit()
        {
            new ConsoleLogger();

            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                // Source : file with existing tag incl. chapters
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_NERO.mp4");
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                // Check if the two fields are indeed accessible
                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(4, theFile.NativeTag.Chapters.Count);

                Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

                ChapterInfo ch = new ChapterInfo();
                ch.StartTime = 0;
                ch.Title = "Chapter One";
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 1139;
                ch.Title = "Chapter 2";
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 2728;
                ch.Title = "Chapter 003";
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 3269;
                ch.Title = "Chapter 四";
                expectedChaps.Add(ch.StartTime, ch);

                int found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(ChapterInfo.FORMAT.Nero, chap.Format);
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    }
                    else
                    {
                        System.Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(4, found);


                // Modify elements
                TagData theTag = new TagData();

                theTag.Chapters = new List<ChapterInfo>();
                expectedChaps.Clear();

                ch = new ChapterInfo();
                ch.StartTime = 123;
                ch.Title = "aaa";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 1230;
                ch.Title = "aaa0";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    }
                    else
                    {
                        System.Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);


                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_Nero_Create()
        {
            new ConsoleLogger();

            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                // Source : file without 'chpl' atom
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                Assert.IsTrue(theFile.ReadFromFile());

                Assert.IsNotNull(theFile.getMeta(tagType));
                Assert.IsFalse(theFile.getMeta(tagType).Exists);

                // Modify elements
                TagData theTag = new TagData();

                Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

                theTag.Chapters = new List<ChapterInfo>();

                ChapterInfo ch = new ChapterInfo();
                ch.StartTime = 123;
                ch.Title = "aaa";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 1230;
                ch.Title = "aaa0";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                int found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(ChapterInfo.FORMAT.Nero, chap.Format);
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    }
                    else
                    {
                        System.Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_meta_Create()
        {
            new ConsoleLogger();

            // Source : file without 'chpl' atom
            String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_NERO.mp4");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Modify elements
            TagHolder theTag = new TagHolder();

            theTag.Title = "test_meta_atom";

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("test_meta_atom", theFile.NativeTag.Title);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Edit()
        {
            new ConsoleLogger();

            ATL.Settings.MP4_createNeroChapters = false;
            try
            {
                // Source : file with existing tag incl. chapters
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_QT.m4v");
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                // 1- Read / Check if both fields are indeed accessible
                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(4, theFile.NativeTag.Chapters.Count);

                Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

                ChapterInfo ch = new ChapterInfo(0, "Chapter One");
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(1139, "Chapter 2");
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(2728, "Chapter 003");
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(3269, "Chapter 四");
                expectedChaps.Add(ch.StartTime, ch);

                int found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    }
                    else
                    {
                        Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(4, found);

                // 2- Modify timecode and title
                TagData theTag = new TagData();

                theTag.Chapters = new List<ChapterInfo>();
                expectedChaps.Clear();

                ch = new ChapterInfo(0, "aaa");
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(1230, "aaa0四");
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    }
                    else
                    {
                        Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // 3- Add pictures over existing timecode + title chapters
                theTag = new TagData();

                theTag.Chapters = new List<ChapterInfo>();
                expectedChaps.Clear();

                ch = new ChapterInfo(0, "aaa");
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
                ch.Picture.ComputePicHash();
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(1230, "aaa0四");
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg"));
                ch.Picture.ComputePicHash();
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                        Assert.IsNotNull(chap.Picture);
                        chap.Picture.ComputePicHash();
                        Assert.AreEqual(chap.Picture.PictureHash, expectedChaps[chap.StartTime].Picture.PictureHash);
                    }
                    else
                    {
                        Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // 4- Modify existing picture
                theTag = new TagData();

                theTag.Chapters = new List<ChapterInfo>();
                expectedChaps.Clear();

                ch = new ChapterInfo(0, "aaa");
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
                ch.Picture.ComputePicHash();
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo(1230, "aaa0四");
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg")); // <-- modified compared to 3-
                ch.Picture.ComputePicHash();
                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                        Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                        Assert.IsNotNull(chap.Picture);
                        chap.Picture.ComputePicHash();
                        Assert.AreEqual(chap.Picture.PictureHash, expectedChaps[chap.StartTime].Picture.PictureHash);
                    }
                    else
                    {
                        Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Create()
        {
            new ConsoleLogger();

            ATL.Settings.MP4_createNeroChapters = false;
            try
            {
                // Source : file without 'chpl' atom
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                Assert.IsTrue(theFile.ReadFromFile());

                Assert.IsNotNull(theFile.getMeta(tagType));
                Assert.IsFalse(theFile.getMeta(tagType).Exists);

                // Modify elements
                TagData theTag = new TagData();

                Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

                theTag.Chapters = new List<ChapterInfo>();

                ChapterInfo ch = new ChapterInfo();
                ch.StartTime = 0;
                ch.Title = "aaa";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 1230;
                ch.Title = "aaa0";

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(false, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                int found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(ChapterInfo.FORMAT.QT, chap.Format);
                        Assert.AreEqual(expectedChaps[chap.StartTime].StartTime, chap.StartTime);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Title, chap.Title);
                    }
                    else
                    {
                        System.Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Pic_Create()
        {
            new ConsoleLogger();

            ATL.Settings.MP4_createNeroChapters = false;
            try
            {
                // Source : file without 'chpl' atom
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                Assert.IsTrue(theFile.ReadFromFile());

                Assert.IsNotNull(theFile.getMeta(tagType));
                Assert.IsFalse(theFile.getMeta(tagType).Exists);

                // Modify elements
                TagData theTag = new TagData();

                Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

                theTag.Chapters = new List<ChapterInfo>();

                ChapterInfo ch = new ChapterInfo();
                ch.StartTime = 0;
                ch.Title = "aaa";
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
                ch.Picture.ComputePicHash();

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                ch = new ChapterInfo();
                ch.StartTime = 10000;
                ch.Title = "aaa0";
                ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"));
                ch.Picture.ComputePicHash();

                theTag.Chapters.Add(ch);
                expectedChaps.Add(ch.StartTime, ch);

                // Check if they are persisted properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                Assert.IsTrue(theFile.ReadFromFile(true, true));
                Assert.IsNotNull(theFile.NativeTag);
                Assert.IsTrue(theFile.NativeTag.Exists);

                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);

                // Check if values are the same
                int found = 0;
                foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
                {
                    if (expectedChaps.ContainsKey(chap.StartTime))
                    {
                        found++;
                        Assert.AreEqual(ChapterInfo.FORMAT.QT, chap.Format);
                        Assert.AreEqual(expectedChaps[chap.StartTime].StartTime, chap.StartTime);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Title, chap.Title);
                        Assert.IsNotNull(chap.Picture);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.PicType, chap.Picture.PicType);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.MimeType, chap.Picture.MimeType);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.PictureData.Length, chap.Picture.PictureData.Length);
                    }
                    else
                    {
                        Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(2, found);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Pic_Warnings()
        {
            new ConsoleLogger();
            ArrayLogger log = new ArrayLogger();

            // Source : file without 'chpl' atom
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            Assert.IsFalse(theFile.getMeta(tagType).Exists);

            // Modify elements
            TagData theTag = new TagData();

            Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

            theTag.Chapters = new List<ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 111;
            ch.Title = "aaa";

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 10000;
            ch.Title = "aaa0";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            // Check if proper warnings have been issued
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            IList<LogItem> logItems = log.GetAllItems(LV_WARNING);
            Assert.IsTrue(logItems.Count > 0);
            int nbFound = 0;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("First chapter start time is > 0:00")) nbFound++;
            }
            Assert.AreEqual(1, nbFound);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        // Test behaviour when creating empty chapter pics
        // Cases
        // A: No chapter track should be created if there's no chapter
        // B: No chapter picture (video) track should be created if none of the chapters have an attached picture
        // C: No empty chapter picture should be generated if all chapters that contain a picture come one after the other
        // D: An empty chapter picture should be generated for any chapter without pictures that's included between chapters that have one
        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Pic_DynamicPics()
        {
            new ConsoleLogger();
            ArrayLogger log = new ArrayLogger();

            // Source : file without 'chpl' atom
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            Assert.IsFalse(theFile.getMeta(tagType).Exists);

            // Case A
            TagData theTag = new TagData();
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsFalse(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
            }

            // Case B
            theTag = new TagData();

            theTag.Chapters = new List<ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 111;
            ch.Title = "aaa";

            theTag.Chapters.Add(ch);

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsFalse(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter pictures")));
            }


            // Case C
            var dynamicPicSegment = new byte[] { 0xff, 0xdb, 0x00, 0x43, 0x00, 0x01 };
            theTag = new TagData();

            theTag.Chapters = new List<ChapterInfo>();

            ch = new ChapterInfo();
            ch.StartTime = 111;
            ch.Title = "aaa";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 222;
            ch.Title = "bbb";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);

            // Two chapters with pictures => Shouldn't create a dynamic pic
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter pictures")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsFalse(StreamUtils.FindSequence(s, dynamicPicSegment));
            }

            ch = new ChapterInfo();
            ch.StartTime = 333;
            ch.Title = "ccc";
            theTag.Chapters.Add(ch);

            // Three chapters; the first two with pictures => Shouldn't create a dynamic pic
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter pictures")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsFalse(StreamUtils.FindSequence(s, dynamicPicSegment));
            }

            // Case D
            theTag = new TagData();

            theTag.Chapters = new List<ChapterInfo>();

            ch = new ChapterInfo();
            ch.StartTime = 111;
            ch.Title = "aaa";

            theTag.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 222;
            ch.Title = "bbb";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);

            // Two chapters; the first without picture, the 2nd with a picture => Should create a dynamic pic
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter pictures")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, dynamicPicSegment));
            }
            

            theTag = new TagData();

            theTag.Chapters = new List<ChapterInfo>();

            ch = new ChapterInfo();
            ch.StartTime = 111;
            ch.Title = "aaa";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 222;
            ch.Title = "bbb";

            theTag.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 333;
            ch.Title = "ccc";
            ch.Picture = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg"));
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);

            // Three chapters; 1 and 3 with a picture, 2 without => Should create a dynamic pic
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            using (FileStream s = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter titles")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("Chapter pictures")));
                s.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(s, dynamicPicSegment));
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_CapNero()
        {
            new ConsoleLogger();

            // Source :file with 260 Quicktime chapters and 255 Nero chapters (capped by some apps)
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_260qt255nero.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            bool initialCap = ATL.Settings.MP4_capNeroChapters;
            int initialChapRead = ATL.Settings.MP4_readChaptersFormat;
            ATL.Settings.MP4_capNeroChapters = true;
            ATL.Settings.MP4_readChaptersFormat = 0;
            try
            {
                Assert.IsTrue(theFile.ReadFromFile());
                Assert.IsNotNull(theFile.getMeta(tagType));
                IMetaDataIO meta = theFile.getMeta(tagType);
                Assert.IsTrue(meta.Exists);

                // Check if the chapters presented by ATL are the ones from the format that has most (QT here)
                Assert.AreEqual(260, meta.Chapters.Count);

                // Check if Nero chapters are correctly capped to 255 when the option is on
                TagData tagData = new TagData();
                Assert.IsTrue(theFile.UpdateTagInFileAsync(tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                ATL.Settings.MP4_readChaptersFormat = 3; // Check Nero chapters only
                Assert.IsTrue(theFile.ReadFromFile());
                Assert.IsNotNull(theFile.getMeta(tagType));
                meta = theFile.getMeta(tagType);
                Assert.IsTrue(meta.Exists);

                Assert.AreEqual(255, meta.Chapters.Count);

                ATL.Settings.MP4_capNeroChapters = false;
                ATL.Settings.MP4_readChaptersFormat = 0; // Read them all again for the update to work properly
                Assert.IsTrue(theFile.UpdateTagInFileAsync(tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                ATL.Settings.MP4_readChaptersFormat = 3; // Check Nero chapters only
                Assert.IsTrue(theFile.ReadFromFile());
                Assert.IsNotNull(theFile.getMeta(tagType));
                meta = theFile.getMeta(tagType);
                Assert.IsTrue(meta.Exists);

                Assert.AreEqual(260, meta.Chapters.Count);
            }
            finally
            {
                ATL.Settings.MP4_capNeroChapters = initialCap;
                ATL.Settings.MP4_readChaptersFormat = initialChapRead;
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Lyrics_Unsynched()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/lyrics.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.IsTrue(theFile.NativeTag.Lyrics.UnsynchronizedLyrics.StartsWith("JAPANESE:\r\n\r\n煙と雲\r\n\r\n世の中を"));

            // Write
            TagData theTag = new TagData();
            theTag.Lyrics = new LyricsInfo();
            theTag.Lyrics.UnsynchronizedLyrics = "Государственный гимн\r\nРоссийской Федерации";

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.AreEqual(theTag.Lyrics.UnsynchronizedLyrics, theFile.NativeTag.Lyrics.UnsynchronizedLyrics);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void checkUnsynchLyrics(AudioDataManager theFile)
        {
            Assert.IsTrue(0 == theFile.NativeTag.Lyrics.UnsynchronizedLyrics.Length);

            Assert.AreEqual(5, theFile.NativeTag.Lyrics.Metadata.Count);
            Assert.AreEqual("Chubby Checker oppure  Beatles, The", theFile.NativeTag.Lyrics.Metadata["ar"]);
            Assert.AreEqual("Hits Of The 60's - Vol. 2 – Oldies", theFile.NativeTag.Lyrics.Metadata["al"]);
            Assert.AreEqual("Let's Twist Again", theFile.NativeTag.Lyrics.Metadata["ti"]);
            Assert.AreEqual("Written by Kal Mann / Dave Appell, 1961", theFile.NativeTag.Lyrics.Metadata["au"]);
            Assert.AreEqual("2:23", theFile.NativeTag.Lyrics.Metadata["length"]);

            Assert.AreEqual(3, theFile.NativeTag.Lyrics.SynchronizedLyrics.Count);
            Assert.AreEqual("Naku Penda Piya-Naku Taka Piya-Mpenziwe", theFile.NativeTag.Lyrics.SynchronizedLyrics[0].Text);
            Assert.AreEqual(12000, theFile.NativeTag.Lyrics.SynchronizedLyrics[0].TimestampMs);
            Assert.AreEqual("Some more lyrics", theFile.NativeTag.Lyrics.SynchronizedLyrics[1].Text);
            Assert.AreEqual(15030, theFile.NativeTag.Lyrics.SynchronizedLyrics[1].TimestampMs);
            Assert.AreEqual("Even more lyrics", theFile.NativeTag.Lyrics.SynchronizedLyrics[2].Text);
            Assert.AreEqual(83045, theFile.NativeTag.Lyrics.SynchronizedLyrics[2].TimestampMs);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Lyrics_LRC()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/lyrics_LRC.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            checkUnsynchLyrics(theFile);

            // Write
            TagHolder theTag = new TagHolder();
            theTag.Title = "blah";

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            checkUnsynchLyrics(theFile);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Multiple_Values()
        {
            new ConsoleLogger();

            // == 1st variant : repetition of the same metadata structure
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/multiple_artists.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("ArtistA" + ATL.Settings.InternalValueSeparator + "ArtistB" + ATL.Settings.InternalValueSeparator + "Demo", theFile.NativeTag.Artist);

            // Write
            TagHolder theTag = new TagHolder();
            theTag.Title = "blah";

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            // Check if separated values are still intact after rewriting
            Assert.AreEqual("ArtistA" + ATL.Settings.InternalValueSeparator + "ArtistB" + ATL.Settings.InternalValueSeparator + "Demo", theFile.NativeTag.Artist);

            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 2nd variant : repetition of the 'data' atom inside one single field
            testFileLocation = TestUtils.CopyAsTempTestFile("MP4/multiple_artists2.m4a");
            theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("ArtistA" + ATL.Settings.InternalValueSeparator + "ArtistB" + ATL.Settings.InternalValueSeparator + "Demo2", theFile.NativeTag.Artist);

            // Write
            theTag = new TagHolder();
            theTag.Title = "blah";

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            // Check if separated values are still intact after rewriting
            Assert.AreEqual("ArtistA" + ATL.Settings.InternalValueSeparator + "ArtistB" + ATL.Settings.InternalValueSeparator + "Demo2", theFile.NativeTag.Artist);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_R_MP4_Rating()
        {
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.mp4", 0, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.5.mp4", 0.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.mp4", 1.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.5.mp4", 1.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.mp4", 2.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.5.mp4", 2.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.mp4", 3.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.5.mp4", 3.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.mp4", 4.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.5.mp4", 4.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/5.mp4", 1, MetaDataIOFactory.TagType.NATIVE);

            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.mp4", 0, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.5.mp4", 0.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.mp4", 1.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.5.mp4", 1.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.mp4", 2.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.5.mp4", 2.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.mp4", 3.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.5.mp4", 3.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.mp4", 4.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.5.mp4", 4.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/5.mp4", 1, MetaDataIOFactory.TagType.NATIVE);
        }

        [TestMethod]
        public void TagIO_RW_MP4_XtraFields()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/xtraField.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual((float)(4.0 / 5), theFile.NativeTag.Popularity);
            Assert.AreEqual("conductor", theFile.NativeTag.Conductor);
            Assert.AreEqual(5, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/SharedUserRating"));
            Assert.AreEqual("80", theFile.NativeTag.AdditionalFields["WM/SharedUserRating"]); // ASF (MP4) convention
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/Publisher"));
            Assert.AreEqual("editor", theFile.NativeTag.AdditionalFields["WM/Publisher"]);

            // Write
            TagHolder theTag = new TagHolder();
            theTag.Popularity = 3.0f / 5;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.AreEqual(5, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/SharedUserRating"));
            Assert.AreEqual("60", theFile.NativeTag.AdditionalFields["WM/SharedUserRating"]);  // ASF (MP4) convention
            Assert.AreEqual((float)3.0 / 5, theFile.NativeTag.Popularity);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/Publisher"));
            Assert.AreEqual("editor", theFile.NativeTag.AdditionalFields["WM/Publisher"]);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Uuid_Existing()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/uuid.m4a"); // XMP data with botched UUID to test generic UUID support
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("uuid.BEAACFCB97A942E89C71999491E3AFAC"));
            var data = theFile.NativeTag.AdditionalFields["uuid.BEAACFCB97A942E89C71999491E3AFAC"];
            Assert.AreEqual(3070, data.Length); // 3072 actual bytes, but 3070 UTF-8 characters
            Assert.IsTrue(data.StartsWith("<?xpacket begin=\"\ufeff\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"));
            Assert.IsTrue(data.EndsWith("<?xpacket end=\"w\"?>"));

            // Write
            TagHolder theTag = new TagHolder();
            theTag.Popularity = 3.0f / 5;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("uuid.BEAACFCB97A942E89C71999491E3AFAC"));
            data = theFile.NativeTag.AdditionalFields["uuid.BEAACFCB97A942E89C71999491E3AFAC"];
            Assert.AreEqual(3070, data.Length); // 3072 actual bytes, but 3070 UTF-8 characters
            Assert.IsTrue(data.StartsWith("<?xpacket begin=\"\ufeff\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>"));
            Assert.IsTrue(data.EndsWith("<?xpacket end=\"w\"?>"));

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Uuid_Empty()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            TagHolder theTag = new TagHolder();
            theTag.AdditionalFields = new Dictionary<string, string>
            {
                {"uuid.BEAACFCB97A942E89C71999491E3AFAC", "that's a very long story I'm about to tell you~"}
            };

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("uuid.BEAACFCB97A942E89C71999491E3AFAC"));
            var value = theFile.NativeTag.AdditionalFields["uuid.BEAACFCB97A942E89C71999491E3AFAC"];
            Assert.AreEqual("that's a very long story I'm about to tell you~", value);

            theTag.tagData.AdditionalFields[0].MarkedForDeletion = true;
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.AreEqual(0, theFile.NativeTag.AdditionalFields.Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Xmp_Existing()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/xmp.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            var xmpCount = theFile.NativeTag.AdditionalFields.Count(f => f.Key.StartsWith("xmp."));
            Assert.AreEqual(7, xmpCount); // Without namespaces
            var originalFields = theFile.NativeTag.AdditionalFields
                .Where(f => f.Key.StartsWith("xmp."))
                .ToDictionary(field => field.Key, field => field.Value);

            // Write
            TagHolder theTag = new TagHolder();
            theTag.Popularity = 3.0f / 5;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            var newFields = theFile.NativeTag.AdditionalFields
                .Where(f => f.Key.StartsWith("xmp."))
                .ToDictionary(field => field.Key, field => field.Value);

            Assert.AreEqual(originalFields.Count, newFields.Count);
            foreach (var key in originalFields.Keys)
            {
                Assert.IsTrue(newFields.ContainsKey(key), "Key not found : " + key);
                Assert.AreEqual(originalFields[key], newFields[key]);
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Xmp_Empty()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read nothing
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            // Write
            TagHolder theTag = new TagHolder();
            var originalFields = new Dictionary<string, string>
            {
                { "xmp.rdf:RDF.rdf:Description.xmp:CreateDate", "1904-01-01T00:00Z" },
                { "xmp.rdf:RDF.rdf:Description.xmpDM:duration.xmpDM:value", "1264768" }
            };
            theTag.AdditionalFields = originalFields;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            var newFields = theFile.NativeTag.AdditionalFields
                .Where(f => f.Key.StartsWith("xmp."))
                .Where(f => !f.Key.Contains(".xmlns:", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(field => field.Key, field => field.Value);

            Assert.AreEqual(originalFields.Count, newFields.Count);
            foreach (var key in originalFields.Keys)
            {
                Assert.IsTrue(newFields.ContainsKey(key));
                Assert.AreEqual(originalFields[key], newFields[key]);
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_InvalidValues()
        {
            ArrayLogger log = new ArrayLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/xtraField.m4a");
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Write letter as signed int
            TagHolder theTag = new TagHolder();
            IDictionary<string, string> data = new Dictionary<string, string>();
            data["©mvc"] = "a";
            theTag.AdditionalFields = data;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            IList<LogItem> logItems = log.GetAllItems(Log.LV_WARNING);
            Assert.IsTrue(logItems.Count > 0);
            bool found = false;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("value a could not be converted to integer; ignoring")) found = true;
            }
            Assert.IsTrue(found);

            // Write signed as unsigned int
            theTag = new TagHolder();
            data = new Dictionary<string, string>();
            data["tvsn"] = "-2";
            theTag.AdditionalFields = data;

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            logItems = log.GetAllItems(Log.LV_WARNING);
            Assert.IsTrue(logItems.Count > 0);
            found = false;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("value -2 could not be converted to unsigned integer; ignoring")) found = true;
            }
            Assert.IsTrue(found);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V1);
        }

        [TestMethod]
        public void TagIO_RW_MP4_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.APE);
        }

        private const long twoTracksQTchapsEmptySize = 133914;
        private const int nbLoops = 5;

        [TestMethod]
        public void TagIO_RW_MP4_RemoveTag()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");

            Track track = new Track(testFileLocation);
            double preDuration = track.DurationMs; Console.WriteLine("Pre Duration: " + preDuration);
            double preSize = TestUtils.GetFileSize(testFileLocation); Console.WriteLine("Pre File Length: " + preSize);
            track.Remove(MetaDataIOFactory.TagType.NATIVE);

            var log = new ArrayLogger();
            track = new Track(testFileLocation);
            double dPostLength = TestUtils.GetFileSize(testFileLocation);
            Console.WriteLine("POST Duration: " + track.DurationMs.ToString());
            Console.WriteLine("POST File Length: " + dPostLength);

            Assert.AreEqual(preDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(preSize > dPostLength, "File should be smaller.");
            // 8 extra bytes because the empty padding atom (`free` atom) isn't removed by design when using Track.Remove
            // as padding areas aren't considered as metadata per se, and are kept to facilitate file expansion
            Assert.AreEqual(twoTracksQTchapsEmptySize + 8, dPostLength, "File should be " + twoTracksQTchapsEmptySize + 8 + " once tags are removed.");

            foreach (LogItem l in log.GetAllItems(LV_ERROR)) Console.WriteLine("[E] " + l.Message);
            foreach (LogItem l in log.GetAllItems(LV_WARNING)) Console.WriteLine("[W] " + l.Message);
            Assert.AreEqual(0, log.GetAllItems(LV_ERROR).Count + log.GetAllItems(LV_WARNING).Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_AddChap2Image_then_RemoveTag()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");
            string testImageLocation = TestUtils.GetResourceLocationRoot() + "_Images/big.jpg";
            Track track = new Track(testFileLocation);

            System.Console.WriteLine("# Initial Details #");
            double tDuration = track.DurationMs; System.Console.WriteLine("Duration: " + tDuration);
            double dLength = TestUtils.GetFileSize(testFileLocation); System.Console.WriteLine("File Length: " + dLength);
            System.Console.WriteLine("Chapters: " + track.Chapters.Count.ToString());
            System.Console.WriteLine("Chapters(1) Image: " + (track.Chapters[0].Picture != null));
            System.Console.WriteLine("Chapters(2) Image: " + (track.Chapters[1].Picture != null));

            System.Console.WriteLine("# Chap 2 Image added #");
            track.Chapters[1].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(testImageLocation));
            track.Save();
            track = new Track(testFileLocation);
            System.Console.WriteLine("Duration: " + track.DurationMs);
            System.Console.WriteLine("File Length: " + TestUtils.GetFileSize(testFileLocation));
            System.Console.WriteLine("Chapters: " + track.Chapters.Count.ToString());
            System.Console.WriteLine("Chapters(1) Image: " + (track.Chapters[0].Picture != null));
            System.Console.WriteLine("Chapters(2) Image: " + (track.Chapters[1].Picture != null));

            //Switch these Assertions for expected editing.
            Assert.IsTrue(track.Chapters[0].Picture != null && 734 == track.Chapters[0].Picture.PictureData.Length); // 1x1 default pic from ATL
            Assert.IsTrue(track.Chapters[1].Picture != null);

            System.Console.WriteLine("# Remove Tags #");
            new ConsoleLogger();
            track.Remove(ATL.AudioData.MetaDataIOFactory.TagType.NATIVE);
            track = new Track(testFileLocation);
            double dPostLength = TestUtils.GetFileSize(testFileLocation);
            System.Console.WriteLine("Duration: " + track.DurationMs.ToString());
            System.Console.WriteLine("File Length: " + dPostLength);
            System.Console.WriteLine("Chapters: " + track.Chapters.Count.ToString());
            if (track.Chapters.Count > 0)
            {
                System.Console.WriteLine("Chapters(1) Image: " + (track.Chapters[0].Picture != null));
                System.Console.WriteLine("Chapters(2) Image: " + (track.Chapters[1].Picture != null));
            }

            Assert.AreEqual(tDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(dLength > dPostLength, "File should be smaller.");
            // File 
            Assert.AreEqual(twoTracksQTchapsEmptySize, dPostLength, "File should be " + twoTracksQTchapsEmptySize + " once tags are removed - As per test TagIO_RW_MP4_RemoveTag.");

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /// <summary>
        /// Test Track.Remove() and editing still works using a loop.
        /// </summary>
        [TestMethod]
        public void TagIO_RW_MP4_RemoveTag_AddMetaLoop()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");
            string testImageLocation = TestUtils.GetResourceLocationRoot() + "_Images/big.jpg";

            Track track = new Track(testFileLocation);
            double tDuration = track.DurationMs; Console.WriteLine("Pre Duration: " + tDuration);
            double dLength = TestUtils.GetFileSize(testFileLocation); Console.WriteLine("Pre File Length: " + dLength);

            track.Remove(MetaDataIOFactory.TagType.NATIVE);

            track = new Track(testFileLocation);
            double dPostLength = TestUtils.GetFileSize(testFileLocation);
            Console.WriteLine("Clear Duration: " + track.DurationMs.ToString());
            Console.WriteLine("Clear File Length: " + dPostLength);

            Assert.AreEqual(tDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(dLength > dPostLength, "File should be smaller.");
            // 8 extra bytes because the empty padding atom (`free` atom) isn't removed by design when using Track.Remove
            // as padding areas aren't considered as metadata per se, and are kept to facilitate file expansion
            Assert.AreEqual(twoTracksQTchapsEmptySize + 8, dPostLength, $"File should be {twoTracksQTchapsEmptySize + 8} once tags are removed.");

            //Add Meta again
            bool WithErrors = false;
            for (int n = 0; n <= nbLoops; n++)
            {
                System.Console.WriteLine($"Save {n}: ");
                var log = new ArrayLogger();
                track = new Track(testFileLocation);
                track.Description = "New Description" + n.ToString();
                track.Title = "New Title" + n.ToString();
                track.Album = "New Album" + n.ToString();
                Action<float> progress = x => System.Console.WriteLine(x.ToString());
                if (!track.Save(progress))
                    Assert.Fail("Failed to save.");
                System.Console.WriteLine($"ErrorLOG: {n} ");
                foreach (Logging.Log.LogItem l in log.GetAllItems(Logging.Log.LV_ERROR))
                    System.Console.WriteLine("- " + l.Message);
                WithErrors = (WithErrors || log.GetAllItems(Logging.Log.LV_ERROR).Count > 0);
            }

            track = new Track(testFileLocation);
            dPostLength = TestUtils.GetFileSize(testFileLocation);
            System.Console.WriteLine("POST Add Duration: " + track.DurationMs.ToString());
            System.Console.WriteLine("POST Add File Length: " + dPostLength);
            Assert.AreEqual($"New Description{nbLoops}", track.Description, "Description should be the same.");
            Assert.AreEqual($"New Title{nbLoops}", track.Title, "Title should be the same.");
            Assert.AreEqual($"New Album{nbLoops}", track.Album, "Album should be the same.");

            if (WithErrors) Assert.Fail("There were errors noted in the Logs on saving;");

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /// <summary>
        /// Test Track.Remove() and editing still works using a loop.
        /// </summary>
        [TestMethod]
        public void TagIO_RW_MP4_RemoveTag_AddMetaAndChapImagesLoop()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");
            string testImageLocation1 = TestUtils.GetResourceLocationRoot() + "_Images/big.jpg";
            string testImageLocation2 = TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg";

            Track track = new Track(testFileLocation);
            double tDuration = track.DurationMs; Console.WriteLine("Pre Duration: " + tDuration);
            double dLength = TestUtils.GetFileSize(testFileLocation); Console.WriteLine("Pre File Length: " + dLength);

            track.Remove(MetaDataIOFactory.TagType.NATIVE);

            track = new Track(testFileLocation);
            double dPostLength = TestUtils.GetFileSize(testFileLocation);
            Console.WriteLine("Clear Duration: " + track.DurationMs.ToString());
            Console.WriteLine("Clear File Length: " + dPostLength);

            Assert.AreEqual(tDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(dLength > dPostLength, "File should be smaller.");
            // 8 extra bytes because the empty padding atom (`free` atom) isn't removed by design when using Track.Remove
            // as padding areas aren't considered as metadata per se, and are kept to facilitate file expansion
            Assert.AreEqual(twoTracksQTchapsEmptySize + 8, dPostLength, $"File should be {twoTracksQTchapsEmptySize + 8} once tags are removed.");

            bool WithErrors = false;
            //Add Meta again
            System.Console.WriteLine("Initial Save Meta: ");
            var log = new ArrayLogger();
            track = new Track(testFileLocation);
            track.Description = "New Description";
            track.Title = "New Title";
            track.Album = "New Album";
            track.Chapters = new List<ChapterInfo>();
            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 0;
            ch.Title = "New Chap0";
            ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"));
            track.Chapters.Add(ch);
            ch = new ChapterInfo();
            ch.StartTime = 10000;
            ch.Title = "New Chap1";
            ch.Picture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"));
            track.Chapters.Add(ch);
            Action<float> progress = x => Console.WriteLine(x.ToString());
            new ConsoleLogger();
            if (!track.Save(progress))
                Assert.Fail("Failed to save.");
            Console.WriteLine("ErrorLOG: ");
            foreach (LogItem l in log.GetAllItems(LV_ERROR))
                Console.WriteLine("- " + l.Message);
            WithErrors = (WithErrors || log.GetAllItems(LV_ERROR).Count > 0);

            PictureInfo picture1 = PictureInfo.fromBinaryData(File.ReadAllBytes(testImageLocation1));
            PictureInfo picture2 = PictureInfo.fromBinaryData(File.ReadAllBytes(testImageLocation2));

            //Add Meta again
            for (int n = 0; n <= nbLoops; n++)
            {
                Console.WriteLine($"Save {n}: ");
                log = new ArrayLogger();
                track = new Track(testFileLocation);
                Assert.IsTrue(track.Description.Length > 0, "Description not found!");
                track.Description = "New Description" + n.ToString();
                track.Title = "New Title" + n.ToString();
                track.Album = "New Album" + n.ToString();
                Assert.AreEqual(2, track.Chapters.Count, "Chapters not found!");
                track.Chapters[0].Title = "New Chap0-" + n.ToString();
                track.Chapters[0].Picture = (n % 2 > 0) ? picture1 : picture2;
                track.Chapters[1].Title = "New Chap1-" + n.ToString();
                track.Chapters[1].Picture = (n % 2 > 0) ? picture1 : picture2;
                progress = x => Console.WriteLine(x.ToString());
                if (!track.Save(progress))
                    Assert.Fail("Failed to save.");
                Console.WriteLine($"ErrorLOG: {n} ");
                foreach (LogItem l in log.GetAllItems(LV_ERROR))
                    Console.WriteLine("- " + l.Message);
                WithErrors = (WithErrors || log.GetAllItems(LV_ERROR).Count > 0);
            }

            track = new Track(testFileLocation);
            dPostLength = TestUtils.GetFileSize(testFileLocation);
            Console.WriteLine("POST Add Duration: " + track.DurationMs.ToString());
            Console.WriteLine("POST Add File Length: " + dPostLength);
            Assert.AreEqual($"New Description{nbLoops}", track.Description, "Description should be the same.");
            Assert.AreEqual($"New Title{nbLoops}", track.Title, "Title should be the same.");
            Assert.AreEqual($"New Album{nbLoops}", track.Album, "Album should be the same.");
            Assert.AreEqual($"New Chap0-{nbLoops}", track.Chapters[0].Title, "Title 0 should be the same.");
            Assert.AreEqual($"New Chap1-{nbLoops}", track.Chapters[1].Title, "Title 1 should be the same.");

            if (WithErrors) Assert.Fail("There were errors noted in the Logs on saving;");

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /// <summary>
        /// Test Track.Remove() and editing still works using a loop.
        /// </summary>
        [TestMethod]
        public void TagIO_RW_MP4_ChangeMetaAndChapLoop()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");

            Track track = new Track(testFileLocation);
            double tDuration = track.DurationMs; Console.WriteLine("Pre Duration: " + tDuration);
            double dLength = TestUtils.GetFileSize(testFileLocation); Console.WriteLine("Pre File Length: " + dLength);

            //Update Meta again
            bool WithErrors = false;
            for (int n = 0; n <= nbLoops; n++)
            {
                Console.WriteLine($"Save {n}: ");
                var log = new ArrayLogger();
                track = new Track(testFileLocation);
                track.Description = "New Description" + n.ToString();
                track.Title = "New Title" + n.ToString();
                track.Album = "New Album" + n.ToString();
                track.Chapters[0].Title = "New Chap0-" + n.ToString();
                track.Chapters[1].Title = "New Chap1-" + n.ToString();
                Action<float> progress = x => Console.WriteLine(x.ToString());
                if (!track.Save(progress))
                    Assert.Fail("Failed to save.");
                Console.WriteLine($"ErrorLOG: {n} ");
                foreach (LogItem l in log.GetAllItems(LV_ERROR))
                    Console.WriteLine("- " + l.Message);
                WithErrors = WithErrors || log.GetAllItems(LV_ERROR).Count > 0;
            }

            track = new Track(testFileLocation);
            Console.WriteLine("POST Add Duration: " + track.DurationMs.ToString());
            Console.WriteLine("POST Add File Length: " + TestUtils.GetFileSize(testFileLocation));
            Assert.AreEqual($"New Description{nbLoops}", track.Description, "Description should be the same.");
            Assert.AreEqual($"New Title{nbLoops}", track.Title, "Title should be the same.");
            Assert.AreEqual($"New Album{nbLoops}", track.Album, "Album should be the same.");
            Assert.AreEqual($"New Chap0-{nbLoops}", track.Chapters[0].Title, "Album should be the same.");
            Assert.AreEqual($"New Chap1-{nbLoops}", track.Chapters[1].Title, "Album should be the same.");

            if (WithErrors) Assert.Fail("There were errors noted in the Logs on saving;");

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /// <summary>
        /// Test Track.Remove() and editing with chapters and images still works using a loop.
        /// </summary>
        [TestMethod]
        public void TagIO_RW_MP4_RemoveTag_AddMetaAndChap2Images_RemoveTag()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP4/2tracks_QTchaps.m4a");

            Track track = new Track(testFileLocation);
            double tDuration = track.DurationMs; Console.WriteLine("Pre Duration: " + tDuration);
            double dLength = TestUtils.GetFileSize(testFileLocation); Console.WriteLine("Pre File Length: " + dLength);

            //1. Remove Tag first
            track.RemoveAsync(MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult();
            track = new Track(testFileLocation);
            double dPostLength = TestUtils.GetFileSize(testFileLocation);
            System.Console.WriteLine("Clear Duration: " + track.DurationMs.ToString());
            System.Console.WriteLine("Clear File Length: " + dPostLength);

            Assert.AreEqual(tDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(dLength > dPostLength, "File should be smaller.");
            // 8 extra bytes because the empty padding atom (`free` atom) isn't removed by design when using Track.Remove
            // as padding areas aren't considered as metadata per se, and are kept to facilitate file expansion
            Assert.AreEqual(twoTracksQTchapsEmptySize + 8, dPostLength, $"File should be {twoTracksQTchapsEmptySize + 8} once tags are removed.");

            bool WithErrors = false;
            //2. Add Meta again and Image to chap 2 only
            System.Console.WriteLine("Initial Save Meta: ");
            var log = new ArrayLogger();
            track = new Track(testFileLocation);
            track.Description = "New Description";
            track.Title = "New Title";
            track.Album = "New Album";
            track.Chapters = new List<ChapterInfo>();
            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 0;
            ch.Title = "New Chap0";
            //ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"));
            track.Chapters.Add(ch);
            ch = new ChapterInfo();
            ch.StartTime = 10000;
            ch.Title = "New Chap1";
            ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"));
            track.Chapters.Add(ch);
            Action<float> progress = x => System.Console.WriteLine(x.ToString());
            if (track.Save(progress) == false)
                Assert.Fail("Failed to save.");
            System.Console.WriteLine("ErrorLOG: ");
            foreach (Logging.Log.LogItem l in log.GetAllItems(LV_ERROR))
                System.Console.WriteLine("- " + l.Message);
            WithErrors = (WithErrors || log.GetAllItems(LV_ERROR).Count > 0);

            track = new Track(testFileLocation); //Reload
            dPostLength = TestUtils.GetFileSize(testFileLocation);
            System.Console.WriteLine("POST Add Duration: " + track.DurationMs.ToString());
            System.Console.WriteLine("POST Add File Length: " + dPostLength);
            Assert.AreEqual($"New Description", track.Description, "Description should be the same.");
            Assert.AreEqual($"New Title", track.Title, "Title should be the same.");
            Assert.AreEqual($"New Album", track.Album, "Album should be the same.");
            Assert.AreEqual($"New Chap0", track.Chapters[0].Title, "Chapter0 Title should be the same.");
            Assert.AreEqual($"New Chap1", track.Chapters[1].Title, "Chapter1 should be the same.");
            Assert.IsTrue(track.Chapters[0].Picture != null && 734 == track.Chapters[0].Picture.PictureData.Length); // 1x1 default pic from ATL
            Assert.IsTrue(track.Chapters[1].Picture != null);

            //3. Remove Tag first
            track.Remove(MetaDataIOFactory.TagType.NATIVE);
            track = new Track(testFileLocation);
            double dPostLenghtEnd = TestUtils.GetFileSize(testFileLocation);
            System.Console.WriteLine("Clear Duration: " + track.DurationMs.ToString());
            System.Console.WriteLine("Clear File Length: " + dPostLength);
            Assert.AreEqual(tDuration, track.DurationMs, "Duration should be the same.");
            Assert.IsTrue(dLength > dPostLenghtEnd, "File should be smaller.");
            // Should be the same size as the empty file obtained at step 1
            Assert.AreEqual(twoTracksQTchapsEmptySize, dPostLenghtEnd, $"File should be {twoTracksQTchapsEmptySize} once tags are removed.");

            if (WithErrors) Assert.Fail("There were errors noted in the Logs on saving;");

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }
    }
}
