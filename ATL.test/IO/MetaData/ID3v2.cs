using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v2
    {
        /* TODO
         * 
         * add a standard unsupported field => standard field
         * add a non-standard unsuported field => TXXX field
         * 
         * conservation of unmodified tag items
         * individual field removal
         x whole tag removal
         * 
         x Test reading of unsupported tag field
         * Test conservation of unsupported tag field while rewriting tag
         * 
         * Test conservation of unsupported image type
         * Test conservation of unedited field
         * 
         * Implement an extended header compliance option and test limit cases
         */

        [TestMethod]
        public void TagIO_RW_ID3v2_Empty()
        {
            // Source : tag-free MP3
            String location = "../../Resources/empty.mp3";
            String testFileLocation = location.Replace("empty", "tmp/testID3v2"+ System.DateTime.Now.ToShortTimeString().Replace(":", "."));

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
        public void TagIO_RW_ID3v2_Existing() // TODO DO ACTUAL STUFF !
        {
/*
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            String testFileLocation = location.Replace("id3v2.3_UTF16", "tmp/testID3v2" + System.DateTime.Now.ToShortTimeString().Replace(":", "."));

            // Create a working copy
            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);

            // Construct a new tag
            Assert.IsTrue(theFile.ReadFromFile());
            
            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);
            Assert.AreEqual("Title", theFile.ID3v2.Title);
            Assert.AreEqual("父", theFile.ID3v2.Album);
            Assert.AreEqual("Artist", theFile.ID3v2.Artist);
            Assert.AreEqual("Test!", theFile.ID3v2.Comment);
            Assert.AreEqual("2017", theFile.ID3v2.Year);
            Assert.AreEqual("Test", theFile.ID3v2.Genre);
            Assert.AreEqual(22, theFile.ID3v2.Track);
            Assert.AreEqual("Me", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);


            // Check that the resulting file (working copy that has been tagged, then untagged) remains identical to the original file (i.e. no byte lost nor added)
            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(testFileInfo.Length, originalFileInfo.Length);

            string originalMD5 = TestUtils.GetFileMD5Hash(location);
            string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            // Get rid of the working copy
            File.Delete(testFileLocation);
*/
        }

        [TestMethod]
        public void TagIO_R_ID3v23_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            Assert.IsTrue(theFile.ReadFromFile(null, true));

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
            Assert.AreEqual("xxx",theFile.ID3v2.OtherFields["MOOD"]);


            // Supported picture
            /*
                        Image picture = theFile.ID3v2.
                        Assert.IsNotNull(picture);
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(picture.Height, 150);
                        Assert.AreEqual(picture.Width, 150);
            */
            // Unsupported picture (Conductor - 0x09)
        }

        [TestMethod]
        public void TagIO_R_ID3v24_UTF8()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.4_UTF8.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            Assert.IsTrue(theFile.ReadFromFile(null, true));

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


            // Supported picture
            /*
                        Image picture = theFile.ID3v2.
                        Assert.IsNotNull(picture);
                        Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                        Assert.AreEqual(picture.Height, 150);
                        Assert.AreEqual(picture.Width, 150);
            */
            // Unsupported picture (Conductor - 0x09)
        }

    }
}
