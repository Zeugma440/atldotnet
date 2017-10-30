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
        }

        [TestMethod]
        public void Audio_MP3()
        {
            testGenericAudio("MP3/01 - Title Screen.mp3", 4, 129, 44100, true, AudioDataIOFactory.CF_LOSSY); // VBR
            testGenericAudio("MP3/headerPatternIsNotHeader.mp3", 0, 192, 44100, false, AudioDataIOFactory.CF_LOSSY); // Malpositioned header
            testGenericAudio("MP3/mp1Layer1.mp1", 1, 384, 44100, false, AudioDataIOFactory.CF_LOSSY); // MPEG1 Layer 1
            testGenericAudio("MP3/mp1Layer2.mp1", 1, 384, 44100, false, AudioDataIOFactory.CF_LOSSY); // MPEG1 Layer 2
            testGenericAudio("MP3/mp2Layer1.mp2", 1, 128, 22050, false, AudioDataIOFactory.CF_LOSSY); // MPEG2 Layer 1
            testGenericAudio("MP3/mp2Layer2.mp2", 1, 160, 24000, false, AudioDataIOFactory.CF_LOSSY); // MPEG2 Layer 2
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
            testGenericAudio("MPC/SV8.mpc", 8, 127, 44100, true, AudioDataIOFactory.CF_LOSSY);
            testGenericAudio("MPC/SV7.mpc", 8, 131, 44100, true, AudioDataIOFactory.CF_LOSSY);
            testGenericAudio("MPC/SV5.mp+", 8, 112, 44100, true, AudioDataIOFactory.CF_LOSSY);
            testGenericAudio("MPC/SV4.mp+", 8, 112, 44100, true, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AC3()
        {
            testGenericAudio("AC3/empty.ac3", 18, 128, 44100, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_DTS()
        {
            testGenericAudio("DTS/dts.dts", 10, 1536, 48000, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_DSF_DSD()
        {
            testGenericAudio("DSF/dsf.dsf", 4, 5671, 2822400, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_IT()
        {
            testGenericAudio("IT/empty.it", 476, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("IT/it.it", 42, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("IT/hasInstruments.it", 68, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void Audio_Midi()
        {
            testGenericAudio("MID/ataezou - I (HEART) RUEAMATASU.mid", 66, 0, 0, false, AudioDataIOFactory.CF_SEQ);
            testGenericAudio("MID/TRANSIT1.MID", 105, 0, 0, false, AudioDataIOFactory.CF_SEQ);
            testGenericAudio("MID/ROQ.MID", 504, 0, 0, false, AudioDataIOFactory.CF_SEQ);
            testGenericAudio("MID/yoru-uta.mid", 251, 0, 0, false, AudioDataIOFactory.CF_SEQ); // This one has a track header position issue
        }

        [TestMethod]
        public void Audio_MOD()
        {
            testGenericAudio("MOD/empty.mod", 159, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("MOD/mod.mod", 330, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void Audio_Ape()
        {
            testGenericAudio("APE/ape.ape", 8, 652, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
            testGenericAudio("APE/v394.ape", 8, 599, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_S3M()
        {
            testGenericAudio("S3M/empty.s3m", 127, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("S3M/s3m.s3m", 405, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("S3M/s3m2.s3m", 10, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV); // This one contains extra instructions
            testGenericAudio("S3M/s3m3.s3m", 475, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV); // This one contains yet other extra instructions
        }

        [TestMethod]
        public void Audio_XM()
        {
            testGenericAudio("XM/empty.xm", 55, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("XM/xm.xm", 261, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void Audio_DSF_PSF()
        {
            testGenericAudio("PSF/psf.psf", 159, 10, 44100, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("PSF/nolength.psf", 180, 13, 44100, false, AudioDataIOFactory.CF_SEQ_WAV);
            testGenericAudio("DSF/adgpp_PLAY_01_05.dsf", 26, 0, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, 1);
        }

        [TestMethod]
        public void Audio_SPC()
        {
            testGenericAudio("SPC/spc.spc", 69, 8, 32000, false, AudioDataIOFactory.CF_SEQ_WAV);
        }

        [TestMethod]
        public void Audio_VQF()
        {
            testGenericAudio("VQF/vqf.vqf", 120, 20, 22050, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_TAK()
        {
            testGenericAudio("TAK/003 BlackBird.tak", 6, 634, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_WAV()
        {
            testGenericAudio("WAV/duck hunt.wav", 8, 1411, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_WV()
        {
            testGenericAudio("WV/losslessv3.wv", 8, 659, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
            testGenericAudio("WV/lossyv3.wv", 8, 342, 44100, false, AudioDataIOFactory.CF_LOSSY);
            testGenericAudio("WV/lossyv440.wv", 8, 206, 44100, false, AudioDataIOFactory.CF_LOSSY);
            testGenericAudio("WV/losslessv4.wv", 6, 645, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_OFR()
        {
            testGenericAudio("OFR/BlackBird.ofr", 6, 620, 44100, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_TTA()
        {
            testGenericAudio("TTA/BlackBird.tta", 6, 659, 44100, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_AIFF()
        {
            testGenericAudio("AIF/aiff_empty.aif", 3, 512, 8000, false, AudioDataIOFactory.CF_LOSSLESS);
        }

        [TestMethod]
        public void Audio_AIFC()
        {
            testGenericAudio("AIF/aifc_tagged.aif", 3, 128, 8000, false, AudioDataIOFactory.CF_LOSSY);
        }

        [TestMethod]
        public void Audio_VGM()
        {
            testGenericAudio("VGM/vgm.vgm", 86, 1, 44100, false, AudioDataIOFactory.CF_SEQ_WAV);
            // VGZ not supported yet
        }

        [TestMethod]
        public void Audio_GYM()
        {
            testGenericAudio("GYM/gym.gym", 73, 37, 44100, false, AudioDataIOFactory.CF_SEQ_WAV);
        }
    }
}