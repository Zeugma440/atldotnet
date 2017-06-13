using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v2
    {
        [TestMethod]
        public void TagIO_RW_ID3v2()
        {
            // Source : tag-free MP3
            String location = "../../Resources/empty.mp3";
            String testFileLocation = location.Replace("empty", "testID3v2");

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

            // Construct a new tag by using TagData
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Comment = "This is a test";
            theTag.ReleaseYear = "2008";
            theTag.ReleaseDate = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";

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
    }
}
