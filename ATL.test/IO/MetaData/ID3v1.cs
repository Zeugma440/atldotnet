using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v1
    {
        [TestMethod]
        public void TagIO_RW_ID3v1_Empty()
        {
            // Source : tag-free MP3
            string resourceName = "empty.mp3";
            string location = TestUtils.GetResourceLocationRoot() + resourceName;
            string testFileLocation = TestUtils.GetTempTestFile(resourceName);
            AudioDataIO theFile = new AudioDataIO( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation) );


            // Check that it is indeed tag-free
            Assert.IsTrue(theFile.ReadFromFile());
            
            Assert.IsNotNull(theFile.ID3v1);
            Assert.IsFalse(theFile.ID3v1.Exists);


            // Construct a new tag
            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Comment = "This is a test";
            theTag.RecordingYear = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";


            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V1));
            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v1);
            Assert.IsTrue(theFile.ID3v1.Exists);

            Assert.AreEqual("Test !!", theFile.ID3v1.Title);
            Assert.AreEqual("Album", theFile.ID3v1.Album);
            Assert.AreEqual("Artist", theFile.ID3v1.Artist);
            Assert.AreEqual("This is a test", theFile.ID3v1.Comment);
            Assert.AreEqual("2008", theFile.ID3v1.Year);
            Assert.AreEqual("Merengue", theFile.ID3v1.Genre);
            Assert.AreEqual(1, theFile.ID3v1.Track);


            // Remove the tag and check that it has been indeed removed
            Assert.IsTrue(theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V1));

            Assert.IsTrue(theFile.ReadFromFile());
                
            Assert.IsNotNull(theFile.ID3v1);
            Assert.IsFalse(theFile.ID3v1.Exists);


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
        public void TagIO_RW_ID3v1_Existing()
        {
            // Source : MP3 with existing tag
            String testFileLocation = TestUtils.GetTempTestFile("id3v1.mp3");
            AudioDataIO theFile = new AudioDataIO( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation) );

            // Construct a new tag; only rewrite Genre and track number
            TagData theTag = new TagData();
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "002/04";

            // Add the new tag and check that it has been indeed added with all the correct information
            Assert.IsTrue(theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V1));

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v1);
            Assert.IsTrue(theFile.ID3v1.Exists);
            Assert.AreEqual("Title", theFile.ID3v1.Title);
            Assert.AreEqual("?", theFile.ID3v1.Album);
            Assert.AreEqual("Artist", theFile.ID3v1.Artist);
            Assert.AreEqual("Test!", theFile.ID3v1.Comment);
            Assert.AreEqual("2017", theFile.ID3v1.Year);
            Assert.AreEqual("Merengue", theFile.ID3v1.Genre);
            Assert.AreEqual(2, theFile.ID3v1.Track);

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_R_ID3v1()
        {
            // Source : MP3 with existing tag
            String location = TestUtils.GetResourceLocationRoot()+"id3v1.mp3";
            AudioDataIO theFile = new AudioDataIO( AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location) );

            Assert.IsTrue(theFile.ReadFromFile());

            Assert.IsNotNull(theFile.ID3v1);
            Assert.IsTrue(theFile.ID3v1.Exists);

            // Supported fields
            Assert.AreEqual("Title", theFile.ID3v1.Title);
            Assert.AreEqual("?", theFile.ID3v1.Album);
            Assert.AreEqual("Artist", theFile.ID3v1.Artist);
            Assert.AreEqual("Test!", theFile.ID3v1.Comment);
            Assert.AreEqual("2017", theFile.ID3v1.Year);
            Assert.AreEqual("Bluegrass", theFile.ID3v1.Genre);
            Assert.AreEqual(22, theFile.ID3v1.Track);
        }
    }
}
