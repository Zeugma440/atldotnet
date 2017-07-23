using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;

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
     * 
     * TECHNICAL
     * 
     * Exact picture data conservation after tag editing
     * 
    */


    [TestClass]
    public class APE
    {
        protected class PictureInfo
        {
            public Image Picture;
            public string NativeCode;

            public PictureInfo(Image picture, object code)
            {
                Picture = picture;
                if (code is string) NativeCode = (string)code;
            }
        }

        private IList<KeyValuePair<TagData.PIC_TYPE, PictureInfo>> pictures = new List<KeyValuePair<TagData.PIC_TYPE, PictureInfo>>();

        protected void readPictureData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            pictures.Add(new KeyValuePair<TagData.PIC_TYPE, PictureInfo>(picType, new PictureInfo(Image.FromStream(s), picCode)));
        }


        [TestMethod]
        public void TagIO_R_APE() // My deepest apologies for this dubious method name
        {
            // Source : MP3 with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (MOOD)
            String location = TestUtils.GetResourceLocationRoot() + "APE.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            readExistingTagsOnFile(ref theFile);
        }

        [TestMethod]
        public void TagIO_RW_APE_Empty()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : tag-free MP3
            string location = TestUtils.GetResourceLocationRoot() + "empty.mp3";
            string testFileLocation = TestUtils.GetTempTestFile("empty.mp3");
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsFalse(theFile.APEtag.Exists);


            // Construct a new tag with the most basic options (no un supported fields, no pictures)
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

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_APE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsTrue(theFile.APEtag.Exists);

            Assert.AreEqual("Test !!", theFile.APEtag.Title);
            Assert.AreEqual("Album", theFile.APEtag.Album);
            Assert.AreEqual("Artist", theFile.APEtag.Artist);
            Assert.AreEqual("Mike", theFile.APEtag.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.APEtag.Comment);
            Assert.AreEqual("2008", theFile.APEtag.Year);
            Assert.AreEqual("Merengue", theFile.APEtag.Genre);
            Assert.AreEqual(1, theFile.APEtag.Track);
            Assert.AreEqual(2, theFile.APEtag.Disc);
            Assert.AreEqual("Me", theFile.APEtag.Composer);
            Assert.AreEqual("父", theFile.APEtag.Copyright);
            Assert.AreEqual("John Johnson Jr.", theFile.APEtag.Conductor);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_APE));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsFalse(theFile.APEtag.Exists);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(testFileInfo.Length, originalFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_APE_Existing()
        {
            ConsoleLogger log = new ConsoleLogger();

            // Source : MP3 with existing tag incl. unsupported picture (Cover Art (Fronk)); unsupported field (MOOD)
            String testFileLocation = TestUtils.GetTempTestFile("APE.mp3");
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);

            // Add a new supported field and a new supported picture
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Conductor = "John Jackman";

            TagData.PictureInfo picInfo = new TagData.PictureInfo(ImageFormat.Jpeg, TagData.PIC_TYPE.Back);
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot()+"pic1.jpg");
            theTag.Pictures.Add(picInfo);


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_APE));

            readExistingTagsOnFile(ref theFile, 3);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.APEtag.Conductor);

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Back))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    break;
                }
            }


            // Remove the additional supported field
            theTag = new TagData();
            theTag.Conductor = "";

            // Remove additional picture
            picInfo = new TagData.PictureInfo(ImageFormat.Jpeg, TagData.PIC_TYPE.Back);
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_APE));

            readExistingTagsOnFile(ref theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.APEtag.Conductor);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)

/* NOT POSSIBLE YET mainly due to tag order and tag name (e.g. "Disc" vs. "Discnumber") differences
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(testFileInfo.Length, originalFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));
*/
            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_APE_Unsupported_Empty()
        {
            // Source : tag-free MP3
            String testFileLocation = TestUtils.GetTempTestFile("empty.mp3");
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsFalse(theFile.APEtag.Exists);


            // Add new unsupported fields
            TagData theTag = new TagData();
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_APE, "TEST", "This is a test 父"));
            theTag.AdditionalFields.Add(new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_APE, "TEST2", "This is another test 父"));

            // Add new unsupported pictures
            TagData.PictureInfo picInfo = new TagData.PictureInfo(ImageFormat.Jpeg, MetaDataIOFactory.TAG_APE, "Hey");
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "pic1.jpg");
            theTag.Pictures.Add(picInfo);
            picInfo = new TagData.PictureInfo(ImageFormat.Jpeg, MetaDataIOFactory.TAG_APE, "Ho");
            picInfo.PictureData = File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "pic2.jpg");
            theTag.Pictures.Add(picInfo);


            theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_APE);

            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsTrue(theFile.APEtag.Exists);

            Assert.AreEqual(2, theFile.APEtag.AdditionalFields.Count);

            Assert.IsTrue(theFile.APEtag.AdditionalFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.APEtag.AdditionalFields["TEST"]);

            Assert.IsTrue(theFile.APEtag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.APEtag.AdditionalFields["TEST2"]);

            Assert.AreEqual(2, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCode.Equals("Hey"))
                {
                    Image picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 600);
                    Assert.AreEqual(picture.Width, 900);
                    found++;
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCode.Equals("Ho"))
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
            TagData.MetaFieldInfo fieldInfo = new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_APE, "TEST");
            fieldInfo.MarkedForDeletion = true;
            theTag.AdditionalFields.Add(fieldInfo);

            // Remove additional picture
            picInfo = new TagData.PictureInfo(ImageFormat.Jpeg, MetaDataIOFactory.TAG_APE, "Hey");
            picInfo.MarkedForDeletion = true;
            theTag.Pictures.Add(picInfo);

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_APE));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsTrue(theFile.APEtag.Exists);

            // Additional removed field
            Assert.AreEqual(1, theFile.APEtag.AdditionalFields.Count);
            Assert.IsTrue(theFile.APEtag.AdditionalFields.Keys.Contains("TEST2"));
            Assert.AreEqual("This is another test 父", theFile.APEtag.AdditionalFields["TEST2"]);

            // Pictures
            Assert.AreEqual(1, pictures.Count);

            found = 0;

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported) && pic.Value.NativeCode.Equals("Ho"))
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
            File.Delete(testFileLocation);
        }

        private void readExistingTagsOnFile(ref IAudioDataIO theFile, int nbPictures = 2)
        {
            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.APEtag);
            Assert.IsTrue(theFile.APEtag.Exists);

            // Supported fields
            Assert.AreEqual("Title", theFile.APEtag.Title);
            Assert.AreEqual("父", theFile.APEtag.Album);
            Assert.AreEqual("Artist", theFile.APEtag.Artist);
            Assert.AreEqual("Test!", theFile.APEtag.Comment);
            Assert.AreEqual("2017", theFile.APEtag.Year);
            Assert.AreEqual("Test", theFile.APEtag.Genre);
            Assert.AreEqual(22, theFile.APEtag.Track);
            Assert.AreEqual("Me", theFile.APEtag.Composer);
            Assert.AreEqual(2, theFile.APEtag.Disc);

            // Unsupported field (MOOD)
            Assert.IsTrue(theFile.APEtag.AdditionalFields.Keys.Contains("MOOD"));
            Assert.AreEqual("xxx", theFile.APEtag.AdditionalFields["MOOD"]);


            // Pictures
            Assert.AreEqual(nbPictures, pictures.Count);

            foreach (KeyValuePair<TagData.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(TagData.PIC_TYPE.Front)) // Supported picture
                {
                    Assert.AreEqual(pic.Value.NativeCode, "Cover Art (Front)");
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 150);
                    Assert.AreEqual(picture.Width, 150);
                }
                else if (pic.Key.Equals(TagData.PIC_TYPE.Unsupported))  // Unsupported picture
                {
                    Assert.AreEqual(pic.Value.NativeCode, "Cover Art (Fronk)");
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Height, 168);
                    Assert.AreEqual(picture.Width, 175);
                }
            }
        }
    }
}
