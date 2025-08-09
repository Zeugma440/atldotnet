using ATL.AudioData;
using System.Drawing;
using Commons;
using static ATL.PictureInfo;
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
    public class Vorbis_FLAC : MetaIOTest
    {
        public Vorbis_FLAC()
        {
            emptyFile = "FLAC/empty.flac";
            notEmptyFile = "FLAC/flac.flac";
            tagType = MetaDataIOFactory.TagType.NATIVE;
            titleFieldCode = "TITLE";

            var pics = testData.EmbeddedPictures;
            foreach (PictureInfo pic in pics) pic.TagType = tagType;
            testData.EmbeddedPictures = pics;

            testData.Conductor = null;
            testData.Date = DateTime.Parse("1997-06-20");
            testData.PublishingDate = DateTime.Parse("1998-07-21");
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_simple()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + notEmptyFile;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            readExistingTagsOnFile(theFile);
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_dirtyTrackDiscNumbering()
        {
            new ConsoleLogger();

            string location = TestUtils.GetResourceLocationRoot() + "FLAC/flac_dirtyTrackDiscNumbering.flac";
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            float? bpm = testData.BPM;
            DateTime publishingDate = testData.PublishingDate;
            try
            {
                testData.BPM = 0;
                testData.PublishingDate = DateTime.MinValue;
                readExistingTagsOnFile(theFile, 2);
            }
            finally
            {
                testData.BPM = bpm;
                testData.PublishingDate = publishingDate;
            }
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_multipleArtistsCustom()
        {
            new ConsoleLogger();

            string fileName = "FLAC/multiple_artists_custom.flac";
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            // Read
            Assert.AreEqual("lovesick (feat. Punipuni Denki)", meta.Title);
            Assert.AreEqual("Kamome Sano" + ATL.Settings.InternalValueSeparator + "Punipuni Denki", meta.Artist);
            string customStuff = "";
            meta.AdditionalFields.TryGetValue("CUSTOMSTUFF", out customStuff);
            Assert.AreEqual("1" + ATL.Settings.InternalValueSeparator + "2", customStuff);

            // Write same data and keep initial format
            TagHolder theTag = new TagHolder();
            theTag.Artist = "Kamome Sano" + ATL.Settings.DisplayValueSeparator + "Punipuni Denki";
            var additionalFields = new Dictionary<string, string>
            {
                { "CUSTOMSTUFF", "1" + ATL.Settings.DisplayValueSeparator + "2" }
            };
            theTag.AdditionalFields = additionalFields;
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            // Can't test with MD5 here because of field order

            // Write and modify
            theTag = new TagHolder();
            theTag.Artist = "aaa" + ATL.Settings.DisplayValueSeparator + "bbb" + ATL.Settings.DisplayValueSeparator + "ccc";
            additionalFields = new Dictionary<string, string>
            {
                { "CUSTOMSTUFF", "1" + ATL.Settings.DisplayValueSeparator + "2" + ATL.Settings.DisplayValueSeparator + "3"}
            };
            theTag.AdditionalFields = additionalFields;
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            // Read again
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual("aaa" + ATL.Settings.InternalValueSeparator + "bbb" + ATL.Settings.InternalValueSeparator + "ccc", meta.Artist);
            customStuff = "";
            meta.AdditionalFields.TryGetValue("CUSTOMSTUFF", out customStuff);
            Assert.AreEqual("1" + ATL.Settings.InternalValueSeparator + "2" + ATL.Settings.InternalValueSeparator + "3", customStuff);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_Empty()
        {
            new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


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
            theTag.Date = DateTime.Parse("2008/01/01");
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "1";
            theTag.TrackTotal = 1;
            theTag.DiscNumber = 2;
            theTag.Popularity = 2.5f / 5;
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";
            theTag.BPM = 550;

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

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
            Assert.AreEqual("1", theFile.NativeTag.TrackNumber);
            Assert.AreEqual(2, theFile.NativeTag.DiscNumber);
            Assert.AreEqual((float)(2.5 / 5), theFile.NativeTag.Popularity);
            Assert.AreEqual("Me", theFile.NativeTag.Composer);
            Assert.AreEqual("父", theFile.NativeTag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.NativeTag.Conductor);
            Assert.AreEqual(550, theFile.NativeTag.BPM);


            // Setting a standard field using additional fields shouldn't be possible
            theTag.Title = "THE TITLE";

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, tagType).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            var meta = theFile.getMeta(tagType);
            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual("THE TITLE", meta.Title);

            theTag.AdditionalFields = new Dictionary<string, string> { { titleFieldCode, "THAT TITLE" } };

            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, tagType).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            meta = theFile.getMeta(tagType);
            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual("THE TITLE", meta.Title);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TagType.NATIVE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsFalse(theFile.NativeTag.Exists);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_Existing()
        {
            tagIO_RW_VorbisFLAC_Existing(notEmptyFile, 2);
        }

        private void tagIO_RW_VorbisFLAC_Existing(string fileName, int initialNbPictures, bool deleteTempFile = true)
        {
            ATL.Settings.AddNewPadding = true;
            ATL.Settings.PaddingSize = 4025; // Default padding of the sample FLAC file

            try
            {
                new ConsoleLogger();

                // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
                string location = TestUtils.GetResourceLocationRoot() + fileName;
                string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
                AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

                // Add a new supported field and a new supported picture
                Assert.IsTrue(theFile.ReadFromFile());

                TagHolder theTag = new TagHolder();
                theTag.Conductor = "John Jackman";

                PictureInfo picInfo = fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PIC_TYPE.CD);
                var testPics = theTag.EmbeddedPictures;
                testPics.Add(picInfo);
                theTag.EmbeddedPictures = testPics;


                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                readExistingTagsOnFile(theFile, initialNbPictures + 1);

                // Additional supported field
                Assert.AreEqual("John Jackman", theFile.NativeTag.Conductor);

#pragma warning disable CA1416
                int nbFound = 0;
                foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
                {
                    if (pic.PicType.Equals(PIC_TYPE.CD))
                    {
                        Assert.AreEqual(0x06, pic.NativePicCode);
                        using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                        {
                            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                            Assert.AreEqual(600, picture.Height);
                            Assert.AreEqual(900, picture.Width);
                        }
                        nbFound++;
                        break;
                    }
                }
#pragma warning restore CA1416

                Assert.AreEqual(1, nbFound);

                // Remove the additional supported field
                theTag = new TagHolder();
                theTag.Conductor = "";

                // Remove additional picture
                picInfo = new PictureInfo(PIC_TYPE.CD);
                picInfo.MarkedForDeletion = true;
                testPics.Add(picInfo);
                theTag.EmbeddedPictures = testPics;

                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag.tagData, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

                readExistingTagsOnFile(theFile, initialNbPictures);

                // Additional removed field
                Assert.AreEqual("", theFile.NativeTag.Conductor);


                // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                /*
                 * Not possible yet due to field order differences
                 * 
                string originalMD5 = TestUtils.GetFileMD5Hash(location);
                string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                Assert.IsTrue(originalMD5.Equals(testMD5));
                */

                // Get rid of the working copy
                if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.AddNewPadding = false;
                ATL.Settings.PaddingSize = 2048;
            }
        }

        private void checkTrackDiscZeroes(FileStream fs)
        {
            fs.Seek(0, SeekOrigin.Begin);
            Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKNUMBER=")));
            string s = StreamUtils.ReadNullTerminatedString(fs, System.Text.Encoding.ASCII);
            Assert.AreEqual("06", s[..^1]);

            fs.Seek(0, SeekOrigin.Begin);
            Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("TRACKTOTAL=")));
            s = StreamUtils.ReadNullTerminatedString(fs, System.Text.Encoding.ASCII);
            Assert.AreEqual("06", s[..^1]);

            fs.Seek(0, SeekOrigin.Begin);
            Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCNUMBER=")));
            s = StreamUtils.ReadNullTerminatedString(fs, System.Text.Encoding.ASCII);
            Assert.AreEqual("03", s[..^1]);

            fs.Seek(0, SeekOrigin.Begin);
            Assert.IsTrue(StreamUtils.FindSequence(fs, Utils.Latin1Encoding.GetBytes("DISCTOTAL=")));
            s = StreamUtils.ReadNullTerminatedString(fs, System.Text.Encoding.ASCII);
            Assert.AreEqual("04", s[..^1]);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_UpdateKeepTrackDiscZeroes()
        {
            StreamDelegate dlg = checkTrackDiscZeroes;
            test_RW_UpdateTrackDiscZeroes(notEmptyFile, false, false, dlg);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_UpdateFormatTrackDiscZeroes()
        {
            StreamDelegate dlg = checkTrackDiscZeroes;
            test_RW_UpdateTrackDiscZeroes(notEmptyFile, true, true, dlg);
        }

        [TestMethod]
        public void TagIO_RW_VorbisFLAC_Unsupported_Empty()
        {
            // Source : tag-free file
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
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


            theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult();

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            Assert.AreEqual(2, theFile.NativeTag.AdditionalFields.Count);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.NativeTag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            Assert.AreEqual(2, theFile.NativeTag.EmbeddedPictures.Count);

#pragma warning disable CA1416
            byte found = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAA))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(600, picture.Height);
                        Assert.AreEqual(900, picture.Width);
                    }
                    found++;
                }
                else if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
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
            picInfo = new PictureInfo(MetaDataIOFactory.TagType.NATIVE, 0xAA);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFileAsync(theTag, MetaDataIOFactory.TagType.NATIVE).GetAwaiter().GetResult());

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.NativeTag.AdditionalFields.Count);
            Assert.IsTrue(theFile.NativeTag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.NativeTag.AdditionalFields["TEST2"]);

            // Pictures
            Assert.AreEqual(1, theFile.NativeTag.EmbeddedPictures.Count);

#pragma warning disable CA1416
            found = 0;
            foreach (PictureInfo pic in theFile.NativeTag.EmbeddedPictures)
            {
                if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) && pic.NativePicCode.Equals(0xAB))
                {
                    using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                    {
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
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
        public void TagIO_RW_VorbisFLAC_ID3v2()
        {
            test_RW_Cohabitation(MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2);
        }

        [TestMethod]
        public void TagIO_R_VorbisFLAC_Lyrics_Multiple()
        {
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(TestUtils.GetResourceLocationRoot() + "FLAC/vorbisTag_multiple_lyrics.flac"));

            Assert.IsTrue(theFile.ReadFromFile(false, false));
            Assert.IsNotNull(theFile.NativeTag);
            Assert.IsTrue(theFile.NativeTag.Exists);

            checkLyricsMultiple(theFile.NativeTag);
        }

        private static void checkLyricsMultiple(IMetaData meta)
        {
            Assert.AreEqual(1, meta.Lyrics.Count);

            Assert.AreEqual(LyricsInfo.LyricsFormat.LRC, meta.Lyrics[0].Format);
            Assert.AreEqual(99, meta.Lyrics[0].SynchronizedLyrics.Count);
        }
    }
}
