using ATL.AudioData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static ATL.ChannelsArrangements;

namespace ATL.test.IO
{
    [TestClass]
    public class AudioData
    {
        [TestMethod]
        public void Audio_FallbackToDummy()
        {
            IAudioDataIO theReader = AudioDataIOFactory.GetInstance().GetFromPath(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.xyz");
            Assert.IsInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));
        }

        private void testGenericAudio(
            string resource,
            int duration,
            int bitrate,
            int samplerate,
            bool isVbr,
            int codecFamily,
            ChannelsArrangement channelsArrangement,
            string formatName,
            int alternate = 0)
        {
            ConsoleLogger log = new ConsoleLogger();
            string theResource = TestUtils.GetResourceLocationRoot() + resource;

            IAudioDataIO theReader = AudioDataIOFactory.GetInstance().GetFromPath(theResource, alternate);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));

            AudioDataManager manager = new AudioDataManager(theReader);
            manager.ReadFromFile();

            Assert.AreEqual(duration, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(bitrate, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(samplerate, theReader.SampleRate);
            Assert.AreEqual(theReader.IsVBR, isVbr);
            Assert.AreEqual(codecFamily, theReader.CodecFamily);
            Assert.AreEqual(channelsArrangement, theReader.ChannelsArrangement);
            Assert.AreEqual(formatName, theReader.AudioFormat.Name);
        }

        [TestMethod]
        public void Audio_MP3()
        {
            testGenericAudio("MP3/01 - Title Screen.mp3", 3866, 129, 44100, true, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)"); // VBR
            testGenericAudio("MP3/headerPatternIsNotHeader.mp3", 184, 192, 44100, false, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)"); // Malpositioned header
            testGenericAudio("MP3/mp1Layer1.mp1", 520, 384, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "MPEG Audio (Layer I)"); // MPEG1 Layer 1
            testGenericAudio("MP3/mp1Layer2.mp1", 752, 384, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "MPEG Audio (Layer II)"); // MPEG1 Layer 2
            testGenericAudio("MP3/mp2Layer1.mp2", 1408, 128, 22050, false, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer I)"); // MPEG2 Layer 1
            testGenericAudio("MP3/mp2Layer2.mp2", 1296, 160, 24000, false, AudioDataIOFactory.CF_LOSSY, STEREO, "MPEG Audio (Layer II)"); // MPEG2 Layer 2
        }

        [TestMethod]
        public void Audio_MP4()
        {
            testGenericAudio("MP4/mp4.m4a", 14053, 75, 48000, true, AudioDataIOFactory.CF_LOSSY, ISO_3_4_1, "MPEG-4 Part 14");
        }

        [TestMethod]
        public void Audio_AAC_ADTS()
        {
            testGenericAudio("AAC/adts_CBR88_8s.aac", 7742, 88, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "Advanced Audio Coding"); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_AAC_ADIF()
        {
            testGenericAudio("AAC/adif_CBR88_8s.aac", 7729, 88, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "Advanced Audio Coding"); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_WMA()
        {
            testGenericAudio("WMA/wma.wma", 14439, 9, 8000, false, AudioDataIOFactory.CF_LOSSY, MONO, "Windows Media Audio");
        }

        [TestMethod]
        public void Audio_OGG()
        {
            testGenericAudio("OGG/ogg.ogg", 33003, 69, 22050, true, AudioDataIOFactory.CF_LOSSY, STEREO, "OGG : Vorbis, Opus");
        }

        [TestMethod]
        public void Audio_Opus()
        {
            testGenericAudio("OPUS/opus.opus", 30959, 33, 48000, true, AudioDataIOFactory.CF_LOSSY, STEREO, "OGG : Vorbis, Opus");
        }

        [TestMethod]
        public void Audio_FLAC()
        {
            testGenericAudio("FLAC/flac.flac", 5176, 694, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Free Lossless Audio Codec");
        }

        [TestMethod]
        public void Audio_MPC()
        {
            testGenericAudio("MPC/SV8.mpc", 7646, 127, 44100, true, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO_MID_SIDE, "Musepack / MPEGplus");
            testGenericAudio("MPC/SV7.mpc", 7654, 131, 44100, true, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus"); // should be 7646 ms as well
            testGenericAudio("MPC/SV5.mp+", 7654, 112, 44100, true, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus"); // should be 7646 ms as well
            testGenericAudio("MPC/SV4.mp+", 7654, 112, 44100, true, AudioDataIOFactory.CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus"); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_AC3()
        {
            testGenericAudio("AC3/empty.ac3", 4969, 128, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "Dolby Digital");
        }

        [TestMethod]
        public void Audio_DTS()
        {
            testGenericAudio("DTS/dts.dts", 9834, 1536, 48000, false, AudioDataIOFactory.CF_LOSSY, ISO_3_2_0, "Digital Theatre System");
        }

        [TestMethod]
        public void Audio_DSF_DSD()
        {
            testGenericAudio("DSF/dsf.dsf", 3982, 5671, 2822400, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Direct Stream Digital");
        }

        [TestMethod]
        public void Audio_IT()
        {
            testGenericAudio("IT/empty.it", 475505, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Impulse Tracker");
            testGenericAudio("IT/it.it", 42292, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Impulse Tracker");
            testGenericAudio("IT/hasInstruments.it", 68092, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Impulse Tracker");
        }

        [TestMethod]
        public void Audio_Midi()
        {
            testGenericAudio("MID/ataezou - I (HEART) RUEAMATASU.mid", 66497, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            testGenericAudio("MID/TRANSIT1.MID", 104950, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            testGenericAudio("MID/ROQ.MID", 503602, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            // This one has a track header position issue
            testGenericAudio("MID/yoru-uta.mid", 251182, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            // This one has 'sequencer data' and 'smpte offset' events
            testGenericAudio("MID/memory.mid", 300915, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            // This one has 'channel prefix', 'poly pressure' and 'channel pressure' events
            testGenericAudio("MID/villg.mid", 100059, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
            // This one has 'program change repeat' and 'channel pressure repeat' events
            testGenericAudio("MID/chron.mid", 323953, 0, 0, false, AudioDataIOFactory.CF_SEQ, STEREO, "Musical Instruments Digital Interface");
        }

        [TestMethod]
        public void Audio_MOD()
        {
            testGenericAudio("MOD/empty.mod", 158976, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)");
            testGenericAudio("MOD/mod.mod", 330240, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)");
        }

        [TestMethod]
        public void Audio_Ape()
        {
            testGenericAudio("APE/ape.ape", 7646, 652, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Monkey's Audio");
            testGenericAudio("APE/v394.ape", 7646, 599, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Monkey's Audio");
        }

        [TestMethod]
        public void Audio_S3M()
        {
            testGenericAudio("S3M/empty.s3m", 126720, 0, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "ScreamTracker Module");
            testGenericAudio("S3M/s3m.s3m", 404846, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "ScreamTracker Module");
            // This one contains extra instructions
            testGenericAudio("S3M/s3m2.s3m", 9796, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "ScreamTracker Module");
            // This one contains yet other extra instructions
            testGenericAudio("S3M/s3m3.s3m", 475070, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "ScreamTracker Module");
        }

        [TestMethod]
        public void Audio_XM()
        {
            testGenericAudio("XM/empty.xm", 55172, 1, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Extended Module");
            testGenericAudio("XM/xm.xm", 260667, 2, 0, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Extended Module");
        }

        [TestMethod]
        public void Audio_DSF_PSF()
        {
            testGenericAudio("PSF/psf.psf", 159000, 10, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)");
            testGenericAudio("PSF/nolength.psf", 180000, 13, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)");
            testGenericAudio("DSF/adgpp_PLAY_01_05.dsf", 26200, 0, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Portable Sound Format (Dreamcast)", 1);
        }

        [TestMethod]
        public void Audio_SPC()
        {
            testGenericAudio("SPC/spc.spc", 69, 7646, 32000, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "SPC700 Sound Files");
        }

        [TestMethod]
        public void Audio_VQF()
        {
            testGenericAudio("VQF/vqf.vqf", 120130, 20, 22050, false, AudioDataIOFactory.CF_LOSSY, MONO, "TwinVQ");
        }

        [TestMethod]
        public void Audio_TAK()
        {
            testGenericAudio("TAK/003 BlackBird.tak", 6082, 634, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Tom's lossless Audio Kompressor");
        }

        [TestMethod]
        public void Audio_WAV()
        {
            testGenericAudio("WAV/wav.wav", 7646, 1411, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Windows PCM)");
            testGenericAudio("WAV/rifx.wav", 0, 2117, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Unknown)");
        }

        [TestMethod]
        public void Audio_WV()
        {
            testGenericAudio("WV/losslessv3.wv", 7646, 659, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "WAVPack");
            testGenericAudio("WV/lossyv3.wv", 7646, 342, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "WAVPack");
            testGenericAudio("WV/lossyv440.wv", 7646, 206, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "WAVPack");
            testGenericAudio("WV/losslessv4.wv", 6082, 645, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "WAVPack");
        }

        [TestMethod]
        public void Audio_OFR()
        {
            testGenericAudio("OFR/BlackBird.ofr", 6082, 620, 44100, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "OptimFROG");
        }

        [TestMethod]
        public void Audio_TTA()
        {
            testGenericAudio("TTA/BlackBird.tta", 6082, 659, 44100, false, AudioDataIOFactory.CF_LOSSY, STEREO, "True Audio");
        }

        [TestMethod]
        public void Audio_AIFF()
        {
            testGenericAudio("AIF/aiff_empty.aif", 2937, 512, 8000, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Audio Interchange File Format");
        }

        [TestMethod]
        public void Audio_AIFC()
        {
            testGenericAudio("AIF/aifc_tagged.aif", 2937, 128, 8000, false, AudioDataIOFactory.CF_LOSSY, STEREO, "Audio Interchange File Format");
        }

        [TestMethod]
        public void Audio_VGM()
        {
            testGenericAudio("VGM/vgm.vgm", 86840, 1, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Video Game Music");
            testGenericAudio("VGM/vgz.vgz", 232584, 3, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Video Game Music");
        }

        [TestMethod]
        public void Audio_GYM()
        {
            testGenericAudio("GYM/gym.gym", 73000, 37, 44100, false, AudioDataIOFactory.CF_SEQ_WAV, STEREO, "Genesis YM2612");
        }

        [TestMethod]
        public void Audio_AA()
        {
            testGenericAudio("AA/aa.aa", 2967, 1, 8500, false, AudioDataIOFactory.CF_LOSSY, MONO, "Audible (legacy) (acelp85)");
        }

        [TestMethod]
        public void Audio_CAF()
        {
            testGenericAudio("CAF/caf.caf", 3235, 176, 11025, false, AudioDataIOFactory.CF_LOSSLESS, STEREO, "Apple Core Audio / Linear PCM");
        }
    }
}