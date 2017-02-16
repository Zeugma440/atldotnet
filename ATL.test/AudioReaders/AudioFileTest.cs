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
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 550);
            Assert.AreEqual(picture.Width, 400);
        }

        [TestMethod]
        public void TestMIDITrack()
        {
            // Type 1 song; comprehensive tempo map in 1st track
            Track theTrack = new Track("../../Resources/ROQ.MID");

            Assert.AreEqual(504, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ, theTrack.CodecFamily);

            Assert.AreEqual("The Music Shoppe - Path to God/base/Midi of the Week/The Music Shoppe/http://cctr.umkc.edu/user/dschmid/midiweek.htm/816 373.1710", theTrack.Comment);

            // Type 1 song; duration = duration of longest track
            theTrack = new Track("../../Resources/ataezou - I (HEART) RUEAMATASU.mid");

            Assert.AreEqual(66, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ, theTrack.CodecFamily);

            Assert.AreEqual("untitled", theTrack.Comment);
        }

        [TestMethod]
        public void TestDSF_streamedTrack()
        {
            Track theTrack = new Track("../../Resources/Yeah.dsf");

            Assert.AreEqual(4, theTrack.Duration);
            Assert.AreEqual(5953, theTrack.Bitrate);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theTrack.CodecFamily);

            Assert.AreEqual("Yeah", theTrack.Title);
            Assert.AreEqual("oh", theTrack.Artist);
            Assert.AreEqual("yeah", theTrack.Album);
            Assert.AreEqual(2000, theTrack.Year);
            Assert.AreEqual(44, theTrack.TrackNumber);
            Assert.AreEqual("Other", theTrack.Genre);
            
            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Png);
            Assert.AreEqual(picture.Height, 298);
            Assert.AreEqual(picture.Width, 300);
        }

        [TestMethod]
        public void TestOpusTrack()
        {
            Track theTrack = new Track("../../Resources/01_2_32.opus");

            Assert.AreEqual(31, theTrack.Duration);
            Assert.AreEqual(33, theTrack.Bitrate);
            Assert.IsTrue(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theTrack.CodecFamily);

            Assert.AreEqual("01_2_32", theTrack.Title);
            Assert.AreEqual("sample", theTrack.Artist);
            Assert.AreEqual("opus demo", theTrack.Album);
            Assert.AreEqual(2014, theTrack.Year);
            Assert.AreEqual(01, theTrack.TrackNumber);
            Assert.AreEqual("opus", theTrack.Genre);
            Assert.AreEqual("sample rules", theTrack.Comment);
            Assert.AreEqual("opus2", theTrack.Composer);
            Assert.AreEqual(1, theTrack.DiscNumber);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 150);
            Assert.AreEqual(picture.Width, 150);
        }

        [TestMethod]
        public void TestVorbisTrack()
        {
            Track theTrack = new Track("../../Resources/Rayman_2_music_sample.ogg");

            Assert.AreEqual(33, theTrack.Duration);
            Assert.AreEqual(69, theTrack.Bitrate);
            Assert.IsTrue(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theTrack.CodecFamily);

            Assert.AreEqual("Rayman 2 sample", theTrack.Title);
            Assert.AreEqual("Ray", theTrack.Artist);
            Assert.AreEqual("Rayman 2", theTrack.Album);
            Assert.AreEqual(2015, theTrack.Year);
            Assert.AreEqual(01, theTrack.TrackNumber);
            Assert.AreEqual("Game", theTrack.Genre);
            Assert.AreEqual("Ray", theTrack.Comment);
            Assert.AreEqual("Comp", theTrack.Composer);
            Assert.AreEqual(1, theTrack.DiscNumber);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 150);
            Assert.AreEqual(picture.Width, 150);
        }

        [TestMethod]
        public void TestTakTrack()
        {
            Track theTrack = new Track("../../Resources/003 BlackBird.tak");

            Assert.AreEqual(6,theTrack.Duration);
            Assert.AreEqual(634, theTrack.Bitrate);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theTrack.CodecFamily);

            Assert.AreEqual("blackbird", theTrack.Title);
            Assert.AreEqual("tak", theTrack.Artist);
            Assert.AreEqual("takSongs", theTrack.Album);
            Assert.AreEqual(2015, theTrack.Year);
            Assert.AreEqual(01, theTrack.TrackNumber);
            Assert.AreEqual("test", theTrack.Genre);
            Assert.AreEqual("tak test", theTrack.Comment);
            Assert.AreEqual("takMan", theTrack.Composer);
            Assert.AreEqual(1, theTrack.DiscNumber);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 150);
            Assert.AreEqual(picture.Width, 150);
        }

        [TestMethod]
        public void TestDSF_PSFTrack()
        {
            Track theTrack = new Track("../../Resources/adgpp_PLAY_01_05.dsf");

            Assert.AreEqual(26, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theTrack.CodecFamily);

            Assert.AreEqual("Akihabara Dennou-gumi Pata Pies!", theTrack.Album);
        }

        [TestMethod]
        public void TestMODTrack()
        {
            Track theTrack = new Track("../../Resources/4-mat - Thala-Music (Sanxion).mod");

            Assert.AreEqual(330, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theTrack.CodecFamily);

            Assert.AreEqual("THALAMUSIC-SP", theTrack.Title);
        }

        [TestMethod]
        public void TestS3MTrack()
        {
            Track theTrack = new Track("../../Resources/2ND_PM.S3M");

            Assert.AreEqual(405, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theTrack.CodecFamily);

            Assert.AreEqual("Unreal ][ / PM", theTrack.Title);
        }

        [TestMethod]
        public void TestXMTrack()
        {
            Track theTrack = new Track("../../Resources/v_chrtrg.xm");

            Assert.AreEqual(261, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theTrack.CodecFamily);

            Assert.AreEqual("Chrono Trigger", theTrack.Title);
        }

        [TestMethod]
        public void TestITTrack()
        {
            Track theTrack = new Track("../../Resources/SuperMario28bits(tssf).it");

            Assert.AreEqual(42, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theTrack.CodecFamily);

            Assert.AreEqual("God I'm Bored-Part 2", theTrack.Title);
        }

        [TestMethod]
        public void TestM4Track()
        {
            Track theTrack = new Track("../../Resources/06 I'm All In Love.m4a");

            Assert.AreEqual(54, theTrack.Duration);
            Assert.IsTrue(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theTrack.CodecFamily);

            Assert.AreEqual("I'm All In Love", theTrack.Title);
            Assert.AreEqual("Yoko Kanno", theTrack.Artist);
            Assert.AreEqual("Surely Someday", theTrack.Album);
            Assert.AreEqual(2010, theTrack.Year);
            Assert.AreEqual(6, theTrack.TrackNumber);
            Assert.AreEqual("J-Pop", theTrack.Genre);
            Assert.AreEqual("a", theTrack.Comment);
            Assert.AreEqual("b", theTrack.Composer);
            Assert.AreEqual(1, theTrack.DiscNumber);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 600);
            Assert.AreEqual(picture.Width, 600);
        }

        [TestMethod]
        public void TestAIFCTrack()
        {
            Track theTrack = new Track("../../Resources/M1F1-AlawC-AFsp_tagged.aif");

            Assert.AreEqual(3, theTrack.Duration);
            Assert.IsFalse(theTrack.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theTrack.CodecFamily);

            Assert.AreEqual("A", theTrack.Title);
            Assert.AreEqual("B", theTrack.Artist);
            Assert.AreEqual("C", theTrack.Album);
            Assert.AreEqual(2017, theTrack.Year);
            Assert.AreEqual(2, theTrack.TrackNumber);
            Assert.AreEqual("theGenre", theTrack.Genre);
            Assert.AreEqual("theComment", theTrack.Comment);
            Assert.AreEqual("composer", theTrack.Composer);
            Assert.AreEqual(4, theTrack.DiscNumber);

            Image picture = theTrack.GetEmbeddedPicture();
            Assert.IsNotNull(picture);
            Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
            Assert.AreEqual(picture.Height, 80);
            Assert.AreEqual(picture.Width, 195);
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
