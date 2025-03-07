﻿using ATL.AudioData;
using System.Drawing;
using ATL.test.IO.MetaData;
using static ATL.Logging.Log;
using ATL.Logging;
using Commons;

namespace ATL.test.IO
{
    [TestClass]
    public class HighLevel
    {
        [TestMethod]
        public void TagIO_R_Single_ID3v1()
        {
            bool crossreadingDefault = MetaDataIOFactory.GetInstance().CrossReading;
            MetaDataIOFactory.TagType[] tagPriorityDefault = new MetaDataIOFactory.TagType[MetaDataIOFactory.GetInstance().TagPriority.Length];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = false;                            // default behaviour anyway
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.ID3V1, 1);  // Should be entirely read
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.ID3V2, 2);  // Should not be read, since behaviour is single tag reading
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
            MetaDataIOFactory.TagType[] tagPriorityDefault = new MetaDataIOFactory.TagType[MetaDataIOFactory.GetInstance().TagPriority.Length];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = true;
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.ID3V1, 1);  // Should be the main source except for the Year field (empty on ID3v1)
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TagType.ID3V2, 2);  // Should be used for the Year field (valuated on ID3v2)
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
            Assert.AreEqual("Native tagging / AA", theTrack.MetadataFormats[0].Name);
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
            Assert.AreEqual("Native tagging / Vorbis (OGG)", theTrack.MetadataFormats[0].Name);
            theTrack = new Track(TestUtils.GetResourceLocationRoot() + "FLAC/flac.flac");
            Assert.AreEqual(1, theTrack.MetadataFormats.Count);
            Assert.AreEqual("Native tagging / Vorbis (FLAC)", theTrack.MetadataFormats[0].Name);
        }

        [TestMethod]
        public void TagIO_RW_DeleteTagAll()
        {
            testRemove("AA/aa.aa");
            testRemove("MP3/APE.mp3");
            testRemove("MP3/ID3v2.2 3 pictures.mp3");
            testRemove("MP3/id3v1.mp3");
            testRemove("MP3/01 - Title Screen.mp3");
            testRemove("MP4/mp4.m4a");
            testRemove("OGG/ogg.ogg");
            testRemove("FLAC/flac.flac");
            testRemove("WMA/wma.wma");
            testRemove("SPC/spc.spc");
            testRemove("PSF/psf.psf");
            testRemove("VQF/vqf.vqf");
        }

        private void testRemove(string fileName)
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            bool defaultTitleSetting = ATL.Settings.UseFileNameWhenNoTitle;
            ATL.Settings.UseFileNameWhenNoTitle = false;
            try
            {
                Track theTrack = new Track(testFileLocation);

                Assert.IsTrue(theTrack.Title.Length > 0);
                Assert.IsTrue(theTrack.RemoveAsync().Result);
                Assert.AreEqual("", theTrack.Title);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.UseFileNameWhenNoTitle = defaultTitleSetting;
            }
        }

        [TestMethod]
        public void TagIO_RW_DeleteTagMultiple()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            Assert.IsTrue(theTrack.Remove(MetaDataIOFactory.TagType.ID3V2));

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
            Assert.IsTrue(theTrack.SaveAsync(null).Result); // Hack to include async methods in test coverage

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
            IProgress<float> progress = new Progress<float>(p => Console.WriteLine(p));
            Track theTrack = new Track(testFileLocation);

            // Simple field
            theTrack.Artist = "Hey ho";
            // Tricky fields that aren't managed with a 1-to-1 mapping
            theTrack.Year = 1944;
            theTrack.TrackNumber = 10;
            Assert.IsTrue(theTrack.SaveAsync(progress).Result); // Hack to include async methods in test coverage

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
            //            Settings.DefaultTagsWhenNoMetadata = new int[2] { AudioData.MetaDataIOFactory.TagType.NATIVE, AudioData.MetaDataIOFactory.TagType.ID3V2 };
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
                //ATL.Settings.DefaultTagsWhenNoMetadata = new MetaDataIOFactory.TagType[2] { MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.NATIVE };
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
                tagIO_RW_AddPadding("FLAC/empty.flac", 4); // Additional padding : 4 bytes for VorbisComment's PADDING block header
            }
            finally
            {
                ATL.Settings.MP4_createNeroChapters = true;
                ATL.Settings.MP4_createQuicktimeChapters = true;
            }
        }

        private void tagIO_RW_doImageType(string resource, PictureInfo picture, PictureInfo.PIC_TYPE type)
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack = new Track(testFileLocation);

            if (theTrack.EmbeddedPictures.Count > 0) theTrack.EmbeddedPictures[0] = picture;
            else theTrack.EmbeddedPictures.Add(picture);
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            // Check that the picture type can be read properly
            Assert.AreEqual(type, theTrack.EmbeddedPictures[0].PicType);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private void tagIO_RW_ImageType(string resource)
        {
            PictureInfo.PIC_TYPE[] types = (PictureInfo.PIC_TYPE[])Enum.GetValues(typeof(PictureInfo.PIC_TYPE));

            foreach (PictureInfo.PIC_TYPE type in types)
            {
                if (type == PictureInfo.PIC_TYPE.Unsupported) continue;
                PictureInfo newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif"), type);
                tagIO_RW_doImageType(resource, newPicture, type);
            }
        }

        [TestMethod]
        public void tagIO_RW_ImageType()
        {
            tagIO_RW_ImageType("MP3/id3v2.4_UTF8.mp3");
            tagIO_RW_ImageType("MP3/APE.mp3");
        }

        [TestMethod]
        public void TagIO_RW_AddRemoveTagRegularNotNullable()
        {
            bool initialSettings = ATL.Settings.NullAbsentValues;
            ATL.Settings.NullAbsentValues = false;
            try
            {
                string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
                Track theTrack = new Track(testFileLocation);

                DateTime now = DateTime.Now;

                // === FIRST TEST WITH REGULAR FIELDS

                theTrack.Artist = "aaa"; // String data
                theTrack.DiscNumber = 1; // Int data
                theTrack.Year = 1998; // Int data
                theTrack.Popularity = 0.2f; // Float data
                theTrack.PublishingDate = now; // Date data

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual("aaa", theTrack.Artist);
                Assert.AreEqual(1, theTrack.DiscNumber);
                Assert.AreEqual(1998, theTrack.Year);
                Assert.AreEqual(0.2f, theTrack.Popularity);
                Assert.AreEqual(now.ToString(), theTrack.PublishingDate.ToString());

                theTrack.Artist = "";
                theTrack.DiscNumber = 0;
                theTrack.Year = 0;
                theTrack.Popularity = 0;
                theTrack.PublishingDate = DateTime.MinValue;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual("", theTrack.Artist);
                Assert.AreEqual(0, theTrack.DiscNumber);
                Assert.AreEqual(0, theTrack.Year);
                Assert.AreEqual(0, theTrack.Popularity);
                Assert.AreEqual(DateTime.MinValue.ToString(), theTrack.PublishingDate.ToString());


                // === SECOND TEST WITH YEAR/DATE DUAL PROPERTIES

                theTrack.Date = now;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual(now.Year, theTrack.Year);
                Assert.AreEqual(now.ToString(), theTrack.Date.ToString());

                theTrack.Date = DateTime.MinValue;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual(0, theTrack.Year);
                Assert.AreEqual(DateTime.MinValue.ToString(), theTrack.Date.ToString());
                
                // Test additional case where the year is an invalid value
                theTrack.Year = 99999999;
                Assert.AreEqual(0, theTrack.Year);
                
                theTrack.OriginalReleaseYear = 99999999;
                Assert.AreEqual(0, theTrack.OriginalReleaseYear);

                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.NullAbsentValues = initialSettings;
            }
        }

        [TestMethod]
        public void TagIO_RW_AddRemoveTagRegularNullable()
        {
            bool initialSettings = ATL.Settings.NullAbsentValues;
            ATL.Settings.NullAbsentValues = true;
            try
            {
                string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
                Track theTrack = new Track(testFileLocation);

                DateTime now = DateTime.Now;

                // === FIRST TEST WITH REGULAR FIELDS

                theTrack.Artist = "aaa"; // String data
                theTrack.DiscNumber = 1; // Int data
                theTrack.Year = 1998; // Int data
                theTrack.Popularity = 0.2f; // Float data
                theTrack.PublishingDate = now; // Date data

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual("aaa", theTrack.Artist);
                Assert.AreEqual(1, theTrack.DiscNumber);
                Assert.AreEqual(1998, theTrack.Year);
                Assert.AreEqual(0.2f, theTrack.Popularity);
                Assert.AreEqual(now.ToString(), theTrack.PublishingDate.ToString());


                // Change value
                theTrack.Popularity = 0;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual(0, theTrack.Popularity);


                // Remove value
                theTrack.Artist = "";
                theTrack.DiscNumber = null;
                theTrack.Year = null;
                theTrack.Popularity = null;
                theTrack.PublishingDate = null;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual("", theTrack.Artist);
                Assert.IsFalse(theTrack.DiscNumber.HasValue);
                Assert.IsFalse(theTrack.Year.HasValue);
                Assert.IsFalse(theTrack.Popularity.HasValue);
                Assert.IsFalse(theTrack.PublishingDate.HasValue);


                // === SECOND TEST WITH YEAR/DATE DUAL PROPERTIES

                theTrack.Date = now;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.AreEqual(now.Year, theTrack.Year);
                Assert.AreEqual(now.ToString(), theTrack.Date.ToString());

                theTrack.Date = null;

                Assert.IsTrue(theTrack.Save());
                theTrack = new Track(testFileLocation);

                Assert.IsFalse(theTrack.Year.HasValue);
                Assert.IsFalse(theTrack.Date.HasValue);


                // Get rid of the working copy
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.NullAbsentValues = initialSettings;
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

            theTrack.AdditionalFields["TOFN"] = "update test";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(1, theTrack.AdditionalFields.Count);
            Assert.IsTrue(theTrack.AdditionalFields.ContainsKey("TOFN"));
            Assert.AreEqual("update test", theTrack.AdditionalFields["TOFN"]);

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


            // Remove 1st and add it back on 1st position -> should still be listed 1st
            theTrack.EmbeddedPictures.RemoveAt(0);
            newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif"), PictureInfo.PIC_TYPE.Back);
            theTrack.EmbeddedPictures.Insert(0, newPicture);
            Assert.IsTrue(theTrack.Save());

            newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif"), PictureInfo.PIC_TYPE.Front);
            theTrack.EmbeddedPictures.Insert(0, newPicture);
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(3, theTrack.EmbeddedPictures.Count);

            Assert.AreEqual(PictureInfo.PIC_TYPE.Front, theTrack.EmbeddedPictures[0].PicType);
            Assert.AreEqual(PictureInfo.PIC_TYPE.Back, theTrack.EmbeddedPictures[1].PicType);
            Assert.AreEqual(PictureInfo.PIC_TYPE.CD, theTrack.EmbeddedPictures[2].PicType);


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

            // 1- Update Front picture field picture data
            PictureInfo newPicture = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg"), PictureInfo.PIC_TYPE.Front);
            theTrack.EmbeddedPictures.Add(newPicture);

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(2, theTrack.EmbeddedPictures.Count); // Front Cover, Icon

#pragma warning disable CA1416
            bool foundFront = false;
            bool foundIcon = false;
            foreach (PictureInfo pic in theTrack.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Front))
                {
                    foundFront = true;
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        // Properties of updated picture (pic2.jpg)
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(900, picture.Width);
                        Assert.AreEqual(290, picture.Height);
                    }
                }
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Icon)) foundIcon = true;
                // TODO test reading an unsupported pic code ?
            }
            Assert.IsTrue(foundFront);
            Assert.IsTrue(foundIcon);
#pragma warning restore CA1416

            // 2- Update Front picture field description
            theTrack.EmbeddedPictures[0].Description = "aaa";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(2, theTrack.EmbeddedPictures.Count); // Front Cover, Icon
            Assert.AreEqual("aaa", theTrack.EmbeddedPictures[0].Description);

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
        public void TagIO_RW_ResetTrackDisc()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/id3v2.4_UTF8.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.TrackNumber = 1;
            theTrack.TrackTotal = 2;
            theTrack.DiscNumber = 3;
            theTrack.DiscTotal = 4;

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(1, theTrack.TrackNumber);
            Assert.AreEqual(2, theTrack.TrackTotal);
            Assert.AreEqual(3, theTrack.DiscNumber);
            Assert.AreEqual(4, theTrack.DiscTotal);

            theTrack.TrackNumber = 0;
            theTrack.TrackTotal = 0;
            theTrack.DiscNumber = 0;
            theTrack.DiscTotal = 0;

            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(0, theTrack.TrackNumber);
            Assert.AreEqual(0, theTrack.TrackTotal);
            Assert.AreEqual(0, theTrack.DiscNumber);
            Assert.AreEqual(0, theTrack.DiscTotal);

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
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);
            Track theTrack;

            // With Mime-type
            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                theTrack = new Track(fs, "audio/ogg");

                Assert.AreEqual(33, theTrack.Duration);
                Assert.AreEqual(69, theTrack.Bitrate);
                Assert.AreEqual(-1, theTrack.BitDepth);
                Assert.AreEqual(22050, theTrack.SampleRate);
                Assert.AreEqual(true, theTrack.IsVBR);
                Assert.AreEqual(23125, theTrack.TechnicalInformation.AudioDataOffset);
                Assert.AreEqual(278029, theTrack.TechnicalInformation.AudioDataSize);
                Assert.AreEqual(AudioDataIOFactory.CF_LOSSY, theTrack.CodecFamily);
            }

            // Stream without Mime-type (autodetect)
            using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                theTrack = new Track(fs);

                Assert.AreEqual(33, theTrack.Duration);
                Assert.AreEqual(69, theTrack.Bitrate);
                Assert.AreEqual(-1, theTrack.BitDepth);
                Assert.AreEqual(22050, theTrack.SampleRate);
                Assert.AreEqual(true, theTrack.IsVBR);
                Assert.AreEqual(23125, theTrack.TechnicalInformation.AudioDataOffset);
                Assert.AreEqual(278029, theTrack.TechnicalInformation.AudioDataSize);
                Assert.AreEqual(AudioDataIOFactory.CF_LOSSY, theTrack.CodecFamily);
            }

            // File without extension (autodetect)
            int dotIndex = testFileLocation.LastIndexOf("ogg.ogg");
            string testFileLocation2 = testFileLocation.Substring(0, dotIndex + 3);
            File.Copy(testFileLocation, testFileLocation2);

            theTrack = new Track(testFileLocation2);

            Assert.AreEqual(33, theTrack.Duration);
            Assert.AreEqual(69, theTrack.Bitrate);
            Assert.AreEqual(-1, theTrack.BitDepth);
            Assert.AreEqual(22050, theTrack.SampleRate);
            Assert.AreEqual(true, theTrack.IsVBR);
            Assert.AreEqual(23125, theTrack.TechnicalInformation.AudioDataOffset);
            Assert.AreEqual(278029, theTrack.TechnicalInformation.AudioDataSize);
            Assert.AreEqual(AudioDataIOFactory.CF_LOSSY, theTrack.CodecFamily);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation2);
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_multipleArtists()
        {
            string resource = "FLAC/multiple_artists_custom.flac";
            string testFileLocation = TestUtils.CopyAsTempTestFile(resource);

            Track theTrack = new Track(testFileLocation);

            // Read
            Assert.AreEqual("Kamome Sano" + ATL.Settings.DisplayValueSeparator + "Punipuni Denki", theTrack.Artist);

            // Write
            theTrack.Artist = "aaa" + ATL.Settings.DisplayValueSeparator + "bbb" + ATL.Settings.DisplayValueSeparator + "ccc";
            theTrack.Save();

            Track theTrack2 = new Track(testFileLocation);
            Assert.AreEqual("aaa" + ATL.Settings.DisplayValueSeparator + "bbb" + ATL.Settings.DisplayValueSeparator + "ccc", theTrack2.Artist);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void StreamedIO_R_Meta()
        {
            string resource = "OGG/ogg.ogg";
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
        public void TagIO_RW_ID3v1_Ignore()
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
        public void TagIO_RW_ID3v1_Focus()
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

        [TestMethod]
        public void TagIO_RW_Chapters_Consistency()
        {
            ArrayLogger log = new ArrayLogger();

            // Source : OGG with existing tag incl. chapters
            string testFileLocation = TestUtils.CopyAsTempTestFile("OGG/chapters.ogg");
            new Track(testFileLocation);

            IList<LogItem> logItems = log.GetAllItems(Log.LV_INFO);
            Assert.IsTrue(logItems.Count > 0);
            int nbFound = 0;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("is > total tracks")) nbFound++;
                if (l.Message.Contains("is > total discs")) nbFound++;
                if (l.Message.Contains("start timestamp goes beyond file duration")) nbFound++;
            }
            Assert.AreEqual(8, nbFound);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_Lyrics_Consistency()
        {
            ArrayLogger log = new ArrayLogger();

            // Source : OGG with existing tag incl. chapters
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/ID3v2.4-SYLT_invalid.mp3");
            new Track(testFileLocation);

            IList<LogItem> logItems = log.GetAllItems(LV_INFO);
            Assert.IsTrue(logItems.Count > 0);
            int nbFound = 0;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("start timestamp goes beyond file duration")) nbFound++;
            }
            Assert.AreEqual(1, nbFound);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_Lyrics_Remove()
        {
            new ConsoleLogger();

            // 1- Synched lyrics
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/ID3v2.4-SYLT_cn.mp3");
            Track t = new Track(testFileLocation);

            Assert.IsTrue(t.Lyrics.SynchronizedLyrics.Count > 0);

            t.Lyrics = null;
            t.Save();

            t = new Track(testFileLocation);
            Assert.IsTrue(0 == t.Lyrics.SynchronizedLyrics.Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // 2- Unsynched lyrics
            testFileLocation = TestUtils.CopyAsTempTestFile("MP3/ID3v2.4-USLT_JP_eng.mp3");
            t = new Track(testFileLocation);

            Assert.IsTrue(t.Lyrics.UnsynchronizedLyrics.Length > 0);

            t.Lyrics = null;
            t.Save();

            t = new Track(testFileLocation);
            Assert.IsTrue(0 == t.Lyrics.UnsynchronizedLyrics.Length);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        /**
         * Synch lyrics override unsynch lyrics when both exist and the target tagging format
         * doesn't support synch lyrics
         */
        [TestMethod]
        public void TagIO_RW_Lyrics_SynchOverUnsynch()
        {
            new ConsoleLogger();

            string testFileLocation = TestUtils.CopyAsTempTestFile("OGG/empty.ogg");
            Track t = new Track(testFileLocation);

            t.Lyrics.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase(1000, "aaa"));
            t.Lyrics.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase(2000, "bbb"));
            t.Lyrics.UnsynchronizedLyrics = "some stuff";

            t.Save();

            t = new Track(testFileLocation);
            Assert.IsTrue(0 == t.Lyrics.UnsynchronizedLyrics.Length);
            Assert.IsTrue(2 == t.Lyrics.SynchronizedLyrics.Count);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_Year_or_Date_On_File()
        {
            tagIO_RW_Year_or_Date_On_File("MP4/empty.m4a", "MP4/mp4.m4a", "MP4/mp4_date_in_©day.m4a", 20, 26);
            tagIO_RW_Year_or_Date_On_File("OGG/empty.ogg", "OGG/chapters.ogg", "OGG/ogg.ogg", 9, 15);
        }

        private void tagIO_RW_Year_or_Date_On_File(
            string emptyFile,
            string yearFile,
            string dateFile,
            int yearTagLength,
            int dateTagLength)
        {
            // == 1a- Add a YEAR to an empty file
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            Track theTrack = new Track(testFileLocation);

            theTrack.Year = 1993;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(1993, theTrack.Year);
            Assert.AreEqual(yearTagLength, getDateFieldLength(testFileLocation)); // Date stored as Year

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 1b- Add a DATE to an empty file
            testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            theTrack = new Track(testFileLocation);

            DateTime date = new DateTime(2001, 4, 20);
            theTrack.Date = date;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(date.ToString(), theTrack.Date.ToString());
            Assert.AreEqual(dateTagLength, getDateFieldLength(testFileLocation)); // Date stored as Date

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);



            // == 2a- Add a YEAR to a file tagged with a DATE
            testFileLocation = TestUtils.CopyAsTempTestFile(dateFile);
            theTrack = new Track(testFileLocation);

            theTrack.Year = 1993;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(1993, theTrack.Year);
            Assert.AreEqual(yearTagLength, getDateFieldLength(testFileLocation)); // Date stored as Year

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 2b- Add a DATE to a file tagged with a YEAR
            testFileLocation = TestUtils.CopyAsTempTestFile(yearFile);
            theTrack = new Track(testFileLocation);

            theTrack.Date = date;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(date.ToString(), theTrack.Date.ToString());
            Assert.AreEqual(dateTagLength, getDateFieldLength(testFileLocation)); // Date stored as Date

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);



            // == 3a- Keep YEAR after rewriting file
            testFileLocation = TestUtils.CopyAsTempTestFile(yearFile);
            theTrack = new Track(testFileLocation);

            int year = theTrack.Year.Value;
            theTrack.Title = "That's some new title you got here!";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(year, theTrack.Year);
            Assert.AreEqual(yearTagLength, getDateFieldLength(testFileLocation)); // Date stored as Year

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 3b- Keep DATE after rewriting file
            testFileLocation = TestUtils.CopyAsTempTestFile(dateFile);
            theTrack = new Track(testFileLocation);

            date = theTrack.Date.Value;
            theTrack.Title = "That's some new title you got here!";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(date.ToString(), theTrack.Date.ToString());
            Assert.AreEqual(dateTagLength, getDateFieldLength(testFileLocation)); // Date stored as Date

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        private int getDateFieldLength(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                if (filePath.EndsWith("ogg")) return getVorbisDateFieldLength(fs);
                if (filePath.EndsWith("m4a")) return getMP4DateFieldLength(fs);
                return -1;
            }
        }

        private int getVorbisDateFieldLength(Stream s)
        {
            // Prevent recording date search from catching release date
            long origDateOffset = -1;
            if (StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("ORIGINALDATE=")))
            {
                origDateOffset = s.Position;
            }

            s.Seek(0, SeekOrigin.Begin);
            Assert.AreEqual(true, StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("DATE=")));
            if (s.Position == origDateOffset) Assert.AreEqual(true, StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("DATE=")));

            s.Seek(-9, SeekOrigin.Current);
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return StreamUtils.DecodeInt32(buffer);
        }

        private int getMP4DateFieldLength(Stream s)
        {
            Assert.AreEqual(true, StreamUtils.FindSequence(s, Utils.Latin1Encoding.GetBytes("©day")));
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return StreamUtils.DecodeBEInt32(buffer);
        }

        [TestMethod]
        public void TagIO_RW_Track_Number_Str_Num()
        {
            // Using VorbisTag as it allows storing track numbers as strings
            var emptyFile = "OGG/empty.ogg";

            // == 1- Add a numeric track number to an empty file
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            Track theTrack = new Track(testFileLocation);

            theTrack.TrackNumber = 2;
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(2, theTrack.TrackNumber);
            Assert.AreEqual("2", theTrack.TrackNumberStr);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 2- Add a string track number to an empty file
            testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            theTrack = new Track(testFileLocation);

            theTrack.TrackNumberStr = "02";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(2, theTrack.TrackNumber);
            Assert.AreEqual("02", theTrack.TrackNumberStr);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 3- Add an LP string track number to an empty file
            testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            theTrack = new Track(testFileLocation);

            theTrack.TrackNumberStr = "A2";
            Assert.IsTrue(theTrack.Save());

            theTrack = new Track(testFileLocation);
            Assert.AreEqual(2, theTrack.TrackNumber);
            Assert.AreEqual("A2", theTrack.TrackNumberStr);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_WriteSpecificTagType()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/APE.mp3");
            const string newValue = "La-Li-Lu-Le-Lo";

            // == 1- No type specified => update existing tag types (legacy behaviour)
            Track theTrack = new Track(testFileLocation);
            theTrack.Artist = newValue;
            Assert.IsTrue(theTrack.Save());

            AudioDataManager theFile =
                new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            theFile.ReadFromFile();
            Assert.IsTrue(theFile.hasMeta(MetaDataIOFactory.TagType.APE));
            Assert.IsFalse(theFile.hasMeta(MetaDataIOFactory.TagType.ID3V1));
            Assert.IsFalse(theFile.hasMeta(MetaDataIOFactory.TagType.ID3V2));
            Assert.IsFalse(theFile.HasNativeMeta());

            Assert.AreEqual(newValue, theFile.APEtag.Artist);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 2- Type specified + ANY => create given tag type + update existing tag types
            testFileLocation = TestUtils.CopyAsTempTestFile("MP3/APE.mp3");
            theTrack = new Track(testFileLocation);
            theTrack.Artist = newValue;
            Assert.IsTrue(theTrack.Save(MetaDataIOFactory.TagType.ID3V2));

            theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));
            theFile.ReadFromFile();
            Assert.IsTrue(theFile.hasMeta(MetaDataIOFactory.TagType.APE));
            Assert.IsFalse(theFile.hasMeta(MetaDataIOFactory.TagType.ID3V1));
            Assert.IsTrue(theFile.hasMeta(MetaDataIOFactory.TagType.ID3V2));
            Assert.IsFalse(theFile.HasNativeMeta());

            Assert.AreEqual(newValue, theFile.ID3v2.Artist);
            Assert.AreEqual(newValue, theFile.APEtag.Artist);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);


            // == 3- Log warning when trying to write an unsupported tag
            ArrayLogger log = new ArrayLogger();
            testFileLocation = TestUtils.CopyAsTempTestFile("MOD/mod.mod");
            theTrack = new Track(testFileLocation);
            theTrack.Title = newValue;
            Assert.IsTrue(theTrack.Save(MetaDataIOFactory.TagType.ID3V2));

            IList<LogItem> logItems = log.GetAllItems(LV_WARNING);
            Assert.IsTrue(logItems.Count > 0);
            bool found = false;
            foreach (LogItem l in logItems)
            {
                if (l.Message.Contains("as it is not supported")) found = true;
            }
            Assert.IsTrue(found);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_SaveTo()
        {
            string testFileLocation = TestUtils.CopyAsTempTestFile("MP3/APE.mp3");
            string testFileLocation2 = testFileLocation.Replace(".mp3", "_2.mp3");
            string testFileLocation3 = testFileLocation.Replace(".mp3", "_3.mp3");

            // SYNC
            try
            {
                // File -> File
                Track theTrack = new Track(testFileLocation);
                Assert.IsTrue(theTrack.SaveTo(testFileLocation2));
                Assert.IsTrue(File.Exists(testFileLocation2));
                FileInfo testFileInfo = new FileInfo(testFileLocation);
                FileInfo testFileInfo2 = new FileInfo(testFileLocation2);
                Assert.AreEqual(testFileInfo.Length, testFileInfo2.Length);

                // File -> Stream
                theTrack = new Track(testFileLocation);
                using (FileStream target = new FileStream(testFileLocation3, FileMode.Create, FileAccess.ReadWrite,
                           FileShare.Read))
                {
                    Assert.IsTrue(theTrack.SaveTo(target));
                }
                Assert.IsTrue(File.Exists(testFileLocation3));
                FileInfo testFileInfo3 = new FileInfo(testFileLocation3);
                Assert.AreEqual(testFileInfo.Length, testFileInfo3.Length);
            }
            finally
            {
                if (File.Exists(testFileLocation2)) File.Delete(testFileLocation2);
                if (File.Exists(testFileLocation3)) File.Delete(testFileLocation3);
            }

            // ASYNC
            try
            {
                // File -> File
                Track theTrack = new Track(testFileLocation);
                Assert.IsTrue(theTrack.SaveToAsync(testFileLocation2).Result);
                Assert.IsTrue(File.Exists(testFileLocation2));
                FileInfo testFileInfo = new FileInfo(testFileLocation);
                FileInfo testFileInfo2 = new FileInfo(testFileLocation2);
                Assert.AreEqual(testFileInfo.Length, testFileInfo2.Length);

                // File -> Stream
                theTrack = new Track(testFileLocation);
                using (FileStream target = new FileStream(testFileLocation3, FileMode.Create, FileAccess.ReadWrite,
                           FileShare.Read))
                {
                    Assert.IsTrue(theTrack.SaveToAsync(target).Result);
                }
                Assert.IsTrue(File.Exists(testFileLocation3));
                FileInfo testFileInfo3 = new FileInfo(testFileLocation3);
                Assert.AreEqual(testFileInfo.Length, testFileInfo3.Length);
            }
            finally
            {
                if (File.Exists(testFileLocation2)) File.Delete(testFileLocation2);
                if (File.Exists(testFileLocation3)) File.Delete(testFileLocation3);
            }


            // SYNC
            try
            {
                // Stream -> File
                using (FileStream source = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Track theTrack = new Track(source);
                    Assert.IsTrue(theTrack.SaveTo(testFileLocation2));

                    Assert.IsTrue(File.Exists(testFileLocation2));
                    FileInfo originalFileInfo = new FileInfo(testFileLocation);
                    FileInfo testFileInfo = new FileInfo(testFileLocation2);
                    Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                }

                // Stream -> Stream
                using (FileStream source = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Track theTrack = new Track(source);
                    using (FileStream target = new FileStream(testFileLocation3, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                    {
                        Assert.IsTrue(theTrack.SaveTo(target));
                    }
                    Assert.IsTrue(File.Exists(testFileLocation3));
                    FileInfo originalFileInfo = new FileInfo(testFileLocation);
                    FileInfo testFileInfo = new FileInfo(testFileLocation3);
                    Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                }
            }
            finally
            {
                if (File.Exists(testFileLocation2)) File.Delete(testFileLocation2);
                if (File.Exists(testFileLocation3)) File.Delete(testFileLocation3);
            }


            // ASYNC
            try
            {
                // Stream -> File
                using (FileStream source = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Track theTrack = new Track(source);
                    Assert.IsTrue(theTrack.SaveToAsync(testFileLocation2).Result);

                    Assert.IsTrue(File.Exists(testFileLocation2));
                    FileInfo originalFileInfo = new FileInfo(testFileLocation);
                    FileInfo testFileInfo = new FileInfo(testFileLocation2);
                    Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                }

                // Stream -> Stream
                using (FileStream source =
                       new FileStream(testFileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Track theTrack = new Track(source);
                    using (FileStream target = new FileStream(testFileLocation3, FileMode.Create, FileAccess.ReadWrite,
                               FileShare.Read))
                    {
                        Assert.IsTrue(theTrack.SaveToAsync(target).Result);
                    }

                    Assert.IsTrue(File.Exists(testFileLocation3));
                    FileInfo originalFileInfo = new FileInfo(testFileLocation);
                    FileInfo testFileInfo = new FileInfo(testFileLocation3);
                    Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                }
            }
            finally
            {
                if (File.Exists(testFileLocation2)) File.Delete(testFileLocation2);
                if (File.Exists(testFileLocation3)) File.Delete(testFileLocation3);
            }
        }

        [TestMethod]
        public void VersionExists()
        {
            Assert.IsTrue(ATL.Version.getVersion().Length > 0);
        }
    }
}
