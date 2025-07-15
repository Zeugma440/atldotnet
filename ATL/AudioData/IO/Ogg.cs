using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ATL.AudioData.FlacHelper;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OGG files manipulation. Current implementation covers :
    ///   - Vorbis data (extensions : .OGG, .OGA)
    ///   - Opus data (extensions : .OPUS)
    ///   - Speex data (extensions : .SPX)
    ///   - Embedded FLAC data (extensions : .OGG)
    ///   
    /// Implementation notes
    /// 
    ///   1. CRC's : Current implementation does not test OGG page header CRC's
    ///   2. Page numbers : Current implementation does not test page numbers consistency
    ///   3. When the file has multiple bitstreams, only those whose headers 
    ///   are positioned at the beginning of the file are detected
    /// 
    /// </summary>
	partial class Ogg : VorbisTagHolder, IMetaDataIO, IAudioDataIO
    {
        // Contents of the file
        private const int CONTENTS_VORBIS = 0;				// Vorbis
        private const int CONTENTS_OPUS = 1;                // Opus
        private const int CONTENTS_FLAC = 2;                // FLAC
        private const int CONTENTS_SPEEX = 3;               // Speex

        private const int MAX_PAGE_SIZE = 255 * 255;

        // Ogg page header ID
        private static readonly byte[] OGG_PAGE_ID = Utils.Latin1Encoding.GetBytes("OggS");

        // Vorbis identification packet (frame) ID
        private static readonly byte[] VORBIS_HEADER_ID = { 1, 0x76, 0x6F, 0x72, 0x62, 0x69, 0x73 }; // 1 + "vorbis"

        // Vorbis comment (tags) packet (frame) ID
        private static readonly byte[] VORBIS_COMMENT_ID = { 3, 0x76, 0x6F, 0x72, 0x62, 0x69, 0x73 }; // 3 + "vorbis"

        // Vorbis setup packet (frame) ID
        private static readonly byte[] VORBIS_SETUP_ID = { 5, 0x76, 0x6F, 0x72, 0x62, 0x69, 0x73 }; // 5 + "vorbis"


        // Theora identification packet (frame) ID
        private static readonly byte[] THEORA_HEADER_ID = { 0x80, 0x74, 0x68, 0x65, 0x6F, 0x72, 0x61 }; // 0x80 + "theora"


        // Opus parameter frame ID
        private static readonly byte[] OPUS_HEADER_ID = Utils.Latin1Encoding.GetBytes("OpusHead");

        // Opus tag frame ID
        private static readonly byte[] OPUS_TAG_ID = Utils.Latin1Encoding.GetBytes("OpusTags");


        // FLAC identification packet (frame) ID
        private static readonly byte[] FLAC_HEADER_ID = { 0x7F, 0x46, 0x4C, 0x41, 0x43 }; // 0x7f + "FLAC"


        // Speex identification packet (frame) ID
        private static readonly byte[] SPEEX_HEADER_ID = Utils.Latin1Encoding.GetBytes("Speex   ");


        private readonly AudioFormat audioFormat;

        private readonly FileInfo info = new();

        private int contents;

        private ushort bitRateNominal;
        private ulong samples;

        private AudioDataManager.SizeInfo sizeInfo;



        public int SampleRate { get; private set; }

        public bool Valid => isValid();
        public string FileName { get; }

        public double BitRate => getBitRate();
        public int BitDepth { get; private set; } // Only for embedded FLACs

        public double Duration => getDuration();
        public ChannelsArrangement ChannelsArrangement { get; private set; }
        public bool IsVBR => isVbr();

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }



        // Ogg page header
        private sealed class OggPageHeader
        {
            private byte[] ID;                                              // Always "OggS"
            private byte StreamVersion;                          // Stream structure version
            public byte TypeFlag;                                        // Header type flag
            public ulong AbsolutePosition;                      // Absolute granule position
            public int StreamId;                                     // Stream serial number
            public int PageNumber;                                   // Page sequence number
            private uint Checksum;                                             // Page CRC32
            public byte Segments;                                 // Number of page segments
            public byte[] LacingValues;                     // Lacing values - segment sizes
            public long Offset;                                             // Header offset

            private int forcedPageSize;                          // Page size (forced value)

            public OggPageHeader(int streamId = 0)
            {
                ID = OGG_PAGE_ID;
                StreamVersion = 0; // Constant
                TypeFlag = 0;
                AbsolutePosition = ulong.MaxValue;
                StreamId = streamId;
                PageNumber = 1;
                Checksum = 0;
                Offset = 0;
                forcedPageSize = -1;
            }

            public void ReadFromStream(Stream r)
            {
                Offset = r.Position;
                byte[] buffer = new byte[8];
                ID = new byte[4];
                if (r.Read(ID, 0, 4) < 4) return;
                if (r.Read(buffer, 0, 2) < 2) return;
                StreamVersion = buffer[0];
                TypeFlag = buffer[1];
                if (r.Read(buffer, 0, 8) < 8) return;
                AbsolutePosition = StreamUtils.DecodeUInt64(buffer);
                if (r.Read(buffer, 0, 4) < 4) return;
                StreamId = StreamUtils.DecodeInt32(buffer);
                if (r.Read(buffer, 0, 4) < 4) return;
                PageNumber = StreamUtils.DecodeInt32(buffer);
                if (r.Read(buffer, 0, 4) < 4) return;
                Checksum = StreamUtils.DecodeUInt32(buffer);
                if (r.Read(buffer, 0, 1) < 1) return;
                Segments = buffer[0];

                LacingValues = new byte[Segments];
                Segments = (byte)r.Read(LacingValues, 0, Segments);
                forcedPageSize = -1;
            }

            public static OggPageHeader ReadFromStream(BufferedBinaryReader r)
            {
                OggPageHeader result = new OggPageHeader
                {
                    Offset = r.Position,
                    ID = r.ReadBytes(4),
                    StreamVersion = r.ReadByte(),
                    TypeFlag = r.ReadByte(),
                    AbsolutePosition = r.ReadUInt64(),
                    StreamId = r.ReadInt32(),
                    PageNumber = r.ReadInt32(),
                    Checksum = r.ReadUInt32(),
                    Segments = r.ReadByte(),
                    forcedPageSize = -1
                };
                result.LacingValues = r.ReadBytes(result.Segments);
                return result;
            }

            public void WriteToStream(Stream w)
            {
                var buffer = new Span<byte>(new byte[8]);
                StreamUtils.WriteBytes(w, ID);
                w.WriteByte(StreamVersion);
                w.WriteByte(TypeFlag);
                StreamUtils.WriteUInt64(w, AbsolutePosition, buffer);
                StreamUtils.WriteInt32(w, StreamId, buffer);
                StreamUtils.WriteInt32(w, PageNumber, buffer);
                StreamUtils.WriteUInt32(w, Checksum, buffer);
                w.WriteByte(Segments);
                StreamUtils.WriteBytes(w, LacingValues);
            }

            public int GetPageSize()
            {
                if (forcedPageSize > -1) return forcedPageSize;

                int result = 0;
                for (int i = 0; i < Segments; i++) result += LacingValues[i];
                return result;
            }

            public void ForcePageSize(int value)
            {
                forcedPageSize = value;
            }

            public int HeaderSize => 27 + LacingValues.Length;

            public bool IsValid => ID != null && ID.SequenceEqual(OGG_PAGE_ID);

            public bool IsFirstPage => 0 == (TypeFlag & 1);
        }

#pragma warning disable S4487 // Unread "private" fields should be removed
        // Vorbis parameter header
        private sealed class VorbisHeader
        {
            private byte[] ID;
            private byte[] BitstreamVersion = new byte[4]; // Bitstream version number
            public byte ChannelMode;                             // Number of channels
            public int SampleRate;                                 // Sample rate (hz)
            public int BitRateMaximal;                         // Bit rate upper limit
            public int BitRateNominal;                             // Nominal bit rate
            public int BitRateMinimal;                         // Bit rate lower limit
            public byte BlockSize;             // Coded size for small and long blocks
            public byte StopFlag;                                          // Always 1

            public void Reset()
            {
                ID = Array.Empty<byte>();
                Array.Clear(BitstreamVersion, 0, BitstreamVersion.Length);
                ChannelMode = 0;
                SampleRate = 0;
                BitRateMaximal = 0;
                BitRateNominal = 0;
                BitRateMinimal = 0;
                BlockSize = 0;
                StopFlag = 0;
            }

            public void FromStream(BufferedBinaryReader source)
            {
                ID = source.ReadBytes(7);
                BitstreamVersion = source.ReadBytes(4);
                ChannelMode = source.ReadByte();
                SampleRate = source.ReadInt32();
                BitRateMaximal = source.ReadInt32();
                BitRateNominal = source.ReadInt32();
                BitRateMinimal = source.ReadInt32();
                BlockSize = source.ReadByte();
                StopFlag = source.ReadByte();
            }

            public bool IsValid()
            {
                return StreamUtils.ArrBeginsWith(ID, VORBIS_HEADER_ID);
            }
        }

        // Opus parameter header
        private sealed class OpusHeader
        {
            public byte[] ID;
            public byte Version;
            public byte OutputChannelCount;
            public ushort PreSkip;
            public uint InputSampleRate;
            public short OutputGain;
            public byte ChannelMappingFamily;

            public byte StreamCount;
            public byte CoupledStreamCount;
            public byte[] ChannelMapping;

            public void Reset()
            {
                ID = Array.Empty<byte>();
                Version = 0;
                OutputChannelCount = 0;
                PreSkip = 0;
                InputSampleRate = 0;
                OutputGain = 0;
                ChannelMappingFamily = 0;
                StreamCount = 0;
                CoupledStreamCount = 0;
            }

            public void FromStream(BufferedBinaryReader source)
            {
                ID = source.ReadBytes(8);

                Version = source.ReadByte();
                OutputChannelCount = source.ReadByte();
                PreSkip = source.ReadUInt16();
                // Actual sample rate is hardware-dependent. Let's assume for now that the hardware ATL runs on supports 48KHz
                InputSampleRate = 48000;

                source.Seek(4, SeekOrigin.Current);
                OutputGain = source.ReadInt16();

                ChannelMappingFamily = source.ReadByte();

                if (ChannelMappingFamily <= 0) return;

                StreamCount = source.ReadByte();
                CoupledStreamCount = source.ReadByte();

                ChannelMapping = new byte[OutputChannelCount];
                for (int i = 0; i < OutputChannelCount; i++)
                {
                    ChannelMapping[i] = source.ReadByte();
                }
            }

            public bool IsValid()
            {
                return StreamUtils.ArrBeginsWith(ID, OPUS_HEADER_ID);
            }
        }

        // Speex parameter header
        private sealed class SpeexHeader
        {
            private byte[] ID;
            public byte[] VersionStr;
            public int Version;
            public int HeaderSize;
            public int Rate;
            public int Mode;
            public int ModeBitstreamVersion;
            public int NbChannels;
            public int Bitrate;
            public int FrameSize;
            public int Vbr;
            public int FramesPerPacket;
            public int ExtraHeaders;


            public void Reset()
            {
                ID = Array.Empty<byte>();
                VersionStr = Array.Empty<byte>();
                Version = 0;
                HeaderSize = 0;
                Rate = 0;
                Mode = 0;
                ModeBitstreamVersion = 0;
                NbChannels = 0;
                Bitrate = 0;
                FrameSize = 0;
                Vbr = 0;
                FramesPerPacket = 0;
                ExtraHeaders = 0;
            }

            public void FromStream(BufferedBinaryReader source)
            {
                ID = source.ReadBytes(8);
                VersionStr = source.ReadBytes(20);
                Version = source.ReadInt32();
                HeaderSize = source.ReadInt32();
                Rate = source.ReadInt32();
                Mode = source.ReadInt32();
                ModeBitstreamVersion = source.ReadInt32();
                NbChannels = source.ReadInt32();
                Bitrate = source.ReadInt32();
                FrameSize = source.ReadInt32();
                Vbr = source.ReadInt32();
                FramesPerPacket = source.ReadInt32();
                ExtraHeaders = source.ReadInt32();
                source.Seek(8, SeekOrigin.Current); // Skip reserved bytes
            }

            public bool IsValid()
            {
                return StreamUtils.ArrBeginsWith(ID, SPEEX_HEADER_ID);
            }
        }
#pragma warning restore S4487 // Unread "private" fields should be removed


        // File data
        private sealed class FileInfo
        {
            // First, second and third Vorbis packets
            public int AudioStreamId;

            // Following properties are mutually exclusive
            public readonly VorbisHeader VorbisParameters = new(); // Vorbis parameter header
            public readonly OpusHeader OpusParameters = new();       // Opus parameter header
            public readonly SpeexHeader SpeexParameters = new();    // Speex parameter header
            public FlacHeader FlacParameters;                                   // FLAC parameter header

            // Total number of samples
            public ulong Samples;

            // Metrics to ease parsing
            public long CommentHeaderStart;     // Begin offset of comment header
            public long CommentHeaderEnd;       // End offset of comment header
            public int CommentHeaderSpanPages;  // Number of pages the Comment header spans over

            public long SetupHeaderStart;       // Begin offset of setup header
            public long SetupHeaderEnd;         // End offset of setup header
            public int SetupHeaderSpanPages;    // Number of pages the Setup header spans over

            public void Reset()
            {
                AudioStreamId = 0;

                VorbisParameters.Reset();
                OpusParameters.Reset();
                SpeexParameters.Reset();

                Samples = 0;

                CommentHeaderStart = 0;
                CommentHeaderEnd = 0;
                CommentHeaderSpanPages = 0;
                SetupHeaderStart = 0;
                SetupHeaderEnd = 0;
                SetupHeaderSpanPages = 0;
            }
        }

        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            SampleRate = 0;
            bitRateNominal = 0;
            BitDepth = -1;
            samples = 0;
            contents = -1;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            info.Reset();
        }

        public Ogg(string filePath, AudioFormat format) : base(true, true, true, true)
        {
            FileName = filePath;
            audioFormat = format;
            resetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public AudioFormat AudioFormat
        {
            get
            {
                AudioFormat f = new AudioFormat(audioFormat);
                string subformat;
                switch (contents)
                {
                    case CONTENTS_VORBIS:
                        subformat = "Vorbis";
                        break;
                    case CONTENTS_OPUS:
                        subformat = "Opus";
                        break;
                    case CONTENTS_SPEEX:
                        subformat = "Speex";
                        break;
                    case CONTENTS_FLAC:
                        subformat = "FLAC";
                        f.DataFormat = AudioDataIOFactory.GetInstance().getFormat(AudioDataIOFactory.CID_FLAC);
                        break;
                    default:
                        subformat = "Unsupported";
                        break;
                }
                f.Name = f.Name + " (" + subformat + ")";
                f.ComputeId();
                return f;
            }
        }
        public int CodecFamily => contents == CONTENTS_FLAC ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY;

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            // According to id3.org (FAQ), ID3 is not compatible with OGG. Hence ATL does not allow ID3 tags to be written on OGG files; native is for VorbisTag
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => true;

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                IList<Format> result = base.MetadataFormats;
                result[0].Name += " (OGG)";
                result[0].ID += AudioFormat.ID;
                return result;
            }
        }


        // ---------------------------------------------------------------------------

        // Read total samples of OGG file, which are located on the very last page of the file
        private static ulong getSamples(BufferedBinaryReader source)
        {
            byte typeFlag;
            byte[] lacingValues = new byte[255];
            byte nbLacingValues = 0;
            long nextPageOffset = 0;
            double seekDistanceRatio = 0.5;

            int seekDistance = (int)Math.Round(MAX_PAGE_SIZE * 0.75);
            if (seekDistance > source.Length) seekDistance = (int)Math.Round(source.Length * seekDistanceRatio);

            bool found = false;
            while (!found && seekDistanceRatio <= 1)
            {
                source.Seek(-seekDistance, SeekOrigin.End);
                found = StreamUtils.FindSequence(source, OGG_PAGE_ID);
                if (!found) // Increase seek distance if not found
                {
                    seekDistanceRatio += 0.1;
                    seekDistance = (int)Math.Round(source.Length * seekDistanceRatio);
                }
            }
            if (!found)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "No OGG header found; aborting read operation"); // Throw exception ?
                return 0;
            }
            source.Seek(-4, SeekOrigin.Current);

            // Iterate until last page is encountered
            do
            {
                if (source.Position + nextPageOffset + 27 > source.Length) // End of stream about to be reached => last OGG header did not have the proper type flag
                {
                    break;
                }

                source.Seek(nextPageOffset, SeekOrigin.Current);
                if (source.ReadBytes(4).SequenceEqual(OGG_PAGE_ID))
                {
                    source.Seek(1, SeekOrigin.Current);
                    typeFlag = source.ReadByte();
                    source.Seek(20, SeekOrigin.Current);
                    nbLacingValues = source.ReadByte();
                    nextPageOffset = 0;
                    int read = source.Read(lacingValues, 0, nbLacingValues);
                    for (int i = 0; i < read; i++) nextPageOffset += lacingValues[i];
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Invalid OGG header found while looking for total samples; aborting read operation"); // Throw exception ?
                    return 0;
                }

            } while (0 == (typeFlag & 0x04)); // 0x04 marks the last page of the logical bitstream


            // Stream is positioned at the end of the last page header; backtracking to read AbsolutePosition field
            source.Seek(-nbLacingValues - 21, SeekOrigin.Current);

            return source.ReadUInt64();
        }

        private bool getInfo(BufferedBinaryReader source, FileInfo info, ReadTagParams readTagParams)
        {
            IList<long> pageOffsets = new List<long>();
            IDictionary<int, MemoryStream> bitstreams = new Dictionary<int, MemoryStream>();
            IDictionary<int, int> pageCount = new Dictionary<int, int>();
            IDictionary<int, bool> isSupported = new Dictionary<int, bool>();
            IDictionary<int, bool> multiPagecommentPacket = new Dictionary<int, bool>();
            bool isValidHeader = false;

            try
            {
                // Reads all Vorbis pages that describe bitstream metadata (i.e. Identification, Comment and Setup packets)
                // and concatenate their content into a single, continuous data stream
                //
                // As per OGG specs :
                //   - ID packet is alone on the 1st single page of its stream
                //   - Comment and Setup packets are together on the 2nd page and may share its last sub-page
                //   - Audio data starts on a fresh page
                //
                // NB : Only detects bitstream headers positioned at the beginning of the file
                OggPageHeader pageHeader;
                source.Seek(0, SeekOrigin.Begin);
                do
                {
                    pageOffsets.Add(source.Position);

                    pageHeader = OggPageHeader.ReadFromStream(source);
                    if (!pageHeader.IsValid)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Malformed Ogg Header");
                        break;
                    }

                    // Make sure next byte after page end is a page header; try to auto-correct if not
                    // NB : Only useful when reading the first two pages as page 4 doesn't need to be parsed
                    if (pageCount.ContainsKey(pageHeader.StreamId) && pageCount[pageHeader.StreamId] < 2)
                    {
                        source.Position = pageHeader.Offset + pageHeader.HeaderSize + pageHeader.GetPageSize();
                        byte[] data = source.ReadBytes(OGG_PAGE_ID.Length);
                        if (!StreamUtils.ArrBeginsWith(data, OGG_PAGE_ID))
                        {
                            source.Position = pageHeader.Offset + pageHeader.HeaderSize;
                            // Last chance : try to reach the next page header by searching for its marker
                            if (StreamUtils.FindSequence(source, OGG_PAGE_ID, (long)Math.Round(source.Length * 0.1)))
                            {
                                pageHeader.ForcePageSize((int)(source.Position - OGG_PAGE_ID.Length -
                                                               pageHeader.Offset - pageHeader.HeaderSize));
                            }
                            else
                            {
                                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Malformed Ogg Header");
                                break;
                            }
                        }
                    }

                    int pageSize = pageHeader.GetPageSize();
                    source.Position = pageHeader.Offset + pageHeader.HeaderSize;

                    if (!pageCount.TryAdd(pageHeader.StreamId, 1))
                    {
                        // Nth page of a known stream
                        if (pageHeader.IsFirstPage)
                        {
                            int newPageCount = pageCount[pageHeader.StreamId] + 1;
                            pageCount[pageHeader.StreamId] = newPageCount;
                            if (2 == newPageCount) info.CommentHeaderStart = source.Position - pageHeader.HeaderSize;
                            else if (3 == newPageCount) info.SetupHeaderEnd = source.Position - pageHeader.HeaderSize;
                        }

                        if (isSupported[pageHeader.StreamId] && 2 == pageCount[pageHeader.StreamId]) // Comment packet
                        {
                            // Load all Comment packet sub-pages into a MemoryStream to read them later in one go
                            // NB : Detecting when to read Comment packet page directly is too difficult
                            // as some files have their 1st subpage of many using less than 255 segments
                            multiPagecommentPacket[pageHeader.StreamId] = true;
                            MemoryStream stream;
                            if (bitstreams.TryGetValue(pageHeader.StreamId, out var bitstream)) stream = bitstream;
                            else
                            {
                                stream = new MemoryStream();
                                bitstreams[pageHeader.StreamId] = stream;
                            }

                            stream.Write(source.ReadBytes(pageSize), 0, pageSize);
                        }
                    }
                    else // 1st page of a new stream
                    {
                        multiPagecommentPacket[pageHeader.StreamId] = false;
                        // The very first page of any given stream is its Identification packet
                        bool supported = readIdentificationPacket(source);
                        isSupported[pageHeader.StreamId] = supported;
                        if (supported)
                        {
                            info.AudioStreamId = pageHeader.StreamId;
                            isValidHeader = true;
                        }
                    }

                    source.Seek(pageHeader.Offset + pageHeader.HeaderSize + pageSize, SeekOrigin.Begin);

                    // Stop when the two first pages (containing ID, Comment and Setup packets) have been scanned
                } while (pageCount[pageHeader.StreamId] < 3);

                AudioDataOffset = info.SetupHeaderEnd; // Not exactly true as audio is useless without the setup header
                AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

                if (readTagParams.PrepareForWriting) // Metrics to prepare writing
                {
                    switch (contents)
                    {
                        case CONTENTS_VORBIS or CONTENTS_FLAC:
                            {
                                // Determine the boundaries of 3rd header (Setup header) by searching from the last-but-one page
                                source.Position = pageOffsets.Count > 1 ? pageOffsets[^2] : pageOffsets[0];
                                source.Position += OGG_PAGE_ID.Length;
                                if (StreamUtils.FindSequence(source, VORBIS_SETUP_ID))
                                {
                                    info.SetupHeaderStart = source.Position - VORBIS_SETUP_ID.Length;
                                    info.CommentHeaderEnd = info.SetupHeaderStart;

                                    // Determine over how many OGG pages Comment and Setup pages span
                                    if (pageOffsets.Count > 1)
                                    {
                                        int firstSetupPage = -1;
                                        for (int i = 1; i < pageOffsets.Count; i++)
                                        {
                                            if (info.CommentHeaderEnd < pageOffsets[i])
                                            {
                                                info.CommentHeaderSpanPages = i - 1;
                                                firstSetupPage = i - 1;
                                            }

                                            if (info.SetupHeaderEnd <= pageOffsets[i])
                                                info.SetupHeaderSpanPages = i - firstSetupPage;
                                        }

                                        // Not found yet => comment header takes up all pages, and setup header is on the end of the last page
                                        if (-1 == firstSetupPage)
                                        {
                                            info.CommentHeaderSpanPages = pageOffsets.Count;
                                            info.SetupHeaderSpanPages = 1;
                                        }
                                    }
                                    else
                                    {
                                        info.CommentHeaderSpanPages = 1;
                                        info.SetupHeaderSpanPages = 1;
                                    }
                                }

                                // Case of embedded FLAC as setup header doesn't exist => end is the end of the page
                                if (0 == info.CommentHeaderEnd && StreamUtils.FindSequence(source, OGG_PAGE_ID))
                                {
                                    info.CommentHeaderEnd = source.Position - OGG_PAGE_ID.Length;
                                }

                                break;
                            }
                        case CONTENTS_OPUS or CONTENTS_SPEEX:
                            info.SetupHeaderStart = info.SetupHeaderEnd;
                            info.CommentHeaderEnd = info.SetupHeaderStart;
                            info.CommentHeaderSpanPages = pageOffsets.Count;
                            info.SetupHeaderSpanPages = 0;
                            break;
                    }
                }

                // Get total number of samples
                info.Samples = getSamples(source);

                // Read metadata from Comment pages that span over multiple segments
                foreach (var stream in bitstreams.Values)
                {
                    stream.Position = 0;
                    readCommentPacket(stream, contents, vorbisTag, readTagParams);
                }
            }
            finally
            {
                // Liberate all MemoryStreams
                foreach (KeyValuePair<int, MemoryStream> entry in bitstreams)
                {
                    entry.Value.Close();
                }
            }
            return isValidHeader;
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, OGG_PAGE_ID);
        }

        private bool readIdentificationPacket(BufferedBinaryReader source)
        {
            bool isSupportedHeader = false;
            long initialOffset = source.Position;

            byte[] headerStart = source.ReadBytes(3);
            source.Seek(initialOffset, SeekOrigin.Begin);

            if (StreamUtils.ArrBeginsWith(VORBIS_HEADER_ID, headerStart))
            {
                contents = CONTENTS_VORBIS;
                info.VorbisParameters.FromStream(source);
                isSupportedHeader = info.VorbisParameters.IsValid();
            }
            else if (StreamUtils.ArrBeginsWith(OPUS_HEADER_ID, headerStart))
            {
                contents = CONTENTS_OPUS;
                info.OpusParameters.FromStream(source);
                isSupportedHeader = info.OpusParameters.IsValid();
            }
            else if (StreamUtils.ArrBeginsWith(FLAC_HEADER_ID, headerStart))
            {
                contents = CONTENTS_FLAC;
                source.Seek(FLAC_HEADER_ID.Length, SeekOrigin.Current); // Skip the entire FLAC segment header
                source.Seek(2, SeekOrigin.Current); // FLAC-to-Ogg mapping version
                short nbHeaderPackets = StreamUtils.DecodeBEInt16(source.ReadBytes(2));
                info.FlacParameters = FlacHelper.ReadHeader(source);
                isSupportedHeader = info.FlacParameters.IsValid();
            }
            else if (StreamUtils.ArrBeginsWith(SPEEX_HEADER_ID, headerStart))
            {
                contents = CONTENTS_SPEEX;
                info.SpeexParameters.FromStream(source);
                isSupportedHeader = info.SpeexParameters.IsValid();
            }
            else if (StreamUtils.ArrBeginsWith(THEORA_HEADER_ID, headerStart))
            {
                // ATL doesn't support video data; don't examine this bitstream
            }
            return isSupportedHeader;
        }

        private static void readCommentPacket(Stream source, int contentType, VorbisTag tag, ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[8];
            bool isValidTagHeader = false;
            if (contentType.Equals(CONTENTS_VORBIS))
            {
                if (source.Read(buffer, 0, 7) < 7) return;
                isValidTagHeader = StreamUtils.ArrBeginsWith(buffer, VORBIS_COMMENT_ID);
            }
            else if (contentType.Equals(CONTENTS_OPUS))
            {
                if (source.Read(buffer, 0, 8) < 8) return;
                isValidTagHeader = StreamUtils.ArrBeginsWith(buffer, OPUS_TAG_ID);
            }
            else if (contentType.Equals(CONTENTS_SPEEX))
            {
                isValidTagHeader = true; // No specific tag header
            }
            else if (contentType.Equals(CONTENTS_FLAC))
            {
                if (source.Read(buffer, 0, 4) < 4) return;
                //uint blockLength = StreamUtils.DecodeBEUInt24(buffer, 1);
                byte blockType = (byte)(buffer[0] & 0x7F); // decode metablock type
                isValidTagHeader = blockType < 7;
            }

            if (!isValidTagHeader) return;

            tag.Clear();
            tag.Read(source, readTagParams);
        }

        // Calculate duration time
        private double getDuration()
        {
            double result;

            if (samples > 0)
            {
                if (SampleRate > 0)
                    result = samples * 1000.0 / SampleRate;
                else
                    result = 0;
            }
            else if ((bitRateNominal > 0) && (ChannelsArrangement.NbChannels > 0))
            {
                result = 1000.0 * sizeInfo.FileSize / bitRateNominal / ChannelsArrangement.NbChannels / 125.0 * 2;
            }
            else
                result = 0;

            return result;
        }

        /// <summary>
        /// Calculate average bitrate
        /// </summary>
        /// <returns>Average bitrate</returns>
        private double getBitRate()
        {
            double result = 0;

            if (getDuration() > 0) result = (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 8.0 / getDuration();

            return result;
        }

        private bool isVbr()
        {
            switch (contents)
            {
                case CONTENTS_FLAC: return false;
                case CONTENTS_SPEEX: return info.SpeexParameters.Vbr > 0;
                default: return true;
            }
        }


        /// <summary>
        /// Check for file correctness
        /// </summary>
        /// <returns>True if file data is coherent; false if not</returns>
        private bool isValid()
        {
            return (ChannelsArrangement.NbChannels > 0) && (SampleRate > 0) && (getDuration() > 0.1) && (getBitRate() > 0);
        }

        private static ChannelsArrangement getArrangementFromCode(int vorbisCode)
        {
            if (vorbisCode > 8) return new ChannelsArrangement(vorbisCode);
            return vorbisCode switch
            {
                1 => MONO,
                2 => STEREO,
                3 => ISO_3_0_0,
                4 => QUAD,
                5 => ISO_3_2_0,
                6 => ISO_3_2_1,
                7 => LRCLFECrLssRss,
                8 => LRCLFELrRrLssRss,
                _ => UNKNOWN
            };
        }

        // ---------------------------------------------------------------------------

        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return Read(source, readTagParams);
        }

        public bool Read(Stream source, ReadTagParams readTagParams)
        {
            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            info.Reset();

            if (!getInfo(reader, info, readTagParams)) return false;

            switch (contents)
            {
                case CONTENTS_VORBIS:
                    ChannelsArrangement = getArrangementFromCode(info.VorbisParameters.ChannelMode);
                    SampleRate = info.VorbisParameters.SampleRate;
                    bitRateNominal = (ushort)(info.VorbisParameters.BitRateNominal / 1000); // Integer division
                    break;
                case CONTENTS_OPUS:
                    ChannelsArrangement = getArrangementFromCode(info.OpusParameters.OutputChannelCount);
                    SampleRate = (int)info.OpusParameters.InputSampleRate;
                    // No nominal bitrate for OPUS
                    break;
                case CONTENTS_SPEEX:
                    ChannelsArrangement = GuessFromChannelNumber(info.SpeexParameters.NbChannels);
                    SampleRate = info.SpeexParameters.Rate;
                    bitRateNominal = (ushort)(info.SpeexParameters.Bitrate / 1000);
                    break;
                case CONTENTS_FLAC:
                    ChannelsArrangement = info.FlacParameters.getChannelsArrangement();
                    SampleRate = info.FlacParameters.SampleRate;
                    BitDepth = info.FlacParameters.BitsPerSample;
                    // No nominal bitrate for FLAC
                    break;
            }

            samples = info.Samples;

            return true;
        }

        // Specific implementation for OGG container (multiple pages with limited size)

        // TODO DOC
        // Simplified implementation of MetaDataIO tweaked for OGG-Vorbis specifics, i.e.
        //  - tag spans over multiple pages, each having its own header
        //  - last page may include whole or part of Vorbis Setup header
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null)
        {
            bool result = true;
            int writtenPages;
            long nextPageOffset;

            // Read all the fields in the existing tag (including unsupported fields)
            var readTagParams = new ReadTagParams(true, true);
            readTagParams.PrepareForWriting = true;
            Read(s, readTagParams);

            // Create the "unpaged" in-memory stream to be written, containing the vorbis tag (=comment header)
            using (var memStream = new MemoryStream((int)(info.SetupHeaderEnd - info.CommentHeaderStart)))
            {
                if (CONTENTS_VORBIS == contents)
                {
                    await memStream.WriteAsync(VORBIS_COMMENT_ID, 0, VORBIS_COMMENT_ID.Length);
                    vorbisTag.switchOggBehaviour();
                    await vorbisTag.WriteAsync(memStream, tag, args);
                }
                else if (CONTENTS_OPUS == contents)
                {
                    await memStream.WriteAsync(OPUS_TAG_ID, 0, OPUS_TAG_ID.Length);
                    vorbisTag.switchOggBehaviour();
                    await vorbisTag.WriteAsync(memStream, tag, args);
                }
                else if (CONTENTS_SPEEX == contents)
                {
                    vorbisTag.switchFlacBehaviour();
                    await vorbisTag.WriteAsync(memStream, tag, args);
                }
                else if (CONTENTS_FLAC == contents)
                {
                    vorbisTag.switchFlacBehaviour();
                    FLAC.writeVorbisCommentBlock(memStream, tag, vorbisTag, true);
                }

                long newTagSize = memStream.Position;

                int setupHeaderSize = 0;
                int setupHeader_nbSegments = 0;
                byte setupHeader_remainingBytesInLastSegment = 0;

                // VORBIS: Append the setup header in the "unpaged" in-memory stream
                if (CONTENTS_VORBIS == contents)
                {
                    s.Seek(info.SetupHeaderStart, SeekOrigin.Begin);
                    if (1 == info.SetupHeaderSpanPages)
                    {
                        setupHeaderSize = (int)(info.SetupHeaderEnd - info.SetupHeaderStart);
                        await StreamUtils.CopyStreamAsync(s, memStream, setupHeaderSize);
                    }
                    else
                    {
                        // TODO - handle case where initial setup header spans across two pages
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "The case where Vorbis setup header spans across two OGG pages is not supported yet");
                        return false;
                    }
                    setupHeader_nbSegments = (int)Math.Ceiling(1.0 * setupHeaderSize / 255);
                    setupHeader_remainingBytesInLastSegment = (byte)(setupHeaderSize % 255);
                }

                writtenPages = constructSegmentsTable(memStream, newTagSize, setupHeaderSize, setupHeader_nbSegments, setupHeader_remainingBytesInLastSegment);

                // Insert the in-memory paged stream into the actual file
                long oldHeadersSize = info.SetupHeaderEnd - info.CommentHeaderStart;
                long newHeadersSize = memStream.Length;

                if (newHeadersSize > oldHeadersSize) // Need to build a larger file
                {
                    await StreamUtils.LengthenStreamAsync(s, info.CommentHeaderEnd, (uint)(newHeadersSize - oldHeadersSize));
                }
                else if (newHeadersSize < oldHeadersSize) // Need to reduce file size
                {
                    await StreamUtils.ShortenStreamAsync(s, info.CommentHeaderEnd, (uint)(oldHeadersSize - newHeadersSize));
                }

                // Rewrite Comment and Setup headers
                s.Seek(info.CommentHeaderStart, SeekOrigin.Begin);
                memStream.Seek(0, SeekOrigin.Begin);

                await StreamUtils.CopyStreamAsync(memStream, s);

                nextPageOffset = info.CommentHeaderStart + memStream.Length;
            } // using MemoryStream memStream

            // If the number of written pages is different than the number of previous existing pages,
            // all the next pages of the file need to be renumbered, and their CRC accordingly recalculated
            if (writtenPages != info.CommentHeaderSpanPages + info.SetupHeaderSpanPages - 1)
            {
                result &= renumberRemainingPages(s, nextPageOffset, writtenPages);
            }

            return result;
        }

        // Construct the entire segments table
        private int constructSegmentsTable(Stream memStream, long newTagSize, int setupHeaderSize, int setupHeader_nbSegments, byte setupHeader_remainingBytesInLastSegment)
        {
            int commentsHeader_nbSegments = (int)Math.Ceiling(1.0 * newTagSize / 255);
            resizeMemStream(memStream, commentsHeader_nbSegments, setupHeader_nbSegments);
            return repageMemStream(memStream, newTagSize, commentsHeader_nbSegments, setupHeaderSize, setupHeader_nbSegments, setupHeader_remainingBytesInLastSegment);
        }

        private byte[] buildSegmentsTable(long newTagSize, int commentsHeader_nbSegments, int setupHeader_nbSegments, byte setupHeader_remainingBytesInLastSegment)
        {
            byte commentsHeader_remainingBytesInLastSegment = (byte)(newTagSize % 255);

            byte[] entireSegmentsTable = new byte[commentsHeader_nbSegments + setupHeader_nbSegments];
            for (int i = 0; i < commentsHeader_nbSegments - 1; i++)
            {
                entireSegmentsTable[i] = 255;
            }
            entireSegmentsTable[commentsHeader_nbSegments - 1] = commentsHeader_remainingBytesInLastSegment;
            if (CONTENTS_VORBIS == contents)
            {
                for (int i = commentsHeader_nbSegments; i < commentsHeader_nbSegments + setupHeader_nbSegments - 1; i++)
                {
                    entireSegmentsTable[i] = 255;
                }
                entireSegmentsTable[commentsHeader_nbSegments + setupHeader_nbSegments - 1] = setupHeader_remainingBytesInLastSegment;
            }
            return entireSegmentsTable;
        }

        // Resize the whole in-memory stream once and for all to avoid multiple reallocations while repaging
        private static void resizeMemStream(Stream memStream, int commentsHeader_nbSegments, int setupHeader_nbSegments)
        {
            int nbPageHeaders = (int)Math.Ceiling((commentsHeader_nbSegments + setupHeader_nbSegments) / 255.0);
            int totalPageHeadersSize = (nbPageHeaders * 27) + commentsHeader_nbSegments + setupHeader_nbSegments;

            memStream.SetLength(memStream.Position + totalPageHeadersSize);
        }

        // Repage comments header & setup header within the in-memory stream
        private int repageMemStream(
            Stream memStream,
            long newTagSize,
            int commentsHeader_nbSegments,
            int setupHeaderSize,
            int setupHeader_nbSegments,
            byte setupHeader_remainingBytesInLastSegment)
        {
            memStream.Seek(0, SeekOrigin.Begin);

            OggPageHeader header = new OggPageHeader(info.AudioStreamId);

            int segmentsLeftToPage = commentsHeader_nbSegments + setupHeader_nbSegments;
            int bytesLeftToPage = (int)newTagSize + setupHeaderSize;
            int pagedSegments = 0;
            int pagedBytes = 0;
            long position;

            IList<KeyValuePair<long, int>> pageHeaderOffsets = new List<KeyValuePair<long, int>>();

            // Repaging
            while (segmentsLeftToPage > 0)
            {
                header.Segments = (byte)Math.Min(255, segmentsLeftToPage);
                header.LacingValues = new byte[header.Segments];
                if (segmentsLeftToPage == header.Segments) header.AbsolutePosition = 0; // Last header page has its absolutePosition = 0

                byte[] entireSegmentsTable = buildSegmentsTable(newTagSize, commentsHeader_nbSegments, setupHeader_nbSegments, setupHeader_remainingBytesInLastSegment);
                Array.Copy(entireSegmentsTable, pagedSegments, header.LacingValues, 0, header.Segments);

                position = memStream.Position;
                // Push current data to write header
                // NB : We're manipulating the MemoryStream here; calling an async variant won't have any relevant effect on performance
                StreamUtils.CopySameStream(memStream, memStream.Position, memStream.Position + header.HeaderSize, bytesLeftToPage);
                memStream.Seek(position, SeekOrigin.Begin);

                pageHeaderOffsets.Add(new KeyValuePair<long, int>(position, header.GetPageSize() + header.HeaderSize));

                header.WriteToStream(memStream);
                memStream.Seek(header.GetPageSize(), SeekOrigin.Current);

                pagedSegments += header.Segments;
                segmentsLeftToPage -= header.Segments;
                pagedBytes += header.GetPageSize();
                bytesLeftToPage -= header.GetPageSize();

                header.PageNumber++;
                if (0 == header.TypeFlag) header.TypeFlag = 1;
            }
            generatePageCrc32(memStream, pageHeaderOffsets);

            return header.PageNumber - 1;
        }

        // Generate CRC32 of created pages
        private static void generatePageCrc32(Stream s, IEnumerable<KeyValuePair<long, int>> pageHeaderOffsets)
        {
            byte[] data = Array.Empty<byte>();
            foreach (KeyValuePair<long, int> kv in pageHeaderOffsets)
            {
                s.Seek(kv.Key, SeekOrigin.Begin);
                if (data.Length < kv.Value) data = new byte[kv.Value]; // Enlarge only if needed; max size is 0xffff
                if (s.Read(data, 0, kv.Value) < kv.Value) return;
                uint crc = OggCRC32.CalculateCRC(0, data, (uint)kv.Value);
                // Write CRC value at the dedicated location within the OGG header
                s.Seek(kv.Key + 22, SeekOrigin.Begin);
                s.Write(StreamUtils.EncodeUInt32(crc));
            }
        }

        private static bool renumberRemainingPages(Stream s, long nextPageOffset, int writtenPages)
        {
            OggPageHeader header = new OggPageHeader();
            byte[] data = Array.Empty<byte>();
            do
            {
                s.Seek(nextPageOffset, SeekOrigin.Begin);
                header.ReadFromStream(s);

                if (header.IsValid)
                {
                    // Rewrite page number
                    writtenPages++;
                    s.Seek(nextPageOffset + 18, SeekOrigin.Begin);
                    s.Write(StreamUtils.EncodeInt32(writtenPages));

                    // Rewrite CRC
                    s.Seek(nextPageOffset, SeekOrigin.Begin);
                    int dataSize = header.HeaderSize + header.GetPageSize();
                    if (data.Length < dataSize) data = new byte[dataSize]; // Only realloc when size is insufficient
                    if (s.Read(data, 0, dataSize) < dataSize) return false;

                    // Checksum has to include its own location, as if it were 0
                    data[22] = 0;
                    data[23] = 0;
                    data[24] = 0;
                    data[25] = 0;

                    uint crc = OggCRC32.CalculateCRC(0, data, (uint)dataSize);
                    s.Seek(nextPageOffset + 22, SeekOrigin.Begin); // Position of CRC within OGG header
                    s.Write(StreamUtils.EncodeUInt32(crc));

                    // To the next header
                    nextPageOffset += dataSize;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Invalid OGG header found; aborting writing operation"); // Throw exception ?
                    return false;
                }

            } while (0 == (header.TypeFlag & 0x04));  // 0x04 marks the last page of the logical bitstream
            return true;
        }

        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            TagData tag = vorbisTag.GetDeletionTagData();
            return await WriteAsync(s, tag, args);
        }

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            vorbisTag.Clear();
        }
    }
}