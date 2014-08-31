using ATL.AudioReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace ATL.test
{
    [TestClass]
    public class AudioFileTest
    {
        [TestMethod]
        public void TestFLACTrack()
        {
            Track theTrack = new Track("../../Resources/mustang_12kHz_tagged.flac");

            Assert.AreEqual(5, theTrack.Duration);
            Assert.AreEqual(694, theTrack.Bitrate);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theTrack.CodecFamily);

            Assert.AreEqual("mustang", theTrack.Title);
            Assert.AreEqual("artist", theTrack.Artist);
            Assert.AreEqual("here comes the mustang", theTrack.Album);
            Assert.AreEqual(2014, theTrack.Year);
            Assert.AreEqual(1, theTrack.TrackNumber);
            Assert.AreEqual(1, theTrack.DiscNumber);
            Assert.AreEqual("soundtrack", theTrack.Genre.ToLower());
            Assert.AreEqual("hey there", theTrack.Comment);

            //TODO code for embedded picture
        }

        [TestMethod]
        public void TestMP3Track()
        {
            Track theTrack = new Track("../../Resources/01 - Title Screen_pic.mp3");

            Assert.AreEqual(3, theTrack.Duration);
            Assert.AreEqual(129, theTrack.Bitrate);
            Assert.IsTrue(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theTrack.CodecFamily);

            Assert.AreEqual("Title Screen", theTrack.Title);
            Assert.AreEqual("Nintendo Sound Team", theTrack.Artist);
            Assert.AreEqual("Duck Hunt", theTrack.Album);
            Assert.AreEqual(1984, theTrack.Year);
            Assert.AreEqual(1, theTrack.TrackNumber);
            Assert.AreEqual(1, theTrack.DiscNumber);
            Assert.AreEqual("game", theTrack.Genre.ToLower());
            Assert.AreEqual("comment", theTrack.Comment);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 550);
            Assert.AreEqual(picture.Width, 400);
        }

        /* ------------------------- */

        [TestMethod]
        public void TestSingleTagging_ID3v1()
        {
            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at aff if relying on default settings */
            MetaReaderFactory.GetInstance().CrossReading = false;                            // default behaviour anyway
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_ID3V1, 1);  // Should be entirely read
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_ID3V2, 2);  // Should not be read, since behaviour is single tag reading
            /* end set options */

            Track theTrack = new Track("../../Resources/01 - Title Screen.mp3");

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(0, theTrack.Year); // Specifically tagged as empty on the ID3v1 tag
        }

        [TestMethod]
        public void TestMultiTagging()
        {
            /* Set options for Metadata reader behaviour - this only needs to be done once, or not at aff if relying on default settings */
            MetaReaderFactory.GetInstance().CrossReading = true;
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_APE, 0);    // No APEtag on sample file => should be ignored
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_ID3V1, 1);  // Should be the main source except for the Year field (empty on ID3v1)
            MetaReaderFactory.GetInstance().SetTagPriority(MetaReaderFactory.TAG_ID3V2, 2);  // Should be used for the Year field (valuated on ID3v2)
            /* end set options */

            Track theTrack = new Track("../../Resources/01 - Title Screen.mp3");

            Assert.AreEqual("Nintendo Sound Scream", theTrack.Artist); // Specifically tagged like this on the ID3v1 tag
            Assert.AreEqual(1984, theTrack.Year); // Empty on the ID3v1 tag => cross-reading should read it on ID3v2
        }
    }
}
