using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OptimFROG files manipulation (extensions : .OFR, .OFS)
    /// </summary>
	class OptimFrog : IAudioDataIO
    {
        private static readonly byte[] OFR_SIGNATURE = Utils.Latin1Encoding.GetBytes("OFR ");

#pragma warning disable S1144 // Unused private types or members should be removed
        private static readonly string[] OFR_COMPRESSION = new string[10]
        {
            "fast", "normal", "high", "extra",
            "best", "ultra", "insane", "highnew", "extranew", "bestnew"
        };

        private static readonly sbyte[] OFR_BITS = new sbyte[11]
        {
            8, 8, 16, 16, 24, 24, 32, 32,
            -32, -32, -32 //negative value corresponds to floating point type.
        };
#pragma warning restore S1144 // Unused private types or members should be removed


        // Real structure of OptimFROG header
        public class TOfrHeader
        {
            public byte[] ID = new byte[4];                      // Always 'OFR '
            public uint Size;
            public uint Length;
            public ushort HiLength;
            public byte SampleType;
            public byte ChannelMode;
            public int SampleRate;
            public ushort EncoderID;
            public byte CompressionID;
            public void Reset()
            {
                Array.Clear(ID, 0, ID.Length);
                Size = 0;
                Length = 0;
                HiLength = 0;
                SampleType = 0;
                ChannelMode = 0;
                SampleRate = 0;
                EncoderID = 0;
                CompressionID = 0;
            }
        }


        private readonly TOfrHeader header = new TOfrHeader();

        private SizeInfo sizeInfo;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        /// <inheritdoc/>
        public int SampleRate => getSampleRate();

        /// <inheritdoc/>
        public bool IsVBR => false;

        /// <inheritdoc/>
        public AudioFormat AudioFormat { get; }

        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;

        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public double BitRate { get; private set; }

        /// <inheritdoc/>
        public int BitDepth => getBits();

        /// <inheritdoc/>
        public double Duration { get; private set; }

        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement => GuessFromChannelNumber(header.ChannelMode + 1);

        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }

        /// <inheritdoc/>
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            header.Reset();
        }

        public OptimFrog(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        // Get number of samples
        private long getSamples()
        {
            return (header.Length >> header.ChannelMode) * 0x00000001 +
                   (header.HiLength >> header.ChannelMode) * 0x00010000;
        }

        // Get song duration
        private double getDuration()
        {
            if (header.SampleRate > 0) return getSamples() * 1000.0 / header.SampleRate;
            return 0;
        }

        private int getSampleRate()
        {
            return header.SampleRate;
        }

        private double getBitrate()
        {
            return (sizeInfo.FileSize - header.Size - sizeInfo.TotalTagSize) * 8 / Duration;
        }

        private sbyte getBits()
        {
            return OFR_BITS[header.SampleType];
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, OFR_SIGNATURE);
        }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            this.sizeInfo = sizeNfo;
            resetData();

            // Read header data
            source.Seek(sizeNfo.ID3v2Size, SeekOrigin.Begin);

            long initialPos = source.Position;
            byte[] buffer = new byte[4];

            if (source.Read(header.ID, 0, 4) < 4) return false;
            if (source.Read(buffer, 0, 4) < 4) return false;
            header.Size = StreamUtils.DecodeUInt32(buffer);
            if (source.Read(buffer, 0, 4) < 4) return false;
            header.Length = StreamUtils.DecodeUInt32(buffer);
            if (source.Read(buffer, 0, 2) < 2) return false;
            header.HiLength = StreamUtils.DecodeUInt16(buffer);
            if (source.Read(buffer, 0, 2) < 2) return false;
            header.SampleType = buffer[0];
            header.ChannelMode = buffer[1];
            if (source.Read(buffer, 0, 4) < 4) return false;
            header.SampleRate = StreamUtils.DecodeInt32(buffer);
            if (source.Read(buffer, 0, 2) < 2) return false;
            header.EncoderID = StreamUtils.DecodeUInt16(buffer);
            if (source.Read(buffer, 0, 1) < 1) return false;
            header.CompressionID = buffer[0];

            if (IsValidHeader(header.ID))
            {
                result = true;
                AudioDataOffset = initialPos;
                AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

                Duration = getDuration();
                BitRate = getBitrate();
            }

            return result;
        }
    }
}