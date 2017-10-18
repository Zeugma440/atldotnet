using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;

namespace ATL.test.IO
{
    [TestClass]
    public class HighLevel
    {
        // TODO test access to native tag on MP4 file


        [TestMethod]
        public void TagIO_R_Single_ID3v1()
        {
            bool crossreadingDefault = MetaDataIOFactory.GetInstance().CrossReading;
            int[] tagPriorityDefault = new int[MetaDataIOFactory.TAG_TYPE_COUNT];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = false;                            // default behaviour anyway
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be entirely read
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should not be read, since behaviour is single tag reading
            /* end set options */

            try
            {
                Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.mp3");

                Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
                Assert.AreEqual(0, theTrack.Year); // Specifically tagged as empty on the ID3v1 tag
            } finally
            {
                // Set back default settings
                MetaDataIOFactory.GetInstance().CrossReading = crossreadingDefault;
                MetaDataIOFactory.GetInstance().TagPriority = tagPriorityDefault;
            }
        }

        [TestMethod]
        public void TagIO_R_Multi()
        {
            bool crossreadingDefault = MetaDataIOFactory.GetInstance().CrossReading;
            int[] tagPriorityDefault = new int[MetaDataIOFactory.TAG_TYPE_COUNT];
            MetaDataIOFactory.GetInstance().TagPriority.CopyTo(tagPriorityDefault, 0);

            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at all if relying on default settings */
            MetaDataIOFactory.GetInstance().CrossReading = true;
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V1, 1);  // Should be the main source except for the Year field (empty on ID3v1)
            MetaDataIOFactory.GetInstance().SetTagPriority(MetaDataIOFactory.TAG_ID3V2, 2);  // Should be used for the Year field (valuated on ID3v2)
            /* end set options */

            try
            {
                Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.mp3");

                Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
                Assert.AreEqual(1984, theTrack.Year); // Empty on the ID3v1 tag => cross-reading should read it on ID3v2
            } finally
            {
                // Set back default settings
                MetaDataIOFactory.GetInstance().CrossReading = crossreadingDefault;
                MetaDataIOFactory.GetInstance().TagPriority = tagPriorityDefault;
            }
        }

        [TestMethod]
        public void TagIO_R_MultiplePictures()
        {
            Track theTrack = new Track(TestUtils.GetResourceLocationRoot() + "OGG/bigPicture.ogg");

            // Check if all embedded pictures are accessible from Track
            Assert.AreEqual(3, theTrack.EmbeddedPictures.Count);
        }

        [TestMethod]
        public void TagIO_RW_DeleteTag()
        {
            string testFileLocation = TestUtils.GetTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.Remove(MetaDataIOFactory.TAG_ID3V2);

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(0, theTrack.Year); // Empty on the ID3v1 tag => should really come empty since ID3v2 tag has been removed

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagBaseField()
        {
            string testFileLocation = TestUtils.GetTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.Artist = "Hey ho";
            theTrack.Save();

            theTrack = new Track(testFileLocation);

            Assert.AreEqual("Hey ho", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateTagAdditionalField()
        {
            string testFileLocation = TestUtils.GetTempTestFile("MP3/01 - Title Screen.mp3");
            Track theTrack = new Track(testFileLocation);

            theTrack.AdditionalFields.Add("ABCD", "efgh");
            theTrack.AdditionalFields.Remove("TENC");
            theTrack.Save();

            theTrack = new Track(testFileLocation);

            Assert.AreEqual(1, theTrack.AdditionalFields.Count); // TENC should have been removed
            Assert.IsTrue(theTrack.AdditionalFields.ContainsKey("ABCD"));
            Assert.AreEqual("efgh", theTrack.AdditionalFields["ABCD"]);

            // Get rid of the working copy
            File.Delete(testFileLocation);
        }

        [TestMethod]
        public void TagIO_RW_UpdateKeepDataIntegrity()
        {
            Settings.EnablePadding = true;

            try
            {
                string resource = "OGG/ogg.ogg";
                string location = TestUtils.GetResourceLocationRoot() + resource;
                string testFileLocation = TestUtils.GetTempTestFile(resource);
                Track theTrack = new Track(testFileLocation);

                System.Console.WriteLine("artist1 : " + theTrack.Artist);

                string initialArtist = theTrack.Artist;
                theTrack.Artist = "Hey ho";
                theTrack.Save();

                theTrack = new Track(testFileLocation);

                System.Console.WriteLine("artist2 : " + theTrack.Artist);

                theTrack.Artist = initialArtist;
                theTrack.Save();

                // Check that the resulting file (working copy that has been processed) remains identical to the original file (i.e. no byte lost nor added)
                FileInfo originalFileInfo = new FileInfo(location);
                FileInfo testFileInfo = new FileInfo(testFileLocation);

                Assert.AreEqual(originalFileInfo.Length, testFileInfo.Length);

                string originalMD5 = TestUtils.GetFileMD5Hash(location);
                string testMD5 = TestUtils.GetFileMD5Hash(testFileLocation);

                Assert.IsTrue(originalMD5.Equals(testMD5));

                // Get rid of the working copy
                File.Delete(testFileLocation);
            } finally
            {
                Settings.EnablePadding = false;
            }
        }
    }
}
