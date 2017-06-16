using ATL;
using ATL.AudioReaders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ATL.test
{
    [TestClass]
    public class AudioDataTest
    {
        // Appveyor tests exec path : C:\projects\atldotnet\TestResults\appveyor_APPVYR-WIN 2017-06-16 06_27_49\Out

        // TODO : if being tested online, prefix = "./ATL.test/Resources"; if not "../../Resources" c/projects/solutionName
        // TODO industrialize with [DataSource]

//#ifdef appveyor



        [TestMethod]
        public void TestFLACAudio()
        {
            string basePath = System.IO.Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            System.Console.WriteLine("@" + basePath + "@");

            string theResource = System.IO.Path.GetFullPath(basePath +"/../../Resources/mustang_12kHz.flac");
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(theResource);

            System.Console.WriteLine("@"+basePath+"@");

            // Not possible since TFLACFile is not visible from the outside of ATL
            //Assert.IsInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.TFLACFile));
            Assert.IsNotInstanceOfType(theReader,typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(theResource);

            Assert.AreEqual(5, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(694, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(44100, (int)Math.Round(theReader.SampleRate));
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
            string resourceLocation = "../../../ATL.test/Resources/Yeah.dsf";
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(resourceLocation);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(resourceLocation);

            Assert.AreEqual(4, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(5953, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(2822400, (int)Math.Round(theReader.SampleRate));
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
            string resourceLocation = "..\\..\\..\\ATL.test\\Resources\\adgpp_PLAY_01_05.dsf";
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(resourceLocation, 1); // Force alternate

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(resourceLocation);

            Assert.AreEqual(26, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(0, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(0, (int)Math.Round(theReader.SampleRate));
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
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/01_2_32.opus");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/01_2_32.opus");

            Assert.AreEqual(31, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(33, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(48000, (int)Math.Round(theReader.SampleRate));
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
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/Rayman_2_music_sample.ogg");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/Rayman_2_music_sample.ogg");

            Assert.AreEqual(33, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(69, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(22050, (int)Math.Round(theReader.SampleRate));
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
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/003 BlackBird.tak");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/003 BlackBird.tak");

            Assert.AreEqual(6, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(634, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(44100, (int)Math.Round(theReader.SampleRate));
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
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/4-mat - Thala-Music (Sanxion).mod");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/4-mat - Thala-Music (Sanxion).mod");

            Assert.AreEqual(330, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(0, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(0, (int)Math.Round(theReader.SampleRate));
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
            String location = "../../Resources/2ND_PM.S3M";
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(405, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(2, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(0, (int)Math.Round(theReader.SampleRate));
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
            String location = "../../Resources/v_chrtrg.xm";
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(261, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(2, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(0, (int)Math.Round(theReader.SampleRate));
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
            String location = "../../Resources/sommix.it";
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(location);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(location);

            Assert.AreEqual(476, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(1, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(0, (int)Math.Round(theReader.SampleRate));
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
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/06 I'm All In Love.m4a");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/06 I'm All In Love.m4a");

            Assert.AreEqual(54, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(260, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(44100, (int)Math.Round(theReader.SampleRate));
            Assert.IsTrue(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSY == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestAIFFAudio()
        {
            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader("../../Resources/M1F1-int32-AFsp.aif");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/M1F1-int32-AFsp.aif");

            Assert.AreEqual(3, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(512, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(8000, (int)Math.Round(theReader.SampleRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSLESS, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSLESS == theReader.CodecFamily);
        }

        [TestMethod]
        public void TestAIFCAudio()
        {
            string resource = "../../Resources/M1F1-AlawC-AFsp_tagged.aif";

            IAudioDataReader theReader = AudioReaderFactory.GetInstance().GetDataReader(resource);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile(resource);

            Assert.AreEqual(3, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(128, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(8000, (int)Math.Round(theReader.SampleRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_LOSSY, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_LOSSY == theReader.CodecFamily);
        }
    }
}