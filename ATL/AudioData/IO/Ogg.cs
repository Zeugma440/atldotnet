using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OGG files manipulation. Current implementation covers :
    ///   - Vorbis data (extensions : .OGG)
    ///   - Opus data (extensions : .OPUS)
    ///   
    /// Implementation notes
    /// 
    ///   1. CRC's : Current implementation does not test OGG page header CRC's
    ///   2. Page numbers : Current implementation does not test page numbers consistency
    /// 
    /// </summary>
	class Ogg : IAudioDataIO, IMetaDataIO
    {
        // Contents of the file
        private const int CONTENTS_UNSUPPORTED = -1;	    // Unsupported
        private const int CONTENTS_VORBIS = 0;				// Vorbis
        private const int CONTENTS_OPUS = 1;                // Opus

        private const int MAX_PAGE_SIZE = 255 * 255;

        // Ogg page header ID
        private const String OGG_PAGE_ID = "OggS";

        // Vorbis identification packet (frame) ID
        private static readonly String VORBIS_HEADER_ID = (char)1 + "vorbis";

        // Vorbis tag packet (frame) ID
        private static readonly String VORBIS_TAG_ID = (char)3 + "vorbis";

        // Vorbis setup packet (frame) ID
        private static readonly String VORBIS_SETUP_ID = (char)5 + "vorbis";

        // Vorbis parameter frame ID
        private const String OPUS_HEADER_ID = "OpusHead";

        // Opus tag frame ID
        private const String OPUS_TAG_ID = "OpusTags";



        private readonly string filePath;
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
            get { return this.sampleRate; }
        }
        public ushort BitRateNominal // Nominal bit rate
        {
            get { return this.bitRateNominal; }
        }
        public bool Valid // True if file valid
        {
            get { return this.isValid(); }
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
            get { return true; }
        }


        // Ogg page header
        private class OggHeader
        {
            public string ID;                                               // Always "OggS"
            public byte StreamVersion;                           // Stream structure version
            public byte TypeFlag;                                        // Header type flag
            public ulong AbsolutePosition;                      // Absolute granule position
            public int Serial;                                       // Stream serial number
            public int PageNumber;                                   // Page sequence number
            public uint Checksum;                                              // Page CRC32
            public byte Segments;                                 // Number of page segments
            public byte[] LacingValues;                     // Lacing values - segment sizes

            public void Reset()
            {
                ID = "";
                StreamVersion = 0;
                TypeFlag = 0;
                AbsolutePosition = 0;
                Serial = 0;
                PageNumber = 0;
                Checksum = 0;
                Segments = 0;
            }

            public void ReadFromStream(BufferedBinaryReader r)
            {
                ID = Utils.Latin1Encoding.GetString(r.ReadBytes(4));
                StreamVersion = r.ReadByte();
                TypeFlag = r.ReadByte();
                AbsolutePosition = r.ReadUInt64();
                Serial = r.ReadInt32();
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
                Serial = r.ReadInt32();
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
                w.Write(Serial);
                w.Write(PageNumber);
                w.Write(Checksum);
                w.Write(Segments);
                w.Write(LacingValues);
            }

            public int GetPageLength()
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
                return ((ID != null) && ID.Equals(OGG_PAGE_ID));
            }
        }

        // Vorbis parameter header
        private class VorbisHeader
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
        private class OpusHeader
        {
            public String ID;
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

        // File data
        private class FileInfo
        {
            // First, second and third Vorbis packets
            public OggHeader IdentificationHeader = new OggHeader();
            public OggHeader CommentHeader = new OggHeader();
            public OggHeader SetupHeader = new OggHeader();

            // Following two properties are mutually exclusive
            // TODO - handle Theora
            public VorbisHeader VorbisParameters = new VorbisHeader();  // Vorbis parameter header
            public OpusHeader OpusParameters = new OpusHeader();        // Opus parameter header

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
                IdentificationHeader.Reset();
                CommentHeader.Reset();
                SetupHeader.Reset();

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

            info.Reset();
        }

        public Ogg(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
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

        public string Year
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Year;
            }
        }

        public string Album
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Album;
            }
        }

        public float Popularity
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

        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE); // According to id3.org (FAQ), ID3 is not compatible with OGG. Hence ATL does not allow ID3 tags to be written on OGG files; native is for VorbisTag
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
            // Get info from file
            bool result = false;
            bool isValidHeader = false;

            // Check for ID3v2 (NB : this case should not even exist since OGG has its own native tagging system, and is not deemed compatible with ID3v2 according to the ID3 FAQ)
            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // Read global file header
            info.IdentificationHeader.ReadFromStream(source);

            if (info.IdentificationHeader.IsValid())
            {
                source.Seek(sizeInfo.ID3v2Size + info.IdentificationHeader.Segments + 27, SeekOrigin.Begin); // 27 being the size from 'ID' to 'Segments'

                // Read Vorbis or Opus stream info
                long position = source.Position;

                String headerStart = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
                source.Seek(position, SeekOrigin.Begin);
                if (VORBIS_HEADER_ID.StartsWith(headerStart))
                {
                    contents = CONTENTS_VORBIS;
                    info.VorbisParameters.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(7));
                    isValidHeader = VORBIS_HEADER_ID.Equals(info.VorbisParameters.ID);

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
                    isValidHeader = OPUS_HEADER_ID.Equals(info.OpusParameters.ID);

                    info.OpusParameters.Version = source.ReadByte();
                    info.OpusParameters.OutputChannelCount = source.ReadByte();
                    info.OpusParameters.PreSkip = source.ReadUInt16();
                    //info.OpusParameters.InputSampleRate = source.ReadUInt32();
                    info.OpusParameters.InputSampleRate = 48000; // Actual sample rate is hardware-dependent. Let's assume for now that the hardware ATL runs on supports 48KHz
                    source.Seek(4, SeekOrigin.Current);
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

                if (isValidHeader)
                {
                    info.CommentHeaderStart = source.Position;
                    IList<long> pageOffsets = new List<long>();

                    // Reads all related Vorbis pages that describe Comment and Setup headers
                    // and concatenate their content into a single, continuous data stream
                    bool loop = true;
                    bool first = true;
                    using (MemoryStream s = new MemoryStream())
                    {
                        // Reconstruct the whole Comment header from OGG pages to a MemoryStream
                        while (loop && source.Position < source.Length)
                        {
                            info.SetupHeaderEnd = source.Position; // When the loop stops, cursor is starting to read a brand new page located after Comment _and_ Setup headers
                            info.CommentHeader.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                            info.CommentHeader.StreamVersion = source.ReadByte();
                            info.CommentHeader.TypeFlag = source.ReadByte();
                            // 0 marks a new page
                            if (0 == info.CommentHeader.TypeFlag)
                            {
                                loop = first;
                            }
                            if (loop)
                            {
                                info.CommentHeader.AbsolutePosition = source.ReadUInt64();
                                info.CommentHeader.Serial = source.ReadInt32();
                                info.CommentHeader.PageNumber = source.ReadInt32();
                                info.CommentHeader.Checksum = source.ReadUInt32();
                                info.CommentHeader.Segments = source.ReadByte();
                                info.CommentHeader.LacingValues = source.ReadBytes(info.CommentHeader.Segments);
                                s.Write(source.ReadBytes(info.CommentHeader.GetPageLength()), 0, info.CommentHeader.GetPageLength());
                                pageOffsets.Add(info.SetupHeaderEnd);
                            }
                            first = false;
                        }

                        if (readTagParams.PrepareForWriting) // Metrics to prepare writing
                        {
                            if (CONTENTS_VORBIS == contents)
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
                                            if (info.SetupHeaderEnd < pageOffsets[i]) info.SetupHeaderSpanPages = i - firstSetupPage;
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

                        // Read metadata from the reconstructed Comment header inside the memoryStream
                        if (readTagParams.ReadTag)
                        {
                            BinaryReader msr = new BinaryReader(s);
                            s.Seek(0, SeekOrigin.Begin);

                            string tagId;
                            bool isValidTagHeader = false;
                            if (contents.Equals(CONTENTS_VORBIS))
                            {
                                tagId = Utils.Latin1Encoding.GetString(msr.ReadBytes(7));
                                isValidTagHeader = (VORBIS_TAG_ID.Equals(tagId));
                            }
                            else if (contents.Equals(CONTENTS_OPUS))
                            {
                                tagId = Utils.Latin1Encoding.GetString(msr.ReadBytes(8));
                                isValidTagHeader = (OPUS_TAG_ID.Equals(tagId));
                            }

                            if (isValidTagHeader)
                            {
                                vorbisTag.Clear();
                                vorbisTag.Read(msr, readTagParams);
                            }
                        }
                    } // using MemoryStream

                    result = true;
                }
            }

            return result;
        }

        // Calculate duration time
        private double getDuration()
        {
            double result;

            if (samples > 0)
                if (sampleRate > 0)
                    result = ((double)samples * 1000.0 / sampleRate);
                else
                    result = 0;
            else
                if ((bitRateNominal > 0) && (channelsArrangement.NbChannels > 0))
                result = (1000.0 * (double)sizeInfo.FileSize - sizeInfo.ID3v2Size) / (double)bitRateNominal / channelsArrangement.NbChannels / 125.0 * 2;
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

                samples = info.Samples;

                result = true;
            }
            return result;
        }

        // Specific implementation for OGG container (multiple pages with limited size)

        // TODO DOC
        // Simplified implementation of MetaDataIO tweaked for OGG-Vorbis specifics, i.e.
        //  - tag spans over multiple pages, each having its own header
        //  - last page may include whole or part of 3rd Vorbis header (setup header)

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            bool result = true;
            int writtenPages = 0;
            long nextPageOffset = 0;
            float currentProgress = 0;

//            if (writeProgress != null) writeProgress.Report(currentProgress / 4);

            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(true, true);
            readTagParams.PrepareForWriting = true;
            Read(r, readTagParams);

            // Create the "unpaged" virtual stream to be written, containing the vorbis tag (=comment header)
            using (MemoryStream memStream = new MemoryStream((int)(info.SetupHeaderEnd - info.CommentHeaderStart)))
            {
                if (CONTENTS_VORBIS == contents)
                    memStream.Write(Utils.Latin1Encoding.GetBytes(VORBIS_TAG_ID), 0, VORBIS_TAG_ID.Length);
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

                OggHeader header = new OggHeader()
                {
                    ID = OGG_PAGE_ID,
                    StreamVersion = info.CommentHeader.StreamVersion,
                    TypeFlag = 0,
                    AbsolutePosition = ulong.MaxValue,
                    Serial = info.CommentHeader.Serial,
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

                    pageHeaderOffsets.Add(new KeyValuePair<long, int>(position, header.GetPageLength() + header.GetHeaderSize()));

                    header.WriteToStream(virtualW);
                    memStream.Seek(header.GetPageLength(), SeekOrigin.Current);

                    pagedSegments += header.Segments;
                    segmentsLeftToPage -= header.Segments;
                    pagedBytes += header.GetPageLength();
                    bytesLeftToPage -= header.GetPageLength();

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

//                if (writeProgress != null) writeProgress.Report(++currentProgress / 4);


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
//                if (writeProgress != null) writeProgress.Report(++currentProgress / 4);

                // Rewrite Comment and Setup headers
                w.BaseStream.Seek(info.CommentHeaderStart, SeekOrigin.Begin);
                memStream.Seek(0, SeekOrigin.Begin);

                StreamUtils.CopyStream(memStream, w.BaseStream);
//                if (writeProgress != null) writeProgress.Report(++currentProgress / 4);

                nextPageOffset = info.CommentHeaderStart + memStream.Length;
            }

            // If the number of written pages is different than the number of previous existing pages,
            // all the next pages of the file need to be renumbered, and their CRC accordingly recalculated
            if (writtenPages != info.CommentHeaderSpanPages + info.SetupHeaderSpanPages - 1)
            {
                OggHeader header = new OggHeader();
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
                        data = new byte[header.GetHeaderSize() + header.GetPageLength()];
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
//                if (writeProgress != null) writeProgress.Report(++currentProgress / 4);
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