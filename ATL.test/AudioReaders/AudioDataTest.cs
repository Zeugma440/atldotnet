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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/mustang_12kHz.flac");

            // Not possible since TFLACFile is not visible from the outside of ATL
            //Assert.IsInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.TFLACFile));
            Assert.IsNotInstanceOfType(theReader,typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/mustang_12kHz.flac");

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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/Yeah.dsf");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/Yeah.dsf");

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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/adgpp_PLAY_01_05.dsf", 1); // Force alternate

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/adgpp_PLAY_01_05.dsf");

            Assert.AreEqual(26, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(0, (int)Math.Round(theReader.BitRate));
            Assert.IsFalse(theReader.IsVBR);
            Assert.AreEqual(AudioReaderFactory.CF_SEQ_WAV, theReader.CodecFamily);

            System.Console.WriteLine(theReader.Duration);
            System.Console.WriteLine(theReader.BitRate);
            System.Console.WriteLine(theReader.IsVBR);
            System.Console.WriteLine(AudioReaderFactory.CF_SEQ_WAV == theReader.CodecFamily);
        }
    }
}
