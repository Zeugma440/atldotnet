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
     *  Reading of unsupported tag field
     *  Reading of unsupported picture
     *  
     *  Individual field removal
     *  Whole tag removal
     *  
     *  Add a supported field
     *  
     *  Conservation of unmodified tag items after tag editing
     *  Conservation of unsupported tag field after tag editing
     *  Conservation of supported pictures after tag editing
     *  Conservation of unsupported pictures after tag editing
     *
     */

    /*
     * TODO
     * 
     * FUNCTIONAL
     * 
     * Add an unsupported field
     * Add an unsupported picture
     * Add a supported picture
     * 
     * Remove a picture
     * 
     * Extended header compliance cases incl. limit cases
     * 
     * TECHNICAL
     * 
     * Add a standard unsupported field => persisted as standard field in tag
     * Add a non-standard unsupported field => persisted as TXXX field
     * Exact picture data conservation after tag editing
     * 
    */


    [TestClass]
    public class ID3v2
    {
        protected class PictureInfo
        {
            public Image Picture;
            public byte NativeCode;

            public PictureInfo(Image picture, byte code) { Picture = picture; NativeCode = code; }
        }

        private IList<KeyValuePair<MetaDataIOFactory.PIC_TYPE, PictureInfo>> pictures = new List<KeyValuePair<MetaDataIOFactory.PIC_TYPE, PictureInfo>>();

        protected void readPictureData(ref MemoryStream s, MetaDataIOFactory.PIC_TYPE picType, byte picCode, ImageFormat imgFormat, int originalTag)
        {
            pictures.Add(new KeyValuePair<MetaDataIOFactory.PIC_TYPE, PictureInfo>(picType, new PictureInfo(Image.FromStream(s), picCode)));
        }


        [TestMethod]
        public void TagIO_RW_ID3v2_Empty()
        {
            // Source : tag-free MP3
            String location = "../../Resources/empty.mp3";
            String testFileLocation = location.Replace("empty", "tmp/testID3v2--" + System.DateTime.Now.ToLongTimeString().Replace(":", "."));

            // Create a working copy
            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


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
            theTag.OriginalArtist = "Bob";
            theTag.OriginalAlbum = "Hey Hey";
            theTag.GeneralDescription = "That's right";
            theTag.Publisher = "Test Media Inc.";
            theTag.Conductor = "John Johnson Jr.";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.AreEqual("Test !!", theFile.ID3v2.Title);
            Assert.AreEqual("Album", theFile.ID3v2.Album);
            Assert.AreEqual("Artist", theFile.ID3v2.Artist);
            Assert.AreEqual("Mike", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("This is a test", theFile.ID3v2.Comment);
            Assert.AreEqual("2008", theFile.ID3v2.Year);
            Assert.AreEqual("Merengue", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual(2, theFile.ID3v2.Disc);
            Assert.AreEqual("Me", theFile.ID3v2.Composer);
            Assert.AreEqual("父", theFile.ID3v2.Copyright);
            Assert.AreEqual("Bob", theFile.ID3v2.OriginalArtist);
            Assert.AreEqual("Hey Hey", theFile.ID3v2.OriginalAlbum);
            Assert.AreEqual("That's right", theFile.ID3v2.GeneralDescription);
            Assert.AreEqual("Test Media Inc.", theFile.ID3v2.Publisher);
            Assert.AreEqual("John Johnson Jr.", theFile.ID3v2.Conductor);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


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
        public void TagIO_RW_ID3v2_Existing()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            String testFileLocation = location.Replace("id3v2.3_UTF16", "tmp/testID3v2--" + System.DateTime.Now.ToLongTimeString().Replace(":", "."));

            ConsoleLogger log = new ConsoleLogger();

            // Create a working copy
            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);

            // Add a new supported field
            Assert.IsTrue(theFile.ReadFromFile());

            TagData theTag = new TagData();
            theTag.Conductor = "John Jackman";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            readExistingTagsOnFile(ref theFile);

            // Additional supported field
            Assert.AreEqual("John Jackman", theFile.ID3v2.Conductor);

            // Remove the additional supported field
            theTag = new TagData();
            theTag.Conductor = "";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V2));

            readExistingTagsOnFile(ref theFile);

            // Additional removed field
            Assert.AreEqual("", theFile.ID3v2.Conductor);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)

/* NOT POSSIBLE YET mainly due to unnecessary unsynchronization and tag order
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
        public void TagIO_R_ID3v23_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            readExistingTagsOnFile(ref theFile);
        }

        [TestMethod]
        public void TagIO_R_ID3v24_UTF8()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.4_UTF8.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            readExistingTagsOnFile(ref theFile);
        }

        [TestMethod]
        public void TagIO_RW_ID3v2_Unsupported_Empty()
        {
            // Source : tag-free MP3
            String location = "../../Resources/empty.mp3";
            String testFileLocation = location.Replace("empty", "tmp/testID3v2--" + System.DateTime.Now.ToLongTimeString().Replace(":", "."));

            // Create a working copy
            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


            // Add a new unsupported field
            theFile.ID3v2.OtherFields.Add("TEST", "This is a test 父");
            theFile.AddTagToFile(MetaDataIOFactory.TAG_ID3V2);

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            Assert.IsTrue(theFile.ID3v2.OtherFields.Keys.Contains("TEST"));
            Assert.AreEqual("This is a test 父", theFile.ID3v2.OtherFields["TEST"]);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V2));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsFalse(theFile.ID3v2.Exists);


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

        private void readExistingTagsOnFile(ref IAudioDataIO theFile)
        {
            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new MetaDataIOFactory.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("Title", theFile.ID3v2.Title);
            Assert.AreEqual("父", theFile.ID3v2.Album);
            Assert.AreEqual("Artist", theFile.ID3v2.Artist);
            Assert.AreEqual("Test!", theFile.ID3v2.Comment);
            Assert.AreEqual("2017", theFile.ID3v2.Year);
            Assert.AreEqual("Test", theFile.ID3v2.Genre);
            Assert.AreEqual(22, theFile.ID3v2.Track);
            Assert.AreEqual("Me", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);

            // Unsupported field (MOOD)
            Assert.IsTrue(theFile.ID3v2.OtherFields.Keys.Contains("MOOD"));
            Assert.AreEqual("xxx", theFile.ID3v2.OtherFields["MOOD"]);


            // Pictures
            Assert.AreEqual(2, pictures.Count);

            foreach (KeyValuePair<MetaDataIOFactory.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(MetaDataIOFactory.PIC_TYPE.Front)) // Supported picture
                {
                    Assert.AreEqual(pic.Value.NativeCode, 0x03);
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 150);
                    Assert.AreEqual(picture.Width, 150);
                }
                else if (pic.Key.Equals(MetaDataIOFactory.PIC_TYPE.Unsupported))  // Unsupported picture
                {
                    Assert.AreEqual(pic.Value.NativeCode, 0x09);
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
                    Assert.AreEqual(picture.Height, 168);
                    Assert.AreEqual(picture.Width, 175);
                }
            }
        }
    }
}
