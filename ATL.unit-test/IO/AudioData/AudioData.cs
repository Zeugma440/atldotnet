using ATL.AudioData;
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
            int containerId,
            int dataId,
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
            bool testStream = true,
            int alternate = 0)
        {
            new ConsoleLogger();

            string theResource = TestUtils.GetResourceLocationRoot() + resource;

            // Test detect format from file name
            testReader(GetInstance().GetFromPath(theResource, alternate), null, containerId, dataId, duration, bitrate, bitDepth, samplerate, isVbr, codecFamily, channelsArrangement, formatName, audioDataOffset, audioDataSize);

            // Test detect format from stream
            if (testStream)
            {
                using (FileStream fs = new FileStream(theResource, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    testReader(GetInstance().GetFromStream(fs), fs, containerId, dataId, duration, bitrate, bitDepth, samplerate, isVbr, codecFamily, channelsArrangement, formatName, audioDataOffset, audioDataSize);
                }
            }
        }

        private void testReader(
            IAudioDataIO theReader,
            Stream stream,
            int containerId,
            int dataId,
            int duration,
            int bitrate,
            int bitDepth,
            int samplerate,
            bool isVbr,
            int codecFamily,
            ChannelsArrangement channelsArrangement,
            string formatName,
            long audioDataOffset,
            long audioDataSize)
        {
            Assert.IsNotInstanceOfType(theReader, typeof(ATL.AudioData.IO.DummyReader));

            AudioDataManager manager = new AudioDataManager(theReader, stream);
            manager.ReadFromFile();

            Assert.AreEqual(containerId, theReader.AudioFormat.ContainerId);
            Assert.AreEqual(dataId, theReader.AudioFormat.DataFormat.ID);

            Assert.AreEqual(duration, (int)Math.Round(theReader.Duration));
            Assert.AreEqual(bitrate, (int)Math.Round(theReader.BitRate));
            Assert.AreEqual(bitDepth, theReader.BitDepth);
            Assert.AreEqual(samplerate, theReader.SampleRate);
            Assert.AreEqual(isVbr, theReader.IsVBR);
            Assert.AreEqual(codecFamily, theReader.CodecFamily);
            Assert.AreEqual(channelsArrangement, theReader.ChannelsArrangement);
            Assert.AreEqual(formatName, theReader.AudioFormat.Name);
            Assert.AreEqual(audioDataOffset, theReader.AudioDataOffset);
            Assert.AreEqual(audioDataSize, theReader.AudioDataSize);
        }

#pragma warning disable S2699 // Tests should include assertions
        [TestMethod]
        public void Audio_MP3()
        {
            var initialSetting = ATL.Settings.MP3_parseExactDuration;
            try
            {
                ATL.Settings.MP3_parseExactDuration = false;

                // MPEG1 Layer 1
                testGenericAudio("MP3/mp1Layer1.mp1", CID_MPEG, CID_MPEG, 522, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer I)", 0, 25080);
                // MPEG1 Layer 2
                testGenericAudio("MP3/mp1Layer2.mp1", CID_MPEG, CID_MPEG, 758, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 36362);
                // MPEG2 Layer 1
                testGenericAudio("MP3/mp2Layer1.mp2", CID_MPEG, CID_MPEG, 1411, 128, -1, 22050, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer I)", 0, 22572);
                // MPEG2 Layer 2
                testGenericAudio("MP3/mp2Layer2.mp2", CID_MPEG, CID_MPEG, 1296, 160, -1, 24000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 25920);

                // VBR
                testGenericAudio("MP3/01 - Title Screen.mp3", CID_MPEG, CID_MPEG, 3866, 129, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 2048, 62342);
                // Malpositioned header
                testGenericAudio("MP3/headerPatternIsNotHeader.mp3", CID_MPEG, CID_MPEG, 139, 192, -1, 44100, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 1252, 3340);
                // Malpositioned header 2
                testGenericAudio("MP3/truncated_frame.mp3", CID_MPEG, CID_MPEG, 498, 320, -1, 48000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer III)", 954, 19908);
                // Fake header + garbage before actual header
                testGenericAudio("MP3/garbage_before_header.mp3", CID_MPEG, CID_MPEG, 6142, 64, -1, 24000, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 141, 49139);
                // Ghost data after the last frame
                testGenericAudio("MP3/ghost_data_after_last_frame.mp3", CID_MPEG, CID_MPEG, 1258, 112, -1, 44100, false, CF_LOSSY, MONO, "MPEG Audio (Layer III)", 0, 17607);
                // Contradictory frames
                testGenericAudio("MP3/different_bitrates_modes.mp3", CID_MPEG, CID_MPEG, 6439, 128, -1, 44100, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 45, 103025);

                ATL.Settings.MP3_parseExactDuration = true;

                // MPEG1 Layer 1
                testGenericAudio("MP3/mp1Layer1.mp1", CID_MPEG, CID_MPEG, 522, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer I)", 0, 25080);
                // MPEG1 Layer 2
                testGenericAudio("MP3/mp1Layer2.mp1", CID_MPEG, CID_MPEG, 758, 384, -1, 44100, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 36362);
                // MPEG2 Layer 1
                testGenericAudio("MP3/mp2Layer1.mp2", CID_MPEG, CID_MPEG, 1411, 128, -1, 22050, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer I)", 0, 22572);
                // MPEG2 Layer 2
                testGenericAudio("MP3/mp2Layer2.mp2", CID_MPEG, CID_MPEG, 1296, 160, -1, 24000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer II)", 0, 25920);

                // VBR
                testGenericAudio("MP3/01 - Title Screen.mp3", CID_MPEG, CID_MPEG, 3866, 129, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 2048, 62346);
                // Malpositioned header
                testGenericAudio("MP3/headerPatternIsNotHeader.mp3", CID_MPEG, CID_MPEG, 139, 192, -1, 44100, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 1252, 3340);
                // Malpositioned header 2
                testGenericAudio("MP3/truncated_frame.mp3", CID_MPEG, CID_MPEG, 498, 320, -1, 48000, false, CF_LOSSY, STEREO, "MPEG Audio (Layer III)", 954, 19908);
                // Fake header + garbage before actual header
                testGenericAudio("MP3/garbage_before_header.mp3", CID_MPEG, CID_MPEG, 6142, 64, -1, 24000, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 141, 49139);
                // Ghost data after the last frame
                testGenericAudio("MP3/ghost_data_after_last_frame.mp3", CID_MPEG, CID_MPEG, 209, 112, -1, 44100, false, CF_LOSSY, MONO, "MPEG Audio (Layer III)", 0, 2929);
                // Contradictory frames
                testGenericAudio("MP3/different_bitrates_modes.mp3", CID_MPEG, CID_MPEG, 6439, 128, -1, 44100, false, CF_LOSSY, JOINT_STEREO, "MPEG Audio (Layer III)", 45, 103025);
            }
            finally
            {
                ATL.Settings.MP3_parseExactDuration = initialSetting;
            }
        }

        [TestMethod]
        public void Audio_MP4()
        {
            testGenericAudio("MP4/mp4.m4a", CID_MP4, CID_MP4, 14053, 75, -1, 48000, true, CF_LOSSY, ISO_3_4_1, "MPEG-4 Part 14", 25746, 131708);
            // Bitrate is 0 as the MDAT has been manually truncated
            testGenericAudio("MP4/fragmented.mp4", CID_MP4, CID_MP4, 87211, 0, -1, 48000, false, CF_LOSSY, STEREO, "MPEG-4 Part 14", 18820, 2252);
        }

        [TestMethod]
        public void Audio_AAC_ADTS()
        {
            // should be 7646 ms as well
            testGenericAudio("AAC/adts_CBR88_8s.aac", CID_AAC, CID_AAC, 7742, 88, -1, 44100, false, CF_LOSSY, STEREO, "Advanced Audio Coding", 0, 85022);
        }

        [TestMethod]
        public void Audio_AAC_ADIF()
        {
            // should be 7646 ms as well
            testGenericAudio("AAC/adif_CBR88_8s.aac", CID_AAC, CID_AAC, 7729, 88, -1, 44100, false, CF_LOSSY, STEREO, "Advanced Audio Coding", 0, 85014);
        }

        [TestMethod]
        public void Audio_WMA()
        {
            testGenericAudio("WMA/wma.wma", CID_WMA, CID_WMA, 14439, 9, 16, 8000, false, CF_LOSSY, MONO, "Windows Media Audio", 18313, 16269);
        }

        [TestMethod]
        public void Audio_OGG()
        {
            testGenericAudio("OGG/ogg.ogg", CID_OGG, CID_OGG, 33003, 69, -1, 22050, true, CF_LOSSY, STEREO, "OGG (Vorbis)", 23125, 278029);
        }

        [TestMethod]
        public void Audio_Opus()
        {
            testGenericAudio("OPUS/opus.opus", CID_OGG, CID_OGG, 30959, 33, -1, 48000, true, CF_LOSSY, STEREO, "OGG (Opus)", 19479, 126225);
        }

        [TestMethod]
        public void Audio_FLAC()
        {
            testGenericAudio("FLAC/flac.flac", CID_FLAC, CID_FLAC, 5176, 694, 16, 44100, false, CF_LOSSLESS, STEREO, "Free Lossless Audio Codec", 18030, 448997);
            testGenericAudio("OGG/embedded-flac.ogg", CID_OGG, CID_FLAC, 5176, 635, 16, 44100, false, CF_LOSSLESS, STEREO, "OGG (FLAC)", 397, 410863);
        }

        [TestMethod]
        public void Audio_MPC()
        {
            testGenericAudio("MPC/SV8.mpc", CID_MPC, CID_MPC, 7646, 127, -1, 44100, true, CF_LOSSY, JOINT_STEREO_MID_SIDE, "Musepack / MPEGplus", 4, 121061);
            // should be 7646 ms as well
            testGenericAudio("MPC/SV7.mpc", CID_MPC, CID_MPC, 7654, 131, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 125432);
            // should be 7646 ms as well; <V7 won't be detected from stream
            testGenericAudio("MPC/SV5.mp+", CID_MPC, CID_MPC, 7654, 112, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 107160, false);
            // should be 7646 ms as well
            testGenericAudio("MPC/SV4.mp+", CID_MPC, CID_MPC, 7654, 112, -1, 44100, true, CF_LOSSY, JOINT_STEREO, "Musepack / MPEGplus", 0, 107156, false);
        }

        [TestMethod]
        public void Audio_AC3()
        {
            testGenericAudio("AC3/empty.ac3", CID_AC3, CID_AC3, 4969, 128, -1, 44100, false, CF_LOSSY, STEREO, "Dolby Digital", 0, 79508);
        }

        [TestMethod]
        public void Audio_DTS()
        {
            testGenericAudio("DTS/dts.dts", CID_DTS, CID_DTS, 9834, 1536, 24, 96000, false, CF_LOSSY, ISO_3_2_1, "Digital Theatre System", 0, 1888194);
        }

        [TestMethod]
        public void Audio_DSF_DSD()
        {
            testGenericAudio("DSF/dsf.dsf", CID_DSF, CID_DSF, 3982, 5671, 1, 2822400, false, CF_LOSSLESS, STEREO, "Direct Stream Digital", 80, 2809868);
        }

        [TestMethod]
        public void Audio_IT()
        {
            testGenericAudio("IT/empty.it", CID_IT, CID_IT, 475505, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 578264);
            testGenericAudio("IT/it.it", CID_IT, CID_IT, 42292, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 22623);
            testGenericAudio("IT/hasInstruments.it", CID_IT, CID_IT, 68092, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Impulse Tracker", 32, 51781);
        }

        [TestMethod]
        public void Audio_Midi()
        {
            testGenericAudio("MID/ataezou - I (HEART) RUEAMATASU.mid", CID_MIDI, CID_MIDI, 66497, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 21264);
            testGenericAudio("MID/TRANSIT1.MID", CID_MIDI, CID_MIDI, 104950, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 7087);
            testGenericAudio("MID/ROQ.MID", CID_MIDI, CID_MIDI, 503602, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 59375);
            // This one has a track header position issue
            testGenericAudio("MID/yoru-uta.mid", CID_MIDI, CID_MIDI, 251182, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 13298);
            // This one has 'sequencer data' and 'smpte offset' events
            testGenericAudio("MID/memory.mid", CID_MIDI, CID_MIDI, 300915, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 93597);
            // This one has 'channel prefix', 'poly pressure' and 'channel pressure' events
            testGenericAudio("MID/villg.mid", CID_MIDI, CID_MIDI, 100059, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 21660);
            // This one has 'program change repeat' and 'channel pressure repeat' events
            testGenericAudio("MID/chron.mid", CID_MIDI, CID_MIDI, 323953, 0, -1, 0, false, CF_SEQ, STEREO, "Musical Instruments Digital Interface", 14, 56129);
        }

        [TestMethod]
        public void Audio_MOD()
        {
            // No distinctive header
            testGenericAudio("MOD/empty.mod", CID_MOD, CID_MOD, 158976, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)", 20, 42042, false);
            // No distinctive header
            testGenericAudio("MOD/mod.mod", CID_MOD, CID_MOD, 330240, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "Tracker Module (ProTracker)", 20, 99986, false);
        }

        [TestMethod]
        public void Audio_Ape()
        {
            testGenericAudio("APE/ape.ape", CID_APE, CID_APE, 7646, 652, 16, 44100, false, CF_LOSSLESS, STEREO, "Monkey's Audio", 0, 623078);
            testGenericAudio("APE/v394.ape", CID_APE, CID_APE, 7646, 599, 16, 44100, false, CF_LOSSLESS, STEREO, "Monkey's Audio", 0, 572806);
        }

        [TestMethod]
        public void Audio_S3M()
        {
            // No distinctive header
            testGenericAudio("S3M/empty.s3m", CID_S3M, CID_S3M, 126720, 0, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module (OpenMPT)", 32, 13936, false);
            // No distinctive header
            testGenericAudio("S3M/s3m.s3m", CID_S3M, CID_S3M, 404846, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module (ScreamTracker)", 32, 626624, false);
            // This one contains extra instructions
            // No distinctive header
            testGenericAudio("S3M/s3m2.s3m", CID_S3M, CID_S3M, 9796, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module (ScreamTracker)", 32, 17870, false);
            // This one contains yet other extra instructions
            // No distinctive header
            testGenericAudio("S3M/s3m3.s3m", CID_S3M, CID_S3M, 475070, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "ScreamTracker Module (ScreamTracker)", 32, 375488, false);
        }

        [TestMethod]
        public void Audio_XM()
        {
            testGenericAudio("XM/empty.xm", CID_XM, CID_XM, 55172, 1, -1, 0, false, CF_SEQ_WAV, STEREO, "Extended Module (OpenMPT 1.26.14.00)", 60, 55729);
            testGenericAudio("XM/xm.xm", CID_XM, CID_XM, 260667, 2, -1, 0, false, CF_SEQ_WAV, STEREO, "Extended Module (FastTracker v2.00)", 60, 430062);
        }

        [TestMethod]
        public void Audio_DSF_PSF()
        {
            testGenericAudio("PSF/psf.psf", CID_PSF, CID_PSF, 159000, 10, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)", 0, 204788);
            testGenericAudio("PSF/nolength.psf", CID_PSF, CID_PSF, 180000, 13, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Playstation)", 0, 287437);
            testGenericAudio("DSF/adgpp_PLAY_01_05.dsf", CID_PSF, CID_PSF, 26200, 0, -1, 44100, false, CF_SEQ_WAV, STEREO, "Portable Sound Format (Dreamcast)", 0, 30, true, 1);
        }

        [TestMethod]
        public void Audio_SPC()
        {
            testGenericAudio("SPC/spc.spc", CID_SPC, CID_SPC, 69, 7635, -1, 32000, false, CF_SEQ_WAV, STEREO, "SPC700 Sound Files", 209, 65852);
        }

        [TestMethod]
        public void Audio_VQF()
        {
            testGenericAudio("VQF/vqf.vqf", CID_VQF, CID_VQF, 120130, 20, -1, 22050, false, CF_LOSSY, MONO, "TwinVQ", 174, 300332);
        }

        [TestMethod]
        public void Audio_TAK()
        {
            testGenericAudio("TAK/003 BlackBird.tak", CID_TAK, CID_TAK, 6082, 634, 16, 44100, false, CF_LOSSLESS, STEREO, "Tom's lossless Audio Kompressor", 0, 476797);
        }

        [TestMethod]
        public void Audio_WAV()
        {
            testGenericAudio("WAV/wav.wav", CID_WAV, CID_WAV, 7646, 1411, 16, 44100, false, CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Windows PCM)", 44, 1348720);
            testGenericAudio("WAV/rifx.wav", CID_WAV, CID_WAV, 150, 2117, 24, 44100, false, CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Unknown)", 80, 39690);
            testGenericAudio("WAV/cortissimo.wav", CID_WAV, CID_WAV, 187, 6144, 32, 96000, false, CF_LOSSLESS, STEREO, "PCM (uncompressed audio) (Unknown)", 80, 143544);
        }

        [TestMethod]
        public void Audio_WV()
        {
            // V3 won't be auto-detected from data stream
            testGenericAudio("WV/losslessv3.wv", CID_WAVPACK, CID_WAVPACK, 7646, 659, -1, 44100, false, CF_LOSSLESS, STEREO, "WAVPack", 44, 629811, false);
            testGenericAudio("WV/lossyv3.wv", CID_WAVPACK, CID_WAVPACK, 7646, 342, 4, 44100, false, CF_LOSSY, STEREO, "WAVPack", 44, 326945, false);
            testGenericAudio("WV/lossyv440.wv", CID_WAVPACK, CID_WAVPACK, 7646, 206, 16, 44100, false, CF_LOSSY, STEREO, "WAVPack", 132, 196658);
            testGenericAudio("WV/losslessv4.wv", CID_WAVPACK, CID_WAVPACK, 6082, 645, 16, 44100, false, CF_LOSSLESS, STEREO, "WAVPack", 154, 490420);
        }

        [TestMethod]
        public void Audio_OFR()
        {
            testGenericAudio("OFR/BlackBird.ofr", CID_OFR, CID_OFR, 6082, 620, 16, 44100, false, CF_LOSSLESS, STEREO, "OptimFROG", 0, 471627);
        }

        [TestMethod]
        public void Audio_TTA()
        {
            testGenericAudio("TTA/BlackBird.tta", CID_TTA, CID_TTA, 6082, 659, 16, 44100, false, CF_LOSSY, STEREO, "True Audio", 0, 501282);
        }

        [TestMethod]
        public void Audio_AIFF()
        {
            testGenericAudio("AIF/aiff_empty.aif", CID_AIFF, CID_AIFF, 2937, 512, 32, 8000, false, CF_LOSSLESS, STEREO, "Audio Interchange File Format", 120, 187960);
        }

        [TestMethod]
        public void Audio_AIFC()
        {
            testGenericAudio("AIF/aifc_tagged.aif", CID_AIFF, CID_AIFF, 2937, 128, 8, 8000, false, CF_LOSSY, STEREO, "Audio Interchange File Format", 146, 47002);
        }

        [TestMethod]
        public void Audio_VGM()
        {
            testGenericAudio("VGM/vgm.vgm", CID_VGM, CID_VGM, 86840, 1, -1, 44100, false, CF_SEQ_WAV, STEREO, "Video Game Music", 4, 7708);
            // Can't autodetect that
            testGenericAudio("VGM/vgz.vgz", CID_VGM, CID_VGM, 232584, 3, -1, 44100, false, CF_SEQ_WAV, STEREO, "Video Game Music", 4, 589589, false);
        }

        [TestMethod]
        public void Audio_GYM()
        {
            testGenericAudio("GYM/gym.gym", CID_GYM, CID_GYM, 73000, 37, -1, 44100, false, CF_SEQ_WAV, STEREO, "Genesis YM2612", 428, 341133);
        }

        [TestMethod]
        public void Audio_AA()
        {
            testGenericAudio("AA/aa.aa", CID_AA, CID_AA, 2967, 1, -1, 8500, false, CF_LOSSY, MONO, "Audible (legacy) (acelp85)", 26806, 3152173);
        }

        [TestMethod]
        public void Audio_CAF()
        {
            testGenericAudio("CAF/caf.caf", CID_CAF, CID_WAV, 3235, 176, 16, 11025, false, CF_LOSSLESS, STEREO, "Apple Core Audio / Linear PCM", 4080, 71340);
        }

        [TestMethod]
        public void Audio_MKA()
        {
            testGenericAudio("MKA/mka.mka", CID_MKA, CID_MPEG, 3422, 128, -1, 44100, false, CF_LOSSY, STEREO, "Matroska / MPEG", 459, 69016);
            testGenericAudio("MKA/no_info_duration.webm", CID_MKA, CID_OGG, 7159, 16, 0, 48000, false, CF_LOSSY, MONO, "Matroska / Opus", 167, 112605);
        }

        [TestMethod]
        public void Audio_SPX()
        {
            testGenericAudio("SPX/empty.spx", CID_OGG, CID_OGG, 15675, 9, -1, 8000, false, CF_LOSSY, MONO, "OGG (Speex)", 156, 17010);
            testGenericAudio("SPX/stereo.spx", CID_OGG, CID_OGG, 8715, 17, -1, 8000, false, CF_LOSSY, STEREO, "OGG (Speex)", 322, 18119);
        }
#pragma warning restore S2699 // Tests should include assertions
    }
}