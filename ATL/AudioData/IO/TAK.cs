using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Tom's lossless Audio Kompressor files manipulation (extension : .TAK)
    /// </summary>
	class TAK : IAudioDataIO
    {
        public static readonly byte[] TAK_ID = Utils.Latin1Encoding.GetBytes("tBaK");

        // Private declarations 
        private uint bits;
        private uint sampleRate;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate => (int)sampleRate;
        public bool IsVBR => false;
        public AudioFormat AudioFormat { get; }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;
        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => bits > 0 ? (int)bits : -1;
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.APE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;

            bits = 0;
            sampleRate = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public TAK(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, TAK_ID);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            bool doLoop = true;

            byte[] buffer = new byte[4];

            resetData();
            source.Seek(sizeNfo.ID3v2Size, SeekOrigin.Begin);

            if (source.Read(buffer, 0, 4) < 4) return false;
            if (IsValidHeader(buffer))
            {
                result = true;
                AudioDataOffset = source.Position - 4;
                AudioDataSize = sizeNfo.FileSize - sizeNfo.APESize - sizeNfo.ID3v1Size - AudioDataOffset;

                do // Loop metadata
                {
                    if (source.Read(buffer, 0, 4) < 4) return false;
                    var readData32 = StreamUtils.DecodeUInt32(buffer);

                    var metaType = readData32 & 0x7F;
                    var metaSize = readData32 >> 8;

                    var position = source.Position;

                    if (0 == metaType) doLoop = false; // End of metadata
                    else if (0x01 == metaType) // Stream info
                    {
                        if (source.Read(buffer, 0, 2) < 2) return false;
                        var readData16 = StreamUtils.DecodeUInt16(buffer);
                        if (source.Read(buffer, 0, 4) < 4) return false;
                        readData32 = StreamUtils.DecodeUInt32(buffer);
                        if (source.Read(buffer, 0, 4) < 4) return false;
                        uint restOfData = StreamUtils.DecodeUInt32(buffer);

                        var sampleCount = (readData16 >> 14) + (readData32 << 2) + ((restOfData & 0x00000080) << 34);

                        sampleRate = ((restOfData >> 4) & 0x03ffff) + 6000; // bits 5 to 22
                        bits = ((restOfData >> 22) & 0x1f) + 8; // bits 23 to 27
                        ChannelsArrangement = GuessFromChannelNumber((int)((restOfData >> 27) & 0x0f) + 1); // bits 28 to 31

                        if (sampleCount > 0)
                        {
                            Duration = (double)sampleCount * 1000.0 / sampleRate;
                            BitRate = Math.Round(((double)(sizeNfo.FileSize - source.Position)) * 8 / Duration); //time to calculate average bitrate
                        }
                    }
                    else if (0x04 == metaType) // Encoder info
                    {
                        if (source.Read(buffer, 0, 4) < 4) return false;
                        readData32 = StreamUtils.DecodeUInt32(buffer);
                    }

                    source.Seek(position + metaSize, SeekOrigin.Begin);
                } while (doLoop); // End of metadata loop
            }

            return result;
        }
    }
}