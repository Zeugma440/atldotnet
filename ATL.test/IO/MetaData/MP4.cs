using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;
using static ATL.PictureInfo;

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
            tagType = MetaDataIOFactory.TAG_NATIVE;

            // MP4 does not support leading zeroes
            testData.TrackNumber = "1";
            testData.TrackTotal = "2";
            testData.DiscNumber = "3";
            testData.DiscTotal = "4";
            testData.RecordingDate = "1997-06-20T00:00:00"; // No timestamp in MP4 date format
            testData.Conductor = null; // TODO - Should be supported; extended field makes it harder to manipulate by the generic test code
            testData.Publisher = null;
            testData.Genre = "Household"; // "House" was generating a 'gnre' numeric field whereas ATL standard way of tagging is '(c)gen' string field => Start with a non-standard Genre

            testData.AdditionalFields.Clear();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "----:com.apple.iTunes:TEST", "xxx"));

            testData.Pictures.Clear();
            PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TAG_ANY, 13);
            pic.ComputePicHash();
            testData.Pictures.Add(pic);

            supportsDateOrYear = true;
        }


        [TestMethod]
        public void TagIO_R_MP4()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : M4A with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));
            readExistingTagsOnFile(theFile, 1);


            location = TestUtils.GetResourceLocationRoot() + "MP4/mp4_date_in_©day.m4a";
            theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));
            readExistingTagsOnFile(theFile, 1);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Empty_no_udta()
        {
            test_RW_Empty("MP4/no_udta.m4a", true, false, false); // ATL leaves an empty udta/meta structure, which is more "standard" than wiping the entire udta branch
        }

        [TestMethod]
        public void TagIO_RW_MP4_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (MOOD)
            String testFileLocation = TestUtils.CopyAsTempTestFile(notEmptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Conductor = "John Jackman";

            byte[] data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.png");
            PictureInfo picInfo = PictureInfo.fromBinaryData(data, PictureInfo.PIC_TYPE.Generic, MetaDataIOFactory.TAG_ANY, 14);
            theTag.Pictures.Add(picInfo);

            theTag.Chapters = theFile.NativeTag.Chapters;
            theTag.Chapters.Add(new ChapterInfo(3000, "Chapter 2"));

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));


            try
            {
                // Read Quicktime chapters specifically
                ATL.Settings.MP4_readChaptersExclusive = 1;
                Assert.IsTrue(theFile.ReadFromFile(true, true));
                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);
                Assert.AreEqual((uint)0, theFile.NativeTag.Chapters[0].StartTime); // 1st Quicktime chapter can't start at position > 0
                Assert.AreEqual("aa父bb", theFile.NativeTag.Chapters[0].Title);
                Assert.AreEqual((uint)2945, theFile.NativeTag.Chapters[1].StartTime); // Approximate due to the way timecodes are formatted in the MP4 format
                Assert.AreEqual("Chapter 2", theFile.NativeTag.Chapters[1].Title);

                // Read Nero chapters specifically
                ATL.Settings.MP4_readChaptersExclusive = 2;
                Assert.IsTrue(theFile.ReadFromFile(true, true));
                Assert.AreEqual(2, theFile.NativeTag.Chapters.Count);
                Assert.AreEqual((uint)55, theFile.NativeTag.Chapters[0].StartTime);
                Assert.AreEqual("aa父bb", theFile.NativeTag.Chapters[0].Title);
                Assert.AreEqual((uint)3000, theFile.NativeTag.Chapters[1].StartTime);
                Assert.AreEqual("Chapter 2", theFile.NativeTag.Chapters[1].Title);
            } finally
            {
                ATL.Settings.MP4_readChaptersExclusive = 0;
            }


            // Read the rest supported fields
            readExistingTagsOnFile(theFile, 2);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.NativeTag.Conductor);

            byte nbFound = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic) && (1 == nbFound))
                {
                    Image picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Width, 175);
                    Assert.AreEqual(picture.Height, 168);
                }
                nbFound++;
            }

            Assert.AreEqual(2, nbFound);

            // Remove the additional supported field
            theTag = new TagData();
            theTag.Conductor = "";

            // Remove additional picture
            picInfo = new PictureInfo(PictureInfo.PIC_TYPE.Back);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            readExistingTagsOnFile(theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.NativeTag.Conductor);


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
        public void TagIO_RW_MP4_Unsupported_Empty()
        {
            // Source : tag-free M4A
            String testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TES2", "This is another test 父"));

            // Add new unsupported pictures
            PictureInfo picInfo = PictureInfo.fromBinaryData(
                File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"),
                PIC_TYPE.Unsupported,
                MetaDataIOFactory.TAG_NATIVE,
                "1234");
            theTag.Pictures.Add(picInfo);
            picInfo = PictureInfo.fromBinaryData(
                File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"),
                PIC_TYPE.Unsupported,
                MetaDataIOFactory.TAG_NATIVE,
                "5678");
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TES2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic) && (0 == found)) // No custom nor categorized picture type in MP4
                {
                    Image picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
                else if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic) && (1 == found))  // No custom nor categorized picture type in MP4
                {
                    Image picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(2, found);

            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TAG_NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new PictureInfo(PictureInfo.PIC_TYPE.Generic, 1);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TES2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TES2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

            found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic) && (0 == found))
                {
                    Image picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(1, found);


            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_Nero_Edit()
        {
            ConsoleLogger log = new ConsoleLogger();

            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                // Source : MP3 with existing tag incl. chapters
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_NERO.mp4");
                AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

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
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

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
            ConsoleLogger log = new ConsoleLogger();

            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                // Source : file without 'chpl' atom
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
                AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

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
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

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
            ConsoleLogger log = new ConsoleLogger();

            // Source : file without 'chpl' atom
            String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_NERO.mp4");
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Modify elements
            TagData theTag = new TagData();

            theTag.Title = "test_meta_atom";

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

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
            ConsoleLogger log = new ConsoleLogger();

            ATL.Settings.MP4_createNeroChapters = false;
            try
            {
                // Source : MP3 with existing tag incl. chapters
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/chapters_QT.m4v");
                AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                // Check if the two fields are indeed accessible
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
                        System.Console.WriteLine(chap.StartTime);
                    }
                }
                Assert.AreEqual(4, found);

                // Modify elements
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
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

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
                ATL.Settings.MP4_createNeroChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_MP4_Chapters_QT_Create()
        {
            ConsoleLogger log = new ConsoleLogger();

            ATL.Settings.MP4_createNeroChapters = false;
            try
            {
                // Source : file without 'chpl' atom
                String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/empty.m4a");
                AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

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
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));

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
        public void TagIO_RW_MP4_Lyrics_Unsynched()
        {
            ConsoleLogger log = new ConsoleLogger();

            String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/lyrics.m4a");
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.IsTrue(theFile.NativeTag.Lyrics.UnsynchronizedLyrics.StartsWith("JAPANESE:\r\n\r\n煙と雲\r\n\r\n世の中を"));

            // Write
            TagData theTag = new TagData();
            theTag.Lyrics = new LyricsInfo();
            theTag.Lyrics.UnsynchronizedLyrics = "Государственный гимн\r\nРоссийской Федерации";

            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.AreEqual(theTag.Lyrics.UnsynchronizedLyrics, theFile.NativeTag.Lyrics.UnsynchronizedLyrics);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_R_MP4_Rating()
        {
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.mp4", 0, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.5.mp4", 0.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.mp4", 1.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.5.mp4", 1.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.mp4", 2.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.5.mp4", 2.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.mp4", 3.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.5.mp4", 3.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.mp4", 4.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.5.mp4", 4.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/5.mp4", 1, MetaDataIOFactory.TAG_NATIVE);

            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.mp4", 0, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.5.mp4", 0.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.mp4", 1.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.5.mp4", 1.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.mp4", 2.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.5.mp4", 2.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.mp4", 3.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.5.mp4", 3.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.mp4", 4.0 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.5.mp4", 4.5 / 5, MetaDataIOFactory.TAG_NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/5.mp4", 1, MetaDataIOFactory.TAG_NATIVE);
        }

        [TestMethod]
        public void TagIO_RW_MP4_XtraFields()
        {
            ConsoleLogger log = new ConsoleLogger();

            String testFileLocation = TestUtils.CopyAsTempTestFile("MP4/xtraField.m4a");
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Read
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual((float)(4.0 / 5), theFile.NativeTag.Popularity);
            Assert.AreEqual("conductor", theFile.NativeTag.Conductor);
            Assert.AreEqual(6, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/SharedUserRating"));
            Assert.AreEqual("80", theFile.NativeTag.AdditionalFields["WM/SharedUserRating"]); // ASF (MP4) convention
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/Publisher"));
            Assert.AreEqual("editor", theFile.NativeTag.AdditionalFields["WM/Publisher"]);

            // Write
            TagData theTag = new TagData();
            theTag.Rating = 3.0 + "";

            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_NATIVE));
            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.AreEqual(6, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/SharedUserRating"));
            Assert.AreEqual("60", theFile.NativeTag.AdditionalFields["WM/SharedUserRating"]);  // ASF (MP4) convention
            Assert.AreEqual((float)3.0 / 5, theFile.NativeTag.Popularity);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.ContainsKey("WM/Publisher"));
            Assert.AreEqual("editor", theFile.NativeTag.AdditionalFields["WM/Publisher"]);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_MP4_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_NATIVE, MetaDataIOFactory.TAG_ID3V1);
        }
        /* No longer supported
        [TestMethod]
        public void TagIO_RW_MP4_ID3v2()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_NATIVE, MetaDataIOFactory.TAG_ID3V2);
        }
        */

        [TestMethod]
        public void TagIO_RW_MP4_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_NATIVE, MetaDataIOFactory.TAG_APE);
        }
    }
}
