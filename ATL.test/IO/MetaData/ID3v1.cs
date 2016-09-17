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
        public void ID3v1ReadWrite()
        {
            String location = "../../Resources/empty.mp3";
            String testFileLocation = location.Replace("empty", "testID3v1");

            File.Copy(location, testFileLocation, true);
            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(testFileLocation);

            if (theFile.ReadFromFile())
            {
                Assert.IsNotNull(theFile.ID3v1);
                Assert.IsFalse(theFile.ID3v1.Exists);
            }
            else
            {
                Assert.Fail();
            }

            TagData theTag = new TagData();
            theTag.Title = "Test !!";
            theTag.Album = "Album";
            theTag.Artist = "Artist";
            theTag.Comment = "This is a test";
            theTag.Date = "2008/01/01";
            theTag.Genre = "Merengue";
            theTag.TrackNumber = "01/01";

            if (theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v1);
                    Assert.IsTrue(theFile.ID3v1.Exists);
                    Assert.AreEqual("Test !!", theFile.ID3v1.Title);
                    Assert.AreEqual("Album", theFile.ID3v1.Album);
                    Assert.AreEqual("Artist", theFile.ID3v1.Artist);
                    Assert.AreEqual("This is a test", theFile.ID3v1.Comment);
                    Assert.AreEqual("2008", theFile.ID3v1.Year);
                    Assert.AreEqual("Merengue", theFile.ID3v1.Genre);
                    Assert.AreEqual(1, theFile.ID3v1.Track);
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

            if (theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v1);
                    Assert.IsFalse(theFile.ID3v1.Exists);
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

            FileInfo originalFileInfo = new FileInfo(location);
            FileInfo testFileInfo = new FileInfo(testFileLocation);

            Assert.AreEqual(testFileInfo.Length, originalFileInfo.Length);

            string originalMD5 = TestUtils.GetMD5Hash(location);
            string testMD5 = TestUtils.GetMD5Hash(testFileLocation);

            Assert.IsTrue(originalMD5.Equals(testMD5));

            File.Delete(testFileLocation);
        }
    }
}
