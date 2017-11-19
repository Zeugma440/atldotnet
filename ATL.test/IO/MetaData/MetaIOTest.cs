using ATL.AudioData;
using Commons;
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

            // This also tests if physical data can still be read (i.e. MP4 structure has not been scrambled by the apparition of the non-native tag)
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

            TagData.PictureInfo picInfo = new TagData.PictureInfo(Commons.ImageFormat.Jpeg, TagData.PIC_TYPE.CD);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
            theTag.Pictures.Add(picInfo);


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            readExistingTagsOnFile(theFile, initialNbPictures + 1);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.getMeta(tagType).Conductor);

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
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.AlbumArtist = "Mike";
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008";
            theTag.RecordingDate = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";
            theTag.DiscNumber = "2";
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.Conductor = "John Johnson Jr.";
            if (!unsupportedFields.Contains(TagData.TAG_FIELD_PUBLISHER)) theTag.Publisher = "Z Corp.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, tagType));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.getMeta(tagType));
            IMetaDataIO meta = theFile.getMeta(tagType);
            Assert.IsTrue(meta.Exists);

            Assert.AreEqual("Test !!", meta.Title);
            Assert.AreEqual("Album", meta.Album);
            Assert.AreEqual("Artist", meta.Artist);
            Assert.AreEqual("Mike", meta.AlbumArtist);
            Assert.AreEqual("This is a test", meta.Comment);
            Assert.AreEqual("2008", meta.Year);
            Assert.AreEqual("Merengue", meta.Genre);
            Assert.AreEqual(1, meta.Track);
            Assert.AreEqual(2, meta.Disc);
            Assert.AreEqual("Me", meta.Composer);
            Assert.AreEqual("父", meta.Copyright);
            Assert.AreEqual("John Johnson Jr.", meta.Conductor);
            if (!unsupportedFields.Contains(TagData.TAG_FIELD_PUBLISHER)) Assert.AreEqual("Z Corp.", meta.Publisher);


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
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(tagType, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(tagType, "TEST2", "This is another test 父"));

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

            Assert.AreEqual(2, meta.AdditionalFields.Count);

            Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", meta.AdditionalFields["TEST"]);

            Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", meta.AdditionalFields["TEST2"]);

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
            theTag = new TagData();
            TagData.MetaFieldInfo fieldInfo = new TagData.MetaFieldInfo(tagType, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

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

            // Additional removed field
            Assert.AreEqual(1, meta.AdditionalFields.Count);
            Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", meta.AdditionalFields["TEST2"]);

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
            Assert.AreEqual("Title", meta.Title);
            Assert.AreEqual("父", meta.Album);
            Assert.AreEqual("Artist", meta.Artist);
            Assert.AreEqual("Test!", meta.Comment);
            Assert.AreEqual("2017", meta.Year);
            Assert.AreEqual("Test", meta.Genre);
            Assert.AreEqual(22, meta.Track);
            Assert.AreEqual("Me", meta.Composer);
            Assert.AreEqual(2, meta.Disc);

            // Unsupported field (TEST)
            Assert.IsTrue(meta.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("xxx", meta.AdditionalFields["TEST"]);


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
