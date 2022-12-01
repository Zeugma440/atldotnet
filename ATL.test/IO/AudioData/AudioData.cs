using ATL.AudioData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static ATL.ChannelsArrangements;
using static ATL.AudioData.AudioDataIOFactory;

namespace ATL.test.IO
{
    [TestClass]
    public class AudioData
    {
        [TestMethod]
        public void Audio_FallbackToDummy()
        {
            IAudioDataIO theReader = GetInstance().GetFromPath(TestUtils.GetResourceLocationRoot() + "MP3/01 - Title Screen.xyz");
            Assert.IsInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));
        }

        private void testGenericAudio(
            string resource,
            int duration,
            int bitrate,
            int bitDepth,
            int samplerate,
            bool isVbr,
            int codecFamily,
            ChannelsArrangement channelsArrangement,
            string formatName,
            long audioDataOffset,
            long audioDataSize,
            int alternate = 0)
        {
            new ConsoleLogger();
            string theResource = TestUtils.GetResourceLocationRoot() + resource;

            IAudioDataIO theReader = GetInstance().GetFromPath(theResource, alternate);

            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));

            AudioDataManager manager = new AudioDataManager(theReader);
            manager.ReadFromFile();

            Assert.AreEqual(duration, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(bitrate, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(bitDepth, theReader.BitDepth);
            Assert.AreEqual(samplerate, theReader.SampleRate);
            Assert.AreEqual(isVbr, theReader.IsVBR);
            Assert.AreEqual(codecFamily, theReader.CodecFamily);
            Assert.AreEqual(channelsArrangement, theReader.ChannelsArrangement);
            Assert.AreEqual(audioDataOffset, theReader.AudioDataOffset);
            Assert.AreEqual(audioDataSize, theReader.AudioDataSize);
            Assert.AreEqual(formatName, theReader.AudioFormat.Name);
        }

#pragma warning disable S2699 // Tests should include assertions
        [TestMethod]
        public void Audio_MP3()
        {
            testGenericAudio("MP3/01 - Title Screen.mp3", 3866, 129, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 2048, 62342); // VBR
            testGenericAudio("MP3/headerPatternIsNotHeader.mp3", 184, 192, -1, 44100, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 1252, 3340); // Malpositioned header
            testGenericAudio("MP3/truncated_frame.mp3", 520, 320, -1, 48000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer III)", 954, 19908); // Malpositioned header 2
            testGenericAudio("MP3/mp1Layer1.mp1", 520, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer I)", 0, 25080); // MPEG1 Layer 1
            testGenericAudio("MP3/mp1Layer2.mp1", 752, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 36362); // MPEG1 Layer 2
            testGenericAudio("MP3/mp2Layer1.mp2", 1408, 128, -1, 22050, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer I)", 0, 22572); // MPEG2 Layer 1
            testGenericAudio("MP3/mp2Layer2.mp2", 1296, 160, -1, 24000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 25920); // MPEG2 Layer 2
        }

        [TestMethod]
        public void Audio_MP4()
        {
            testGenericAudio("MP4/mp4.m4a", 14053, 75, -1, 48000, true, CF_LOSSY, ISO_3_4_1, "MPEG-4 Part 14", 25746, 131708);
        }

        [TestMethod]
        public void Audio_AAC_ADTS()
        {
            testGenericAudio("AAC/adts_CBR88_8s.aac", 7742, 88, -1, 44100, false, CF_LOSSY, STEREO, "Advanced Audio Coding", 0, 85022); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_AAC_ADIF()
        {
            testGenericAudio("AAC/adif_CBR88_8s.aac", 7729, 88, -1, 44100, false, CF_LOSSY, STEREO, "Advanced Audio Coding", 0, 85014); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_WMA()
        {
            testGenericAudio("WMA/wma.wma", 14439, 9, 16, 8000, false, CF_LOSSY, MONO, "Windows Media Audio", 18313, 16269);
        }

        [TestMethod]
        public void Audio_OGG()
        {
            testGenericAudio("OGG/ogg.ogg", 33003, 69, -1, 22050, true, CF_LOSSY, STEREO, "OGG (Vorbis)", 23125, 278029);
        }

        [TestMethod]
        public void Audio_Opus()
        {
            testGenericAudio("OPUS/opus.opus", 30959, 33, -1, 48000, true, CF_LOSSY, STEREO, "OGG (Opus)", 19479, 126225);
        }

        [TestMethod]
        public void Audio_FLAC()
        {
            testGenericAudio("FLAC/flac.flac", 5176, 694, 16, 44100, false, CF_LOSSLESS, STEREO, "Free Lossless Audio Codec", 18030, 448997);
            testGenericAudio("OGG/embedded-flac.ogg", 5176, 635, 16, 44100, false, CF_LOSSLESS, STEREO, "OGG (FLAC)", 397, 410863);
        }

        [TestMethod]
        public void Audio_MPC()
        {
            testGenericAudio("MPC/SV8.mpc", 7646, 127, -1, 44100, true, CF_LOSSY, JOINT_STEREO_MID_SIDE, "Musepack / MPEGplus", 4, 121061);
            testGenericAudio("MPC/SV7.mpc", 7654, 131, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 125432); // should be 7646 ms as well
            testGenericAudio("MPC/SV5.mp+", 7654, 112, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 107160); // should be 7646 ms as well
            testGenericAudio("MPC/SV4.mp+", 7654, 112, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 107156); // should be 7646 ms as well
        }

        [TestMethod]
        public void Audio_AC3()
        {
            testGenericAudio("AC3/empty.ac3", 4969, 128, -1, 44100, false, CF_LOSSY, STEREO, "Dolby Digital", 0, 79508);
        }

        [TestMethod]
        public void Audio_DTS()
        {
            testGenericAudio("DTS/dts.dts", 9834, 1536, 16, 48000, false, CF_LOSSY, ISO_3_2_0, "Digital Theatre System", 0, 1888194);
        }

        [TestMethod]
        public void Audio_DSF_DSD()
        {
            testGenericAudio("DSF/dsf.dsf", 3982, 5671, 1, 2822400, false, CF_LOSSLESS, STEREO, "Direct Stream Digital", 80, 2809868);
        }

        [TestMethod]
        public void Audio_IT()
        {
            testGenericAudio("IT/empty.it", 475505, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 578264);
            testGenericAudio("IT/it.it", 42292, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 22623);
            testGenericAudio("IT/hasInstruments.it", 68092, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 51781);
        }

        [TestMethod]
        public void Audio_Midi()
        {
            testGenericAudio("MID/ataezou - I (HEART) RUEAMATASU.mid", 66497, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 21264);
            testGenericAudio("MID/TRANSIT1.MID", 104950, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 7087);
            testGenericAudio("MID/ROQ.MID", 503602, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 59375);
            // This one has a track header position issue
            testGenericAudio("MID/yoru-uta.mid", 251182, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 13298);
            // This one has 'sequencer data' and 'smpte offset' events
            testGenericAudio("MID/memory.mid", 300915, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 93597);
            // This one has 'channel prefix', 'poly pressure' and 'channel pressure' events
            testGenericAudio("MID/villg.mid", 100059, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 21660);
            // This one has 'program change repeat' and 'channel pressure repeat' events
            testGenericAudio("MID/chron.mid", 323953, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 56129);
        }

        [TestMethod]
        public void Audio_MOD()
        {
            testGenericAudio("MOD/empty.mod", 158976, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)", 20, 42042);
            testGenericAudio("MOD/mod.mod", 330240, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)", 20, 99986);
        }

        [TestMethod]
        public void Audio_Ape()
        {
            testGenericAudio("APE/ape.ape", 7646, 652, 16, 44100, false, CF_LOSSLESS, STEREO, "Monkey's Audio", 0, 623078);
            testGenericAudio("APE/v394.ape", 7646, 599, 16, 44100, false, CF_LOSSLESS, STEREO, "Monkey's Audio", 0, 572806);
        }

        [TestMethod]
        public void Audio_S3M()
        {
            testGenericAudio("S3M/empty.s3m", 126720, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module", 32, 13936);
            testGenericAudio("S3M/s3m.s3m", 404846, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module", 32, 626624);
            // This one contains extra instructions
            testGenericAudio("S3M/s3m2.s3m", 9796, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module", 32, 17870);
            // This one contains yet other extra instructions
            testGenericAudio("S3M/s3m3.s3m", 475070, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module", 32, 375488);
        }

        [TestMethod]
        public void Audio_XM()
        {
            testGenericAudio("XM/empty.xm", 55172, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Extended Module", 60, 55729);
            testGenericAudio("XM/xm.xm", 260667, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "Extended Module", 60, 430062);
        }

        [TestMethod]
        public void Audio_DSF_PSF()
        {
            testGenericAudio("PSF/psf.psf", 159000, 10, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)", 0, 204788);
            testGenericAudio("PSF/nolength.psf", 180000, 13, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)", 0, 287437);
            testGenericAudio("DSF/adgpp_PLAY_01_05.dsf", 26200, 0, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Dreamcast)", 0, 30, 1);
        }

        [TestMethod]
        public void Audio_SPC()
        {
            testGenericAudio("SPC/spc.spc", 69, 7635, -1, 32000, false, CF_SEQ_WAV, STEREO, "SPC700 Sound Files", 209, 65852); ;
        }

        [TestMethod]
        public void Audio_VQF()
        {
            testGenericAudio("VQF/vqf.vqf", 120130, 20, -1, 22050, false, CF_LOSSY, MONO, "TwinVQ", 174, 300332);
        }

        [TestMethod]
        public void Audio_TAK()
        {
            testGenericAudio("TAK/003 BlackBird.tak", 6082, 634, 16, 44100, false, CF_LOSSLESS, STEREO, "Tom's lossless Audio Kompressor", 0, 476797);
        }

        [TestMethod]
        public void Audio_WAV()
        {
            testGenericAudio("WAV/wav.wav", 7646, 1411, 16, 44100, false, CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Windows PCM)", 44, 1348720);
            testGenericAudio("WAV/rifx.wav", 0, 2117, 24, 44100, false, CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Unknown)", 80, 39690);
        }

        [TestMethod]
        public void Audio_WV()
        {
            testGenericAudio("WV/losslessv3.wv", 7646, 659, -1, 44100, false, CF_LOSSLESS, STEREO, "WAVPack", 44, 629811);
            testGenericAudio("WV/lossyv3.wv", 7646, 342, 4, 44100, false, CF_LOSSY, STEREO, "WAVPack", 44, 326945);
            testGenericAudio("WV/lossyv440.wv", 7646, 206, 16, 44100, false, CF_LOSSY, STEREO, "WAVPack", 132, 196658);
            testGenericAudio("WV/losslessv4.wv", 6082, 645, 16, 44100, false, CF_LOSSLESS, STEREO, "WAVPack", 154, 490420);
        }

        [TestMethod]
        public void Audio_OFR()
        {
            testGenericAudio("OFR/BlackBird.ofr", 6082, 620, 16, 44100, false, CF_LOSSLESS, STEREO, "OptimFROG", 0, 471627);
        }

        [TestMethod]
        public void Audio_TTA()
        {
            testGenericAudio("TTA/BlackBird.tta", 6082, 659, 16, 44100, false, CF_LOSSY, STEREO, "True Audio", 0, 501282);
        }

        [TestMethod]
        public void Audio_AIFF()
        {
            testGenericAudio("AIF/aiff_empty.aif", 2937, 512, 32, 8000, false, CF_LOSSLESS, STEREO, "Audio Interchange File Format", 120, 187960);
        }

        [TestMethod]
        public void Audio_AIFC()
        {
            testGenericAudio("AIF/aifc_tagged.aif", 2937, 128, 8, 8000, false, CF_LOSSY, STEREO, "Audio Interchange File Format", 146, 47002);
        }

        [TestMethod]
        public void Audio_VGM()
        {
            testGenericAudio("VGM/vgm.vgm", 86840, 1, -1, 44100, false, CF_SEQ_WAV, STEREO, "Video Game Music", 4, 7708);
            testGenericAudio("VGM/vgz.vgz", 232584, 3, -1, 44100, false, CF_SEQ_WAV, STEREO, "Video Game Music", 4, 589589);
        }

        [TestMethod]
        public void Audio_GYM()
        {
            testGenericAudio("GYM/gym.gym", 73000, 37, -1, 44100, false, CF_SEQ_WAV, STEREO, "Genesis YM2612", 428, 341133);
        }

        [TestMethod]
        public void Audio_AA()
        {
            testGenericAudio("AA/aa.aa", 2967, 1, -1, 8500, false, CF_LOSSY, MONO, "Audible (legacy) (acelp85)", 26806, 3152173);
        }

        [TestMethod]
        public void Audio_CAF()
        {
            testGenericAudio("CAF/caf.caf", 3235, 176, 16, 11025, false, CF_LOSSLESS, STEREO, "Apple Core Audio / Linear PCM", 4080, 71340);
        }
#pragma warning restore S2699 // Tests should include assertions
    }
}