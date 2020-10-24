using ATL.AudioData;
using Commons;
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
        protected int tagType;
        protected TagData testData;
        protected bool supportsDateOrYear = false;
        protected bool supportsInternationalChars = true;
        protected bool canMetaNotExist = true;

        public delegate void StreamDelegate(FileStream fs);

        public MetaIOTest()
        {
            // Initialize default test data
            testData = new TagData();

            testData.Title = "aa父bb";
            testData.Artist = "֎FATHER֎";
            testData.Album = "Papa֍rules";
            testData.AlbumArtist = "aaᱬbb";
            testData.Comment = "父父!";
            testData.RecordingYear = "1997";
            testData.RecordingDate = "1997-06-20T04:04:04";
            testData.Genre = "House";
            testData.Rating = "0";
            testData.TrackNumber = "01";
            testData.TrackTotal = "02";
            testData.Composer = "ccᱬdd";
            testData.Conductor = "";  // Empty string means "supported, but not valued in test sample"
            testData.Publisher = "";
            testData.DiscNumber = "03";
            testData.DiscTotal = "04";
            testData.Copyright = "";
            testData.GeneralDescription = "";

            testData.AdditionalFields = new List<MetaFieldInfo>();
            testData.AdditionalFields.Add(new MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "TEST", "xxx"));

            testData.Pictures = new List<PictureInfo>();
            PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg"), PIC_TYPE.Unsupported, MetaDataIOFactory.TAG_ANY, 0x03);
            pic.ComputePicHash();
            testData.Pictures.Add(pic);

            pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.png"), PIC_TYPE.Unsupported, MetaDataIOFactory.TAG_ANY, 0x02);
            pic.ComputePicHash();
            testData.Pictures.Add(pic);
        }

        protected void test_RW_Cohabitation(int tagType1, int tagType2, bool canMeta1NotExist = true)
        {
            ConsoleLogger log = new ConsoleLogger();

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
            TagData theTag1 = new TagData();
            theTag1.Title = "Test1";
            theTag1.Album = "Album1";

            TagData theTag2 = new TagData();
            theTag2.Title = "Test2";
            theTag2.Album = "Album2";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag1, tagType1));
            Assert.IsTrue(theFile.UpdateTagInFile(theTag2, tagType2));

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
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();

            char internationalChar = supportsInternationalChars ? '父' : '!';

            TagData initialTestData = new TagData(testData);
            // These two cases cover all tag capabilities
            if (testData.Title != null) theTag.Title = "Test !!" + internationalChar;
            else if (testData.GeneralDescription != null) theTag.GeneralDescription = "Description" + internationalChar;

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                theTag.AdditionalFields = new List<MetaFieldInfo>();
                foreach (MetaFieldInfo info in testData.AdditionalFields)
                {
                    theTag.AdditionalFields.Add(info);
                    break; // 1 is enough
                }
            }
            testData = new TagData(theTag);

            PictureInfo picInfo = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"), PictureInfo.PIC_TYPE.CD);
            theTag.Pictures.Add(picInfo);


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures + 1);
            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (testData.Pictures != null && testData.Pictures.Count > 0)
            {
                int nbFound = 0;
                foreach (PictureInfo pic in meta.EmbeddedPictures)
                {
                    if (pic.PicType.Equals(PictureInfo.PIC_TYPE.CD))
                    {
                        if (tagType.Equals(MetaDataIOFactory.TAG_APE))
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
            theTag = new TagData(initialTestData);
            testData = new TagData(initialTestData);

            // Remove additional picture
            picInfo = new PictureInfo(PictureInfo.PIC_TYPE.CD);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures);

            // Additional removed field
            Assert.AreEqual("", theFile.getMeta(tagType).Conductor);


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
            ConsoleLogger log = new ConsoleLogger();

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
            ConsoleLogger log = new ConsoleLogger();

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
            TagData theTag = new TagData();
            if (testData.Title != null) theTag.Title = "Test !!";
            if (testData.Album != null) theTag.Album = "Album";
            if (testData.Artist != null) theTag.Artist = "Artist";
            if (testData.AlbumArtist != null) theTag.AlbumArtist = "Mike";
            if (testData.Comment != null) theTag.Comment = "This is a test";
            if (testData.RecordingYear != null) theTag.RecordingYear = "2008";
            if (testData.RecordingDate != null) theTag.RecordingDate = "2008/01/01";
            if (testData.PublishingDate != null) theTag.PublishingDate = "2007/02/02";
            if (testData.Genre != null) theTag.Genre = "Merengue";
            if (testData.Rating != null) theTag.Rating = 2.5.ToString();
            if (testData.TrackNumber != null) theTag.TrackNumber = "01";
            if (testData.TrackTotal != null) theTag.TrackTotal = "02";
            if (testData.DiscNumber != null) theTag.DiscNumber = "03";
            if (testData.DiscTotal != null) theTag.DiscTotal = "04";
            if (testData.Composer != null) theTag.Composer = "Me";
            if (testData.Copyright != null) theTag.Copyright = "a" + internationalChar + "a";
            if (testData.Conductor != null) theTag.Conductor = "John Johnson Jr.";
            if (testData.Publisher != null) theTag.Publisher = "Z Corp.";
            if (testData.GeneralDescription != null) theTag.GeneralDescription = "Description";

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                theTag.AdditionalFields = new List<MetaFieldInfo>();
                foreach (MetaFieldInfo info in testData.AdditionalFields)
                {
                    theTag.AdditionalFields.Add(info);
                }
            }

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            Assert.IsTrue(theFile.ReadFromFile(false, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (testData.Title != null) Assert.AreEqual("Test !!", meta.Title);
            if (testData.Album != null) Assert.AreEqual("Album", meta.Album);
            if (testData.Artist != null) Assert.AreEqual("Artist", meta.Artist);
            if (testData.AlbumArtist != null) Assert.AreEqual("Mike", meta.AlbumArtist);
            if (testData.Comment != null) Assert.AreEqual("This is a test", meta.Comment);
            if (!supportsDateOrYear)
            {
                if (testData.RecordingYear != null) Assert.AreEqual("2008", meta.Year);
                if (testData.RecordingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2008/01/01", out date));
                    Assert.AreEqual(date, meta.Date);
                }
                if (testData.PublishingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2007/02/02", out date));
                    Assert.AreEqual(date, meta.PublishingDate);
                }
            }
            else
            {
                Assert.IsTrue(meta.Year != null || (meta.Date != null && meta.Date > DateTime.MinValue));
                if (meta.Year != null && testData.RecordingYear != null) Assert.AreEqual("2008", meta.Year);
                if (meta.Date != null && meta.Date > DateTime.MinValue && testData.RecordingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse("2008/01/01", out date));
                    Assert.AreEqual(date, meta.Date);
                }
            }
            if (testData.Genre != null) Assert.AreEqual("Merengue", meta.Genre);
            if (testData.Rating != null) Assert.AreEqual((float)(2.5 / 5), meta.Popularity);
            if (testData.TrackNumber != null) Assert.AreEqual(1, meta.Track);
            if (testData.TrackTotal != null) Assert.AreEqual(2, meta.TrackTotal);
            if (testData.DiscNumber != null) Assert.AreEqual(3, meta.Disc);
            if (testData.DiscTotal != null) Assert.AreEqual(4, meta.DiscTotal);
            if (testData.Composer != null) Assert.AreEqual("Me", meta.Composer);
            if (testData.Copyright != null) Assert.AreEqual("a" + internationalChar + "a", meta.Copyright);
            if (testData.Conductor != null) Assert.AreEqual("John Johnson Jr.", meta.Conductor);
            if (testData.Publisher != null) Assert.AreEqual("Z Corp.", meta.Publisher);
            if (testData.GeneralDescription != null) Assert.AreEqual("Description", meta.GeneralDescription);

            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                foreach (MetaFieldInfo info in testData.AdditionalFields)
                {
                    Assert.IsTrue(meta.AdditionalFields.ContainsKey(info.NativeFieldCode));
                    Assert.AreEqual(info.Value, meta.AdditionalFields[info.NativeFieldCode]);
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
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.CopyAsTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            if (canMetaNotExist) Assert.IsFalse(meta.Exists);


            bool handleUnsupportedFields = (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0);
            bool handleUnsupportedPictures = (testData.Pictures != null && testData.Pictures.Count > 0);
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
            if (tagType.Equals(MetaDataIOFactory.TAG_APE))
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
                byte[] data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
                picInfo = PictureInfo.fromBinaryData(data, PIC_TYPE.Unsupported, tagType, pictureCode1);
                theTag.Pictures.Add(picInfo);

                data = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg");
                picInfo = PictureInfo.fromBinaryData(data, PIC_TYPE.Unsupported, tagType, pictureCode2);
                theTag.Pictures.Add(picInfo);
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
            if (handleUnsupportedFields)
            {
                theTag = new TagData();
                MetaFieldInfo fieldInfo = new MetaFieldInfo(tagType, "TEST");
                fieldInfo.MarkedForDeletion = true;
                theTag.AdditionalFields.Add(fieldInfo);
            }

            // Remove additional picture
            if (handleUnsupportedPictures)
            {
                picInfo = new PictureInfo(tagType, pictureCode1);
                picInfo.MarkedForDeletion = true;
                theTag.Pictures.Add(picInfo);
            }

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

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

            // Get rid of the working copy
            if (deleteTempFile && Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
        }

        protected void readExistingTagsOnFile(AudioDataManager theFile, int nbPictures = 2)
        {
            Assert.IsTrue(theFile.ReadFromFile(true, true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            // Supported fields
            if (testData.Title != null) Assert.AreEqual(testData.Title, meta.Title);
            if (testData.Album != null) Assert.AreEqual(testData.Album, meta.Album);
            if (testData.Artist != null) Assert.AreEqual(testData.Artist, meta.Artist);
            if (testData.AlbumArtist != null) Assert.AreEqual(testData.AlbumArtist, meta.AlbumArtist);
            if (testData.Comment != null) Assert.AreEqual(testData.Comment, meta.Comment);
            if (!supportsDateOrYear)
            {
                if (testData.RecordingYear != null) Assert.AreEqual(testData.RecordingYear, meta.Year);
                if (testData.RecordingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse(testData.RecordingDate, out date));
                    Assert.AreEqual(date, meta.Date);
                }
                if (testData.PublishingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse(testData.PublishingDate, out date));
                    Assert.AreEqual(date, meta.PublishingDate);
                }
            } else
            {
                Assert.IsTrue(meta.Year != null || (meta.Date != null && meta.Date > DateTime.MinValue));
                if (meta.Year != null && testData.RecordingYear != null) Assert.AreEqual(testData.RecordingYear, meta.Year);
                if (meta.Date != null && meta.Date > DateTime.MinValue && testData.RecordingDate != null)
                {
                    DateTime date;
                    Assert.IsTrue(DateTime.TryParse(testData.RecordingDate, out date));
                    Assert.AreEqual(date, meta.Date);
                }
            }
            if (testData.Genre != null) Assert.AreEqual(testData.Genre, meta.Genre);
            if (testData.Rating != null)
            {
                if (Utils.IsNumeric(testData.Rating))
                {
                    float f = float.Parse(testData.Rating);
                    Assert.AreEqual((f / 5.0).ToString(), meta.Popularity.ToString());
                }
                else if (0 == testData.Rating.Length)
                {
                    Assert.AreEqual("0", meta.Popularity.ToString());
                }
                else
                {
                    Assert.AreEqual(testData.Rating, meta.Popularity.ToString());
                }
            }
            if (testData.TrackNumber != null) Assert.AreEqual(ushort.Parse(testData.TrackNumber), meta.Track);
            if (testData.TrackTotal != null) Assert.AreEqual(ushort.Parse(testData.TrackTotal), meta.TrackTotal);
            if (testData.Composer != null) Assert.AreEqual(testData.Composer, meta.Composer);
            if (testData.DiscNumber != null) Assert.AreEqual(ushort.Parse(testData.DiscNumber), meta.Disc);
            if (testData.DiscTotal != null) Assert.AreEqual(ushort.Parse(testData.DiscTotal), meta.DiscTotal);
            if (testData.Conductor != null) Assert.AreEqual(testData.Conductor, meta.Conductor);
            if (testData.Publisher != null) Assert.AreEqual(testData.Publisher, meta.Publisher);
            if (testData.Copyright != null) Assert.AreEqual(testData.Copyright, meta.Copyright);
            if (testData.GeneralDescription != null) Assert.AreEqual(testData.GeneralDescription, meta.GeneralDescription);

            // Unsupported field
            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                foreach (MetaFieldInfo field in testData.AdditionalFields)
                {
                    Assert.IsTrue(meta.AdditionalFields.Keys.Contains(field.NativeFieldCode));
                    Assert.AreEqual(field.Value, meta.AdditionalFields[field.NativeFieldCode]);
                }
            }

            // Pictures
            if (testData.Pictures != null && testData.Pictures.Count > 0)
            {
                Assert.AreEqual(nbPictures, meta.EmbeddedPictures.Count);

                byte nbFound = 0;
                foreach (PictureInfo pic in meta.EmbeddedPictures)
                {
                    foreach (PictureInfo testPicInfo in testData.Pictures)
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
                Assert.AreEqual(testData.Pictures.Count, nbFound);
            }
        }

        protected void assumeRatingInFile(string file, double rating, int tagType)
        {
            string location = TestUtils.GetResourceLocationRoot() + file;
            AudioDataManager theFile = new AudioDataManager(ATL.AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            Assert.IsTrue(theFile.ReadFromFile());

            IMetaDataIO meta = theFile.getMeta(tagType);

            Assert.IsNotNull(meta);
            Assert.IsTrue(meta.Exists);

            //Assert.IsTrue(Math.Abs(rating - meta.Popularity) < 0.01);
            Assert.AreEqual((float)rating, meta.Popularity);
        }

    }
}
