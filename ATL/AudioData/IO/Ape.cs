using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Monkey's Audio file manipulation (extension : .APE)
    /// </summary>
	class APE : IAudioDataIO
    {
        // Compression level codes
        // ReSharper disable UnusedMember.Global
        public const int MONKEY_COMPRESSION_FAST = 1000;  // Fast (poor)
        public const int MONKEY_COMPRESSION_NORMAL = 2000;  // Normal (good)
        public const int MONKEY_COMPRESSION_HIGH = 3000;  // High (very good)	
        public const int MONKEY_COMPRESSION_EXTRA_HIGH = 4000;  // Extra high (best)
        public const int MONKEY_COMPRESSION_INSANE = 5000;  // Insane
        public const int MONKEY_COMPRESSION_BRAINDEAD = 6000;  // BrainDead

        // Compression level names
        public static readonly string[] MONKEY_COMPRESSION = { "Unknown", "Fast", "Normal", "High", "Extra High", "Insane", "BrainDead" };

        // Format flags, only for Monkey's Audio <= 3.97
        public const byte MONKEY_FLAG_8_BIT = 1;  // Audio 8-bit
        public const byte MONKEY_FLAG_CRC = 2;  // New CRC32 error detection
        public const byte MONKEY_FLAG_PEAK_LEVEL = 4;  // Peak level stored
        public const byte MONKEY_FLAG_24_BIT = 8;  // Audio 24-bit
        public const byte MONKEY_FLAG_SEEK_ELEMENTS = 16; // Number of seek elements stored
        public const byte MONKEY_FLAG_WAV_NOT_STORED = 32; // WAV header not stored

        // Channel mode names
        public static readonly string[] MONKEY_MODE = { "Unknown", "Mono", "Stereo" };

        private static readonly byte[] FILE_HEADER = Utils.Latin1Encoding.GetBytes("MAC ");
        // ReSharper restore UnusedMember.Global


        readonly ApeHeader header = new ApeHeader();             // common header

        // Stuff loaded from the header:

        // FormatFlags, only used with Monkey's <= 3.97

        private SizeInfo sizeInfo;


        // Real structure of Monkey's Audio header
        // common header for all versions
        private sealed class ApeHeader
        {
            public byte[] cID = new byte[4]; // should equal 'MAC '
            public ushort nVersion;          // version number * 1000 (3.81 = 3810)
        }

#pragma warning disable S4487 // Unread "private" fields should be removed
        // old header for <= 3.97
        private struct ApeHeaderOld
        {
            public ushort nCompressionLevel; // the compression level
            public ushort nFormatFlags;      // any format flags (for future use)
            public ushort nChannels;         // the number of channels (1 or 2)
            public uint nSampleRate;         // the sample rate (typically 44100)
            public uint nHeaderBytes;        // the bytes after the MAC header that compose the WAV header
            public uint nTerminatingBytes;   // the bytes after that raw data (for extended info)
            public uint nTotalFrames;        // the number of frames in the file
            public uint nFinalFrameBlocks;   // the number of samples in the final frame
            public int nInt;
        }
        // new header for >= 3.98
        private struct ApeHeaderNew
        {
            public ushort nCompressionLevel;  // the compression level (see defines I.E. COMPRESSION_LEVEL_FAST)
            public ushort nFormatFlags;     // any format flags (for future use) Note: NOT the same flags as the old header!
            public uint nBlocksPerFrame;        // the number of audio blocks in one frame
            public uint nFinalFrameBlocks;  // the number of audio blocks in the final frame
            public uint nTotalFrames;           // the total number of frames
            public ushort nBitsPerSample;       // the bits per sample (typically 16)
            public ushort nChannels;            // the number of channels (1 or 2)
            public uint nSampleRate;            // the sample rate (typically 44100)
        }
        // data descriptor for >= 3.98
        private sealed class ApeDescriptor
        {
            public ushort padded;                   // padding/reserved (always empty)
            public uint nDescriptorBytes;           // the number of descriptor bytes (allows later expansion of this header)
            public uint nHeaderBytes;               // the number of header APE_HEADER bytes
            public uint nSeekTableBytes;            // the number of bytes of the seek table
            public uint nHeaderDataBytes;           // the number of header data bytes (from original file)
            public uint nAPEFrameDataBytes;         // the number of bytes of APE frame data
            public uint nAPEFrameDataBytesHigh;     // the high order number of APE frame data bytes
            public uint nTerminatingDataBytes;      // the terminating data of the file (not including tag data)
            public byte[] cFileMD5 = new byte[16];  // the MD5 hash of the file (see notes for usage... it's a littly tricky)
        }
#pragma warning restore S4487 // Unread "private" fields should be removed


        public int Version { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public uint PeakLevel { get; private set; }

        public double PeakLevelRatio { get; private set; }

        public long TotalSamples { get; private set; }

        public int CompressionMode { get; private set; }

        public string CompressionModeStr { get; private set; }

        // FormatFlags, only used with Monkey's <= 3.97
        public int FormatFlags { get; private set; }

        public bool HasPeakLevel { get; private set; }

        public bool HasSeekElements { get; private set; }

        public bool WavNotStored { get; private set; }

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES
        public int SampleRate { get; private set; }

        public bool IsVBR => false;

        public AudioFormat AudioFormat { get; }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;

        public string FileName { get; }

        public double BitRate { get; private set; }

        public double Duration { get; private set; }

        public int BitDepth { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Reset data
            Version = 0;
            SampleRate = 0;
            BitDepth = 0;
            PeakLevel = 0;
            PeakLevelRatio = 0.0;
            TotalSamples = 0;
            CompressionMode = 0;
            CompressionModeStr = "";
            FormatFlags = 0;
            HasPeakLevel = false;
            HasSeekElements = false;
            WavNotStored = false;
            BitRate = 0;
            Duration = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public APE(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private void readCommonHeader(BufferedBinaryReader source)
        {
            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            header.cID = source.ReadBytes(4);
            header.nVersion = source.ReadUInt16();
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, FILE_HEADER);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            ApeHeaderOld APE_OLD = new ApeHeaderOld();  // old header   <= 3.97
            ApeHeaderNew APE_NEW = new ApeHeaderNew();  // new header   >= 3.98
            ApeDescriptor APE_DESC = new ApeDescriptor(); // extra header >= 3.98

            int BlocksPerFrame;
            bool LoadSuccess;
            bool result = false;

            this.sizeInfo = sizeNfo;
            resetData();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            // Read data from file
            readCommonHeader(reader);

            if (IsValidHeader(header.cID))
            {
                Version = header.nVersion;
                AudioDataOffset = reader.Position - 6;
                AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

                // Load New Monkey's Audio Header for version >= 3.98
                if (header.nVersion >= 3980)
                {
                    APE_DESC.padded = 0;
                    APE_DESC.nDescriptorBytes = 0;
                    APE_DESC.nHeaderBytes = 0;
                    APE_DESC.nSeekTableBytes = 0;
                    APE_DESC.nHeaderDataBytes = 0;
                    APE_DESC.nAPEFrameDataBytes = 0;
                    APE_DESC.nAPEFrameDataBytesHigh = 0;
                    APE_DESC.nTerminatingDataBytes = 0;
                    Array.Clear(APE_DESC.cFileMD5, 0, APE_DESC.cFileMD5.Length);

                    APE_DESC.padded = reader.ReadUInt16();
                    APE_DESC.nDescriptorBytes = reader.ReadUInt32();
                    APE_DESC.nHeaderBytes = reader.ReadUInt32();
                    APE_DESC.nSeekTableBytes = reader.ReadUInt32();
                    APE_DESC.nHeaderDataBytes = reader.ReadUInt32();
                    APE_DESC.nAPEFrameDataBytes = reader.ReadUInt32();
                    APE_DESC.nAPEFrameDataBytesHigh = reader.ReadUInt32();
                    APE_DESC.nTerminatingDataBytes = reader.ReadUInt32();
                    APE_DESC.cFileMD5 = reader.ReadBytes(16);

                    // seek past description header
                    if (APE_DESC.nDescriptorBytes != 52) reader.Seek(APE_DESC.nDescriptorBytes - 52, SeekOrigin.Current);
                    // load new ape_header
                    if (APE_DESC.nHeaderBytes > 24/*sizeof(APE_NEW)*/) APE_DESC.nHeaderBytes = 24/*sizeof(APE_NEW)*/;

                    APE_NEW.nCompressionLevel = 0;
                    APE_NEW.nFormatFlags = 0;
                    APE_NEW.nBlocksPerFrame = 0;
                    APE_NEW.nFinalFrameBlocks = 0;
                    APE_NEW.nTotalFrames = 0;
                    APE_NEW.nBitsPerSample = 0;
                    APE_NEW.nChannels = 0;
                    APE_NEW.nSampleRate = 0;

                    APE_NEW.nCompressionLevel = reader.ReadUInt16();
                    APE_NEW.nFormatFlags = reader.ReadUInt16();
                    APE_NEW.nBlocksPerFrame = reader.ReadUInt32();
                    APE_NEW.nFinalFrameBlocks = reader.ReadUInt32();
                    APE_NEW.nTotalFrames = reader.ReadUInt32();
                    APE_NEW.nBitsPerSample = reader.ReadUInt16();
                    APE_NEW.nChannels = reader.ReadUInt16();
                    APE_NEW.nSampleRate = reader.ReadUInt32();

                    // based on MAC SDK 3.98a1 (APEinfo.h)
                    SampleRate = (int)APE_NEW.nSampleRate;
                    ChannelsArrangement = GuessFromChannelNumber(APE_NEW.nChannels);
                    FormatFlags = APE_NEW.nFormatFlags;
                    BitDepth = APE_NEW.nBitsPerSample;
                    CompressionMode = APE_NEW.nCompressionLevel;
                    // calculate total uncompressed samples
                    if (APE_NEW.nTotalFrames > 0)
                    {
                        TotalSamples = (long)(APE_NEW.nBlocksPerFrame) *
                            (long)(APE_NEW.nTotalFrames - 1) +
                            (long)(APE_NEW.nFinalFrameBlocks);
                    }
                    LoadSuccess = true;
                }
                else
                {
                    // Old Monkey <= 3.97               

                    APE_OLD.nCompressionLevel = 0;
                    APE_OLD.nFormatFlags = 0;
                    APE_OLD.nChannels = 0;
                    APE_OLD.nSampleRate = 0;
                    APE_OLD.nHeaderBytes = 0;
                    APE_OLD.nTerminatingBytes = 0;
                    APE_OLD.nTotalFrames = 0;
                    APE_OLD.nFinalFrameBlocks = 0;
                    APE_OLD.nInt = 0;

                    APE_OLD.nCompressionLevel = reader.ReadUInt16();
                    APE_OLD.nFormatFlags = reader.ReadUInt16();
                    APE_OLD.nChannels = reader.ReadUInt16();
                    APE_OLD.nSampleRate = reader.ReadUInt32();
                    APE_OLD.nHeaderBytes = reader.ReadUInt32();
                    APE_OLD.nTerminatingBytes = reader.ReadUInt32();
                    APE_OLD.nTotalFrames = reader.ReadUInt32();
                    APE_OLD.nFinalFrameBlocks = reader.ReadUInt32();
                    APE_OLD.nInt = reader.ReadInt32();

                    CompressionMode = APE_OLD.nCompressionLevel;
                    SampleRate = (int)APE_OLD.nSampleRate;
                    ChannelsArrangement = ChannelsArrangements.GuessFromChannelNumber(APE_OLD.nChannels);
                    FormatFlags = APE_OLD.nFormatFlags;
                    BitDepth = 16;
                    if ((APE_OLD.nFormatFlags & MONKEY_FLAG_8_BIT) != 0) BitDepth = 8;
                    if ((APE_OLD.nFormatFlags & MONKEY_FLAG_24_BIT) != 0) BitDepth = 24;

                    HasSeekElements = (APE_OLD.nFormatFlags & MONKEY_FLAG_PEAK_LEVEL) != 0;
                    WavNotStored = (APE_OLD.nFormatFlags & MONKEY_FLAG_SEEK_ELEMENTS) != 0;
                    HasPeakLevel = (APE_OLD.nFormatFlags & MONKEY_FLAG_WAV_NOT_STORED) != 0;

                    if (HasPeakLevel)
                    {
                        PeakLevel = (uint)APE_OLD.nInt;
                        PeakLevelRatio = PeakLevel / (1 << BitDepth) / 2.0 * 100.0;
                    }

                    // based on MAC_SDK_397 (APEinfo.cpp)
                    if (Version >= 3950)
                        BlocksPerFrame = 73728 * 4;
                    else if (Version >= 3900 || (Version >= 3800 && MONKEY_COMPRESSION_EXTRA_HIGH == APE_OLD.nCompressionLevel))
                        BlocksPerFrame = 73728;
                    else
                        BlocksPerFrame = 9216;

                    // calculate total uncompressed samples
                    if (APE_OLD.nTotalFrames > 0)
                        TotalSamples = (APE_OLD.nTotalFrames - 1) * BlocksPerFrame + APE_OLD.nFinalFrameBlocks;

                    LoadSuccess = true;
                }
                if (LoadSuccess)
                {
                    // compression profile name
                    if (0 == CompressionMode % 1000 && CompressionMode <= 6000)
                    {
                        CompressionModeStr = MONKEY_COMPRESSION[CompressionMode / 1000]; // int division
                    }
                    else
                    {
                        CompressionModeStr = CompressionMode.ToString();
                    }
                    // length
                    if (SampleRate > 0) Duration = TotalSamples * 1000.0 / SampleRate;
                    // average bitrate
                    if (Duration > 0) BitRate = 8 * (sizeNfo.FileSize - sizeNfo.TotalTagSize) / Duration;
                    // some extra sanity checks
                    result = BitDepth > 0 && SampleRate > 0 && TotalSamples > 0 && ChannelsArrangement.NbChannels > 0;
                }
            }

            return result;
        }

    }
}