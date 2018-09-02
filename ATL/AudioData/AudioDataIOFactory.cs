using System;
using System.Collections.Generic;

namespace ATL.AudioData
{
    /// <summary>
    /// Factory for audio data readers
    /// </summary>
    public class AudioDataIOFactory : ReaderFactory
    {
        // Codec families
        public const int CF_LOSSY = 0; // Streamed, lossy data
        public const int CF_LOSSLESS = 1; // Streamed, lossless data
        public const int CF_SEQ_WAV = 2; // Sequenced with embedded sound library
        public const int CF_SEQ = 3; // Sequenced with codec or hardware-dependent sound library

        public static int NB_CODEC_FAMILIES = 4;

        public const int MAX_ALTERNATES = 10;   // Max number of alternate formats having the same file extension

        // The instance of this factory
        private static AudioDataIOFactory theFactory = null;

        // Codec IDs
        public const int CID_MP3 = 0;
        public const int CID_OGG = 1;
        public const int CID_MPC = 2;
        public const int CID_FLAC = 3;
        public const int CID_APE = 4;
        public const int CID_WMA = 5;
        public const int CID_MIDI = 6;
        public const int CID_AAC = 7;
        public const int CID_AC3 = 8;
        public const int CID_OFR = 9;
        public const int CID_WAVPACK = 10;
        public const int CID_WAV = 11;
        public const int CID_PSF = 12;
        public const int CID_SPC = 13;
        public const int CID_DTS = 14;
        public const int CID_VQF = 15;
        public const int CID_TTA = 16;
        public const int CID_DSF = 17;
        public const int CID_TAK = 18;
        public const int CID_MOD = 19;
        public const int CID_S3M = 20;
        public const int CID_XM = 21;
        public const int CID_IT = 22;
        public const int CID_AIFF = 23;
        public const int CID_VGM = 24;
        public const int CID_GYM = 25;

        public const int NB_CODECS = 26;

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the instance of this factory (Singleton pattern) 
        /// </summary>
        /// <returns>Instance of the AudioReaderFactory of the application</returns>
        public static AudioDataIOFactory GetInstance()
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Big-endian based platforms are not supported by ATL");

            if (null == theFactory)
            {
                theFactory = new AudioDataIOFactory();

                theFactory.formatListByExt = new Dictionary<string, IList<Format>>();
                theFactory.formatListByMime = new Dictionary<string, IList<Format>>();

                Format tempFmt = new Format("MPEG Audio Layer");
                tempFmt.ID = CID_MP3;
                tempFmt.AddMimeType("audio/mp3");
                tempFmt.AddMimeType("audio/mpeg");
                tempFmt.AddMimeType("audio/x-mpeg");
                tempFmt.AddExtension(".mp1");
                tempFmt.AddExtension(".mp2");
                tempFmt.AddExtension(".mp3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OGG : Vorbis, Opus");
                tempFmt.ID = CID_OGG;
                tempFmt.AddMimeType("audio/ogg");
                tempFmt.AddMimeType("audio/vorbis");
                tempFmt.AddMimeType("audio/opus");
                tempFmt.AddMimeType("audio/ogg;codecs=opus");
                tempFmt.AddExtension(".ogg");
                tempFmt.AddExtension(".opus");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musepack / MPEGplus");
                tempFmt.ID = CID_MPC;
                tempFmt.AddMimeType("audio/x-musepack");
                tempFmt.AddMimeType("audio/musepack");
                tempFmt.AddExtension(".mp+");
                tempFmt.AddExtension(".mpc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Windows Media Audio");
                tempFmt.ID = CID_WMA;
                tempFmt.AddMimeType("audio/x-ms-wma");
                tempFmt.AddMimeType("video/x-ms-asf");
                tempFmt.AddExtension(".asf");
                tempFmt.AddExtension(".wma");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Advanced Audio Coding");
                tempFmt.ID = CID_AAC;
                tempFmt.AddMimeType("audio/mp4");
                tempFmt.AddMimeType("audio/aac");
                tempFmt.AddMimeType("audio/mp4a-latm");
                tempFmt.AddExtension(".aac");
                tempFmt.AddExtension(".mp4");
                tempFmt.AddExtension(".m4a");
                tempFmt.AddExtension(".m4v");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Dolby Digital");
                tempFmt.ID = CID_AC3;
                tempFmt.AddMimeType("audio/ac3");
                tempFmt.AddExtension(".ac3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Digital Theatre System");
                tempFmt.ID = CID_DTS;
                tempFmt.AddMimeType("audio/vnd.dts");
                tempFmt.AddMimeType("audio/vnd.dts.hd");
                tempFmt.AddExtension(".dts");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("TwinVQ");
                tempFmt.ID = CID_VQF;
                tempFmt.AddExtension(".vqf");
                tempFmt.AddMimeType("audio/x-twinvq");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Free Lossless Audio Codec");
                tempFmt.ID = CID_FLAC;
                tempFmt.AddMimeType("audio/x-flac");
                tempFmt.AddExtension(".flac");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Monkey's Audio");
                tempFmt.ID = CID_APE;
                tempFmt.AddMimeType("audio/ape");
                tempFmt.AddMimeType("audio/x-ape");
                tempFmt.AddExtension(".ape");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OptimFROG");
                tempFmt.ID = CID_OFR;
                tempFmt.AddMimeType("audio/ofr");
                tempFmt.AddMimeType("audio/x-ofr");
                tempFmt.AddExtension(".ofr");
                tempFmt.AddExtension(".ofs");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("WAVPack");
                tempFmt.ID = CID_WAVPACK;
                tempFmt.AddMimeType("audio/x-wavpack");
                tempFmt.AddMimeType("audio/wavpack");
                tempFmt.AddExtension(".wv");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("PCM (uncompressed audio)");
                tempFmt.ID = CID_WAV;
                tempFmt.AddMimeType("audio/x-wav");
                tempFmt.AddMimeType("audio/wav");
                tempFmt.AddExtension(".wav");
                tempFmt.AddExtension(".bwf");
                tempFmt.AddExtension(".bwav");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musical Instruments Digital Interface");
                tempFmt.ID = CID_MIDI;
                tempFmt.AddMimeType("audio/mid");
                tempFmt.AddExtension(".mid");
                tempFmt.AddExtension(".midi");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Direct Stream Digital");
                tempFmt.ID = CID_DSF;
                tempFmt.AddMimeType("audio/dsf");
                tempFmt.AddMimeType("audio/x-dsf");
                tempFmt.AddMimeType("audio/dsd");
                tempFmt.AddMimeType("audio/x-dsd");
                tempFmt.AddExtension(".dsf");
                tempFmt.AddExtension(".dsd");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Portable Sound Format");
                tempFmt.ID = CID_PSF;
                tempFmt.AddMimeType("audio/psf");   // Unofficial
                tempFmt.AddMimeType("audio/x-psf"); // Unofficial
                tempFmt.AddExtension(".psf");
                tempFmt.AddExtension(".psf1");
                tempFmt.AddExtension(".minipsf");
                tempFmt.AddExtension(".minipsf1");
                tempFmt.AddExtension(".psf2");
                tempFmt.AddExtension(".minipsf2");
                tempFmt.AddExtension(".ssf");
                tempFmt.AddExtension(".minissf");
                tempFmt.AddExtension(".dsf");
                tempFmt.AddExtension(".minidsf");
                tempFmt.AddExtension(".gsf");
                tempFmt.AddExtension(".minigsf");
                tempFmt.AddExtension(".qsf");
                tempFmt.AddExtension(".miniqsf");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("SPC700 Sound Files");
                tempFmt.ID = CID_SPC;
                tempFmt.AddMimeType("audio/spc");   // Unofficial
                tempFmt.AddMimeType("audio/x-spc"); // Unofficial
                tempFmt.AddExtension(".spc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("True Audio");
                tempFmt.ID = CID_TTA;
                tempFmt.AddMimeType("audio/tta");
                tempFmt.AddMimeType("audio/x-tta");
                tempFmt.AddExtension(".tta");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Tom's lossless Audio Kompressor (TAK)");
                tempFmt.ID = CID_TAK;
                tempFmt.AddMimeType("audio/tak");   // Unofficial
                tempFmt.AddMimeType("audio/x-tak"); // Unofficial
                tempFmt.AddExtension(".tak");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Noisetracker/Soundtracker/Protracker Module");
                tempFmt.ID = CID_MOD;
                tempFmt.AddMimeType("audio/x-mod");
                tempFmt.AddExtension(".mod");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("ScreamTracker Module");
                tempFmt.ID = CID_S3M;
                tempFmt.AddMimeType("audio/s3m");
                tempFmt.AddMimeType("audio/x-s3m");
                tempFmt.AddExtension(".s3m");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Extended Module");
                tempFmt.ID = CID_XM;
                tempFmt.AddMimeType("audio/xm");
                tempFmt.AddMimeType("audio/x-xm");
                tempFmt.AddExtension(".xm");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Impulse Tracker");
                tempFmt.ID = CID_IT;
                tempFmt.AddMimeType("audio/it");
                tempFmt.AddExtension(".it");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Audio Interchange File Format: (Audio IFF)");
                tempFmt.ID = CID_AIFF;
                tempFmt.AddMimeType("audio/x-aiff");
                tempFmt.AddExtension(".aif");
                tempFmt.AddExtension(".aiff");
                tempFmt.AddExtension(".aifc");
                tempFmt.AddExtension(".snd");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Video Game Music");
                tempFmt.ID = CID_VGM;
                tempFmt.AddMimeType("audio/vgm");   // Unofficial
                tempFmt.AddMimeType("audio/x-vgm"); // Unofficial
                tempFmt.AddExtension(".vgm");
                tempFmt.AddExtension(".vgz");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Genesis YM2612");
                tempFmt.ID = CID_GYM;
                tempFmt.AddMimeType("audio/gym");   // Unofficial
                tempFmt.AddMimeType("audio/x-gym"); // Unofficial
                tempFmt.AddExtension(".gym");
                theFactory.addFormat(tempFmt);
            }

            return theFactory;
        }

        public IAudioDataIO GetFromPath(String path, int alternate = 0)
        {
            IList<Format> formats = getFormatsFromPath(path);
            int formatId = NO_FORMAT;

            if (formats != null && formats.Count > alternate)
            {
                formatId = formats[alternate].ID;
            }

            IAudioDataIO theDataReader = null;

            switch (formatId)
            {
                case CID_MP3:
                    theDataReader = new IO.MPEGaudio(path);
                    break;
                case CID_AAC:
                    theDataReader = new IO.AAC(path);
                    break;
                case CID_WMA:
                    theDataReader = new IO.WMA(path);
                    break;
                case CID_OGG:
                    theDataReader = new IO.Ogg(path);
                    break;
                case CID_FLAC:
                    theDataReader = new IO.FLAC(path);
                    break;
                case CID_MPC:
                    theDataReader = new IO.MPEGplus(path);
                    break;
                case CID_AC3:
                    theDataReader = new IO.AC3(path);
                    break;
                case CID_DSF:
                    theDataReader = new IO.DSF(path);
                    break;
                case CID_DTS:
                    theDataReader = new IO.DTS(path);
                    break;
                case CID_IT:
                    theDataReader = new IO.IT(path);
                    break;
                case CID_MIDI:
                    theDataReader = new IO.Midi(path);
                    break;
                case CID_MOD:
                    theDataReader = new IO.MOD(path);
                    break;
                case CID_APE:
                    theDataReader = new IO.APE(path);
                    break;
                case CID_OFR:
                    theDataReader = new IO.OptimFrog(path);
                    break;
                case CID_WAVPACK:
                    theDataReader = new IO.WAVPack(path);
                    break;
                case CID_WAV:
                    theDataReader = new IO.WAV(path);
                    break;
                case CID_PSF:
                    theDataReader = new IO.PSF(path);
                    break;
                case CID_SPC:
                    theDataReader = new IO.SPC(path);
                    break;
                case CID_TAK:
                    theDataReader = new IO.TAK(path);
                    break;
                case CID_S3M:
                    theDataReader = new IO.S3M(path);
                    break;
                case CID_XM:
                    theDataReader = new IO.XM(path);
                    break;
                case CID_TTA:
                    theDataReader = new IO.TTA(path);
                    break;
                case CID_VQF:
                    theDataReader = new IO.TwinVQ(path);
                    break;
                case CID_AIFF:
                    theDataReader = new IO.AIFF(path);
                    break;
                case CID_VGM:
                    theDataReader = new IO.VGM(path);
                    break;
                case CID_GYM:
                    theDataReader = new IO.GYM(path);
                    break;
                default:
                    theDataReader = new IO.DummyReader(path);
                    break;
            }

            return theDataReader;
        }

        public IAudioDataIO GetFromMimeType(String mimeType, String path, int alternate = 0)
        {
            IList<Format> formats;
            if (mimeType.StartsWith(".")) formats = getFormatsFromPath(mimeType);
            else formats = getFormatsFromMimeType(mimeType);

            int formatId = NO_FORMAT;

            if (formats != null && formats.Count > alternate)
            {
                formatId = formats[alternate].ID;
            }

            IAudioDataIO theDataReader = null;

            switch (formatId)
            {
                case CID_MP3:
                    theDataReader = new IO.MPEGaudio(path);
                    break;
                case CID_AAC:
                    theDataReader = new IO.AAC(path);
                    break;
                case CID_WMA:
                    theDataReader = new IO.WMA(path);
                    break;
                case CID_OGG:
                    theDataReader = new IO.Ogg(path);
                    break;
                case CID_FLAC:
                    theDataReader = new IO.FLAC(path);
                    break;
                case CID_MPC:
                    theDataReader = new IO.MPEGplus(path);
                    break;
                case CID_AC3:
                    theDataReader = new IO.AC3(path);
                    break;
                case CID_DSF:
                    theDataReader = new IO.DSF(path);
                    break;
                case CID_DTS:
                    theDataReader = new IO.DTS(path);
                    break;
                case CID_IT:
                    theDataReader = new IO.IT(path);
                    break;
                case CID_MIDI:
                    theDataReader = new IO.Midi(path);
                    break;
                case CID_MOD:
                    theDataReader = new IO.MOD(path);
                    break;
                case CID_APE:
                    theDataReader = new IO.APE(path);
                    break;
                case CID_OFR:
                    theDataReader = new IO.OptimFrog(path);
                    break;
                case CID_WAVPACK:
                    theDataReader = new IO.WAVPack(path);
                    break;
                case CID_WAV:
                    theDataReader = new IO.WAV(path);
                    break;
                case CID_PSF:
                    theDataReader = new IO.PSF(path);
                    break;
                case CID_SPC:
                    theDataReader = new IO.SPC(path);
                    break;
                case CID_TAK:
                    theDataReader = new IO.TAK(path);
                    break;
                case CID_S3M:
                    theDataReader = new IO.S3M(path);
                    break;
                case CID_XM:
                    theDataReader = new IO.XM(path);
                    break;
                case CID_TTA:
                    theDataReader = new IO.TTA(path);
                    break;
                case CID_VQF:
                    theDataReader = new IO.TwinVQ(path);
                    break;
                case CID_AIFF:
                    theDataReader = new IO.AIFF(path);
                    break;
                case CID_VGM:
                    theDataReader = new IO.VGM(path);
                    break;
                case CID_GYM:
                    theDataReader = new IO.GYM(path);
                    break;
                default:
                    theDataReader = new IO.DummyReader(path);
                    break;
            }

            return theDataReader;
        }
    }
}
