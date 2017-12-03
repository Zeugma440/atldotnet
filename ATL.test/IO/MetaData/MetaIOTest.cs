using ATL.AudioData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ATL.test.IO.MetaData
{
    /* TODO generic tests to add
     *   - Correct update of tagData within the IMetaDataIO just after an Update/Remove (by comparing with the same TagData obtained with Read)
     *   - Individual picture removal (from index > 1)
     *   - Exact picture binary data conservation after tag editing
    */
    public class MetaIOTest
    {
        protected class PictureInfo
        {
            public Image Picture;
            public TagData.PictureInfo info;

            public PictureInfo(Image picture, Commons.ImageFormat imgFormat, object code)
            {
                Picture = picture;
                info = new TagData.PictureInfo(imgFormat, MetaDataIOFactory.TAG_ANY, code);
            }

            // Retrocompatibility with old interface
            public int PictureCodeInt
            {
                get { return info.NativePicCode;  }
            }
            public string PictureCodeStr
            {
                get { return info.NativePicCodeStr; }
            }
        }

        protected IList<KeyValuePair<TagData.PIC_TYPE, PictureInfo>> pictures = new List<KeyValuePair<TagData.PIC_TYPE, PictureInfo>>();

        protected void readPictureData(ref MemoryStream s, TagData.PIC_TYPE picType, Commons.ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            pictures.Add(new KeyValuePair<TagData.PIC_TYPE, PictureInfo>(picType, new PictureInfo(Image.FromStream(s), imgFormat, picCode)));
        }


        protected string emptyFile;
        protected string notEmptyFile;
        protected int tagType;
        protected IList<byte> unsupportedFields = new List<byte>();
        protected TagData testData;

        public MetaIOTest()
        {
            // Initialize default test data
            testData = new TagData();

            testData.Title = "Title";
            testData.Album = "父";
            testData.Artist = "Artist";
            testData.AlbumArtist = "Bob"; 
            testData.Comment = "Test!";
            testData.RecordingYear = "2017";
            testData.RecordingDate = ""; // Empty string means "supported, but not valued in test sample"
            testData.Genre = "Test";
            testData.Rating = "0";
            testData.TrackNumber = "22";
            testData.Composer = "Me";
            testData.Conductor = "";
            testData.Publisher = "";
            testData.DiscNumber = "2";
            testData.Copyright = "";
            testData.GeneralDescription = "";

            testData.AdditionalFields = new List<TagData.MetaFieldInfo>();
            testData.AdditionalFields.Add(new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_ANY, "TEST", "xxx"));
        }

        protected void test_RW_Cohabitation(int tagType1, int tagType2, bool canMeta1NotExist = true)
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : empty file
            string location = TestUtils.GetResourceLocationRoot() + emptyFile;
            string testFileLocation = TestUtils.GetTempTestFile(emptyFile);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            IMetaDataIO meta1 = theFile.getMeta(tagType1);
            IMetaDataIO meta2 = theFile.getMeta(tagType2);

            Assert.IsNotNull(meta1);
            if (canMeta1NotExist) Assert.IsFalse(meta1.Exists);
            Assert.IsNotNull(meta2);
            Assert.IsFalse(meta2.Exists);

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

            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.AreEqual(originalMD5, testMD5);

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        protected void test_RW_Existing(string fileName, int initialNbPictures, bool deleteTempFile = true, bool sameSizeAfterEdit = false, bool sameBitsAfterEdit = false)
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : file with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.GetTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Conductor = "John Jackman";
            testData.Conductor = "John Jackman";

            TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.CD);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
            theTag.Pictures.Add(picInfo);


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures + 1);

            int nbFound = 0;
            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.CD))
                {
                    if (tagType.Equals(MetaDataIOFactory.TAG_APE))
                    {
                        Assert.AreEqual("Cover Art (Media)", pic.Value.info.NativePicCodeStr);
                    }
                    else // ID3v2 convention
                    {
                        Assert.AreEqual(0x06, pic.Value.info.NativePicCode);
                    }
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    nbFound++;
                    break;
                }
            }

            Assert.AreEqual(1, nbFound);

            // Remove the additional supported field
            theTag = new TagData();
            theTag.Conductor = "";
            testData.Conductor = "";

            // Remove additional picture
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.CD);
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
            if (deleteTempFile) File.Delete(testFileLocation);
        }

        public void test_RW_Empty(string fileName, bool deleteTempFile = true, bool sameSizeAfterEdit = false, bool sameBitsAfterEdit = false)
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.GetTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));


            // Check that it is indeed metadata-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            Assert.IsFalse(theFile.getMeta(tagType).Exists);

            // Construct a new tag
            TagData theTag = new TagData();
            if (testData.Title != null) theTag.Title = "Test !!";
            if (testData.Album != null) theTag.Album = "Album";
            if (testData.Artist != null) theTag.Artist = "Artist";
            if (testData.AlbumArtist != null) theTag.AlbumArtist = "Mike";
            if (testData.Comment != null) theTag.Comment = "This is a test";
            if (testData.RecordingYear != null) theTag.RecordingYear = "2008";
            if (testData.RecordingDate != null) theTag.RecordingDate = "2008/01/01";
            if (testData.Genre != null) theTag.Genre = "Merengue";
            if (testData.Rating != null) theTag.Rating = "5";
            if (testData.TrackNumber != null) theTag.TrackNumber = "01/01";
            if (testData.DiscNumber != null) theTag.DiscNumber = "2";
            if (testData.Composer != null) theTag.Composer = "Me";
            if (testData.Copyright != null) theTag.Copyright = "父";
            if (testData.Conductor != null) theTag.Conductor = "John Johnson Jr.";
            if (testData.Publisher != null) theTag.Publisher = "Z Corp.";
            if (testData.GeneralDescription != null) theTag.GeneralDescription = "Description";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (testData.Title != null) Assert.AreEqual("Test !!", meta.Title);
            if (testData.Album != null) Assert.AreEqual("Album", meta.Album);
            if (testData.Artist != null) Assert.AreEqual("Artist", meta.Artist);
            if (testData.AlbumArtist != null) Assert.AreEqual("Mike", meta.AlbumArtist);
            if (testData.Comment != null) Assert.AreEqual("This is a test", meta.Comment);
            if (testData.RecordingYear != null) Assert.AreEqual("2008", meta.Year);
            if (testData.Genre != null) Assert.AreEqual("Merengue", meta.Genre);
            if (testData.Rating != null) Assert.AreEqual(5, meta.Rating);
            if (testData.TrackNumber != null) Assert.AreEqual(1, meta.Track);
            if (testData.DiscNumber != null) Assert.AreEqual(2, meta.Disc);
            if (testData.Composer != null) Assert.AreEqual("Me", meta.Composer);
            if (testData.Copyright != null) Assert.AreEqual("父", meta.Copyright);
            if (testData.Conductor != null) Assert.AreEqual("John Johnson Jr.", meta.Conductor);
            if (testData.Publisher != null) Assert.AreEqual("Z Corp.", meta.Publisher);
            if (testData.GeneralDescription != null) Assert.AreEqual("Description", meta.GeneralDescription);

            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(tagType));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            Assert.IsFalse(theFile.getMeta(tagType).Exists);


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
            if (deleteTempFile) File.Delete(testFileLocation);
        }

        public void test_RW_Unsupported_Empty(string fileName, bool deleteTempFile = true)
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : totally metadata-free file
            string location = TestUtils.GetResourceLocationRoot() + fileName;
            string testFileLocation = TestUtils.GetTempTestFile(fileName);
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation));

            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsFalse(meta.Exists);


            // Add new unsupported fields
            bool handleUnsupportedFields = (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0);
            TagData theTag = new TagData();
            if (handleUnsupportedFields)
            {
                theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(tagType, "TEST", "This is a test 父"));
                theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(tagType, "TEST2", "This is another test 父"));
            }

            // Add new unsupported pictures
            TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, tagType, "Hey");
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
            theTag.Pictures.Add(picInfo);
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, tagType, "Ho");
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg");
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, tagType);

            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (handleUnsupportedFields)
            {
                Assert.AreEqual(2, meta.AdditionalFields.Count);

                Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST"));
                Assert.AreEqual("This is a test 父", meta.AdditionalFields["TEST"]);

                Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST2"));
                Assert.AreEqual("This is another test 父", meta.AdditionalFields["TEST2"]);
            }

            Assert.AreEqual(2, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.info.NativePicCodeStr.Equals("Hey"))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.info.NativePicCodeStr.Equals("Ho"))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(2, found);

            // Remove the additional unsupported field
            if (handleUnsupportedFields)
            {
                theTag = new TagData();
                TagData.MetaFieldInfo fieldInfo = new TagData.MetaFieldInfo(tagType, "TEST");
                fieldInfo.MarkedForDeletion = true;
                theTag.AdditionalFields.Add(fieldInfo);
            }

            // Remove additional picture
            picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, tagType, "Hey");
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            if (handleUnsupportedFields)
            {
                // Additional removed field
                Assert.AreEqual(1, meta.AdditionalFields.Count);
                Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST2"));
                Assert.AreEqual("This is another test 父", meta.AdditionalFields["TEST2"]);
            }

            // Pictures
            Assert.AreEqual(1, pictures.Count);

            found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.info.NativePicCodeStr.Equals("Ho"))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 290);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
            }

            Assert.AreEqual(1, found);

            // Get rid of the working copy
            if (deleteTempFile) File.Delete(testFileLocation);
        }

        protected void readExistingTagsOnFile(AudioDataManager theFile, int nbPictures = 2)
        {
            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            // Supported fields
            if (testData.Title != null) Assert.AreEqual(testData.Title, meta.Title);
            if (testData.Album != null) Assert.AreEqual(testData.Album, meta.Album);
            if (testData.Artist != null) Assert.AreEqual(testData.Artist, meta.Artist);
            if (testData.AlbumArtist != null) Assert.AreEqual(testData.AlbumArtist, meta.AlbumArtist);
            if (testData.Comment != null) Assert.AreEqual(testData.Comment, meta.Comment);
            if (testData.RecordingYear != null) Assert.AreEqual(testData.RecordingYear, meta.Year);
            //if (testData.RecordingDate != null) Assert.AreEqual(testData.RecordingDate, meta.);
            if (testData.Genre != null) Assert.AreEqual(testData.Genre, meta.Genre);
            if (testData.Rating != null) Assert.AreEqual(testData.Rating, meta.Rating.ToString());
            if (testData.TrackNumber != null) Assert.AreEqual(testData.TrackNumber, meta.Track.ToString());
            if (testData.Composer != null) Assert.AreEqual(testData.Composer, meta.Composer);
            if (testData.DiscNumber != null) Assert.AreEqual(testData.DiscNumber, meta.Disc.ToString());
            if (testData.Conductor != null) Assert.AreEqual(testData.Conductor, meta.Conductor);
            if (testData.Publisher != null) Assert.AreEqual(testData.Publisher, meta.Publisher);
            if (testData.Copyright != null) Assert.AreEqual(testData.Copyright, meta.Copyright);
            if (testData.GeneralDescription != null) Assert.AreEqual(testData.GeneralDescription, meta.GeneralDescription);

            // Unsupported field
            if (testData.AdditionalFields != null && testData.AdditionalFields.Count > 0)
            {
                foreach (TagData.MetaFieldInfo field in testData.AdditionalFields)
                {
                    Assert.IsTrue(meta.AdditionalFields.Keys.Contains(field.NativeFieldCode));
                    Assert.AreEqual(field.Value, meta.AdditionalFields[field.NativeFieldCode]);
                }
            }

            // Pictures
            Assert.AreEqual(nbPictures, pictures.Count);

            byte nbFound = 0;
            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Front)) // Supported picture
                {
                    if (tagType.Equals(MetaDataIOFactory.TAG_APE))
                    {
                        Assert.AreEqual("Cover Art (Front)", pic.Value.info.NativePicCodeStr);
                    }
                    else // ID3v2 convention
                    {
                        Assert.AreEqual(0x03, pic.Value.info.NativePicCode);
                    }
                    picture = pic.Value.Picture;
                    Assert.AreEqual(Commons.ImageFormat.Jpeg, pic.Value.info.NativeFormat);
                    Assert.AreEqual(150, picture.Height);
                    Assert.AreEqual(150, picture.Width);
                    nbFound++;
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported))  // Unsupported picture
                {
                    if (tagType.Equals(MetaDataIOFactory.TAG_APE))
                    {
                        Assert.AreEqual("Cover Art (Icon)", pic.Value.info.NativePicCodeStr);
                    }
                    else // ID3v2 convention
                    {
                        Assert.AreEqual(0x02, pic.Value.info.NativePicCode);
                    }
                    picture = pic.Value.Picture;
                    Assert.AreEqual(168, picture.Height);
                    Assert.AreEqual(175, picture.Width);
                    nbFound++;
                }
            }
            Assert.AreEqual(2, nbFound);
        }

    }
}
