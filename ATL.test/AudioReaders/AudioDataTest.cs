using ATL.AudioReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ATL.test
{
    [TestClass]
    public class AudioDataTest
    {
        [TestMethod]
        public void TestFLACAudio()
        {
            string resource = "mustang_12kHz.flac";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+resource);

            // Not possible since TFLACFile is not visible from the outside of ATL
            //Assert.IsInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.TFLACFile));
            Assert.IsNotInstanceOfType(theReader,typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+ resource);

            Assert.AreEqual(5, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(694, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSLESS == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestDSF_DSDAudio()
        {
            string resource = "Yeah.dsf";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+resource);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+ resource);

            Assert.AreEqual(4, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(5953, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSLESS == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestDSF_PSFAudio()
        {
            string resource = "adgpp_PLAY_01_05.dsf";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+resource, 1); // Force alternate

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+resource);

            Assert.AreEqual(26, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(0, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestOpusAudio()
        {
            string resource = "01_2_32.opus";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+resource);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+resource);

            Assert.AreEqual(31, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(33, (int)Math.Round(theReader.BitRate));
            Assert.IsTrue(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSY == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestVorbisAudio()
        {
            string resource = "01_2_32.opus";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+"Rayman_2_music_sample.ogg");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+"Rayman_2_music_sample.ogg");

            Assert.AreEqual(33, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(69, (int)Math.Round(theReader.BitRate));
            Assert.IsTrue(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSY == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestTakAudio()
        {
            string resource = "01_2_32.opus";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+"003 BlackBird.tak");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+"003 BlackBird.tak");

            Assert.AreEqual(6, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(634, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSLESS == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestModAudio()
        {
            string resource = "4-mat - Thala-Music (Sanxion).mod";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(TestUtils.GetResourceLocationRoot()+resource);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(TestUtils.GetResourceLocationRoot()+resource);

            Assert.AreEqual(330, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(0, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestS3MAudio()
        {
            String location = TestUtils.GetResourceLocationRoot()+"2ND_PM.S3M";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(405, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(2, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestXMAudio()
        {
            String location = TestUtils.GetResourceLocationRoot()+"v_chrtrg.xm";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(261, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(2, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestITAudio()
        {
            String location = TestUtils.GetResourceLocationRoot()+"sommix.it";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(476, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(1, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestM4AAudio()
        {
            String location = TestUtils.GetResourceLocationRoot()+"06 I'm All In Love.m4a";
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(54, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(260, (int)Math.Round(theReader.BitRate));
            Assert.IsTrue(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSY == theReader.CodecFamily);
        }
        

    }
}