using System;
using System.Collections.Generic;

namespace ATL.AudioData
{
    /// <summary>
    /// Factory for audio data readers
    /// </summary>
    public class AudioDataIOFactory : Factory
    {
        // Codec families
        /// <summary>
        /// Streamed, lossy data
        /// </summary>
        public const int CF_LOSSY = 0;
        /// <summary>
        /// Streamed, lossless data
        /// </summary>
        public const int CF_LOSSLESS = 1;
        /// <summary>
        /// Sequenced with embedded sound library
        /// </summary>
        public const int CF_SEQ_WAV = 2;
        /// <summary>
        /// Sequenced with codec or hardware-dependent sound library
        /// </summary>
        public const int CF_SEQ = 3;

        /// <summary>
        /// Number of codec families
        /// </summary>
        public static readonly int NB_CODEC_FAMILIES = 4;

        /// <summary>
        /// Max number of alternate formats having the same file extension
        /// </summary>
        public const int MAX_ALTERNATES = 2;

        // The instance of this factory
        private static AudioDataIOFactory theFactory = null;

        private static readonly object _lockable = new object();

        // Codec IDs
#pragma warning disable CS1591 // Missing XML comment
        public const int CID_MP3 = 0;
        public const int CID_OGG = 1000;
        public const int CID_MPC = 2000;
        public const int CID_FLAC = 3000;
        public const int CID_APE = 4000;
        public const int CID_WMA = 5000;
        public const int CID_MIDI = 6000;
        public const int CID_AAC = 7000;
        public const int CID_AC3 = 8000;
        public const int CID_OFR = 9000;
        public const int CID_WAVPACK = 10000;
        public const int CID_WAV = 11000;
        public const int CID_PSF = 12000;
        public const int CID_SPC = 13000;
        public const int CID_DTS = 14000;
        public const int CID_VQF = 15000;
        public const int CID_TTA = 16000;
        public const int CID_DSF = 17000;
        public const int CID_TAK = 18000;
        public const int CID_MOD = 19000;
        public const int CID_S3M = 20000;
        public const int CID_XM = 21000;
        public const int CID_IT = 22000;
        public const int CID_AIFF = 23000;
        public const int CID_VGM = 24000;
        public const int CID_GYM = 25000;
        public const int CID_MP4 = 26000;
        public const int CID_AA = 27000;
        public const int CID_CAF = 28000;

        public const int NB_CODECS = 29;
#pragma warning restore CS1591 // Missing XML comment

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the instance of this factory (Singleton pattern) 
        /// </summary>
        /// <returns>Instance of the AudioReaderFactory of the application</returns>
        public static AudioDataIOFactory GetInstance()
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("Big-endian based platforms are not supported by ATL");

            lock (_lockable)
            {
                if (null == theFactory)
                {
                    theFactory = new AudioDataIOFactory();

                    theFactory.formatListByExt = new Dictionary<string, IList<Format>>();
                    theFactory.formatListByMime = new Dictionary<string, IList<Format>>();

                    Format tempFmt = new Format(CID_MP3, "MPEG Audio", "MPEG");
                    tempFmt.AddMimeType("audio/mp3");
                    tempFmt.AddMimeType("audio/mpeg");
                    tempFmt.AddMimeType("audio/x-mpeg");
                    tempFmt.AddExtension(".mp1");
                    tempFmt.AddExtension(".mp2");
                    tempFmt.AddExtension(".mp3");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_OGG, "OGG : Vorbis, Opus", "OGG");
                    tempFmt.AddMimeType("audio/ogg");
                    tempFmt.AddMimeType("audio/vorbis");
                    tempFmt.AddMimeType("audio/opus");
                    tempFmt.AddMimeType("audio/ogg;codecs=opus");
                    tempFmt.AddExtension(".ogg");
                    tempFmt.AddExtension(".opus");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MPC, "Musepack / MPEGplus", "MPC");
                    tempFmt.AddMimeType("audio/x-musepack");
                    tempFmt.AddMimeType("audio/musepack");
                    tempFmt.AddExtension(".mp+");
                    tempFmt.AddExtension(".mpc");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WMA, "Windows Media Audio", "WMA");
                    tempFmt.AddMimeType("audio/x-ms-wma");
                    tempFmt.AddMimeType("video/x-ms-asf");
                    tempFmt.AddExtension(".asf");
                    tempFmt.AddExtension(".wma");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AAC, "Advanced Audio Coding");
                    tempFmt.AddMimeType("audio/aac");
                    tempFmt.AddExtension(".aac");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MP4, "MPEG-4 Part 14", "MPEG-4");
                    tempFmt.AddMimeType("audio/mp4");
                    tempFmt.AddMimeType("audio/mp4a-latm");
                    tempFmt.AddMimeType("audio/vnd.audible.aax");
                    tempFmt.AddExtension(".mp4");
                    tempFmt.AddExtension(".m4a");
                    tempFmt.AddExtension(".m4b");
                    tempFmt.AddExtension(".m4p");
                    tempFmt.AddExtension(".m4r");
                    tempFmt.AddExtension(".m4v");
                    tempFmt.AddExtension(".aax");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AC3, "Dolby Digital", "Dolby");
                    tempFmt.AddMimeType("audio/ac3");
                    tempFmt.AddExtension(".ac3");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_DTS, "Digital Theatre System", "DTS");
                    tempFmt.AddMimeType("audio/vnd.dts");
                    tempFmt.AddMimeType("audio/vnd.dts.hd");
                    tempFmt.AddExtension(".dts");
                    tempFmt.Readable = false;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_VQF, "TwinVQ");
                    tempFmt.AddExtension(".vqf");
                    tempFmt.AddMimeType("audio/x-twinvq");
                    tempFmt.Readable = false;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_FLAC, "Free Lossless Audio Codec", "FLAC");
                    tempFmt.AddMimeType("audio/x-flac");
                    tempFmt.AddExtension(".flac");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_APE, "Monkey's Audio", "APE");
                    tempFmt.AddMimeType("audio/ape");
                    tempFmt.AddMimeType("audio/x-ape");
                    tempFmt.AddExtension(".ape");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_OFR, "OptimFROG");
                    tempFmt.AddMimeType("audio/ofr");
                    tempFmt.AddMimeType("audio/x-ofr");
                    tempFmt.AddExtension(".ofr");
                    tempFmt.AddExtension(".ofs");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WAVPACK, "WAVPack");
                    tempFmt.AddMimeType("audio/x-wavpack");
                    tempFmt.AddMimeType("audio/wavpack");
                    tempFmt.AddExtension(".wv");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WAV, "PCM (uncompressed audio)", "WAV");
                    tempFmt.AddMimeType("audio/x-wav");
                    tempFmt.AddMimeType("audio/wav");
                    tempFmt.AddExtension(".wav");
                    tempFmt.AddExtension(".bwf");
                    tempFmt.AddExtension(".bwav");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MIDI, "Musical Instruments Digital Interface", "MIDI");
                    tempFmt.AddMimeType("audio/mid");
                    tempFmt.AddExtension(".mid");
                    tempFmt.AddExtension(".midi");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_DSF, "Direct Stream Digital", "DSD");
                    tempFmt.AddMimeType("audio/dsf");
                    tempFmt.AddMimeType("audio/x-dsf");
                    tempFmt.AddMimeType("audio/dsd");
                    tempFmt.AddMimeType("audio/x-dsd");
                    tempFmt.AddExtension(".dsf");
                    tempFmt.AddExtension(".dsd");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_PSF, "Portable Sound Format", "PSF");
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

                    tempFmt = new Format(CID_SPC, "SPC700 Sound Files", "SPC");
                    tempFmt.AddMimeType("audio/spc");   // Unofficial
                    tempFmt.AddMimeType("audio/x-spc"); // Unofficial
                    tempFmt.AddExtension(".spc");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_TTA, "True Audio");
                    tempFmt.AddMimeType("audio/tta");
                    tempFmt.AddMimeType("audio/x-tta");
                    tempFmt.AddExtension(".tta");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_TAK, "Tom's lossless Audio Kompressor", "TAK");
                    tempFmt.AddMimeType("audio/tak");   // Unofficial
                    tempFmt.AddMimeType("audio/x-tak"); // Unofficial
                    tempFmt.AddExtension(".tak");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MOD, "Tracker Module", "MOD");
                    tempFmt.AddMimeType("audio/x-mod");
                    tempFmt.AddExtension(".mod");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_S3M, "ScreamTracker Module", "S3M");
                    tempFmt.AddMimeType("audio/s3m");
                    tempFmt.AddMimeType("audio/x-s3m");
                    tempFmt.AddExtension(".s3m");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_XM, "Extended Module", "XM");
                    tempFmt.AddMimeType("audio/xm");
                    tempFmt.AddMimeType("audio/x-xm");
                    tempFmt.AddExtension(".xm");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_IT, "Impulse Tracker", "IT");
                    tempFmt.AddMimeType("audio/it");
                    tempFmt.AddExtension(".it");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AIFF, "Audio Interchange File Format", "AIFF");
                    tempFmt.AddMimeType("audio/x-aiff");
                    tempFmt.AddExtension(".aif");
                    tempFmt.AddExtension(".aiff");
                    tempFmt.AddExtension(".aifc");
                    tempFmt.AddExtension(".snd");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_VGM, "Video Game Music", "VGM");
                    tempFmt.AddMimeType("audio/vgm");   // Unofficial
                    tempFmt.AddMimeType("audio/x-vgm"); // Unofficial
                    tempFmt.AddExtension(".vgm");
                    tempFmt.AddExtension(".vgz");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_GYM, "Genesis YM2612", "GYM");
                    tempFmt.AddMimeType("audio/gym");   // Unofficial
                    tempFmt.AddMimeType("audio/x-gym"); // Unofficial
                    tempFmt.AddExtension(".gym");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AA, "Audible (legacy)", "AA");
                    tempFmt.AddMimeType("audio/audible");
                    tempFmt.AddMimeType("audio/x-pn-audibleaudio");
                    tempFmt.AddExtension(".aa");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_CAF, "Apple Core Audio", "CAF");
                    tempFmt.AddMimeType("audio/x-caf");
                    tempFmt.AddExtension(".caf");
                    theFactory.addFormat(tempFmt);
                }
            }

            return theFactory;
        }

        /// <summary>
        /// Get the proper IAudioDataIO to exploit the file at the given path,
        /// or a dummy object if no proper IAudioDataIO has been found
        /// </summary>
        /// <param name="path">Path of the file to exploit</param>
        /// <param name="alternate">Index of the alternate format to use (for internal use only)</param>
        /// <returns>Appropriate IAudioDataIO to exploit the file at the given path, or dummy object if no proper IAudioDataIO has been found</returns>
        public IAudioDataIO GetFromPath(string path, int alternate = 0)
        {
            IList<Format> formats = getFormatsFromPath(path);
            Format theFormat;
            if (formats != null && formats.Count > alternate)
                theFormat = formats[alternate];
            else
                theFormat = UNKNOWN_FORMAT;

            int formatId = theFormat.ID;

            IAudioDataIO theDataReader;

            switch (formatId)
            {
                case CID_MP3:
                    theDataReader = new IO.MPEGaudio(path, theFormat);
                    break;
                case CID_AAC:
                    theDataReader = new IO.AAC(path, theFormat);
                    break;
                case CID_MP4:
                    theDataReader = new IO.MP4(path, theFormat);
                    break;
                case CID_WMA:
                    theDataReader = new IO.WMA(path, theFormat);
                    break;
                case CID_OGG:
                    theDataReader = new IO.Ogg(path, theFormat);
                    break;
                case CID_FLAC:
                    theDataReader = new IO.FLAC(path, theFormat);
                    break;
                case CID_MPC:
                    theDataReader = new IO.MPEGplus(path, theFormat);
                    break;
                case CID_AC3:
                    theDataReader = new IO.AC3(path, theFormat);
                    break;
                case CID_DSF:
                    theDataReader = new IO.DSF(path, theFormat);
                    break;
                case CID_DTS:
                    theDataReader = new IO.DTS(path, theFormat);
                    break;
                case CID_IT:
                    theDataReader = new IO.IT(path, theFormat);
                    break;
                case CID_MIDI:
                    theDataReader = new IO.Midi(path, theFormat);
                    break;
                case CID_MOD:
                    theDataReader = new IO.MOD(path, theFormat);
                    break;
                case CID_APE:
                    theDataReader = new IO.APE(path, theFormat);
                    break;
                case CID_OFR:
                    theDataReader = new IO.OptimFrog(path, theFormat);
                    break;
                case CID_WAVPACK:
                    theDataReader = new IO.WAVPack(path, theFormat);
                    break;
                case CID_WAV:
                    theDataReader = new IO.WAV(path, theFormat);
                    break;
                case CID_PSF:
                    theDataReader = new IO.PSF(path, theFormat);
                    break;
                case CID_SPC:
                    theDataReader = new IO.SPC(path, theFormat);
                    break;
                case CID_TAK:
                    theDataReader = new IO.TAK(path, theFormat);
                    break;
                case CID_S3M:
                    theDataReader = new IO.S3M(path, theFormat);
                    break;
                case CID_XM:
                    theDataReader = new IO.XM(path, theFormat);
                    break;
                case CID_TTA:
                    theDataReader = new IO.TTA(path, theFormat);
                    break;
                case CID_VQF:
                    theDataReader = new IO.TwinVQ(path, theFormat);
                    break;
                case CID_AIFF:
                    theDataReader = new IO.AIFF(path, theFormat);
                    break;
                case CID_VGM:
                    theDataReader = new IO.VGM(path, theFormat);
                    break;
                case CID_GYM:
                    theDataReader = new IO.GYM(path, theFormat);
                    break;
                case CID_AA:
                    theDataReader = new IO.AA(path, theFormat);
                    break;
                case CID_CAF:
                    theDataReader = new IO.CAF(path, theFormat);
                    break;
                default:
                    theDataReader = new IO.DummyReader(path);
                    break;
            }

            return theDataReader;
        }

        /// <summary>
        /// Get the proper IAudioDataIO to exploit the data of the given Mime-type,
        /// or a dummy object if no proper IAudioDataIO has been found
        /// </summary>
        /// <param name="mimeType">Mime-type of the data to exploit</param>
        /// <param name="path">Path of the file to exploit</param>
        /// <param name="alternate">Index of the alternate format to use (for internal use only)</param>
        /// <returns>Appropriate IAudioDataIO to exploit the data of the given Mime-type, or dummy object if no proper IAudioDataIO has been found</returns>
        public IAudioDataIO GetFromMimeType(string mimeType, string path, int alternate = 0)
        {
            IList<Format> formats;
            if (mimeType.StartsWith(".")) formats = getFormatsFromPath(mimeType);
            else formats = getFormatsFromMimeType(mimeType);

            Format theFormat;
            if (formats != null && formats.Count > alternate)
                theFormat = formats[alternate];
            else
                theFormat = UNKNOWN_FORMAT;

            int formatId = theFormat.ID;

            IAudioDataIO theDataReader;

            switch (formatId)
            {
                case CID_MP3:
                    theDataReader = new IO.MPEGaudio(path, theFormat);
                    break;
                case CID_AAC:
                    theDataReader = new IO.AAC(path, theFormat);
                    break;
                case CID_MP4:
                    theDataReader = new IO.MP4(path, theFormat);
                    break;
                case CID_WMA:
                    theDataReader = new IO.WMA(path, theFormat);
                    break;
                case CID_OGG:
                    theDataReader = new IO.Ogg(path, theFormat);
                    break;
                case CID_FLAC:
                    theDataReader = new IO.FLAC(path, theFormat);
                    break;
                case CID_MPC:
                    theDataReader = new IO.MPEGplus(path, theFormat);
                    break;
                case CID_AC3:
                    theDataReader = new IO.AC3(path, theFormat);
                    break;
                case CID_DSF:
                    theDataReader = new IO.DSF(path, theFormat);
                    break;
                case CID_DTS:
                    theDataReader = new IO.DTS(path, theFormat);
                    break;
                case CID_IT:
                    theDataReader = new IO.IT(path, theFormat);
                    break;
                case CID_MIDI:
                    theDataReader = new IO.Midi(path, theFormat);
                    break;
                case CID_MOD:
                    theDataReader = new IO.MOD(path, theFormat);
                    break;
                case CID_APE:
                    theDataReader = new IO.APE(path, theFormat);
                    break;
                case CID_OFR:
                    theDataReader = new IO.OptimFrog(path, theFormat);
                    break;
                case CID_WAVPACK:
                    theDataReader = new IO.WAVPack(path, theFormat);
                    break;
                case CID_WAV:
                    theDataReader = new IO.WAV(path, theFormat);
                    break;
                case CID_PSF:
                    theDataReader = new IO.PSF(path, theFormat);
                    break;
                case CID_SPC:
                    theDataReader = new IO.SPC(path, theFormat);
                    break;
                case CID_TAK:
                    theDataReader = new IO.TAK(path, theFormat);
                    break;
                case CID_S3M:
                    theDataReader = new IO.S3M(path, theFormat);
                    break;
                case CID_XM:
                    theDataReader = new IO.XM(path, theFormat);
                    break;
                case CID_TTA:
                    theDataReader = new IO.TTA(path, theFormat);
                    break;
                case CID_VQF:
                    theDataReader = new IO.TwinVQ(path, theFormat);
                    break;
                case CID_AIFF:
                    theDataReader = new IO.AIFF(path, theFormat);
                    break;
                case CID_VGM:
                    theDataReader = new IO.VGM(path, theFormat);
                    break;
                case CID_GYM:
                    theDataReader = new IO.GYM(path, theFormat);
                    break;
                case CID_AA:
                    theDataReader = new IO.AA(path, theFormat);
                    break;
                case CID_CAF:
                    theDataReader = new IO.CAF(path, theFormat);
                    break;
                default:
                    theDataReader = new IO.DummyReader(path);
                    break;
            }

            return theDataReader;
        }
    }
}
