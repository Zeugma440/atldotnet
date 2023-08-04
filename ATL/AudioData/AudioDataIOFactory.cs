using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

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
                    theFactory = new AudioDataIOFactory
                    {
                        formatListByExt = new Dictionary<string, IList<Format>>(),
                        formatListByMime = new Dictionary<string, IList<Format>>()
                    };

                    Format tempFmt = new Format(CID_MP3, "MPEG Audio", "MPEG");
                    tempFmt.AddMimeType("audio/mp3");
                    tempFmt.AddMimeType("audio/mpeg");
                    tempFmt.AddMimeType("audio/x-mpeg");
                    tempFmt.AddExtension(".mp1");
                    tempFmt.AddExtension(".mp2");
                    tempFmt.AddExtension(".mp3");
                    tempFmt.CheckHeader = MPEGaudio.IsValidFrameHeader;
                    tempFmt.SearchHeader = MPEGaudio.HasValidFrame;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_OGG, "OGG", "OGG");
                    tempFmt.AddMimeType("audio/ogg");
                    tempFmt.AddMimeType("audio/vorbis");
                    tempFmt.AddMimeType("audio/opus");
                    tempFmt.AddMimeType("audio/ogg;codecs=opus");
                    tempFmt.AddExtension(".ogg");
                    tempFmt.AddExtension(".oga");
                    tempFmt.AddExtension(".opus");
                    tempFmt.CheckHeader = Ogg.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MPC, "Musepack / MPEGplus", "MPC");
                    tempFmt.AddMimeType("audio/x-musepack");
                    tempFmt.AddMimeType("audio/musepack");
                    tempFmt.AddExtension(".mp+");
                    tempFmt.AddExtension(".mpc");
                    tempFmt.CheckHeader = MPEGplus.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WMA, "Windows Media Audio", "WMA");
                    tempFmt.AddMimeType("audio/x-ms-wma");
                    tempFmt.AddMimeType("video/x-ms-asf");
                    tempFmt.AddExtension(".asf");
                    tempFmt.AddExtension(".wma");
                    tempFmt.CheckHeader = WMA.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AAC, "Advanced Audio Coding");
                    tempFmt.AddMimeType("audio/aac");
                    tempFmt.AddExtension(".aac");
                    tempFmt.CheckHeader = AAC.IsValidHeader;
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
                    tempFmt.CheckHeader = MP4.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AC3, "Dolby Digital", "Dolby");
                    tempFmt.AddMimeType("audio/ac3");
                    tempFmt.AddExtension(".ac3");
                    tempFmt.CheckHeader = AC3.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_DTS, "Digital Theatre System", "DTS");
                    tempFmt.AddMimeType("audio/vnd.dts");
                    tempFmt.AddMimeType("audio/vnd.dts.hd");
                    tempFmt.AddExtension(".dts");
                    tempFmt.CheckHeader = DTS.IsValidHeader;
                    tempFmt.Readable = false;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_VQF, "TwinVQ");
                    tempFmt.AddExtension(".vqf");
                    tempFmt.AddMimeType("audio/x-twinvq");
                    tempFmt.CheckHeader = TwinVQ.IsValidHeader;
                    tempFmt.Readable = false;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_FLAC, "Free Lossless Audio Codec", "FLAC");
                    tempFmt.AddMimeType("audio/flac");
                    tempFmt.AddMimeType("audio/x-flac");
                    tempFmt.AddMimeType("audio/x-ogg");
                    tempFmt.AddExtension(".flac");
                    tempFmt.CheckHeader = FlacHelper.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_APE, "Monkey's Audio", "APE");
                    tempFmt.AddMimeType("audio/ape");
                    tempFmt.AddMimeType("audio/x-ape");
                    tempFmt.AddExtension(".ape");
                    tempFmt.CheckHeader = APE.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_OFR, "OptimFROG");
                    tempFmt.AddMimeType("audio/ofr");
                    tempFmt.AddMimeType("audio/x-ofr");
                    tempFmt.AddExtension(".ofr");
                    tempFmt.AddExtension(".ofs");
                    tempFmt.CheckHeader = OptimFrog.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WAVPACK, "WAVPack");
                    tempFmt.AddMimeType("audio/x-wavpack");
                    tempFmt.AddMimeType("audio/wavpack");
                    tempFmt.AddExtension(".wv");
                    tempFmt.CheckHeader = WAVPack.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_WAV, "PCM (uncompressed audio)", "WAV");
                    tempFmt.AddMimeType("audio/x-wav");
                    tempFmt.AddMimeType("audio/wav");
                    tempFmt.AddExtension(".wav");
                    tempFmt.AddExtension(".bwf");
                    tempFmt.AddExtension(".bwav");
                    tempFmt.CheckHeader = WAV.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_MIDI, "Musical Instruments Digital Interface", "MIDI");
                    tempFmt.AddMimeType("audio/mid");
                    tempFmt.AddExtension(".mid");
                    tempFmt.AddExtension(".midi");
                    tempFmt.CheckHeader = Midi.IsValidHeader;
                    tempFmt.SearchHeader = Midi.FindValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_DSF, "Direct Stream Digital", "DSD");
                    tempFmt.AddMimeType("audio/dsf");
                    tempFmt.AddMimeType("audio/x-dsf");
                    tempFmt.AddMimeType("audio/dsd");
                    tempFmt.AddMimeType("audio/x-dsd");
                    tempFmt.AddExtension(".dsf");
                    tempFmt.AddExtension(".dsd");
                    tempFmt.CheckHeader = DSF.IsValidHeader;
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
                    tempFmt.CheckHeader = PSF.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_SPC, "SPC700 Sound Files", "SPC");
                    tempFmt.AddMimeType("audio/spc");   // Unofficial
                    tempFmt.AddMimeType("audio/x-spc"); // Unofficial
                    tempFmt.AddExtension(".spc");
                    tempFmt.CheckHeader = SPC.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_TTA, "True Audio");
                    tempFmt.AddMimeType("audio/tta");
                    tempFmt.AddMimeType("audio/x-tta");
                    tempFmt.AddExtension(".tta");
                    tempFmt.CheckHeader = TTA.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_TAK, "Tom's lossless Audio Kompressor", "TAK");
                    tempFmt.AddMimeType("audio/tak");   // Unofficial
                    tempFmt.AddMimeType("audio/x-tak"); // Unofficial
                    tempFmt.AddExtension(".tak");
                    tempFmt.CheckHeader = TAK.IsValidHeader;
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
                    tempFmt.CheckHeader = XM.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_IT, "Impulse Tracker", "IT");
                    tempFmt.AddMimeType("audio/it");
                    tempFmt.AddExtension(".it");
                    tempFmt.CheckHeader = IT.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AIFF, "Audio Interchange File Format", "AIFF");
                    tempFmt.AddMimeType("audio/x-aiff");
                    tempFmt.AddExtension(".aif");
                    tempFmt.AddExtension(".aiff");
                    tempFmt.AddExtension(".aifc");
                    tempFmt.AddExtension(".snd");
                    tempFmt.CheckHeader = AIFF.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_VGM, "Video Game Music", "VGM");
                    tempFmt.AddMimeType("audio/vgm");   // Unofficial
                    tempFmt.AddMimeType("audio/x-vgm"); // Unofficial
                    tempFmt.AddExtension(".vgm");
                    tempFmt.AddExtension(".vgz");
                    tempFmt.CheckHeader = VGM.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_GYM, "Genesis YM2612", "GYM");
                    tempFmt.AddMimeType("audio/gym");   // Unofficial
                    tempFmt.AddMimeType("audio/x-gym"); // Unofficial
                    tempFmt.AddExtension(".gym");
                    tempFmt.CheckHeader = GYM.IsValidHeader;
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_AA, "Audible (legacy)", "AA");
                    tempFmt.AddMimeType("audio/audible");
                    tempFmt.AddMimeType("audio/x-pn-audibleaudio");
                    tempFmt.CheckHeader = AA.IsValidHeader;
                    tempFmt.AddExtension(".aa");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new Format(CID_CAF, "Apple Core Audio", "CAF");
                    tempFmt.AddMimeType("audio/x-caf");
                    tempFmt.AddExtension(".caf");
                    tempFmt.CheckHeader = CAF.IsValidHeader;
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
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Unrecognized file extension : " + path);
                theFormat = UNKNOWN_FORMAT;
            }

            return getFromFormat(path, theFormat);
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
            if (mimeType.StartsWith('.')) formats = getFormatsFromPath(mimeType);
            else formats = getFormatsFromMimeType(mimeType);

            Format theFormat;
            if (formats != null && formats.Count > alternate)
                theFormat = formats[alternate];
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Unrecognized MIME type : " + mimeType);
                theFormat = UNKNOWN_FORMAT;
            }

            return getFromFormat(path, theFormat);
        }

        /// <summary>
        /// Get the proper IAudioDataIO to exploit the data of the given Stream,
        /// or a dummy object if no proper IAudioDataIO has been found
        /// </summary>
        /// <param name="s">Stream to exploit</param>
        /// <returns>Appropriate IAudioDataIO to exploit the data of the given Stream, or dummy object if no proper IAudioDataIO has been found</returns>
        public IAudioDataIO GetFromStream(Stream s)
        {
            // TODO : memorize initial offset?
            s.Seek(0, SeekOrigin.Begin);
            byte[] data = new byte[32];
            long offset = 0;
            s.Read(data, 0, 32);
            // Hardcoded case of ID3v2 as it is the sole standard metadata system to appear at the beginning of file
            if (ID3v2.isValidHeader(data))
            {
                byte[] data2 = new byte[4];
                Array.Copy(data, 6, data2, 0, 4); // bytes 6-9 only
                int id3v2Size = StreamUtils.DecodeSynchSafeInt32(data2) + 10;  // 10 being the size of the header
                s.Seek(id3v2Size, SeekOrigin.Begin);
                offset = s.Position;
                s.Read(data, 0, 32);
            }
            try
            {
                List<Format> expensiveFormats = new List<Format>();
                foreach (Format f in getFormats())
                {
                    if (f.CheckHeader != null && f.CheckHeader(data)) return getFromFormat("in-memory", f);
                    if (f.SearchHeader != null) expensiveFormats.Add(f);
                }
                foreach (Format f in expensiveFormats)
                {
                    s.Seek(offset, SeekOrigin.Begin);
                    if (f.SearchHeader(s)) return getFromFormat("in-memory", f);
                }
                return getFromFormat("in-memory", UNKNOWN_FORMAT);
            }
            finally
            {
                s.Seek(0, SeekOrigin.Begin);
            }
        }

        private static IAudioDataIO getFromFormat(string path, Format theFormat)
        {
            switch (theFormat.ID)
            {
                case CID_MP3:
                    return new MPEGaudio(path, theFormat);
                case CID_AAC:
                    return new AAC(path, theFormat);
                case CID_MP4:
                    return new MP4(path, theFormat);
                case CID_WMA:
                    return new WMA(path, theFormat);
                case CID_OGG:
                    return new Ogg(path, theFormat);
                case CID_FLAC:
                    return new FLAC(path, theFormat);
                case CID_MPC:
                    return new MPEGplus(path, theFormat);
                case CID_AC3:
                    return new AC3(path, theFormat);
                case CID_DSF:
                    return new DSF(path, theFormat);
                case CID_DTS:
                    return new DTS(path, theFormat);
                case CID_IT:
                    return new IT(path, theFormat);
                case CID_MIDI:
                    return new Midi(path, theFormat);
                case CID_MOD:
                    return new MOD(path, theFormat);
                case CID_APE:
                    return new APE(path, theFormat);
                case CID_OFR:
                    return new OptimFrog(path, theFormat);
                case CID_WAVPACK:
                    return new WAVPack(path, theFormat);
                case CID_WAV:
                    return new WAV(path, theFormat);
                case CID_PSF:
                    return new PSF(path, theFormat);
                case CID_SPC:
                    return new SPC(path, theFormat);
                case CID_TAK:
                    return new TAK(path, theFormat);
                case CID_S3M:
                    return new S3M(path, theFormat);
                case CID_XM:
                    return new XM(path, theFormat);
                case CID_TTA:
                    return new TTA(path, theFormat);
                case CID_VQF:
                    return new TwinVQ(path, theFormat);
                case CID_AIFF:
                    return new AIFF(path, theFormat);
                case CID_VGM:
                    return new VGM(path, theFormat);
                case CID_GYM:
                    return new GYM(path, theFormat);
                case CID_AA:
                    return new AA(path, theFormat);
                case CID_CAF:
                    return new CAF(path, theFormat);
                default:
                    return new DummyReader(path);
            }
        }
    }
}
