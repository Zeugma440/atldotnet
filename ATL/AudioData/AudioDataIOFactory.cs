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
                tempFmt.AddMimeTypes("audio/mp3", "audio/mpeg", "audio/x-mpeg");
                tempFmt.AddExtensions(".mp1", ".mp2", ".mp3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OGG : Vorbis, Opus");
                tempFmt.ID = CID_OGG;
                tempFmt.AddMimeTypes("audio/ogg", "audio/vorbis", "audio/opus", "audio/ogg;codecs=opus");
                tempFmt.AddExtensions(".ogg", ".opus");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musepack / MPEGplus");
                tempFmt.ID = CID_MPC;
                tempFmt.AddMimeTypes("audio/x-musepack", "audio/musepack");
                tempFmt.AddExtensions(".mp+", ".mpc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Windows Media Audio");
                tempFmt.ID = CID_WMA;
                tempFmt.AddMimeTypes("audio/x-ms-wma", "video/x-ms-asf");
                tempFmt.AddExtensions(".asf", ".wma");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Advanced Audio Coding");
                tempFmt.ID = CID_AAC;
                tempFmt.AddMimeTypes("audio/mp4", "audio/aac", "audio/mp4a-latm");
                tempFmt.AddExtensions(".aac", ".mp4", ".m4a", ".m4v");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Dolby Digital");
                tempFmt.ID = CID_AC3;
                tempFmt.AddMimeTypes("audio/ac3");
                tempFmt.AddExtensions(".ac3");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Digital Theatre System");
                tempFmt.ID = CID_DTS;
                tempFmt.AddMimeTypes("audio/vnd.dts", "audio/vnd.dts.hd");
                tempFmt.AddExtensions(".dts");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("TwinVQ");
                tempFmt.ID = CID_VQF;
                tempFmt.AddExtensions(".vqf");
                tempFmt.AddMimeTypes("audio/x-twinvq");
                tempFmt.Readable = false;
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Free Lossless Audio Codec");
                tempFmt.ID = CID_FLAC;
                tempFmt.AddMimeTypes("audio/x-flac");
                tempFmt.AddExtensions(".flac");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Monkey's Audio");
                tempFmt.ID = CID_APE;
                tempFmt.AddMimeTypes("audio/ape", "audio/x-ape");
                tempFmt.AddExtensions(".ape");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("OptimFROG");
                tempFmt.ID = CID_OFR;
                tempFmt.AddMimeTypes("audio/ofr", "audio/x-ofr");
                tempFmt.AddExtensions(".ofr", ".ofs");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("WAVPack");
                tempFmt.ID = CID_WAVPACK;
                tempFmt.AddMimeTypes("audio/x-wavpack", "audio/wavpack");
                tempFmt.AddExtensions(".wv");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("PCM (uncompressed audio)");
                tempFmt.ID = CID_WAV;
                tempFmt.AddMimeTypes("audio/x-wav", "audio/wav");
                tempFmt.AddExtensions(".wav", ".bwf", ".bwav");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Musical Instruments Digital Interface");
                tempFmt.ID = CID_MIDI;
                tempFmt.AddMimeTypes("audio/mid");
                tempFmt.AddExtensions(".mid", ".midi");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Direct Stream Digital");
                tempFmt.ID = CID_DSF;
                tempFmt.AddMimeTypes("audio/dsf", "audio/x-dsf", "audio/dsd", "audio/x-dsd");
                tempFmt.AddExtensions(".dsf", ".dsd");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Portable Sound Format");
                tempFmt.ID = CID_PSF;
                tempFmt.AddMimeTypes("audio/psf", "audio/x-psf"); // Unofficial
                tempFmt.AddExtensions(".psf", ".psf1", ".minipsf", ".minipsf1", ".psf2");
                tempFmt.AddExtensions(".minipsf2", ".ssf", ".minissf", ".dsf", ".minidsf", ".gsf", ".minigsf", ".qsf", ".miniqsf");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("SPC700 Sound Files");
                tempFmt.ID = CID_SPC;
                tempFmt.AddMimeTypes("audio/spc", "audio/x-spc"); // Unofficial
                tempFmt.AddExtensions(".spc");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("True Audio");
                tempFmt.ID = CID_TTA;
                tempFmt.AddMimeTypes("audio/tta", "audio/x-tta");
                tempFmt.AddExtensions(".tta");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Tom's lossless Audio Kompressor (TAK)");
                tempFmt.ID = CID_TAK;
                tempFmt.AddMimeTypes("audio/tak", "audio/x-tak"); // Unofficial
                tempFmt.AddExtensions(".tak");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Noisetracker/Soundtracker/Protracker Module");
                tempFmt.ID = CID_MOD;
                tempFmt.AddMimeTypes("audio/x-mod");
                tempFmt.AddExtensions(".mod");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("ScreamTracker Module");
                tempFmt.ID = CID_S3M;
                tempFmt.AddMimeTypes("audio/s3m", "audio/x-s3m");
                tempFmt.AddExtensions(".s3m");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Extended Module");
                tempFmt.ID = CID_XM;
                tempFmt.AddMimeTypes("audio/xm", "audio/x-xm");
                tempFmt.AddExtensions(".xm");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Impulse Tracker");
                tempFmt.ID = CID_IT;
                tempFmt.AddMimeTypes("audio/it");
                tempFmt.AddExtensions(".it");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Audio Interchange File Format: (Audio IFF)");
                tempFmt.ID = CID_AIFF;
                tempFmt.AddMimeTypes("audio/x-aiff");
                tempFmt.AddExtensions(".aif", ".aiff", ".aifc", ".snd");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Video Game Music");
                tempFmt.ID = CID_VGM;
                tempFmt.AddMimeTypes("audio/vgm", "audio/x-vgm"); // Unofficial
                tempFmt.AddExtensions(".vgm", ".vgz");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("Genesis YM2612");
                tempFmt.ID = CID_GYM;
                tempFmt.AddMimeTypes("audio/gym", "audio/x-gym"); // Unofficial
                tempFmt.AddExtensions(".gym");
                theFactory.addFormat(tempFmt);
            }

            return theFactory;
        }

        public IAudioDataIO GetFromPath(string path, int alternate = 0)
        {
            IList<Format> formats = getFormatsFromPath(path);
            int formatId = formats.Count > alternate ? formats[alternate].ID : NO_FORMAT;

            switch (formatId)
            {
                case CID_MP3: return new IO.MPEGaudio(path);
                case CID_AAC: return new IO.AAC(path);
                case CID_WMA: return new IO.WMA(path);
                case CID_OGG: return new IO.Ogg(path);
                case CID_FLAC: return new IO.FLAC(path);
                case CID_MPC: return new IO.MPEGplus(path);
                case CID_AC3: return new IO.AC3(path);
                case CID_DSF: return new IO.DSF(path);
                case CID_DTS: return new IO.DTS(path);
                case CID_IT: return new IO.IT(path);
                case CID_MIDI: return new IO.Midi(path);
                case CID_MOD: return new IO.MOD(path);
                case CID_APE: return new IO.APE(path);
                case CID_OFR: return new IO.OptimFrog(path);
                case CID_WAVPACK: return new IO.WAVPack(path);
                case CID_WAV: return new IO.WAV(path);
                case CID_PSF: return new IO.PSF(path);
                case CID_SPC: return new IO.SPC(path);
                case CID_TAK: return new IO.TAK(path);
                case CID_S3M: return new IO.S3M(path);
                case CID_XM: return new IO.XM(path);
                case CID_TTA: return new IO.TTA(path);
                case CID_VQF: return new IO.TwinVQ(path);
                case CID_AIFF: return new IO.AIFF(path);
                case CID_VGM: return new IO.VGM(path);
                case CID_GYM: return new IO.GYM(path);
                default: return new IO.DummyReader(path);
            }
        }

        public IAudioDataIO GetFromMimeType(string mimeType, string path, int alternate = 0)
        {
            var formats = mimeType.StartsWith(".") ? getFormatsFromPath(mimeType) : getFormatsFromMimeType(mimeType);
            int formatId = formats.Count > alternate ? formats[alternate].ID : NO_FORMAT;

            switch (formatId)
            {
                case CID_MP3: return new IO.MPEGaudio(path);
                case CID_AAC: return new IO.AAC(path);
                case CID_WMA: return new IO.WMA(path);
                case CID_OGG: return new IO.Ogg(path);
                case CID_FLAC: return new IO.FLAC(path);
                case CID_MPC: return new IO.MPEGplus(path);
                case CID_AC3: return new IO.AC3(path);
                case CID_DSF: return new IO.DSF(path);
                case CID_DTS: return new IO.DTS(path);
                case CID_IT: return new IO.IT(path);
                case CID_MIDI: return new IO.Midi(path);
                case CID_MOD: return new IO.MOD(path);
                case CID_APE: return new IO.APE(path);
                case CID_OFR: return new IO.OptimFrog(path);
                case CID_WAVPACK: return new IO.WAVPack(path);
                case CID_WAV: return new IO.WAV(path);
                case CID_PSF: return new IO.PSF(path);
                case CID_SPC: return new IO.SPC(path);
                case CID_TAK: return new IO.TAK(path);
                case CID_S3M: return new IO.S3M(path);
                case CID_XM: return new IO.XM(path);
                case CID_TTA: return new IO.TTA(path);
                case CID_VQF: return new IO.TwinVQ(path);
                case CID_AIFF: return new IO.AIFF(path);
                case CID_VGM: return new IO.VGM(path);
                case CID_GYM: return new IO.GYM(path);
                default: return new IO.DummyReader(path);
            }
        }
    }
}
