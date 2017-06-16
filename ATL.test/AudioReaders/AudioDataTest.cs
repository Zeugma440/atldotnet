using ATL;
using ATL.AudioReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ATL.test
{
    [TestClass]
    public class AudioDataTest
    {

        private void testGenericAudio(string resource, int duration, int bitrate, int samplerate, bool isVbr, int codecFamily, int alternate = 0)
        {
            string theResource = TestHelper.getResourceLocationRoot() + resource;

            IAudioDataReader theReader= AudioReaderFactory.GetInstance().GetDataReader(theResource, alternate);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(theResource);

            Assert.AreEqual(duration, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(bitrate, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(samplerate, (int)Math.Round(theReader.SampleRate));
            Assert.AreEqual(theReader.IsVBR, isVbr);
            Assert.AreEqual(codecFamily, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.SampleRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(codecFamily == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestFLACAudio()
        {
            testGenericAudio("mustang_12kHz.flac", 5, 694, 44100, false, AudioReaderFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void TestDSF_DSDAudio()
        {
            testGenericAudio("Yeah.dsf", 4, 5953, 2822400, false, AudioReaderFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void TestDSF_PSFAudio()
        {
            testGenericAudio("adgpp_PLAY_01_05.dsf", 26, 0, 0, false, AudioReaderFactory.CF_SEQ_WAV, 1);
        }

        [TestMethod]
        public void TestOpusAudio()
        {
            testGenericAudio("01_2_32.opus", 31, 33, 48000, true, AudioReaderFactory.CF_LOSSY);
        }

        [TestMethod]
        public void TestVorbisAudio()
        {
            testGenericAudio("Rayman_2_music_sample.ogg", 33, 69, 22050, true, AudioReaderFactory.CF_LOSSY);
        }

        [TestMethod]
        public void TestTakAudio()
        {
            testGenericAudio("003 BlackBird.tak", 6, 634, 44100, false, AudioReaderFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void TestModAudio()
        {
            testGenericAudio("4-mat - Thala-Music (Sanxion).mod", 330, 0, 0, false, AudioReaderFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void TestS3MAudio()
        {
            testGenericAudio("2ND_PM.S3M", 405, 2, 0, false, AudioReaderFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void TestXMAudio()
        {
            testGenericAudio("v_chrtrg.xm", 261, 2, 0, false, AudioReaderFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void TestITAudio()
        {
            testGenericAudio("sommix.it", 476, 1, 0, false, AudioReaderFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void TestM4AAudio()
        {
            testGenericAudio("06 I'm All In Love.m4a", 54, 260, 44100, true, AudioReaderFactory.CF_LOSSY);
        }

        [TestMethod]
        public void TestAIFFAudio()
        {
            testGenericAudio("M1F1-int32-AFsp.aif", 3, 512, 8000, false, AudioReaderFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void TestAIFCAudio()
        {
            testGenericAudio("M1F1-AlawC-AFsp_tagged.aif", 3, 128, 8000, false, AudioReaderFactory.CF_LOSSY);
        }
    }
}