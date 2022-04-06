using ATL.AudioData;
using ATL.AudioData.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
     *  Cohabitation with ID3v2 and ID3v1
     *
     */

    /* TODO generic tests to add
     *   - Correct update of tagData within the IMetaDataIO just after an Update/Remove (by comparing with the same TagData obtained with Read)
     *   - Individual picture removal (from index > 1)
     *   - Exact picture binary data conservation after tag editing
    */
    public class MetaIOTest
    {
        protected string emptyFile;
        protected string notEmptyFile;
        protected MetaDataIOFactory.TagType tagType = MetaDataIOFactory.TagType.ANY;
        protected TagHolder testData;
        protected bool supportsDateOrYear = false;
        protected bool supportsInternationalChars = true;
        protected bool canMetaNotExist = true;

        public delegate void StreamDelegate(FileStream fs);

        public MetaIOTest()
        {
            // Initialize default test data
            testData = new TagHolder();

            testData.Title = "aa父bb";
            testData.Artist = "֎FATHER֎";
            testData.Album = "Papa֍rules";
            testData.AlbumArtist = "aaᱬbb";
            testData.Comment = "父父!";
            testData.Date = DateTime.Parse("1997-06-20T04:04:04");
            testData.Genre = "House";
            testData.Popularity = null;
            testData.Track = 1;
            testData.TrackTotal = 2;
            testData.Composer = "ccᱬdd";
            testData.Conductor = "";  // Empty string means "supported, but not valued in test sample"
            testData.Publisher = "";
            testData.PublishingDate = DateTime.MinValue;
            testData.Disc = 3;
            testData.DiscTotal = 4;
            testData.Copyright = "";
            testData.GeneralDescription = "";


            var testAddFields = new Dictionary<string, string>();
            testAddFields["TEST"] = "xxx";
            testData.AdditionalFields = testAddFields;

            IList<PictureInfo> testPictureInfos = new List<PictureInfo>();
            // 0x03 : front cover according to ID3v2 conventions
            PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"), PIC_TYPE.Unsupported, tagType, 0x03);
            pic.ComputePicHash();
            testPictureInfos.Add(pic);

            // 0x02 : conductor according to ID3v2 conventions
            pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.png"), PIC_TYPE.Unsupported, tagType, 0x02);
            pic.ComputePicHash();
            testPictureInfos.Add(pic);

            testData.EmbeddedPictures = testPictureInfos;
        }

        protected void test_RW_Cohabitation(MetaDataIOFactory.TagType tagType1, MetaDataIOFactory.TagType tagType2, bool canMeta1NotExist = true)
        {
            new ConsoleLogger();

            // Source : empty file
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.CopyAsTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            IMetaDataIO meta1 = theFile.getMeta(tagType1);
            IMetaDataIO meta2 = theFile.getMeta(tagType2);

            Assert.IsNotNull(meta1);
            if (canMeta1NotExist) Assert.IsFalse(meta1.Exists);
            Assert.IsNotNull(meta2);
            Assert.IsFalse(meta2.Exists);

            long initialPaddingSize1 = meta1.PaddingSize;


            // Construct a new tag with the most basic options (no un supported fields, no pictures)
            TagHolder theTag1 = new TagHolder();
            theTag1.Title = "Test1";
            theTag1.Album = "Album1";

            TagHolder theTag2 = new TagHolder();
            theTag2.Title = "Test2";
            theTag2.Album = "Album2";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag1.tagData, tagType1));
            Assert.IsTrue(theFile.UpdateTagInFile(theTag2.tagData, tagType2));

            // This also tests if physical data can still be read (e.g. native tag has not been scrambled by the apparition of a non-native tag)
            Assert.IsTrue(theFile.ReadFromFile());

            meta1 = theFile.getMeta(tagType1);
            meta2 = theFile.getMeta(tagType2);

            Assert.IsNotNull(meta1);
            Assert.IsTrue(meta1.Exists);

            Assert.IsNotNull(meta2);
            Assert.IsTrue(meta2.Exists);

            Assert.AreEqual("Test1", meta1.Title);
            Assert.AreEqual("Album1", meta1.Album);

            Assert.AreEqual("Test2", meta2.Title);
            Assert.AreEqual("Album2", meta2.Album);

            Assert.IsTrue(theFile.RemoveTagFromFile(tagType1));
            Assert.IsTrue(theFile.ReadFromFile());

            meta1 = theFile.getMeta(tagType1);
            meta2 = theFile.getMeta(tagType2);

            Assert.IsNotNull(meta1);
            if (canMeta1NotExist) Assert.IsFalse(meta1.Exists);

            Assert.IsNotNull(meta2);
            Assert.IsTrue(meta2.Exists);

            Assert.AreEqual("Test2", meta2.Title);
            Assert.AreEqual("Album2", meta2.Album);

            Assert.IsTrue(theFile.RemoveTagFromFile(tagType2));
            Assert.IsTrue(theFile.ReadFromFile());

            meta1 = theFile.getMeta(tagType1);
            meta2 = theFile.getMeta(tagType2);

            Assert.IsNotNull(meta1);
            if (canMeta1NotExist) Assert.IsFalse(meta1.Exists);

            Assert.IsNotNull(meta2);
            Assert.IsFalse(meta2.Exists);

            // Restore initial padding
            TagData theTagFinal = new TagData();
            theTagFinal.PaddingSize = initialPaddingSize1;
            Assert.IsTrue(theFile.UpdateTagInFile(theTagFinal, tagType1));

            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.AreEqual(originalMD5, testMD5);

            // Get rid of the working copy
            if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        protected void test_RW_Existing(string fileName, int initialNbPictures, bool deleteTempFile = true, bool sameSizeAfterEdit = false, bool sameBitsAfterEdit = false)
        {
            new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagHolder theTag = new TagHolder();

            char internationalChar = supportsInternationalChars ? '父' : '!';

            TagData initialTestData = new TagData(testData.tagData);
            // These two cases cover all tag capabilities
            if (testData.Title != "") theTag.Title = "Test !!" + internationalChar;
            else if (testData.GeneralDescription != "") theTag.GeneralDescription = "Description" + internationalChar;

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                IDictionary<string, string> additionalFields = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kvp in testData.AdditionalFields)
                {
                    additionalFields.Add(kvp.Key, kvp.Value);
                }
                theTag.AdditionalFields = additionalFields;
            }
            testData = new TagHolder(theTag.tagData);

            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PictureInfo.PIC_TYPE.CD);
            var testPics = theTag.EmbeddedPictures;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag.tagData, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures + 1);
            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (testData.EmbeddedPictures != null && testData.EmbeddedPictures.Count > 0)
            {
                int nbFound = 0;
                foreach (PictureInfo pic in meta.EmbeddedPictures)
                {
                    if (pic.PicType.Equals(PictureInfo.PIC_TYPE.CD))
                    {
                        if (tagType.Equals(MetaDataIOFactory.TagType.APE))
                        {
                            Assert.AreEqual("Cover Art (Media)", pic.NativePicCodeStr);
                        }
                        else // ID3v2 convention
                        {
                            Assert.AreEqual(0x06, pic.NativePicCode);
                        }
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

                Assert.AreEqual(1, nbFound);
            }

            // Remove the additional supported field
            theTag = new TagHolder(initialTestData);
            testData = new TagHolder(initialTestData);

            // Remove additional picture
            picInfo = new PictureInfo(PictureInfo.PIC_TYPE.CD);
            picInfo.MarkedForDeletion = true;
            testPics = theTag.EmbeddedPictures;
            testPics.Add(picInfo);
            theTag.EmbeddedPictures = testPics;

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures);

            // Additional removed field
            meta = theFile.getMeta(tagType);
            Assert.AreEqual("", meta.Conductor);

            // Additional removed picture
            Assert.AreEqual(initialNbPictures, meta.EmbeddedPictures.Count);

            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            if (sameSizeAfterEdit || sameBitsAfterEdit)
            {
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                if (sameSizeAfterEdit) Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                if (sameBitsAfterEdit)
                {
                    string originalMD5 = TestUtils.GetFileMD5Hash(location);
                    string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                    Assert.AreEqual(originalMD5, testMD5);
                }
            }

            // Get rid of the working copy
            if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        protected void test_RW_UpdateTrackDiscZeroes(string fileName, bool useLeadingZeroes, bool overrideExistingLeadingZeroesFormat, StreamDelegate checkDelegate, bool deleteTempFile = true)
        {
            new ConsoleLogger();

            bool settingsInit1 = ATL.Settings.UseLeadingZeroes;
            ATL.Settings.UseLeadingZeroes = useLeadingZeroes;
            bool settingsInit2 = ATL.Settings.OverrideExistingLeadingZeroesFormat;
            ATL.Settings.OverrideExistingLeadingZeroesFormat = overrideExistingLeadingZeroesFormat;

            try
            {
                // Source : totally metadata-free file
                string location = TestUtils.GetResourceLocationRoot() + fileName;
                string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
                Track theTrack = new Track(testFileLocation);

                // Update Track count
                theTrack.TrackNumber = 6;
                theTrack.TrackTotal = 6;

                theTrack.Save();

                // Check if values are as expected
                theTrack = new Track(testFileLocation);
                Assert.AreEqual(6, theTrack.TrackNumber);
                Assert.AreEqual(6, theTrack.TrackTotal);
                Assert.AreEqual(3, theTrack.DiscNumber);
                Assert.AreEqual(4, theTrack.DiscTotal);

                // Check if formatting of track and disc are correctly formatted
                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open, FileAccess.Read))
                    checkDelegate(fs);

                if (!overrideExistingLeadingZeroesFormat)
                {
                    FileInfo originalFileInfo = new FileInfo(location);
                    FileInfo testFileInfo = new FileInfo(testFileLocation);
                    Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);
                }

                // Get rid of the working copy
                if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
            finally
            {
                ATL.Settings.UseLeadingZeroes = settingsInit1;
                ATL.Settings.OverrideExistingLeadingZeroesFormat = settingsInit2;
            }
        }

        public void test_RW_Empty(string fileName, bool deleteTempFile = true, bool sameSizeAfterEdit = false, bool sameBitsAfterEdit = false)
        {
            new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            if (canMetaNotExist) Assert.IsFalse(theFile.getMeta(tagType).Exists);

            long initialPaddingSize = theFile.getMeta(tagType).PaddingSize;


            char internationalChar = supportsInternationalChars ? '父' : '!';

            // Construct a new tag
            TagHolder theTag = new TagHolder();
            if (testData.Title != "") theTag.Title = "Test !!";
            if (testData.Album != "") theTag.Album = "Album";
            if (testData.Artist != "") theTag.Artist = "Artist";
            if (testData.AlbumArtist != "") theTag.AlbumArtist = "Mike";
            if (testData.Comment != "") theTag.Comment = "This is a test";
            if (testData.Date > DateTime.MinValue) theTag.Date = DateTime.Parse("2008/01/01");
            if (testData.PublishingDate > DateTime.MinValue) theTag.PublishingDate = DateTime.Parse("2007/02/02");
            if (testData.Genre != "") theTag.Genre = "Merengue";
            if (testData.Popularity != 0) theTag.Popularity = 2.5f / 5;
            if (testData.Track != 0) theTag.Track = 1;
            if (testData.TrackTotal != 0) theTag.TrackTotal = 2;
            if (testData.Disc != 0) theTag.Disc = 3;
            if (testData.DiscTotal != 0) theTag.DiscTotal = 4;
            if (testData.Composer != "") theTag.Composer = "Me";
            if (testData.Copyright != "") theTag.Copyright = "a" + internationalChar + "a";
            if (testData.Conductor != "") theTag.Conductor = "John Johnson Jr.";
            if (testData.Publisher != "") theTag.Publisher = "Z Corp.";
            if (testData.GeneralDescription != "") theTag.GeneralDescription = "Description";

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                var testAddFields = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> info in testData.AdditionalFields)
                {
                    testAddFields.Add(info.Key, info.Value);
                }
                theTag.AdditionalFields = testAddFields;
            }

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag.tagData, tagType));

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (testData.Title != "") Assert.AreEqual("Test !!", meta.Title);
            if (testData.Album != "") Assert.AreEqual("Album", meta.Album);
            if (testData.Artist != "") Assert.AreEqual("Artist", meta.Artist);
            if (testData.AlbumArtist != "") Assert.AreEqual("Mike", meta.AlbumArtist);
            if (testData.Comment != "") Assert.AreEqual("This is a test", meta.Comment);
            if (!supportsDateOrYear)
            {
                if (!testData.Date.Equals(DateTime.MinValue))
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2008/01/01", out date));
                    Assert.AreEqual(date, meta.Date);
                }
                if (!testData.PublishingDate.Equals(DateTime.MinValue))
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2007/02/02", out date));
                    Assert.AreEqual(date, meta.PublishingDate);
                }
            }
            else
            {
                Assert.IsNotNull(meta.Date);
                Assert.IsTrue(meta.Date > DateTime.MinValue);
                if (meta.Date != null && meta.Date > DateTime.MinValue && testData.Date != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2008/01/01", out date));
                    Assert.AreEqual(date, meta.Date);
                }
            }
            if (testData.Genre != "") Assert.AreEqual("Merengue", meta.Genre);
            if (testData.Popularity != 0) Assert.AreEqual(2.5f / 5, meta.Popularity);
            if (testData.Track != 0) Assert.AreEqual(1, meta.Track);
            if (testData.TrackTotal != 0) Assert.AreEqual(2, meta.TrackTotal);
            if (testData.Disc != 0) Assert.AreEqual(3, meta.Disc);
            if (testData.DiscTotal != 0) Assert.AreEqual(4, meta.DiscTotal);
            if (testData.Composer != "") Assert.AreEqual("Me", meta.Composer);
            if (testData.Copyright != "") Assert.AreEqual("a" + internationalChar + "a", meta.Copyright);
            if (testData.Conductor != "") Assert.AreEqual("John Johnson Jr.", meta.Conductor);
            if (testData.Publisher != "") Assert.AreEqual("Z Corp.", meta.Publisher);
            if (testData.GeneralDescription != "") Assert.AreEqual("Description", meta.GeneralDescription);

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                foreach (KeyValuePair<string, string> info in testData.AdditionalFields)
                {
                    Assert.IsTrue(meta.AdditionalFields.ContainsKey(info.Key), info.Key);
                    Assert.AreEqual(info.Value, meta.AdditionalFields[info.Key], info.Key);
                }
            }

            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(tagType));

            if (initialPaddingSize > 0)
            {
                TagData paddingRestore = new TagData();
                paddingRestore.PaddingSize = initialPaddingSize;
                Assert.IsTrue(theFile.UpdateTagInFile(paddingRestore, tagType));
            }

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            if (canMetaNotExist) Assert.IsFalse(theFile.getMeta(tagType).Exists);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            if (sameSizeAfterEdit || sameBitsAfterEdit)
            {
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                if (sameSizeAfterEdit) Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                if (sameBitsAfterEdit)
                {
                    string originalMD5 = TestUtils.GetFileMD5Hash(location);
                    string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                    Assert.AreEqual(originalMD5, testMD5);
                }
            }

            // Get rid of the working copy
            if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        public void test_RW_Unsupported_Empty(string fileName, bool deleteTempFile = true)
        {
            new ConsoleLogger();

            // Source : totally metadata-free file
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            if (canMetaNotExist) Assert.IsFalse(meta.Exists);


            bool handleUnsupportedFields = testData.AdditionalFields != null && testData.AdditionalFields.Count > 0;
            bool handleUnsupportedPictures = testData.EmbeddedPictures != null && testData.EmbeddedPictures.Count > 0;
            char internationalChar = supportsInternationalChars ? '父' : '!';

            // Add new unsupported fields
            TagData theTag = new TagData();
            if (handleUnsupportedFields)
            {
                theTag.AdditionalFields.Add(new MetaFieldInfo(tagType, "TEST", "This is a test " + internationalChar));
                theTag.AdditionalFields.Add(new MetaFieldInfo(tagType, "TES2", "This is another test " + internationalChar));
            }

            // Add new unsupported pictures
            PictureInfo picInfo = null;
            byte found = 0;

            object pictureCode1, pictureCode2;
            if (tagType.Equals(MetaDataIOFactory.TagType.APE))
            {
                pictureCode1 = "pic1";
                pictureCode2 = "pic2";
            }
            else
            {
                pictureCode1 = 23;
                pictureCode2 = 24;
            }

            if (handleUnsupportedPictures)
            {
                IList<PictureInfo> pics = new List<PictureInfo>();
                byte[] data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
                picInfo = PictureInfo.fromBinaryData(data, PIC_TYPE.Unsupported, tagType, pictureCode1);
                pics.Add(picInfo);

                data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg");
                picInfo = PictureInfo.fromBinaryData(data, PIC_TYPE.Unsupported, tagType, pictureCode2);
                pics.Add(picInfo);

                theTag.Pictures = pics;
            }

            theFile.UpdateTagInFile(theTag, tagType);

            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (handleUnsupportedFields)
            {
                Assert.AreEqual(2, meta.AdditionalFields.Count);

                Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST"));
                Assert.AreEqual("This is a test " + internationalChar, meta.AdditionalFields["TEST"]);

                Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TES2"));
                Assert.AreEqual("This is another test " + internationalChar, meta.AdditionalFields["TES2"]);
            }

            if (handleUnsupportedPictures)
            {
                Assert.AreEqual(2, meta.EmbeddedPictures.Count);
                found = 0;

                foreach (PictureInfo pic in meta.EmbeddedPictures)
                {
                    if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) &&
                         (pic.NativePicCode.Equals(pictureCode1) || (pic.NativePicCodeStr != null && pic.NativePicCodeStr.Equals(pictureCode1)))
                       )
                    {
                        using (Image picture = Image.FromStream(new MemoryStream(pic.PictureData)))
                        {
                            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                            Assert.AreEqual(600, picture.Height);
                            Assert.AreEqual(900, picture.Width);
                        }
                        found++;
                    }
                    else if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported)
                                && (pic.NativePicCode.Equals(pictureCode2) || (pic.NativePicCodeStr != null && pic.NativePicCodeStr.Equals(pictureCode2)))
                            )
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
            }

            // Remove the additional unsupported field
            if (handleUnsupportedFields || handleUnsupportedPictures)
            {
                TagData newTag = new TagData();

                if (handleUnsupportedFields)
                {
                    MetaFieldInfo fieldInfo = new MetaFieldInfo(tagType, "TEST");
                    fieldInfo.MarkedForDeletion = true;
                    newTag.AdditionalFields.Add(fieldInfo);
                    newTag.AdditionalFields.Add(theTag.AdditionalFields[1]);
                }

                // Remove additional picture
                if (handleUnsupportedPictures)
                {
                    picInfo = new PictureInfo(tagType, pictureCode1);
                    picInfo.MarkedForDeletion = true;
                    newTag.Pictures.Add(picInfo);
                    newTag.Pictures.Add(theTag.Pictures[1]);
                }

                // Add the new tag and check that it has been indeed added with all the correct information
                Assert.IsTrue(theFile.UpdateTagInFile(newTag, tagType));

                Assert.IsTrue(theFile.ReadFromFile(true, true));

                Assert.IsNotNull(theFile.getMeta(tagType));
                meta = theFile.getMeta(tagType);
                Assert.IsTrue(meta.Exists);

                if (handleUnsupportedFields)
                {
                    // Additional removed field
                    Assert.AreEqual(1, meta.AdditionalFields.Count);
                    Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TES2"));
                    Assert.AreEqual("This is another test " + internationalChar, meta.AdditionalFields["TES2"]);
                }

                // Pictures
                if (handleUnsupportedPictures)
                {
                    Assert.AreEqual(1, meta.EmbeddedPictures.Count);

                    found = 0;

                    foreach (PictureInfo pic in meta.EmbeddedPictures)
                    {
                        if (pic.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) &&
                             (pic.NativePicCode.Equals(pictureCode2) || (pic.NativePicCodeStr != null && pic.NativePicCodeStr.Equals(pictureCode2)))
                           )
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
                }
            }

            // Get rid of the working copy
            if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        protected void readExistingTagsOnFile(AudioDataManager theFile, int nbPictures = 2)
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            // Supported fields
            if (testData.Title != "") Assert.AreEqual(testData.Title, meta.Title);
            if (testData.Album != "") Assert.AreEqual(testData.Album, meta.Album);
            if (testData.Artist != "") Assert.AreEqual(testData.Artist, meta.Artist);
            if (testData.AlbumArtist != "") Assert.AreEqual(testData.AlbumArtist, meta.AlbumArtist);
            if (testData.Comment != "") Assert.AreEqual(testData.Comment, meta.Comment);
            if (!supportsDateOrYear)
            {
                if (testData.Date > DateTime.MinValue)
                {
                    Assert.AreEqual(testData.Date, meta.Date);
                }
                if (testData.PublishingDate > DateTime.MinValue)
                {
                    Assert.AreEqual(testData.PublishingDate, meta.PublishingDate);
                }
            }
            else
            {
                Assert.IsNotNull(meta.Date);
                Assert.IsTrue(meta.Date > DateTime.MinValue);
                if (meta.Date != null && meta.Date > DateTime.MinValue && testData.Date != null)
                {
                    Assert.AreEqual(testData.Date, meta.Date);
                }
            }
            if (testData.Genre != "") Assert.AreEqual(testData.Genre, meta.Genre);
            if (testData.Composer != "") Assert.AreEqual(testData.Composer, meta.Composer);
            if (testData.Popularity != 0) Assert.AreEqual(testData.Popularity, meta.Popularity);
            if (testData.Track != 0) Assert.AreEqual(testData.Track, meta.Track);
            if (testData.TrackTotal != 0) Assert.AreEqual(testData.TrackTotal, meta.TrackTotal);
            if (testData.Disc != 0) Assert.AreEqual(testData.Disc, meta.Disc);
            if (testData.DiscTotal != 0) Assert.AreEqual(testData.DiscTotal, meta.DiscTotal);
            if (testData.Conductor != "") Assert.AreEqual(testData.Conductor, meta.Conductor);
            if (testData.Publisher != "") Assert.AreEqual(testData.Publisher, meta.Publisher);
            if (testData.Copyright != "") Assert.AreEqual(testData.Copyright, meta.Copyright);
            if (testData.GeneralDescription != "") Assert.AreEqual(testData.GeneralDescription, meta.GeneralDescription);
            if (testData.ProductId != "") Assert.AreEqual(testData.ProductId, meta.ProductId);

            // Unsupported field
            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                foreach (KeyValuePair<string, string> field in testData.AdditionalFields)
                {
                    Assert.IsTrue(meta.AdditionalFields.Keys.Contains(field.Key), field.Key);
                    Assert.AreEqual(field.Value, meta.AdditionalFields[field.Key], field.Key);
                }
            }

            // Pictures
            if (testData.EmbeddedPictures != null && testData.EmbeddedPictures.Count > 0)
            {
                Assert.AreEqual(nbPictures, meta.EmbeddedPictures.Count);

                byte nbFound = 0;
                foreach (PictureInfo pic in meta.EmbeddedPictures)
                {
                    foreach (PictureInfo testPicInfo in testData.EmbeddedPictures)
                    {
                        if ((pic.NativePicCode > -1 && pic.NativePicCode.Equals(testPicInfo.NativePicCode))
                            || (pic.NativePicCodeStr != null && pic.NativePicCodeStr.Equals(testPicInfo.NativePicCodeStr, System.StringComparison.OrdinalIgnoreCase))
                           )
                        {
                            nbFound++;
                            pic.ComputePicHash();
                            Assert.AreEqual(testPicInfo.PictureHash, pic.PictureHash);
                            break;
                        }
                    }
                }
                Assert.AreEqual(testData.EmbeddedPictures.Count, nbFound);
            }
        }

        protected void assumeRatingInFile(string file, double rating, MetaDataIOFactory.TagType tagType)
        {
            string location = TestUtils.GetResourceLocationRoot() + file;
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile());

            IMetaDataIO meta = theFile.getMeta(tagType);

            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            if (0 == rating)
                Assert.IsTrue(!meta.Popularity.HasValue || (float)rating == meta.Popularity.Value);
            else
                Assert.AreEqual((float)rating, meta.Popularity);
        }

    }
}
