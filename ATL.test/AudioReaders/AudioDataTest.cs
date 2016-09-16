using ATL.AudioReaders;
using ATL.AudioData;
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

        [TestMethod]
        public void TestOpusAudio()
        {
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/01_2_32.opus");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/01_2_32.opus");

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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/Rayman_2_music_sample.ogg");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/Rayman_2_music_sample.ogg");

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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/003 BlackBird.tak");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/003 BlackBird.tak");

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
            IAudioDataReader theReader = AudioReaders.AudioReaderFactory.GetInstance().GetDataReader("../../Resources/4-mat - Thala-Music (Sanxion).mod");

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioReaders.BinaryLogic.DummyReader));

            theReader.ReadFromFile("../../Resources/4-mat - Thala-Music (Sanxion).mod");

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
            String location = "../../Resources/2ND_PM.S3M";
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
            String location = "../../Resources/v_chrtrg.xm";
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
            String location = "../../Resources/sommix.it";
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
            String location = "../../Resources/06 I'm All In Love.m4a";
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

        [TestMethod]
        public void TestID3v1ReadWrite()
        {
            String location = "../../Resources/empty.mp3";

            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            if (theFile.ReadFromFile(location))
            {
                Assert.IsNotNull(theFile.ID3v1);
                Assert.IsFalse(theFile.ID3v1.Exists);
            }
            else
            {
                Assert.Fail();
            }

            TagData theTag = new TagData();
            theTag.Title = "test !!";

            if (theFile.AddTagToFile(location, theTag, MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile(location))
                {
                    Assert.IsNotNull(theFile.ID3v1);
                    Assert.IsTrue(theFile.ID3v1.Exists);
                    Assert.AreEqual(theFile.ID3v1.Title, "test !!");
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

            if (theFile.RemoveTagFromFile(location, MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile(location))
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

        }

    }
}