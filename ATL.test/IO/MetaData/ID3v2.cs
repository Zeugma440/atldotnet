using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using Commons;

namespace ATL.test.IO.MetaData
{
    /*
     * TODO TECHNICAL TESTS
     * 
     * Add a standard unsupported field => persisted as standard field in tag
     * Add a non-standard unsupported field => persisted as TXXX field
     * 
    */
    [TestClass]
    public class ID3v2 : MetaIOTest
    {
        public ID3v2()
        {
            emptyFile = "MP3/empty.mp3";
            notEmptyFile = "MP3/id3v2.3_UTF16.mp3";
            tagType = MetaDataIOFactory.TAG_ID3V2;
        }

        [TestMethod]
        public void TagIO_R_ID3v22_simple()
        {
            string location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 ANSI charset only.mp3";
            AudioDataManager theFile = new AudioDataManager( AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location) );

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("noTagnoTag", theFile.ID3v2.Title);
            Assert.AreEqual("ALBUM!", theFile.ID3v2.Album);
            Assert.AreEqual("ARTIST", theFile.ID3v2.Artist);
            Assert.AreEqual("ALBUMARTIST", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("I have no IDE and i must code", theFile.ID3v2.Comment);
            Assert.AreEqual("1997", theFile.ID3v2.Year);
            Assert.AreEqual("House", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual(2, theFile.ID3v2.TrackTotal);
            Assert.AreEqual("COMP!", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);
            Assert.AreEqual(3, theFile.ID3v2.DiscTotal);

            // Pictures
            Assert.AreEqual(1, theFile.ID3v2.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.ID3v2.EmbeddedPictures)
            {
                Image picture;
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 656);
                    Assert.AreEqual(picture.Width, 552);
                    found++;
                }
            }

            Assert.AreEqual(1, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v22_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 UTF16.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("﻿bébé", theFile.ID3v2.Title);
            Assert.AreEqual("ALBUM!", theFile.ID3v2.Album);
            Assert.AreEqual("﻿父", theFile.ID3v2.Artist);
            Assert.AreEqual("ALBUMARTIST", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("﻿I have no IDE and i must code bébé 父", theFile.ID3v2.Comment);
            Assert.AreEqual("1997", theFile.ID3v2.Year);
            Assert.AreEqual("House", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual(2, theFile.ID3v2.TrackTotal);
            Assert.AreEqual("COMP!", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);
            Assert.AreEqual(3, theFile.ID3v2.DiscTotal);

            // Pictures
            Assert.AreEqual(1, theFile.ID3v2.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.ID3v2.EmbeddedPictures)
            {
                Image picture;
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 656);
                    Assert.AreEqual(picture.Width, 552);
                    found++;
                }
            }

            Assert.AreEqual(1, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v22_3pictures()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 3 pictures.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);


            // Pictures
            Assert.AreEqual(3, theFile.ID3v2.EmbeddedPictures.Count);
            byte found = 0;

            foreach (PictureInfo pic in theFile.ID3v2.EmbeddedPictures)
            {
                Image picture;
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = Image.FromStream(new MemoryStream(pic.PictureData));
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Height, 256);
                    Assert.AreEqual(picture.Width, 256);
                    found++;
                }
            }

            Assert.AreEqual(3, found);
        }

        [TestMethod]
        public void TagIO_R_ID3v23_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.3_UTF16.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_R_ID3v24_UTF8()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.4_UTF8.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v24_Extended()
        {
            ArrayLogger logger = new ArrayLogger();

            // Source : MP3 with extended tag properties (tag restrictions)
            string testFileLocation = TestUtils.GetTempTestFile("MP3/id3v2.4_UTF8_extendedTag.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that the presence of an extended tag does not disrupt field reading
            readExistingTagsOnFile(theFile);

            Settings.ID3v2_useExtendedHeaderRestrictions = true;

            try
            {
                // Insert a very long field while tag restrictions specify that string shouldn't be longer than 30 characters
                TagData theTag = new TagData();
                theTag.Conductor = "Veeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeery long field";

                // Insert a large picture while tag restrictions specify that pictures shouldn't be larger than 64x64pixels AND tag size shouldn't be larger than 4 KB
                PictureInfo picInfo = new PictureInfo(Commons.ImageFormat.Jpeg, PictureInfo.PIC_TYPE.Back);
                picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
                theTag.Pictures.Add(picInfo);

                // Insert a gif picture while tag restrictions specify that pictures should be either jpeg or png
                picInfo = new PictureInfo(Commons.ImageFormat.Gif, PictureInfo.PIC_TYPE.Back);
                picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif");
                theTag.Pictures.Add(picInfo);

                // Insert 20 garbage fields to raise the number of field above maximum required fields (30)
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA01", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA02", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA03", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA04", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA05", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA06", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA07", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA08", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA09", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA10", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA11", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA12", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA13", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA14", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA15", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA16", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA17", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA18", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA19", "aaa"));
                theTag.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ID3V2, "GA20", "aaa"));


                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

                // Get rid of the working copy
                File.Delete(testFileLocation);
            }
            finally
            {
                Settings.ID3v2_useExtendedHeaderRestrictions = false;
            }

            bool isAlertFieldLength = false;
            bool isAlertTagSize = false;
            bool isAlertNbFrames = false;
            bool isAlertPicDimension = false;
            bool isAlertPicType = false;

            foreach (Logging.Log.LogItem logItem in logger.Items)
            {
                if (logItem.Message.Contains("is longer than authorized")) isAlertFieldLength = true;
                if (logItem.Message.StartsWith("Tag is too large")) isAlertTagSize = true;
                if (logItem.Message.StartsWith("Tag has too many frames")) isAlertNbFrames = true;
                if (logItem.Message.EndsWith("does not respect ID3v2 restrictions (exactly 64x64)")) isAlertPicDimension = true;
                if (logItem.Message.EndsWith("does not respect ID3v2 restrictions (jpeg or png required)")) isAlertPicType = true;
            }

            Assert.IsTrue(isAlertFieldLength);
            Assert.IsTrue(isAlertTagSize);
            Assert.IsTrue(isAlertNbFrames);
            Assert.IsTrue(isAlertPicDimension);
            Assert.IsTrue(isAlertPicType);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Empty()
        {
            test_RW_Empty(emptyFile, true, true, true);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Existing()
        {
            // Final size and hash checks NOT POSSIBLE YET mainly due to tag order and padding differences
            test_RW_Existing(notEmptyFile, 2, true, false, false);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Unsupported_Empty()
        {
            test_RW_Unsupported_Empty(emptyFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_CommentFields()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. comment fields (iTunNORM, iTunPGAP)
            String testFileLocation = TestUtils.GetTempTestFile("MP3/id3v2.2_iTunNORM-iTunPGAP.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual(4, theFile.ID3v2.AdditionalFields.Count);

            int found = 0;
            foreach (KeyValuePair<string,string> field in theFile.ID3v2.AdditionalFields)
            {
                if (field.Key.Equals("iTunNORM"))
                {
                    Assert.AreEqual(" 00000099 000000A2 000002F0 000002F4 0000002E 0000002E 00002E6E 00002C5C 00000017 00000017", field.Value); // Why an empty space at the beginning ?!
                    found++;
                }
                else if (field.Key.Equals("iTunPGAP"))
                {
                    Assert.AreEqual("1", field.Value);
                    found++;
                }
            }
            Assert.AreEqual(2, found);


            // Check if they are persisted as comment fields when editing the tag
            TagData theTag = new TagData();
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            // For this we need to open the file in binary mode and check that the two fields belong to a comment field
            byte[] readBytes = new byte[4];
            byte[] expected = Utils.Latin1Encoding.GetBytes("COMM");

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("iTunNORM")));
                fs.Seek(-22, SeekOrigin.Current);
                fs.Read(readBytes, 0, 4);
                Assert.IsTrue(StreamUtils.ArrEqualsArr(expected, readBytes));

                fs.Seek(0, SeekOrigin.Begin);
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("iTunPGAP")));
                fs.Seek(-22, SeekOrigin.Current);
                fs.Read(readBytes, 0, 4);
                Assert.IsTrue(StreamUtils.ArrEqualsArr(expected, readBytes));
            }

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_FieldCodev22Tov24()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing unsupported fields : RVA & TBP
            String testFileLocation = TestUtils.GetTempTestFile("MP3/id3v2.2_iTunNORM-iTunPGAP.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual("1997", theFile.ID3v2.Year);

            int found = 0;
            string rvaValue = "";
            string tbpValue = "";
            foreach (KeyValuePair<string, string> field in theFile.ID3v2.AdditionalFields)
            {
                if (field.Key.Equals("RVA"))
                {
                    rvaValue = field.Value;
                    found++;
                }
                else if (field.Key.Equals("TBP"))
                {
                    tbpValue = field.Value;
                    found++;
                }
            }
            Assert.AreEqual(2, found);

            // Check if they are persisted with proper ID3v2.4 field codes when editing the tag
            TagData theTag = new TagData();
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            // 1/ Check if values are the same
            found = 0;
            foreach (KeyValuePair<string, string> field in theFile.ID3v2.AdditionalFields)
            {
                if (field.Key.Equals("RVA2"))
                {
                    Assert.AreEqual(rvaValue, field.Value);
                    found++;
                }
                else if (field.Key.Equals("TBPM"))
                {
                    Assert.AreEqual(tbpValue, field.Value);
                    found++;
                }
            }
            Assert.AreEqual(2, found);

            Assert.AreEqual("1997", theFile.ID3v2.Year);


            // 2/ Check if they are indeed persisted as "classic" ID3v2 fields, and not as sub-codes inside a TXXX field
            byte[] readBytes = new byte[4];
            byte[] expected = Utils.Latin1Encoding.GetBytes("TXXX");

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("RVA2")));
                fs.Seek(-15, SeekOrigin.Current);
                fs.Read(readBytes, 0, 4);
                Assert.IsFalse(StreamUtils.ArrEqualsArr(expected, readBytes));

                fs.Seek(0, SeekOrigin.Begin);


                expected = Utils.Latin1Encoding.GetBytes("TDRC");

                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("1997")));
                fs.Seek(-15, SeekOrigin.Current);
                fs.Read(readBytes, 0, 4);
                Assert.IsTrue(StreamUtils.ArrEqualsArr(expected, readBytes));
            }

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Chapters()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : empty MP3
            String testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);

            Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

            TagData theTag = new TagData();
            theTag.Chapters = new List<ChapterInfo>();
            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 123;
            ch.StartOffset = 456;
            ch.EndTime = 789;
            ch.EndOffset = 101112;
            ch.UniqueID = "";
            ch.Title = "aaa";
            ch.Subtitle = "bbb";
            ch.Url = "ccc\0ddd";
            ch.Picture = new PictureInfo(ImageFormat.Jpeg, PictureInfo.PIC_TYPE.Generic);
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg");
            ch.Picture.PictureData = data;
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 1230;
            ch.StartOffset = 4560;
            ch.EndTime = 7890;
            ch.EndOffset = 1011120;
            ch.UniqueID = "002";
            ch.Title = "aaa0";
            ch.Subtitle = "bbb0";
            ch.Url = "ccc\0ddd0";

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual(2, theFile.ID3v2.Chapters.Count);

            // Check if values are the same
            int found = 0;
            foreach (ChapterInfo chap in theFile.ID3v2.Chapters)
            {
                if (expectedChaps.ContainsKey(chap.StartTime))
                {
                    found++;
                    if (1 == found) Assert.AreNotEqual(chap.UniqueID, expectedChaps[chap.StartTime].UniqueID); // ID of first chapter was empty; ATL has generated a random ID for it
                    else Assert.AreEqual(chap.UniqueID, expectedChaps[chap.StartTime].UniqueID);
                    Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                    Assert.AreEqual(chap.EndTime, expectedChaps[chap.StartTime].EndTime);
                    Assert.AreEqual(chap.StartOffset, expectedChaps[chap.StartTime].StartOffset);
                    Assert.AreEqual(chap.EndOffset, expectedChaps[chap.StartTime].EndOffset);
                    Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    Assert.AreEqual(chap.Subtitle, expectedChaps[chap.StartTime].Subtitle);
                    Assert.AreEqual(chap.Url, expectedChaps[chap.StartTime].Url);
                    if (expectedChaps[chap.StartTime].Picture != null)
                    {
                        Assert.IsNotNull(chap.Picture);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.PictureHash, chap.Picture.ComputePicHash());
                    }
                }
                else
                {
                    System.Console.WriteLine(chap.StartTime);
                }
            }
            Assert.AreEqual(2, found);

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Chapters_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. chapters
            String testFileLocation = TestUtils.GetTempTestFile("MP3/chapters.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual(9, theFile.ID3v2.Chapters.Count);

            Dictionary<uint, ChapterInfo> expectedChaps = new Dictionary<uint, ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 0;
            ch.Title = "Intro";
            ch.Url = "chapter url\0https://auphonic.com/";
            ch.Picture = new PictureInfo(ImageFormat.Jpeg, PictureInfo.PIC_TYPE.Generic);
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "MP3/chapterImage1.jpg");
            ch.Picture.PictureData = data;
            ch.Picture.ComputePicHash();
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 15000;
            ch.Title = "Creating a new production";
            ch.Url = "chapter url\0https://auphonic.com/engine/upload/";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 22000;
            ch.Title = "Sound analysis";
            ch.Url = "";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 34000;
            ch.Title = "Adaptive leveler";
            ch.Url = "chapter url\0https://auphonic.com/audio_examples%23leveler";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 45000;
            ch.Title = "Global loudness normalization";
            ch.Url = "chapter url\0https://auphonic.com/audio_examples%23loudnorm";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 60000;
            ch.Title = "Audio restoration algorithms";
            ch.Url = "chapter url\0https://auphonic.com/audio_examples%23denoise";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 76000;
            ch.Title = "Output file formats";
            ch.Url = "chapter url\0http://auphonic.com/blog/5/";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 94000;
            ch.Title = "External services";
            ch.Url = "chapter url\0http://auphonic.com/blog/16/";
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 111500;
            ch.Title = "Get a free account!";
            ch.Url = "chapter url\0https://auphonic.com/accounts/register";
            expectedChaps.Add(ch.StartTime, ch);

            int found = 0;
            foreach (ChapterInfo chap in theFile.ID3v2.Chapters)
            {
                if (expectedChaps.ContainsKey(chap.StartTime))
                {
                    found++;
                    Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                    Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    Assert.AreEqual(chap.Url, expectedChaps[chap.StartTime].Url);
                    if (expectedChaps[chap.StartTime].Picture != null)
                    {
                        Assert.IsNotNull(chap.Picture);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.PictureHash, chap.Picture.ComputePicHash());
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
            ch.StartOffset = 456;
            ch.EndTime = 789;
            ch.EndOffset = 101112;
            ch.UniqueID = "";
            ch.Title = "aaa";
            ch.Subtitle = "bbb";
            ch.Url = "ccc\0ddd";
            ch.Picture = new PictureInfo(ImageFormat.Jpeg, PictureInfo.PIC_TYPE.Generic);
            data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg");
            ch.Picture.PictureData = data;
            ch.Picture.ComputePicHash();

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            ch = new ChapterInfo();
            ch.StartTime = 1230;
            ch.StartOffset = 4560;
            ch.EndTime = 7890;
            ch.EndOffset = 1011120;
            ch.UniqueID = "002";
            ch.Title = "aaa0";
            ch.Subtitle = "bbb0";
            ch.Url = "ccc\0ddd0";

            theTag.Chapters.Add(ch);
            expectedChaps.Add(ch.StartTime, ch);

            // Check if they are persisted properly
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile(true, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual(2, theFile.ID3v2.Chapters.Count);

            // Check if values are the same
            found = 0;
            foreach (ChapterInfo chap in theFile.ID3v2.Chapters)
            {
                if (expectedChaps.ContainsKey(chap.StartTime))
                {
                    found++;
                    if (1 == found) Assert.AreNotEqual(chap.UniqueID, expectedChaps[chap.StartTime].UniqueID); // ID of first chapter was empty; ATL has generated a random ID for it
                    else Assert.AreEqual(chap.UniqueID, expectedChaps[chap.StartTime].UniqueID);
                    Assert.AreEqual(chap.StartTime, expectedChaps[chap.StartTime].StartTime);
                    Assert.AreEqual(chap.EndTime, expectedChaps[chap.StartTime].EndTime);
                    Assert.AreEqual(chap.StartOffset, expectedChaps[chap.StartTime].StartOffset);
                    Assert.AreEqual(chap.EndOffset, expectedChaps[chap.StartTime].EndOffset);
                    Assert.AreEqual(chap.Title, expectedChaps[chap.StartTime].Title);
                    Assert.AreEqual(chap.Subtitle, expectedChaps[chap.StartTime].Subtitle);
                    Assert.AreEqual(chap.Url, expectedChaps[chap.StartTime].Url);
                    if (expectedChaps[chap.StartTime].Picture != null)
                    {
                        Assert.IsNotNull(chap.Picture);
                        Assert.AreEqual(expectedChaps[chap.StartTime].Picture.PictureHash, chap.Picture.ComputePicHash());
                    }
                }
                else
                {
                    System.Console.WriteLine(chap.StartTime);
                }
            }
            Assert.AreEqual(2, found);

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_UrlFrames()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. chapters
            String testFileLocation = TestUtils.GetTempTestFile("MP3/chapters.mp3");
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check if the two fields are indeed accessible
            Assert.IsTrue(theFile.ReadFromFile(false, true));
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.IsTrue(theFile.ID3v2.AdditionalFields.ContainsKey("WPUB"));
            Assert.AreEqual(theFile.ID3v2.AdditionalFields["WPUB"], "http://auphonic.com/");

            // Check if URLs are persisted properly, i.e. without encoding byte
            TagData theTag = new TagData();
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            // 1/ Check value through ATL
            Assert.IsTrue(theFile.ID3v2.AdditionalFields.ContainsKey("WPUB"));
            Assert.AreEqual(theFile.ID3v2.AdditionalFields["WPUB"], "http://auphonic.com/");

            // 2/ Check absence of encoding field in the file itself
            byte[] readBytes = new byte[4];
            byte[] expected = Utils.Latin1Encoding.GetBytes("WPUB");

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
            {
                Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("WPUB")));
                fs.Seek(6, SeekOrigin.Current);
                Assert.IsTrue(fs.ReadByte() > 10); // i.e. byte is not a 'text encoding descriptor'
            }

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_R_ID3v2_Rating()
        {
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.mp3", 0, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/0.5.mp3", 0.5/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.mp3", 1.0/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/1.5.mp3", 1.5/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.mp3", 2.0/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/2.5.mp3", 2.5/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.mp3", 3.0/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/3.5.mp3", 3.5/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.mp3", 4.0/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/4.5.mp3", 4.5/5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/mediaMonkey_4.1.19.1859/5.mp3", 1, MetaDataIOFactory.TAG_ID3V2);

            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.mp3", 0, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/0.5.mp3", 0.5 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.mp3", 1.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/1.5.mp3", 1.5 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.mp3", 2.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/2.5.mp3", 2.5 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.mp3", 3.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/3.5.mp3", 3.5 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.mp3", 4.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/4.5.mp3", 4.5 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/musicBee_3.1.6512/5.mp3", 1, MetaDataIOFactory.TAG_ID3V2);

            assumeRatingInFile("_Ratings/windows7/0.mp3", 0, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/windows7/1.mp3", 1.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/windows7/2.mp3", 2.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/windows7/3.mp3", 3.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/windows7/4.mp3", 4.0 / 5, MetaDataIOFactory.TAG_ID3V2);
            assumeRatingInFile("_Ratings/windows7/5.mp3", 1, MetaDataIOFactory.TAG_ID3V2);
        }

        [TestMethod]
        public void TagIO_R_ID3v2_HugeTrackAlbumNum()
        {
            String location = TestUtils.GetResourceLocationRoot() + "MP3/hugeAlbumTrackNumber.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual(0, theFile.ID3v2.Disc);
            Assert.AreEqual(0, theFile.ID3v2.Track);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_ID3v1()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_ID3V2, MetaDataIOFactory.TAG_ID3V1);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_APE()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TAG_ID3V2, MetaDataIOFactory.TAG_APE);
        }
    }
}
