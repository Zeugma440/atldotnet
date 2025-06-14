using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Threading.Tasks;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for TwinVQ files manipulation (extension : .VQF)
    /// </summary>
	partial class TwinVQ : MetaDataIO, IAudioDataIO
    {
        // Twin VQ header ID
        private static readonly byte[] TWIN_ID = Utils.Latin1Encoding.GetBytes("TWIN");

        // Mapping between TwinVQ frame codes and ATL frame codes
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { "NAME", Field.TITLE },
            { "ALBM", Field.ALBUM },
            { "AUTH", Field.ARTIST },
            { "(c) ", Field.COPYRIGHT },
            { "MUSC", Field.COMPOSER },
            { "CDCT", Field.CONDUCTOR },
            { "TRCK", Field.TRACK_NUMBER }, // Unofficial; found in sample files
            { "DATE", Field.RECORDING_YEAR }, // Unofficial; found in sample files
            { "GENR", Field.GENRE }, // Unofficial; found in sample files
            { "COMT", Field.COMMENT }
            // TODO - handle integer extension sub-chunks : YEAR, TRAC
        };


        // Private declarations
        private bool isValid;

        private SizeInfo sizeInfo;


        public bool Corrupted => isCorrupted(); // True if file corrupted

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }


        // TwinVQ chunk header
        private sealed class ChunkHeader
        {
            public string ID;
            public uint Size;                                            // Chunk size
        }

#pragma warning disable S4487 // Unread "private" fields should be removed
        // File header data - for internal use
        private sealed class HeaderInfo
        {
            // Real structure of TwinVQ file header
            public byte[] ID = new byte[4];                           // Always "TWIN"
            public char[] Version = new char[8];                         // Version ID
            public uint Size;                                           // Header size
            public readonly ChunkHeader Common = new ChunkHeader();      // Common chunk header
            public uint ChannelMode;             // Channel mode: 0 - mono, 1 - stereo
            public uint BitRate;                                     // Total bit rate
            public uint SampleRate;                               // Sample rate (khz)
            public uint SecurityLevel;                                     // Always 0
        }
#pragma warning restore S4487 // Unread "private" fields should be removed


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate { get; private set; }

        public bool IsVBR => false;

        public AudioFormat AudioFormat { get; }

        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;

        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => -1; // Irrelevant for lossy formats
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }
        public override byte FieldCodeFixedLength => 4;

        protected override bool isLittleEndian => false;


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;
            isValid = false;
            SampleRate = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public TwinVQ(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private static bool readHeader(BufferedBinaryReader source, ref HeaderInfo Header)
        {
            // Read header and get file size
            Header.ID = source.ReadBytes(4);
            Header.Version = Utils.Latin1Encoding.GetString(source.ReadBytes(8)).ToCharArray();
            Header.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.Common.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
            Header.Common.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.ChannelMode = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.BitRate = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.SampleRate = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            Header.SecurityLevel = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            return true;
        }

        private static ChannelsArrangement getChannelArrangement(HeaderInfo Header)
        {
            return Header.ChannelMode switch
            {
                0 => MONO,
                1 => STEREO,
                _ => new ChannelsArrangement((int)Header.ChannelMode)
            };
        }

        private static uint getBitRate(HeaderInfo Header)
        {
            return Header.BitRate;
        }

        private static int GetSampleRate(HeaderInfo Header)
        {
            int result = (int)Header.SampleRate;
            result = result switch
            {
                11 => 11025,
                22 => 22050,
                44 => 44100,
                _ => (result * 1000)
            };
            return result;
        }

        // Get duration from header
        private double getDuration(HeaderInfo Header)
        {
            return Math.Abs(sizeInfo.FileSize - Header.Size - 20) * 1000.0 / 125.0 / Header.BitRate;
        }

        private static bool headerEndReached(ChunkHeader Chunk)
        {
            // Check for header end
            return (byte)Chunk.ID[0] < 32 ||
                (byte)Chunk.ID[1] < 32 ||
                (byte)Chunk.ID[2] < 32 ||
                (byte)Chunk.ID[3] < 32 ||
                "DSIZ".Equals(Chunk.ID);
        }

        private void readTag(BufferedBinaryReader source, HeaderInfo Header, ReadTagParams readTagParams)
        {
            ChunkHeader chunk = new ChunkHeader();
            bool first = true;
            long tagStart = -1;

            source.Seek(40, SeekOrigin.Begin);
            do
            {
                // Read chunk header (length : 8 bytes)
                chunk.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                chunk.Size = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                // Read chunk data and set tag item if chunk header valid
                if (headerEndReached(chunk)) break;

                if (first)
                {
                    tagStart = source.Position - 8;
                    first = false;
                }
                //                tagExists = true; // If something else than mandatory info is stored, we can consider metadata is present
                var data = Encoding.UTF8.GetString(source.ReadBytes((int)chunk.Size)).Trim();

                SetMetaField(chunk.ID, data, readTagParams.ReadAllMetaFrames);
            }
            while (source.Position < source.Length);

            if (readTagParams.PrepareForWriting)
            {
                // Metadata zone goes from the first field after COMM to the last field before DSIZ
                if (-1 == tagStart) structureHelper.AddZone(source.Position - 8, 0);
                else structureHelper.AddZone(tagStart, (int)(source.Position - tagStart - 8));
                structureHelper.AddSize(12, Header.Size);
            }
        }

        private bool isCorrupted()
        {
            // Check for file corruption
            return isValid &&
                (0 == ChannelsArrangement.NbChannels ||
                BitRate < 8000 || BitRate > 192000 ||
                SampleRate < 8000 || SampleRate > 44100 ||
                Duration < 0.1 || Duration > 10000);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, TWIN_ID);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            HeaderInfo Header = new HeaderInfo();

            resetData();
            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            reader.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            bool result = readHeader(reader, ref Header);
            // Process data if loaded and header valid
            if (result && IsValidHeader(Header.ID))
            {
                isValid = true;
                // Fill properties with header data
                ChannelsArrangement = getChannelArrangement(Header);
                BitRate = getBitRate(Header);
                SampleRate = GetSampleRate(Header);
                Duration = getDuration(Header);
                // Get tag information and fill properties
                readTag(reader, Header, readTagParams);

                AudioDataOffset = reader.Position;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;
            }
            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            string recordingYear = "";

            IDictionary<Field, string> map = tag.ToMap();

            // 1st pass to gather date information
            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length > 0 && Field.RECORDING_YEAR == frameType) // No frame with empty value
                {
                    recordingYear = map[frameType];
                }
            }
            if (recordingYear.Length > 0)
            {
                string recordingDate = Utils.ProtectValue(tag[Field.RECORDING_DATE]);
                if (0 == recordingDate.Length || !recordingDate.StartsWith(recordingYear)) map[Field.RECORDING_DATE] = recordingYear;
            }

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                foreach (string str in frameMapping.Keys)
                {
                    if (frameType == frameMapping[str])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);
                            writeTextFrame(s, str, value);
                            writtenFieldCodes.Add(str.ToUpper());
                            result++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (fieldInfo.NativeFieldCode.Length > 0
                    && !writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper())
                    )
                {
                    writeTextFrame(s, fieldInfo.NativeFieldCode, FormatBeforeWriting(fieldInfo.Value));
                    result++;
                }
            }

            return result;
        }

        private static void writeTextFrame(Stream s, string frameCode, string text)
        {
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(frameCode));
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            StreamUtils.WriteBytes(s, StreamUtils.EncodeBEUInt32((uint)textBytes.Length));
            StreamUtils.WriteBytes(s, textBytes);
        }

        // Specific implementation for conservation of fields that are required for playback
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public override async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            TagData tag = prepareRemove();
            return await WriteAsync(s, tag, args);
        }

        private TagData prepareRemove()
        {
            TagData result = new TagData();
            foreach (Field b in frameMapping.Values)
            {
                result.IntegrateValue(b, "");
            }

            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                var fieldCode = fieldInfo.NativeFieldCode.ToLower();
                if (!fieldCode.StartsWith('_') && !fieldCode.Equals("DSIZ") && !fieldCode.Equals("COMM"))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    result.AdditionalFields.Add(emptyFieldInfo);
                }
            }
            return result;
        }

    }
}