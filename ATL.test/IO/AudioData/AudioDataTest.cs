using ATL.AudioData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ATL.test
{
    [TestClass]
    public class AudioDataIOTest
    {

        private void testGenericAudio(string resource, int duration, int bitrate, int samplerate, bool isVbr, int codecFamily, int alternate = 0)
        {
            ConsoleLogger log = new ConsoleLogger();
            string theResource = TestUtils.GetResourceLocationRoot() + resource;

            IAudioDataIO theReader = AudioDataIOFactory.GetInstance().GetDataReader(theResource, alternate);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));

            AudioDataManager manager = new AudioDataManager(theReader);
            manager.ReadFromFile();

            Assert.AreEqual(duration, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(bitrate, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(samplerate, theReader.SampleRate);
            Assert.AreEqual(theReader.IsVBR, isVbr);
            Assert.AreEqual(codecFamily, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.SampleRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(codecFamily == theReader.CodecFamily);
        }

        [TestMethod]
        public void Audio_MP3_VBR()
        {
            testGenericAudio("MP3/01 - Title Screen.mp3", 4, 129, 44100, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AAC_MP4()
        {
            testGenericAudio("AAC/mp4.m4a", 14, 75, 48000, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AAC_ADTS()
        {
            testGenericAudio("AAC/adts_CBR88_8s.aac", 8, 88, 44100, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AAC_ADIF()
        {
            testGenericAudio("AAC/adif_CBR88_8s.aac", 8, 88, 44100, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_WMA()
        {
            testGenericAudio("WMA/wma.wma", 14, 9, 8000, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_OGG()
        {
            testGenericAudio("OGG/ogg.ogg", 33, 69, 22050, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_Opus()
        {
            testGenericAudio("OPUS/01_2_32.opus", 31, 33, 48000, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_FLAC()
        {
            testGenericAudio("FLAC/flac.flac", 5, 694, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_MPC()
        {
            testGenericAudio("MPC/mpc.mpc", 8, 127, 44100, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AC3()
        {
            testGenericAudio("AC3/empty.ac3", 18, 128, 44100, false, AudioDataIOFactory.CF_LOSSY);
        }

        public void Audio_DTS()
        {
            testGenericAudio("DTS/dts.dts", 10, 1512, 96000, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void TestDSF_DSDAudio()
        {
            testGenericAudio("DSF/Yeah.dsf", 4, 5953, 2822400, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        /*

                [TestMethod]
                public void TestDSF_PSFAudio()
                {
                    testGenericAudio("adgpp_PLAY_01_05.dsf", 26, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV, 1);
                }

                [TestMethod]
                public void TestTakAudio()
                {
                    testGenericAudio("003 BlackBird.tak", 6, 634, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
                }

                [TestMethod]
                public void TestModAudio()
                {
                    testGenericAudio("4-mat - Thala-Music (Sanxion).mod", 330, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
                }

                [TestMethod]
                public void TestS3MAudio()
                {
                    testGenericAudio("2ND_PM.S3M", 405, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
                }

                [TestMethod]
                public void TestXMAudio()
                {
                    testGenericAudio("v_chrtrg.xm", 261, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
                }

                [TestMethod]
                public void TestITAudio()
                {
                    testGenericAudio("sommix.it", 476, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
                }

                [TestMethod]
                public void TestM4AAudio()
                {
                    testGenericAudio("mp4.m4a", 14, 75, 48000, true, AudioDataIOFactory.CF_LOSSY);
                }
        */
        /*
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
        */
    }
}