using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using ATL.test.IO.MetaData;
using System.Collections.Generic;
using static ATL.Logging.Log;

namespace ATL.test.IO
{
    [TestClass]
    public class HighLevel
    {
        [TestMethod]
        public void TagIO_R_Single_ID3v1()
        {
            bool crossreadingDefault = MetaDataIOFactory.GetInstance().CrossReading;
            int[] tagPriorityDefault = new int[MetaDataIOFactory.TAG_TYPE_COUNT];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = false;                            // default behaviour anyway
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be entirely read
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should not be read, since behaviour is single tag reading
            /* end set options */

            try
            {
                Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.mp3");

                Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
                Assert.AreEqual(0, theTrack.Year); // Specifically tagged as empty on the ID3v1 tag
            }
            finally
            {
                // Set back default settings
                MetaDataIOFactory.GetInstance().CrossReading = crossreadingDefault;
                MetaDataIOFactory.GetInstance().TagPriority = tagPriorityDefault;
            }
        }

        [TestMethod]
        public void TagIO_R_Multi()
        {
            bool crossreadingDefault = MetaDataIOFactory.GetInstance().CrossReading;
            int[] tagPriorityDefault = new int[MetaDataIOFactory.TAG_TYPE_COUNT];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = true;
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be the main source except for the Year field (empty on ID3v1)
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should be used for the Year field (valuated on ID3v2)
            /* end set options */

            try
            {
                Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.mp3");

                Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
                Assert.AreEqual(1984, theTrack.Year); // Empty on the ID3v1 tag => cross-reading should read it on ID3v2
            }
            finally
            {
                // Set back default settings
                MetaDataIOFactory.GetInstance().CrossReading = crossreadingDefault;
                MetaDataIOFactory.GetInstance().TagPriority = tagPriorityDefault;
            }
        }

        [TestMethod]
        public void TagIO_R_MultiplePictures()
        {
            Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "OGG/bigPicture.ogg");

            // Check if _all_ embedded pictures are accessible from Track
            Assert.AreEqual(3, theTrack.EmbeddedPictures.Count);
        }

        [TestMethod]
        public void TagIO_R_MetaFormat()
        {
            Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "AA/aa.aa");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("Native / AA", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/APE.mp3");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("APEtag v2", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 3 pictures.mp3");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("ID3v2.2", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/id3v1.mp3");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("ID3v1.1", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.mp3");
            Assert.AreEqual(2, theTrack.MetadataFormats.Count);
            Assert.AreEqual("ID3v2.3", theTrack.MetadataFormats[0].Name);
            Assert.AreEqual("ID3v1.1", theTrack.MetadataFormats[1].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "OGG/ogg.ogg");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("Native / Vorbis (OGG)", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "FLAC/flac.flac");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("Native / Vorbis (FLAC)", theTrack.MetadataFormats[0].Name);
        }

        [TestMethod]
        public void TagIO_RW_DeleteTag()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            Assert.IsTrue(theTrack.Remove(MetaDataIOFactory.TAG_ID3V2));

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(0, theTrack.Year); // Empty on the ID3v1 tag => should really come empty since ID3v2 tag has been removed

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /// <summary>
        /// Check if the given file keeps its integrity after a no-op/neutral update
        /// </summary>
        /// <param name="resource"></param>
        private void tagIO_RW_UpdateNeutral(string resource)
        {
            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack = new Track(testFileLocation);
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            // Check that the resulting file (working copy that has been processed) remains identical to the original file (i.e. no byte lost nor added)

            // 1- File length should be the same
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            // 2- File contents should be the same
            // NB : Due to field order differences, MD5 comparison is not possible yet

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateNeutral()
        {
            ATL.Settings.MP4_createNeroChapters = false;
            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                tagIO_RW_UpdateNeutral("MP3/id3v2.4_UTF8.mp3"); // ID3v2
                tagIO_RW_UpdateNeutral("DSF/dsf.dsf"); // ID3v2 in DSF
                tagIO_RW_UpdateNeutral("FLAC/flac.flac"); // Vorbis-FLAC
                tagIO_RW_UpdateNeutral("OGG/ogg.ogg"); // Vorbis-OGG
                tagIO_RW_UpdateNeutral("OPUS/opus.opus"); // OPUS
                tagIO_RW_UpdateNeutral("MP3/APE.mp3"); // APE
                // Native formats
                tagIO_RW_UpdateNeutral("VQF/vqf.vqf");
                tagIO_RW_UpdateNeutral("VGM/vgm.vgm");
                tagIO_RW_UpdateNeutral("SPC/spc.spc");
                tagIO_RW_UpdateNeutral("MP4/mp4.m4a");
                tagIO_RW_UpdateNeutral("WMA/wma.wma");
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        private void tagIO_RW_UpdateEmpty(string resource, bool supportsTrack = true)
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack = new Track(testFileLocation);

            // Simple field
            theTrack.Artist = "Hey ho";
            // Tricky fields that aren't managed with a 1-to-1 mapping
            theTrack.Year = 1944;
            theTrack.TrackNumber = 10;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual("Hey ho", theTrack.Artist);
            Assert.AreEqual(1944, theTrack.Year);
            if (supportsTrack) Assert.AreEqual(10, theTrack.TrackNumber);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateEmpty()
        {
            //            Settings.DefaultTagsWhenNoMetadata = new int[2] { AudioData.MetaDataIOFactory.TAG_NATIVE, AudioData.MetaDataIOFactory.TAG_ID3V2 };
            try
            {
                tagIO_RW_UpdateEmpty("MP3/empty.mp3"); // ID3v2
                tagIO_RW_UpdateEmpty("DSF/empty.dsf"); // ID3v2 in DSF
                tagIO_RW_UpdateEmpty("FLAC/empty.flac"); // Vorbis-FLAC
                tagIO_RW_UpdateEmpty("OGG/empty.ogg"); // Vorbis-OGG
                tagIO_RW_UpdateEmpty("OPUS/opus.opus"); // Opus
                tagIO_RW_UpdateEmpty("MP3/empty.mp3"); // APE
                // Native formats
                tagIO_RW_UpdateEmpty("VQF/empty.vqf");
                tagIO_RW_UpdateEmpty("VGM/empty.vgm", false);
                tagIO_RW_UpdateEmpty("SPC/empty.spc");

                tagIO_RW_UpdateEmpty("MP4/empty.m4a");
                tagIO_RW_UpdateEmpty("WMA/empty_full.wma");
            }
            finally
            {
                ATL.Settings.DefaultTagsWhenNoMetadata = new int[2] { ATL.AudioData.MetaDataIOFactory.TAG_ID3V2, ATL.AudioData.MetaDataIOFactory.TAG_NATIVE };
            }
        }

        private void tagIO_RW_UpdateTagBaseField(string resource, bool supportsDisc = true, bool supportsTotalTracksDiscs = true, bool supportsTrack = true)
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack = new Track(testFileLocation);

            // Simple field
            theTrack.Artist = "Hey ho";
            // Tricky fields that aren't managed with a 1-to-1 mapping
            theTrack.Year = 1944;
            theTrack.TrackNumber = 10;
            theTrack.TrackTotal = 20;
            theTrack.DiscNumber = 30;
            theTrack.DiscTotal = 40;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual("Hey ho", theTrack.Artist);
            Assert.AreEqual(1944, theTrack.Year);
            if (supportsTrack) Assert.AreEqual(10, theTrack.TrackNumber);
            if (supportsTotalTracksDiscs) Assert.AreEqual(20, theTrack.TrackTotal);
            if (supportsDisc) Assert.AreEqual(30, theTrack.DiscNumber);
            if (supportsTotalTracksDiscs) Assert.AreEqual(40, theTrack.DiscTotal);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagBaseField()
        {
            ATL.Settings.MP4_createNeroChapters = false;
            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                tagIO_RW_UpdateTagBaseField("MP3/id3v2.4_UTF8.mp3"); // ID3v2
                tagIO_RW_UpdateTagBaseField("DSF/dsf.dsf"); // ID3v2 in DSF
                tagIO_RW_UpdateTagBaseField("FLAC/flac.flac"); // Vorbis-FLAC
                tagIO_RW_UpdateTagBaseField("OGG/ogg.ogg"); // Vorbis-OGG
                tagIO_RW_UpdateTagBaseField("OPUS/opus.opus"); // Opus
                tagIO_RW_UpdateTagBaseField("MP3/APE.mp3"); // APE
                // Specific formats
                tagIO_RW_UpdateTagBaseField("VQF/vqf.vqf", false, false);
                tagIO_RW_UpdateTagBaseField("VGM/vgm.vgm", false, false, false);
                tagIO_RW_UpdateTagBaseField("SPC/spc.spc", false, false);
                tagIO_RW_UpdateTagBaseField("MP4/mp4.m4a");
                tagIO_RW_UpdateTagBaseField("WMA/wma.wma");
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        private void tagIO_RW_UpdatePadding(string resource, int paddingSize = 2048)
        {
            ATL.Settings.PaddingSize = paddingSize;
            try
            {
                ArrayLogger log = new ArrayLogger();
                string location = TestUtils.GetResourceLocationRoot() + resource;
                string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
                Track theTrack = new Track(testFileLocation);

                string originalTitle = theTrack.Title;

                // Test the use of padding without padding option on

                // A1- Check that the resulting file (working copy that has been processed) keeps the same quantity of bytes when adding data
                theTrack.Title = originalTitle + "1234567890";
                Assert.IsTrue(theTrack.Save());

                // A11- File length should be the same
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                // A12- No stream manipulation should have occurred
                IList<LogItem> logItems = log.GetAllItems(LV_DEBUG);
                foreach (LogItem item in logItems)
                {
                    if (item.Message.StartsWith("Disk stream operation : "))
                    {
                        Assert.Fail(item.Message);
                    }
                }

                // A2- Check that the resulting file (working copy that has been processed) keeps the same quantity of bytes when removing data
                theTrack.Title = originalTitle;
                Assert.IsTrue(theTrack.Save());

                // A21- File length should be the same
                originalFileInfo = new FileInfo(location);
                testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                // A22- No stream manipulation should have occurred
                logItems = log.GetAllItems(LV_DEBUG);
                foreach (LogItem item in logItems)
                {
                    if (item.Message.StartsWith("Disk stream operation : "))
                    {
                        Assert.Fail(item.Message);
                    }
                }

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.PaddingSize = 2048;
            }
        }

        private void tagIO_RW_AddPadding(string resource, int extraBytes = 0)
        {
            bool initEnablePadding = ATL.Settings.AddNewPadding;
            ATL.Settings.AddNewPadding = false;

            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack = new Track(testFileLocation);

            theTrack.Title = "a";
            Assert.IsTrue(theTrack.Save());

            long initialLength = new FileInfo(testFileLocation).Length;

            ATL.Settings.AddNewPadding = true;
            try
            {
                theTrack.Title = "b";
                Assert.IsTrue(theTrack.Save());

                // B1- Check that the resulting file size has been increased by the size of the padding
                Assert.AreEqual(initialLength + ATL.Settings.PaddingSize + extraBytes, new FileInfo(testFileLocation).Length);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.AddNewPadding = initEnablePadding;
            }
        }

        [TestMethod]
        public void TagIO_RW_Padding()
        {
            ATL.Settings.MP4_createNeroChapters = false;
            ATL.Settings.MP4_createQuicktimeChapters = false;
            try
            {
                tagIO_RW_UpdatePadding("MP3/id3v2.4_UTF8.mp3"); // padded ID3v2
                tagIO_RW_UpdatePadding("OGG/ogg.ogg"); // padded Vorbis-OGG
                tagIO_RW_UpdatePadding("MP4/mp4.m4a", 17100); // padded MP4
                tagIO_RW_UpdatePadding("FLAC/flac.flac", 4063); // padded Vorbis-FLAC

                tagIO_RW_AddPadding("MP3/empty.mp3");
                tagIO_RW_AddPadding("OGG/empty.ogg", 8); // 8 extra bytes for the segments table extension
                tagIO_RW_AddPadding("MP4/chapters_NERO.mp4");
                tagIO_RW_AddPadding("FLAC/empty.flac", ATL.Settings.PaddingSize + 4); // Additional padding for the ID3v2 tag + 4 bytes for VorbisComment's PADDING block header
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        [TestMethod]
        public void TagIO_RW_AddRemoveTagAdditionalField()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.AdditionalFields.Add("ABCD", "efgh");
            theTrack.AdditionalFields.Remove("TENC");
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(1, theTrack.AdditionalFields.Count); // TENC should have been removed
            Assert.IsTrue(theTrack.AdditionalFields.ContainsKey("ABCD"));
            Assert.AreEqual("efgh", theTrack.AdditionalFields["ABCD"]);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagAdditionalField()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.AdditionalFields["TENC"] = "update test";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(1, theTrack.AdditionalFields.Count);
            Assert.IsTrue(theTrack.AdditionalFields.ContainsKey("TENC"));
            Assert.AreEqual("update test", theTrack.AdditionalFields["TENC"]);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_AddRemoveTagPictures()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/id3v2.4_UTF8.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.EmbeddedPictures.RemoveAt(1); // Remove Conductor; Front Cover remains

            // Add CD
            PictureInfo newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif"), PictureInfo.PIC_TYPE.CD);
            theTrack.EmbeddedPictures.Add(newPicture);

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(2, theTrack.EmbeddedPictures.Count); // Front Cover, CD

            bool foundFront = false;
            bool foundCD = false;

            foreach (PictureInfo pic in theTrack.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Front)) foundFront = true;
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.CD)) foundCD = true;
            }

            Assert.IsTrue(foundFront);
            Assert.IsTrue(foundCD);

            // Remove all
            theTrack.EmbeddedPictures.Clear();
            Assert.IsTrue(theTrack.Save());
            theTrack = new Track(testFileLocation);
            Assert.AreEqual(0, theTrack.EmbeddedPictures.Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagPictures()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/id3v2.4_UTF8.mp3");
            Track theTrack = new Track(testFileLocation);

            // Update Front picture
            PictureInfo newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"), PictureInfo.PIC_TYPE.Front);
            theTrack.EmbeddedPictures.Add(newPicture);

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(2, theTrack.EmbeddedPictures.Count); // Front Cover, Conductor

            bool foundFront = false;
            bool foundConductor = false;

            foreach (PictureInfo pic in theTrack.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Front))
                {
                    foundFront = true;
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(900, picture.Width);
                        Assert.AreEqual(290, picture.Height);
                    }
                }
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported)) foundConductor = true;
            }

            Assert.IsTrue(foundFront);
            Assert.IsTrue(foundConductor);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_AddRemoveChapters()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/chapters.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.ChaptersTableDescription = "Content";
            theTrack.Chapters.RemoveAt(2);

            // Add new chapter
            ChapterInfo chapter = new ChapterInfo();
            chapter.StartTime = 440;
            chapter.StartOffset = 4400;
            chapter.EndTime = 880;
            chapter.EndOffset = 8800;
            chapter.UniqueID = "849849";
            chapter.Picture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"));
            theTrack.Chapters.Add(chapter);

            Assert.IsTrue(theTrack.Save());
            IList<ChapterInfo> chaptersSave = new List<ChapterInfo>(theTrack.Chapters);

            theTrack = new Track(testFileLocation);
            IList<PictureInfo> pics = theTrack.EmbeddedPictures; // Hack to load chapter pictures

            Assert.AreEqual("Content", theTrack.ChaptersTableDescription);
            Assert.AreEqual(chaptersSave.Count, theTrack.Chapters.Count);

            ChapterInfo readChapter;
            for (int i = 0; i < theTrack.Chapters.Count; i++)
            {
                readChapter = theTrack.Chapters[i];
                Assert.AreEqual(chaptersSave[i].StartOffset, readChapter.StartOffset);
                Assert.AreEqual(chaptersSave[i].StartTime, readChapter.StartTime);
                Assert.AreEqual(chaptersSave[i].EndOffset, readChapter.EndOffset);
                if (i < theTrack.Chapters.Count - 1) Assert.AreEqual(chaptersSave[i + 1].StartTime, readChapter.EndTime);
                Assert.AreEqual(chaptersSave[i].Title, readChapter.Title);
                Assert.AreEqual(chaptersSave[i].Subtitle, readChapter.Subtitle);
                Assert.AreEqual(chaptersSave[i].UniqueID, readChapter.UniqueID);
                if (chaptersSave[i].Url != null)
                {
                    Assert.AreEqual(chaptersSave[i].Url.Description, readChapter.Url.Description);
                    Assert.AreEqual(chaptersSave[i].Url.Url, readChapter.Url.Url);
                }
                if (chaptersSave[i].Picture != null)
                {
                    Assert.IsNotNull(readChapter.Picture);
                    Assert.AreEqual(chaptersSave[i].Picture.ComputePicHash(), readChapter.Picture.ComputePicHash());
                }
            }

            // Delete all
            theTrack.Chapters.Clear();
            Assert.IsTrue(theTrack.Save());
            theTrack = new Track(testFileLocation);
            Assert.AreEqual(0, theTrack.Chapters.Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagChapters()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/chapters.mp3");
            Track theTrack = new Track(testFileLocation);

            // Update 3rd chapter
            ChapterInfo chapter = new ChapterInfo(theTrack.Chapters[2]);
            chapter.Title = "updated title";
            chapter.Subtitle = "updated subtitle";
            chapter.Url = new ChapterInfo.UrlInfo("updated url");

            theTrack.Chapters[2] = chapter;

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual("toplevel toc", theTrack.ChaptersTableDescription);
            Assert.AreEqual(chapter.Title, theTrack.Chapters[2].Title);
            Assert.AreEqual(chapter.Subtitle, theTrack.Chapters[2].Subtitle);
            Assert.AreEqual(chapter.Url.Url, theTrack.Chapters[2].Url.Url);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateKeepDataIntegrity()
        {
            ATL.Settings.AddNewPadding = true;

            try
            {
                string resource = "OGG/ogg.ogg";
                string location = TestUtils.GetResourceLocationRoot() + resource;
                string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
                Track theTrack = new Track(testFileLocation);

                string initialArtist = theTrack.Artist; // '֎FATHER֎'
                theTrack.Artist = "Hey ho";
                Assert.IsTrue(theTrack.Save());

                theTrack = new Track(testFileLocation);

                theTrack.Artist = initialArtist;
                Assert.IsTrue(theTrack.Save());

                // Check that the resulting file (working copy that has been processed) remains identical to the original file (i.e. no byte lost nor added)
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                /* Not possible due to field order being changed
                                string originalMD5 = TestUtils.GetFileMD5Hash(location);
                                string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                                Assert.IsTrue(originalMD5.Equals(testMD5));
                */
                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.AddNewPadding = false;
            }
        }

        [TestMethod]
        public void StreamedIO_R_Audio()
        {
            string resource = "OGG/ogg.ogg";
            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Track theTrack = new Track(fs, "audio/ogg");

                Assert.AreEqual(33, theTrack.Duration);
                Assert.AreEqual(69, theTrack.Bitrate);
                Assert.AreEqual(22050, theTrack.SampleRate);
                Assert.AreEqual(true, theTrack.IsVBR);
                Assert.AreEqual(AudioDataIOFactory.CF_LOSSY, theTrack.CodecFamily);
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }


        [TestMethod]
        public void StreamedIO_R_Meta()
        {
            string resource = "OGG/ogg.ogg";
            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Vorbis_OGG offTest = new Vorbis_OGG();
                offTest.TagIO_R_VorbisOGG_simple_OnePager(fs);
                fs.Seek(0, SeekOrigin.Begin); // Test if stream is still open
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void StreamedIO_RW_Meta()
        {
            string resource = "OGG/empty.ogg";
            string location = TestUtils.GetResourceLocationRoot() + resource;
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                Vorbis_OGG offTest = new Vorbis_OGG();
                offTest.TagIO_RW_VorbisOGG_Empty(fs);
                fs.Seek(0, SeekOrigin.Begin); // Test if stream is still open
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void ID3v1_Ignore_RW()
        {
            string resource = "MP3/id3v1.mp3";
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            bool defaultSettings = ATL.Settings.EnrichID3v1;
            ATL.Settings.EnrichID3v1 = true;
            try
            {
                Track theTrack = new Track(testFileLocation);

                theTrack.Artist = "bobTheBuilder";
                theTrack.AdditionalFields["test"] = "test1"; // ID3v1 doesn't support that
                Assert.IsTrue(theTrack.Save());

                theTrack = new Track(testFileLocation);
                Assert.AreEqual("bobTheBuilder", theTrack.Artist); // Edited
                Assert.AreEqual(1, theTrack.AdditionalFields.Count);
                Assert.IsTrue(theTrack.AdditionalFields.ContainsKey("TEST"));
                Assert.AreEqual("test1", theTrack.AdditionalFields["TEST"]);
            }
            finally
            {
                ATL.Settings.EnrichID3v1 = defaultSettings;
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void ID3v1_Focus_RW()
        {
            string resource = "MP3/id3v1.mp3";
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            bool defaultSettings = ATL.Settings.EnrichID3v1;
            ATL.Settings.EnrichID3v1 = false;
            try
            {
                Track theTrack = new Track(testFileLocation);

                theTrack.Artist = "bobTheBuilder";
                theTrack.AdditionalFields["test"] = "test1"; // ID3v1 doesn't support that
                Assert.IsTrue(theTrack.Save());

                theTrack = new Track(testFileLocation);
                Assert.AreEqual("bobTheBuilder", theTrack.Artist); // Edited
                Assert.AreEqual(0, theTrack.AdditionalFields.Count); // Not saved
            }
            finally
            {
                ATL.Settings.EnrichID3v1 = defaultSettings;
            }

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

    }
}
