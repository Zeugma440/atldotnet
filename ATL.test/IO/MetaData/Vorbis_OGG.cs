using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using Commons;
using static ATL.PictureInfo;
using static ATL.Logging.Log;
using ATL.Logging;
using ATL.AudioData.IO;

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
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Individual picture removal (from index > 1)
     * 
     * Test multiplicity of field names
     * 
     * 
     * TECHNICAL
     * 
     * Exact picture data conservation after tag editing
     * 
     * 
    */


    [TestClass]
    public class Vorbis_OGG : MetaIOTest
    {
        public Vorbis_OGG()
        {
            emptyFile = "OGG/empty.ogg";
            notEmptyFile = "OGG/ogg.ogg";
            tagType = MetaDataIOFactory.TagType.NATIVE;

            testData.Conductor = null;
            testData.Date = DateTime.Parse("1997-06-20");
        }

        [TestMethod]
        public void TagIO_R_VorbisOGG_simple_OnePager()
        {
            TagIO_R_VorbisOGG_simple_OnePager(null);
        }

        public void TagIO_R_VorbisOGG_simple_OnePager(Stream stream)
        {
            new ConsoleLogger();

            AudioDataManager theFile;
            if (null == stream)
            {
                string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
                theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));
            }
            else
            {
                theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromMimeType("audio/ogg", "In-memory"), stream);
            }

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_R_VorbisOGG_simple_MultiplePager()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "OGG/bigPicture.ogg";
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 3);
        }

        [TestMethod]
        public void TagIO_R_FlacOGG()
        {
            new ConsoleLogger();
            AudioDataManager theFile;
            string location = TestUtils.GetResourceLocationRoot() + "OGG/embedded-flac.ogg";
            theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            string comment = testData.Comment;
            IList<PictureInfo> pictureInfos = testData.EmbeddedPictures;
            try
            {
                // OGG-FLAC sample has its COMMENT and DESCRIPTION metadata scrambled, and no pictures
                testData.GeneralDescription = comment;
                testData.Comment = null;
                testData.EmbeddedPictures = new List<PictureInfo>();
                readExistingTagsOnFile(theFile, 0);
            }
            finally
            {
                testData.GeneralDescription = null;
                testData.Comment = comment;
                testData.EmbeddedPictures = pictureInfos;
            }
        }

        [TestMethod]
        public void TagIO_R_FlacTheora()
        {
            new ConsoleLogger();
            AudioDataManager theFile;
            string location = TestUtils.GetResourceLocationRoot() + "OGG/theora-flac.ogg";
            theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            string comment = testData.Comment;
            IList<PictureInfo> pictureInfos = testData.EmbeddedPictures;
            try
            {
                // Theora-FLAC sample has its COMMENT and DESCRIPTION metadata scrambled, and no pictures
                testData.GeneralDescription = comment;
                testData.Comment = null;
                testData.EmbeddedPictures = new List<PictureInfo>();
                readExistingTagsOnFile(theFile, 0);
            }
            finally
            {
                testData.GeneralDescription = null;
                testData.Comment = comment;
                testData.EmbeddedPictures = pictureInfos;
            }
        }

        [TestMethod]
        public void TagIO_RW_FlacOGG()
        {
            new ConsoleLogger();
            string location = "OGG/embedded-flac.ogg";

            string comment = testData.Comment;
            IList<PictureInfo> pictureInfos = testData.EmbeddedPictures;
            try
            {
                // OGG-FLAC sample has its COMMENT and DESCRIPTION metadata scrambled, and no pictures
                testData.GeneralDescription = comment;
                testData.Comment = null;
                testData.EmbeddedPictures = new List<PictureInfo>();
                tagIO_RW_VorbisOGG_Existing(location, 0);
            }
            finally
            {
                testData.GeneralDescription = null;
                testData.Comment = comment;
                testData.EmbeddedPictures = pictureInfos;
            }
            /*
            ArrayLogger log = new ArrayLogger();
            string fileName = "OGG/embedded-flac.ogg";
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            try
            {
                theFile.ReadFromFile();

                                
            }
            finally
            {
                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            */
        }

        [TestMethod]
        public void TagIO_R_VorbisOGG_dirtyTrackDiscNumbering()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "OGG/ogg_dirtyTrackDiscNumbering.ogg";
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile, 2);
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_Empty()
        {
            TagIO_RW_VorbisOGG_Empty(null);
        }

        public void TagIO_RW_VorbisOGG_Empty(Stream stream)
        {
            new ConsoleLogger();

            // Source : totally metadata-free OGG
            AudioDataManager theFile;
            string location;
            string testFileLocation;
            Stream streamCopy;

            if (null == stream)
            {
                location = TestUtils.GetResourceLocationRoot() + emptyFile;
                testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
                streamCopy = null;
                theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            }
            else
            {
                location = "";
                testFileLocation = "";
                streamCopy = new MemoryStream();
                stream.CopyTo(streamCopy);
                theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromMimeType(".ogg", "In-memory"), stream);
            }


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);

            // Construct a new tag
            TagHolder theTag = new TagHolder();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.Date = DateTime.Parse("2008/01/01"); // <-- TODO : this field is _not_ valued when passing through Track + beware of alternate formattings depending on the format
            theTag.Genre = "Merengue";
            theTag.TrackNumber = 1;
            theTag.TrackTotal = 2;
            theTag.DiscNumber = 3;
            theTag.DiscTotal = 4;
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual("Test !!", theFile.NativeTag.Title);
            Assert.AreEqual("Album", theFile.NativeTag.Album);
            Assert.AreEqual("Artist", theFile.NativeTag.Artist);
            Assert.AreEqual("Mike", theFile.NativeTag.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.NativeTag.Comment);
            Assert.AreEqual(2008, theFile.NativeTag.Date.Year);
            Assert.AreEqual("Merengue", theFile.NativeTag.Genre);
            Assert.AreEqual(1, theFile.NativeTag.TrackNumber);
            Assert.AreEqual(2, theFile.NativeTag.TrackTotal);
            Assert.AreEqual(3, theFile.NativeTag.DiscNumber);
            Assert.AreEqual(4, theFile.NativeTag.DiscTotal);
            Assert.AreEqual("Me", theFile.NativeTag.Composer);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.NativeTag.Conductor);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            if (null == stream)
            {
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                string originalMD5 = TestUtils.GetFileMD5Hash(location);
                string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                Assert.IsTrue(originalMD5.Equals(testMD5));

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            else
            {
                Assert.AreEqual(stream.Length, streamCopy.Length);

                // Not possible due to tag order issues
                /*
                string originalMD5 = TestUtils.GetFileMD5Hash(location);
                string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);
                Assert.IsTrue(originalMD5.Equals(testMD5));
                */

                streamCopy.Close();
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_Existing_OnePager()
        {
            ATL.Settings.AddNewPadding = true;
            ATL.Settings.PaddingSize = 2042; // Padding size in OGG test files

            try
            {
                tagIO_RW_VorbisOGG_Existing(notEmptyFile, 2);
                //                test_RW_Existing(notEmptyFile, 2, true, true);
            }
            finally
            {
                ATL.Settings.AddNewPadding = false;
                ATL.Settings.PaddingSize = 2048;
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_Existing_MultiplePager()
        {
            ATL.Settings.AddNewPadding = true;
            ATL.Settings.PaddingSize = 2042; // Padding size in OGG test files

            try
            {
                tagIO_RW_VorbisOGG_Existing("OGG/bigPicture.ogg", 3);
            }
            finally
            {
                ATL.Settings.AddNewPadding = false;
                ATL.Settings.PaddingSize = 2048;
            }
        }

        private void tagIO_RW_VorbisOGG_Existing(string fileName, int initialNbPictures, bool deleteTempFile = true)
        {
            new ConsoleLogger();

            // Source : OGG with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagHolder theTag = new TagHolder();
            theTag.Conductor = "John Jackman";

            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PictureInfo.PIC_TYPE.CD);
            var testPics = theTag.EmbeddedPictures;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE));

            readExistingTagsOnFile(theFile, initialNbPictures + 1);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.NativeTag.Conductor);

            int nbFound = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.CD))
                {
                    Assert.AreEqual(0x06, pic.NativePicCode);
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    nbFound++;
                    break;
                }
            }
            Assert.AreEqual(initialNbPictures > 0 ? 1 : 0, nbFound);

            // Remove the additional supported field
            theTag = new TagHolder();
            theTag.Conductor = "";

            // Remove additional picture
            picInfo = new PictureInfo(PIC_TYPE.CD);
            picInfo.MarkedForDeletion = true;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE));

            readExistingTagsOnFile(theFile, initialNbPictures);

            // Additional removed field
            Assert.AreEqual("", theFile.NativeTag.Conductor);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            // Not possible due to tag order issues
            /*
            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);
            Assert.IsTrue(originalMD5.Equals(testMD5));
            */

            // Get rid of the working copy
            if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void checkTrackDiscZeroes(FileStream fs)
        {
            using (BinaryReader r = new BinaryReader(fs))
            {
                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKNUMBER=")));
                String s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("06", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKTOTAL=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("06", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCNUMBER=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("03", s.Substring(0, s.Length - 1));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCTOTAL=")));
                s = StreamUtils.ReadNullTerminatedString(r, System.Text.Encoding.ASCII);
                Assert.AreEqual("04", s.Substring(0, s.Length - 1));
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_UpdateKeepTrackDiscZeroes()
        {
            ATL.Settings.AddNewPadding = true;
            try
            {
                StreamDelegate dlg = new StreamDelegate(checkTrackDiscZeroes);
                test_RW_UpdateTrackDiscZeroes(notEmptyFile, false, false, dlg);
            }
            finally
            {
                ATL.Settings.AddNewPadding = false;
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_UpdateFormatTrackDiscZeroes()
        {
            StreamDelegate dlg = new StreamDelegate(checkTrackDiscZeroes);
            test_RW_UpdateTrackDiscZeroes(notEmptyFile, true, true, dlg);
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_Unsupported_Empty()
        {
            // Source : tag-free OGG
            String testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST2", "This is another test 父"));

            // Add new unsupported pictures
            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TagType.NATIVE, 0xAA);
            theTag.Pictures.Add(picInfo);
            picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TagType.NATIVE, 0xAB);
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(3, theFile.NativeTag.AdditionalFields.Count); // 3 instead of 2 because of the VENDOR field... (specific to VorbisTag)

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAA))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(System.Drawing.Imaging.ImageFormat.Jpeg, picture.RawFormat);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
                else if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
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

            // Remove the additional unsupported field
            theTag = new TagData();
            MetaFieldInfo fieldInfo = new MetaFieldInfo(MetaDataIOFactory.TagType.NATIVE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new PictureInfo(MetaDataIOFactory.TagType.NATIVE, 0xAA);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count); // 2 instead of 1 because of the VENDOR field...
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

            found = 0;

            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
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


            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_VorbisOGG_Chapters()
        {
            tagIO_RW_VorbisOGG_Chapters("OGG/chapters.ogg");
            tagIO_RW_VorbisOGG_Chapters("OGG/chapters_0indexed.ogg");
        }
        private void tagIO_RW_VorbisOGG_Chapters(string file)
        {
            new ConsoleLogger();

            // Source : OGG with existing tag incl. chapters
            string testFileLocation = TestUtils.CopyAsTempTestFile(file);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(9, theFile.NativeTag.Chapters.Count);

            Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 0;
            ch.Title = "Intro";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 15000;
            ch.Title = "Creating a new production";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/engine/upload/");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 22000;
            ch.Title = "Sound analysis";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 34000;
            ch.Title = "Adaptive leveler";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/audio_examples#leveler");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 45000;
            ch.Title = "Global loudness normalization";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/audio_examples#loudnorm");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 60000;
            ch.Title = "Audio restoration algorithms";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/audio_examples#denoise");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 76000;
            ch.Title = "Output file formats";
            ch.Url = new ChapterInfo.UrlInfo("http://auphonic.com/blog/5/");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 94000;
            ch.Title = "External services";
            ch.Url = new ChapterInfo.UrlInfo("http://auphonic.com/blog/16/");
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 111500;
            ch.Title = "Get a free account!";
            ch.Url = new ChapterInfo.UrlInfo("https://auphonic.com/accounts/register");
            expectedChaps.Add(ch.StartTime, ch);

            int found = 0;
            foreach (ChapterInfo chap in theFile.NativeTag.Chapters)
            {
                if (expectedChaps.ContainsKey(chap.StartTime))
                {
                    found++;
                    Assert.AreEqual(expectedChaps[chap.StartTime].StartTime, chap.StartTime);
                    Assert.AreEqual(expectedChaps[chap.StartTime].Title, chap.Title);
                    if (expectedChaps[chap.StartTime].Url != null)
                    {
                        Assert.AreEqual(expectedChaps[chap.StartTime].Url.Url, chap.Url.Url);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Url.Description, chap.Url.Description);
                    }
                }
                else
                {
                    System.Console.WriteLine(chap.StartTime);
                }
            }
            Assert.AreEqual(9, found);


            // Modify elements
            TagData theTag = new TagData();
            theTag.Chapters = new List<ChapterInfo>();
            expectedChaps.Clear();

            ch = new ChapterInfo();
            ch.StartTime = 123;
            ch.Title = "aaa";
            ch.Url = new ChapterInfo.UrlInfo("ddd");

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 1230;
            ch.Title = "aaa0";
            ch.Url = new ChapterInfo.UrlInfo("ddd0");

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TagType.NATIVE));

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
                    Assert.AreEqual(expectedChaps[chap.StartTime].StartTime, chap.StartTime);
                    Assert.AreEqual(expectedChaps[chap.StartTime].Title, chap.Title);
                    if (expectedChaps[chap.StartTime].Url != null)
                    {
                        Assert.AreEqual(expectedChaps[chap.StartTime].Url.Url, chap.Url.Url);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Url.Description, chap.Url.Description);
                    }
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

        [TestMethod]
        public void TagIO_R_Vorbis_Rating()
        {
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.ogg", 0, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.5.ogg", 0.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.ogg", 1.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.5.ogg", 1.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.ogg", 2.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.5.ogg", 2.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.ogg", 3.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.5.ogg", 3.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.ogg", 4.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.5.ogg", 4.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/5.ogg", 1, MetaDataIOFactory.TagType.NATIVE);

            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.ogg", 0, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.5.ogg", 0.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.ogg", 1.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.5.ogg", 1.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.ogg", 2.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.5.ogg", 2.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.ogg", 3.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.5.ogg", 3.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.ogg", 4.0 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.5.ogg", 4.5 / 5, MetaDataIOFactory.TagType.NATIVE);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/5.ogg", 1, MetaDataIOFactory.TagType.NATIVE);
        }


        // No cohabitation here since other tags are not supported in OGG files
    }
}
