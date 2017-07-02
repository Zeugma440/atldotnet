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
         * conservation of unmodified tag items
         * tag removal
         * 
         * Test reading of unsupported tag field
         * Test conservation of unsupported tag field
         * 
         * Test conservation of unsupported image type
         * Test conservation of unedited field
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
            if (theFile.ReadFromFile())
            {
                Assert.IsNotNull(theFile.ID3v2);
                Assert.IsFalse(theFile.ID3v2.Exists);
            }
            else
            {
                Assert.Fail();
            }

            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Comment = "This is a test";
            theTag.ReleaseYear = "2008";
            theTag.ReleaseDate = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";
            theTag.DiscNumber = "2";
            theTag.Composer = "Me";
            theTag.Copyright = "父";
            theTag.OriginalArtist = "Bob";
            theTag.OriginalAlbum = "Hey Hey";

            // Add the new tag and check that it has been indeed added with all the correct information
            if (theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V2))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v2);
                    Assert.IsTrue(theFile.ID3v2.Exists);
                    Assert.AreEqual("Test !!", theFile.ID3v2.Title);
                    Assert.AreEqual("Album", theFile.ID3v2.Album);
                    Assert.AreEqual("Artist", theFile.ID3v2.Artist);
                    Assert.AreEqual("This is a test", theFile.ID3v2.Comment);
                    Assert.AreEqual("2008", theFile.ID3v2.Year);
                    Assert.AreEqual("Merengue", theFile.ID3v2.Genre);
                    Assert.AreEqual(1, theFile.ID3v2.Track);
                    Assert.AreEqual(2, theFile.ID3v2.Disc);
                    Assert.AreEqual("Me", theFile.ID3v2.Composer);
                    Assert.AreEqual("父", theFile.ID3v2.Copyright);
                    Assert.AreEqual("Bob", theFile.ID3v2.OriginalArtist);
                    Assert.AreEqual("Hey Hey", theFile.ID3v2.OriginalAlbum);
                }
                else
                {
                    Assert.Fail();
                }
            }
            else
            {
                Assert.Fail();
            }

            // Remove the tag and check that it has been indeed removed
            if (theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V2))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v2);
                    Assert.IsFalse(theFile.ID3v2.Exists);
                }
                else
                {
                    Assert.Fail();
                }
            }
            else
            {
                Assert.Fail();
            }

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
        private void TagIO_RW_ID3v2_Existing()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            String testFileLocation = location.Replace("id3v2.3_UTF16", "tmp/testID3v2" + System.DateTime.Now.ToShortTimeString().Replace(":", "."));

            // Create a working copy
            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);

            // Construct a new tag
            if (theFile.ReadFromFile())
            {
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
            }
            else
            {
                Assert.Fail();
            }

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
        private void TagIO_R_ID3v23_UTF16()
        {
            // Source : MP3 with existing tag incl. unsupported picture (Conductor); unsupported field (MOOD)
            String location = "../../Resources/id3v2.3_UTF16.mp3";
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            Assert.IsTrue(theFile.ReadFromFile());

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

            // Unsupported fields


            // Supported picture
/*
            Image picture = theFile.ID3v2.
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 150);
            Assert.AreEqual(picture.Width, 150);
*/
            // Unsupported picture
        }

    }
}
