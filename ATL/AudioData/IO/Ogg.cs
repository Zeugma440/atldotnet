using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.FlacHelper;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OGG files manipulation. Current implementation covers :
    ///   - Vorbis data (extensions : .OGG)
    ///   - Opus data (extensions : .OPUS)
    ///   - Embedded FLAC data (extensions : .OGG)
    ///   
    /// Implementation notes
    /// 
    ///   1. CRC's : Current implementation does not test OGG page header CRC's
    ///   2. Page numbers : Current implementation does not test page numbers consistency
    ///   3. Writing metadata is not supported yet on embedded FLAC files
    /// 
    /// </summary>
	class Ogg : IAudioDataIO, IMetaDataIO
    {
        // Contents of the file
        private const int CONTENTS_UNSUPPORTED = -1;	    // Unsupported
        private const int CONTENTS_VORBIS = 0;				// Vorbis
        private const int CONTENTS_OPUS = 1;                // Opus
        private const int CONTENTS_FLAC = 2;                // FLAC

        private const int MAX_PAGE_SIZE = 255 * 255;

        // Ogg page header ID
        private const string OGG_PAGE_ID = "OggS";

        // Vorbis identification packet (frame) ID
        private static readonly string VORBIS_HEADER_ID = (char)1 + "vorbis";

        // Vorbis comment (tags) packet (frame) ID
        private static readonly string VORBIS_COMMENT_ID = (char)3 + "vorbis";

        // Vorbis setup packet (frame) ID
        private static readonly string VORBIS_SETUP_ID = (char)5 + "vorbis";


        // Theora identification packet (frame) ID
        private static readonly string THEORA_HEADER_ID = (char)0x80 + "theora";


        // Opus parameter frame ID
        private const string OPUS_HEADER_ID = "OpusHead";

        // Opus tag frame ID
        private const string OPUS_TAG_ID = "OpusTags";


        // FLAC identification packet (frame) ID
        private static readonly string FLAC_HEADER_ID = (char)0x7f + "FLAC";




        private readonly string filePath;
        private readonly Format audioFormat;
        private VorbisTag vorbisTag;

        private readonly FileInfo info = new FileInfo();

        private int contents;

        private int sampleRate;
        private ushort bitRateNominal;
        private ulong samples;
        private ChannelsArrangement channelsArrangement;

        private AudioDataManager.SizeInfo sizeInfo;



        public int SampleRate // Sample rate (hz)
        {
            get { return sampleRate; }
        }
        public ushort BitRateNominal // Nominal bit rate
        {
            get { return bitRateNominal; }
        }
        public bool Valid // True if file valid
        {
            get { return isValid(); }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return getBitRate(); }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsVBR
        {
            get { return contents != CONTENTS_FLAC; }
        }
        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }



        // Ogg page header
        private sealed class OggPageHeader
        {
            public string ID;                                               // Always "OggS"
            public byte StreamVersion;                           // Stream structure version
            public byte TypeFlag;                                        // Header type flag
            public ulong AbsolutePosition;                      // Absolute granule position
            public int StreamId;                                       // Stream serial number
            public int PageNumber;                                   // Page sequence number
            public uint Checksum;                                              // Page CRC32
            public byte Segments;                                 // Number of page segments
            public byte[] LacingValues;                     // Lacing values - segment sizes

            public void ReadFromStream(BufferedBinaryReader r)
            {
                ID = Utils.Latin1Encoding.GetString(r.ReadBytes(4));
                StreamVersion = r.ReadByte();
                TypeFlag = r.ReadByte();
                AbsolutePosition = r.ReadUInt64();
                StreamId = r.ReadInt32();
                PageNumber = r.ReadInt32();
                Checksum = r.ReadUInt32();
                Segments = r.ReadByte();
                LacingValues = r.ReadBytes(Segments);
            }

            public void ReadFromStream(BinaryReader r)
            {
                ID = Utils.Latin1Encoding.GetString(r.ReadBytes(4));
                StreamVersion = r.ReadByte();
                TypeFlag = r.ReadByte();
                AbsolutePosition = r.ReadUInt64();
                StreamId = r.ReadInt32();
                PageNumber = r.ReadInt32();
                Checksum = r.ReadUInt32();
                Segments = r.ReadByte();
                LacingValues = r.ReadBytes(Segments);
            }

            public void WriteToStream(BinaryWriter w)
            {
                w.Write(Utils.Latin1Encoding.GetBytes(ID));
                w.Write(StreamVersion);
                w.Write(TypeFlag);
                w.Write(AbsolutePosition);
                w.Write(StreamId);
                w.Write(PageNumber);
                w.Write(Checksum);
                w.Write(Segments);
                w.Write(LacingValues);
            }

            public int GetPageSize()
            {
                int length = 0;
                for (int i = 0; i < Segments; i++)
                {
                    length += LacingValues[i];
                }
                return length;
            }

            public int GetHeaderSize()
            {
                return 27 + LacingValues.Length;
            }

            public bool IsValid()
            {
                return (ID != null) && ID.Equals(OGG_PAGE_ID);
            }

            public bool IsFirstPage()
            {
                return 0 == (TypeFlag & 1);
            }
        }

#pragma warning disable S4487 // Unread "private" fields should be removed
        // Vorbis parameter header
        private sealed class VorbisHeader
        {
            public String ID;
            public byte[] BitstreamVersion = new byte[4];  // Bitstream version number
            public byte ChannelMode;                             // Number of channels
            public int SampleRate;                                 // Sample rate (hz)
            public int BitRateMaximal;                         // Bit rate upper limit
            public int BitRateNominal;                             // Nominal bit rate
            public int BitRateMinimal;                         // Bit rate lower limit
            public byte BlockSize;             // Coded size for small and long blocks
            public byte StopFlag;                                          // Always 1

            public void Reset()
            {
                ID = "";
                Array.Clear(BitstreamVersion, 0, BitstreamVersion.Length);
                ChannelMode = 0;
                SampleRate = 0;
                BitRateMaximal = 0;
                BitRateNominal = 0;
                BitRateMinimal = 0;
                BlockSize = 0;
                StopFlag = 0;
            }
        }

        // Opus parameter header
        private sealed class OpusHeader
        {
            public string ID;
            public byte Version;
            public byte OutputChannelCount;
            public UInt16 PreSkip;
            public UInt32 InputSampleRate;
            public Int16 OutputGain;
            public byte ChannelMappingFamily;

            public byte StreamCount;
            public byte CoupledStreamCount;
            public byte[] ChannelMapping;

            public void Reset()
            {
                ID = "";
                Version = 0;
                OutputChannelCount = 0;
                PreSkip = 0;
                InputSampleRate = 0;
                OutputGain = 0;
                ChannelMappingFamily = 0;
                StreamCount = 0;
                CoupledStreamCount = 0;
            }
        }
#pragma warning restore S4487 // Unread "private" fields should be removed


        // File data
        private sealed class FileInfo
        {
            // First, second and third Vorbis packets
            public int AudioStreamId;

            // Following two properties are mutually exclusive
            public VorbisHeader VorbisParameters = new VorbisHeader();  // Vorbis parameter header
            public OpusHeader OpusParameters = new OpusHeader();        // Opus parameter header
            public FlacHeader FlacParameters;                           // FLAC parameter header

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
            sampleRate = 0;
            bitRateNominal = 0;
            samples = 0;
            contents = -1;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            info.Reset();
        }

        public Ogg(string filePath, Format format)
        {
            this.filePath = filePath;
            audioFormat = format;
            resetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public Format AudioFormat
        {
            get
            {
                Format f = new Format(audioFormat);
                string subformat;
                if (contents == CONTENTS_VORBIS) subformat = "Vorbis";
                else if (contents == CONTENTS_OPUS) subformat = "Opus";
                else if (contents == CONTENTS_FLAC) subformat = "FLAC";
                else subformat = "Unsupported";
                f.Name = f.Name + " (" + subformat + ")";
                return f;
            }
        }
        public int CodecFamily
        {
            get { return (contents == CONTENTS_FLAC) ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY; }
        }

        #region IMetaDataIO
        public bool Exists
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Exists;
            }
        }
        /// <inheritdoc/>
        public IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
                nativeFormat.Name = "Native / Vorbis (OGG)";
                nativeFormat.ID += AudioFormat.ID;
                return new List<Format>(new Format[1] { nativeFormat });
            }
        }

        public string Title
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Title;
            }
        }

        public string Artist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Artist;
            }
        }

        public string Composer
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Composer;
            }
        }

        public string Comment
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Comment;
            }
        }

        public string Genre
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Genre;
            }
        }

        public ushort Track
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Track;
            }
        }

        public ushort TrackTotal
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).TrackTotal;
            }
        }

        public ushort Disc
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Disc;
            }
        }

        public ushort DiscTotal
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).DiscTotal;
            }
        }

        public DateTime Date
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Date;
            }
        }

        public string Album
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Album;
            }
        }

        public float? Popularity
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Popularity;
            }
        }

        public string Copyright
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Copyright;
            }
        }

        public string OriginalArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalArtist;
            }
        }

        public string OriginalAlbum
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalAlbum;
            }
        }

        public string GeneralDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).GeneralDescription;
            }
        }

        public string Publisher
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Publisher;
            }
        }

        public DateTime PublishingDate
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PublishingDate;
            }
        }

        public string AlbumArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AlbumArtist;
            }
        }

        public string Conductor
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Conductor;
            }
        }

        public string ProductId
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).ProductId;
            }
        }

        public long PaddingSize
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PaddingSize;
            }
        }

        public IList<PictureInfo> PictureTokens
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PictureTokens;
            }
        }

        public long Size
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Size;
            }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AdditionalFields;
            }
        }

        public string ChaptersTableDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).ChaptersTableDescription;
            }
        }

        public IList<ChapterInfo> Chapters
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Chapters;
            }
        }

        public LyricsInfo Lyrics
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Lyrics;
            }
        }

        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).EmbeddedPictures;
            }
        }
        #endregion

        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return metaDataType == MetaDataIOFactory.TagType.NATIVE; // According to id3.org (FAQ), ID3 is not compatible with OGG. Hence ATL does not allow ID3 tags to be written on OGG files; native is for VorbisTag
        }


        // ---------------------------------------------------------------------------

        // Read total samples of OGG file, which are located on the very last page of the file
        private ulong getSamples(BufferedBinaryReader source)
        {
            string headerId;
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
                found = StreamUtils.FindSequence(source, Utils.Latin1Encoding.GetBytes(OGG_PAGE_ID));
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

                headerId = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if (headerId.Equals(OGG_PAGE_ID))
                {
                    source.Seek(1, SeekOrigin.Current);
                    typeFlag = source.ReadByte();
                    source.Seek(20, SeekOrigin.Current);
                    nbLacingValues = source.ReadByte();
                    nextPageOffset = 0;
                    source.Read(lacingValues, 0, nbLacingValues);
                    for (int i = 0; i < nbLacingValues; i++)
                    {
                        nextPageOffset += lacingValues[i];
                    }
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
            bool isValidHeader = false;

            try
            {
                // Reads all Vorbis pages that describe metadata (i.e. Identification, Comment and Setup packets)
                // and concatenate their content into a single, continuous data stream
                OggPageHeader pageHeader;
                source.Seek(0, SeekOrigin.Begin);
                do
                {
                    pageOffsets.Add(source.Position);

                    pageHeader = new OggPageHeader();
                    pageHeader.ReadFromStream(source);

                    MemoryStream stream;
                    if (bitstreams.ContainsKey(pageHeader.StreamId))
                    {
                        stream = bitstreams[pageHeader.StreamId];
                        if (pageHeader.IsFirstPage())
                        {
                            int newPageCount = pageCount[pageHeader.StreamId] + 1;
                            pageCount[pageHeader.StreamId] = newPageCount;
                            if (2 == newPageCount) info.CommentHeaderStart = source.Position - pageHeader.GetHeaderSize();
                            else if (3 == newPageCount) info.SetupHeaderEnd = source.Position - pageHeader.GetHeaderSize();
                        }
                    }
                    else
                    {
                        stream = new MemoryStream();
                        bitstreams.Add(pageHeader.StreamId, stream);
                        pageCount.Add(pageHeader.StreamId, 1);
                    }

                    if (pageCount[pageHeader.StreamId] < 3) stream.Write(source.ReadBytes(pageHeader.GetPageSize()), 0, pageHeader.GetPageSize());
                } while (pageCount[pageHeader.StreamId] < 3);

                AudioDataOffset = info.SetupHeaderEnd; // Not exactly true as audio is useless without the setup header
                AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

                if (readTagParams.PrepareForWriting) // Metrics to prepare writing
                {
                    if (CONTENTS_VORBIS == contents || CONTENTS_FLAC == contents)
                    {
                        // Determine the boundaries of 3rd header (Setup header) by searching from the last-but-one page
                        if (pageOffsets.Count > 1) source.Position = pageOffsets[pageOffsets.Count - 2]; else source.Position = pageOffsets[0];
                        if (StreamUtils.FindSequence(source, Utils.Latin1Encoding.GetBytes(VORBIS_SETUP_ID)))
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
                                    if (info.SetupHeaderEnd <= pageOffsets[i]) info.SetupHeaderSpanPages = i - firstSetupPage;
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
                    }
                    else if (CONTENTS_OPUS == contents)
                    {
                        info.SetupHeaderStart = info.SetupHeaderEnd;
                        info.CommentHeaderEnd = info.SetupHeaderStart;
                        info.CommentHeaderSpanPages = pageOffsets.Count;
                        info.SetupHeaderSpanPages = 0;
                    }
                }

                // Get total number of samples
                info.Samples = getSamples(source);

                // Read through all streams to detect audio ones
                foreach (KeyValuePair<int, MemoryStream> kvp in bitstreams)
                {
                    using (BinaryReader reader = new BinaryReader(kvp.Value))
                    {
                        reader.BaseStream.Position = 0;

                        bool isSupported = readIdentificationPacket(reader);
                        if (!isSupported) continue;

                        isValidHeader = true;

                        info.AudioStreamId = kvp.Key;

                        readCommentPacket(reader, contents, vorbisTag, readTagParams);
                    }
                }
            }
            finally
            {
                // Liberate all resources
                foreach (KeyValuePair<int, MemoryStream> entry in bitstreams)
                {
                    entry.Value.Close();
                }
            }
            return isValidHeader;
        }

        private bool readIdentificationPacket(BinaryReader source)
        {
            bool isSupportedHeader = false;
            long initialOffset = source.BaseStream.Position;

            string headerStart = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
            source.BaseStream.Seek(initialOffset, SeekOrigin.Begin);

            if (VORBIS_HEADER_ID.StartsWith(headerStart))
            {
                contents = CONTENTS_VORBIS;
                info.VorbisParameters.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(7));
                isSupportedHeader = VORBIS_HEADER_ID.Equals(info.VorbisParameters.ID);

                info.VorbisParameters.BitstreamVersion = source.ReadBytes(4);
                info.VorbisParameters.ChannelMode = source.ReadByte();
                info.VorbisParameters.SampleRate = source.ReadInt32();
                info.VorbisParameters.BitRateMaximal = source.ReadInt32();
                info.VorbisParameters.BitRateNominal = source.ReadInt32();
                info.VorbisParameters.BitRateMinimal = source.ReadInt32();
                info.VorbisParameters.BlockSize = source.ReadByte();
                info.VorbisParameters.StopFlag = source.ReadByte();
            }
            else if (OPUS_HEADER_ID.StartsWith(headerStart))
            {
                contents = CONTENTS_OPUS;
                info.OpusParameters.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(8));
                isSupportedHeader = OPUS_HEADER_ID.Equals(info.OpusParameters.ID);

                info.OpusParameters.Version = source.ReadByte();
                info.OpusParameters.OutputChannelCount = source.ReadByte();
                info.OpusParameters.PreSkip = source.ReadUInt16();
                info.OpusParameters.InputSampleRate = 48000; // Actual sample rate is hardware-dependent. Let's assume for now that the hardware ATL runs on supports 48KHz
                source.BaseStream.Seek(4, SeekOrigin.Current);
                info.OpusParameters.OutputGain = source.ReadInt16();

                info.OpusParameters.ChannelMappingFamily = source.ReadByte();

                if (info.OpusParameters.ChannelMappingFamily > 0)
                {
                    info.OpusParameters.StreamCount = source.ReadByte();
                    info.OpusParameters.CoupledStreamCount = source.ReadByte();

                    info.OpusParameters.ChannelMapping = new byte[info.OpusParameters.OutputChannelCount];
                    for (int i = 0; i < info.OpusParameters.OutputChannelCount; i++)
                    {
                        info.OpusParameters.ChannelMapping[i] = source.ReadByte();
                    }
                }
            }
            else if (FLAC_HEADER_ID.StartsWith(headerStart))
            {
                contents = CONTENTS_FLAC;
                source.BaseStream.Seek(FLAC_HEADER_ID.Length, SeekOrigin.Current); // Skip the entire FLAC segment header
                source.BaseStream.Seek(2, SeekOrigin.Current); // FLAC-to-Ogg mapping version
                short nbHeaderPackets = StreamUtils.DecodeBEInt16(source.ReadBytes(2));
                info.FlacParameters = FlacHelper.readHeader(source.BaseStream);
                isSupportedHeader = info.FlacParameters.IsValid();
            }
            else if (THEORA_HEADER_ID.StartsWith(headerStart))
            {
                // ATL doesn't support video data; stop examining this bitstream
            }
            return isSupportedHeader;
        }

        private static void readCommentPacket(BinaryReader source, int contentType, VorbisTag tag, ReadTagParams readTagParams)
        {
            string tagId;
            bool isValidTagHeader = false;
            if (contentType.Equals(CONTENTS_VORBIS))
            {
                tagId = Utils.Latin1Encoding.GetString(source.ReadBytes(7));
                isValidTagHeader = VORBIS_COMMENT_ID.Equals(tagId);
            }
            else if (contentType.Equals(CONTENTS_OPUS))
            {
                tagId = Utils.Latin1Encoding.GetString(source.ReadBytes(8));
                isValidTagHeader = OPUS_TAG_ID.Equals(tagId);
            }
            else if (contentType.Equals(CONTENTS_FLAC))
            {
                byte[] aMetaDataBlockHeader = source.ReadBytes(4);
                uint blockLength = StreamUtils.DecodeBEUInt24(aMetaDataBlockHeader, 1);
                byte blockType = (byte)(aMetaDataBlockHeader[0] & 0x7F); // decode metablock type
                isValidTagHeader = blockType < 7;
            }

            if (isValidTagHeader)
            {
                tag.Clear();
                tag.Read(source, readTagParams);
            }
        }

        // Calculate duration time
        private double getDuration()
        {
            double result;

            if (samples > 0)
            {
                if (sampleRate > 0)
                    result = samples * 1000.0 / sampleRate;
                else
                    result = 0;
            }
            else if ((bitRateNominal > 0) && (channelsArrangement.NbChannels > 0))
            {
                result = 1000.0 * sizeInfo.FileSize / bitRateNominal / channelsArrangement.NbChannels / 125.0 * 2;
            }
            else
                result = 0;

            return result;
        }

        private double getBitRate()
        {
            // Calculate average bit rate
            double result = 0;

            if (getDuration() > 0) result = (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 8.0 / getDuration();

            return result;
        }

        private bool isValid()
        {
            // Check for file correctness
            return ((channelsArrangement.NbChannels > 0) && (sampleRate > 0) && (getDuration() > 0.1) && (getBitRate() > 0));
        }

        private static ChannelsArrangement getArrangementFromCode(int vorbisCode)
        {
            if (vorbisCode > 8) return new ChannelsArrangement(vorbisCode);
            else switch (vorbisCode)
                {
                    case 1: return MONO;
                    case 2: return STEREO;
                    case 3: return ISO_3_0_0;
                    case 4: return QUAD;
                    case 5: return ISO_3_2_0;
                    case 6: return ISO_3_2_1;
                    case 7: return LRCLFECrLssRss;
                    case 8: return LRCLFELrRrLssRss;
                    default: return UNKNOWN;
                }
        }

        // ---------------------------------------------------------------------------

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return Read(source, readTagParams);
        }

        public bool Read(BinaryReader source, ReadTagParams readTagParams)
        {
            bool result = false;

            BufferedBinaryReader reader = new BufferedBinaryReader(source.BaseStream);

            if (readTagParams.ReadTag && null == vorbisTag) vorbisTag = new VorbisTag(true, true, true, true);
            info.Reset();

            if (getInfo(reader, info, readTagParams))
            {
                if (contents.Equals(CONTENTS_VORBIS))
                {
                    channelsArrangement = getArrangementFromCode(info.VorbisParameters.ChannelMode);
                    sampleRate = info.VorbisParameters.SampleRate;
                    bitRateNominal = (ushort)(info.VorbisParameters.BitRateNominal / 1000); // Integer division
                }
                else if (contents.Equals(CONTENTS_OPUS))
                {
                    channelsArrangement = getArrangementFromCode(info.OpusParameters.OutputChannelCount);
                    sampleRate = (int)info.OpusParameters.InputSampleRate;
                    // No nominal bitrate for OPUS
                }
                else if (contents.Equals(CONTENTS_FLAC))
                {
                    channelsArrangement = info.FlacParameters.getChannelsArrangement();
                    sampleRate = info.FlacParameters.getSampleRate();
                    // No nominal bitrate for FLAC
                }

                samples = info.Samples;

                result = true;
            }
            return result;
        }

        // Specific implementation for OGG container (multiple pages with limited size)

        // TODO DOC
        // Simplified implementation of MetaDataIO tweaked for OGG-Vorbis specifics, i.e.
        //  - tag spans over multiple pages, each having its own header
        //  - last page may include whole or part of Vorbis Setup header

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            bool result = true;
            int writtenPages = 0;
            long nextPageOffset = 0;

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(true, true);
            readTagParams.PrepareForWriting = true;
            Read(r, readTagParams);

            if (CONTENTS_FLAC == contents) throw new NotImplementedException("Writing is not supported yet for embedded FLAC");

            // Create the "unpaged" virtual stream to be written, containing the vorbis tag (=comment header)
            using (MemoryStream memStream = new MemoryStream((int)(info.SetupHeaderEnd - info.CommentHeaderStart)))
            {
                if (CONTENTS_VORBIS == contents)
                    memStream.Write(Utils.Latin1Encoding.GetBytes(VORBIS_COMMENT_ID), 0, VORBIS_COMMENT_ID.Length);
                else if (CONTENTS_OPUS == contents)
                    memStream.Write(Utils.Latin1Encoding.GetBytes(OPUS_TAG_ID), 0, OPUS_TAG_ID.Length);

                vorbisTag.Write(memStream, tag);

                long newTagSize = memStream.Position;

                int setupHeaderSize = 0;
                int setupHeader_nbSegments = 0;
                byte setupHeader_remainingBytesInLastSegment = 0;

                // VORBIS: Append the setup header in the "unpaged" virtual stream
                if (CONTENTS_VORBIS == contents)
                {
                    r.BaseStream.Seek(info.SetupHeaderStart, SeekOrigin.Begin);
                    if (1 == info.SetupHeaderSpanPages)
                    {
                        setupHeaderSize = (int)(info.SetupHeaderEnd - info.SetupHeaderStart);
                        StreamUtils.CopyStream(r.BaseStream, memStream, setupHeaderSize);
                    }
                    else
                    {
                        // TODO - handle case where initial setup header spans across two pages
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "ATL does not yet handle the case where Vorbis setup header spans across two OGG pages");
                        return false;
                    }
                    setupHeader_nbSegments = (int)Math.Ceiling(1.0 * setupHeaderSize / 255);
                    setupHeader_remainingBytesInLastSegment = (byte)(setupHeaderSize % 255);
                }

                // Construct the entire segments table
                int commentsHeader_nbSegments = (int)Math.Ceiling(1.0 * newTagSize / 255);
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

                int nbPageHeaders = (int)Math.Ceiling((commentsHeader_nbSegments + setupHeader_nbSegments) / 255.0);
                int totalPageHeadersSize = (nbPageHeaders * 27) + commentsHeader_nbSegments + setupHeader_nbSegments;


                // Resize the whole virtual stream once and for all to avoid multiple reallocations while repaging
                memStream.SetLength(memStream.Position + totalPageHeadersSize);


                // Repage comments header & setup header within the virtual stream
                memStream.Seek(0, SeekOrigin.Begin);

                OggPageHeader header = new OggPageHeader()
                {
                    ID = OGG_PAGE_ID,
                    StreamVersion = 0, // Constant
                    TypeFlag = 0,
                    AbsolutePosition = ulong.MaxValue,
                    StreamId = info.AudioStreamId,
                    PageNumber = 1,
                    Checksum = 0
                };

                int segmentsLeftToPage = commentsHeader_nbSegments + setupHeader_nbSegments;
                int bytesLeftToPage = (int)newTagSize + setupHeaderSize;
                int pagedSegments = 0;
                int pagedBytes = 0;
                long position;

                BinaryWriter virtualW = new BinaryWriter(memStream);
                IList<KeyValuePair<long, int>> pageHeaderOffsets = new List<KeyValuePair<long, int>>();

                // Repaging
                while (segmentsLeftToPage > 0)
                {
                    header.Segments = (byte)Math.Min(255, segmentsLeftToPage);
                    header.LacingValues = new byte[header.Segments];
                    if (segmentsLeftToPage == header.Segments) header.AbsolutePosition = 0; // Last header page has its absolutePosition = 0

                    Array.Copy(entireSegmentsTable, pagedSegments, header.LacingValues, 0, header.Segments);

                    position = memStream.Position;
                    // Push current data to write header
                    StreamUtils.CopySameStream(memStream, memStream.Position, memStream.Position + header.GetHeaderSize(), bytesLeftToPage);
                    memStream.Seek(position, SeekOrigin.Begin);

                    pageHeaderOffsets.Add(new KeyValuePair<long, int>(position, header.GetPageSize() + header.GetHeaderSize()));

                    header.WriteToStream(virtualW);
                    memStream.Seek(header.GetPageSize(), SeekOrigin.Current);

                    pagedSegments += header.Segments;
                    segmentsLeftToPage -= header.Segments;
                    pagedBytes += header.GetPageSize();
                    bytesLeftToPage -= header.GetPageSize();

                    header.PageNumber++;
                    if (0 == header.TypeFlag) header.TypeFlag = 1;
                }
                writtenPages = header.PageNumber - 1;


                // Generate CRC32 of created pages
                uint crc;
                byte[] data;
                foreach (KeyValuePair<long, int> kv in pageHeaderOffsets)
                {
                    crc = 0;
                    memStream.Seek(kv.Key, SeekOrigin.Begin);
                    data = new byte[kv.Value];
                    memStream.Read(data, 0, kv.Value);
                    crc = OggCRC32.CalculateCRC(crc, data, (uint)kv.Value);
                    memStream.Seek(kv.Key + 22, SeekOrigin.Begin); // Position of CRC within OGG header
                    virtualW.Write(crc);
                }

                // Insert the virtual paged stream into the actual file
                long oldHeadersSize = info.SetupHeaderEnd - info.CommentHeaderStart;
                long newHeadersSize = memStream.Length;

                if (newHeadersSize > oldHeadersSize) // Need to build a larger file
                {
                    StreamUtils.LengthenStream(w.BaseStream, info.CommentHeaderEnd, (uint)(newHeadersSize - oldHeadersSize));
                }
                else if (newHeadersSize < oldHeadersSize) // Need to reduce file size
                {
                    StreamUtils.ShortenStream(w.BaseStream, info.CommentHeaderEnd, (uint)(oldHeadersSize - newHeadersSize));
                }

                // Rewrite Comment and Setup headers
                w.BaseStream.Seek(info.CommentHeaderStart, SeekOrigin.Begin);
                memStream.Seek(0, SeekOrigin.Begin);

                StreamUtils.CopyStream(memStream, w.BaseStream);

                nextPageOffset = info.CommentHeaderStart + memStream.Length;
            }

            // If the number of written pages is different than the number of previous existing pages,
            // all the next pages of the file need to be renumbered, and their CRC accordingly recalculated
            if (writtenPages != info.CommentHeaderSpanPages + info.SetupHeaderSpanPages - 1)
            {
                OggPageHeader header = new OggPageHeader();
                byte[] data;
                uint crc;

                do
                {
                    w.BaseStream.Seek(nextPageOffset, SeekOrigin.Begin);
                    header.ReadFromStream(r);

                    if (header.IsValid())
                    {
                        // Rewrite page number
                        writtenPages++;
                        w.BaseStream.Seek(nextPageOffset + 18, SeekOrigin.Begin);
                        w.Write(writtenPages);

                        // Rewrite CRC
                        w.BaseStream.Seek(nextPageOffset, SeekOrigin.Begin);
                        data = new byte[header.GetHeaderSize() + header.GetPageSize()];
                        r.Read(data, 0, data.Length);

                        // Checksum has to include its own location, as if it were 0
                        data[22] = 0;
                        data[23] = 0;
                        data[24] = 0;
                        data[25] = 0;

                        crc = OggCRC32.CalculateCRC(0, data, (uint)data.Length);
                        r.BaseStream.Seek(nextPageOffset + 22, SeekOrigin.Begin); // Position of CRC within OGG header
                        w.Write(crc);

                        // To the next header
                        nextPageOffset += data.Length;
                    }
                    else
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Invalid OGG header found; aborting writing operation"); // Throw exception ?
                        return false;
                    }

                } while (0 == (header.TypeFlag & 0x04));  // 0x04 marks the last page of the logical bitstream
            }

            return result;
        }

        public bool Remove(BinaryWriter w)
        {
            TagData tag = vorbisTag.GetDeletionTagData();

            BinaryReader r = new BinaryReader(w.BaseStream);
            return Write(r, w, tag);
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