using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for MP4 files manipulation (extensions : .MP4, .M4A, .M4B, .M4V)
    /// 
    /// Implementation notes
    /// 
    /// </summary>
	class MP4 : MetaDataIO, IAudioDataIO
    {
        // Header type codes
        public const byte MP4_HEADER_TYPE_UNKNOWN = 0;                       // Unknown
        public const byte MP4_HEADER_TYPE_MP4 = 3;                           // MP4

        // Bit rate type codes
        public const byte MP4_BITRATE_TYPE_UNKNOWN = 0;                      // Unknown
        public const byte MP4_BITRATE_TYPE_CBR = 1;                          // CBR
        public const byte MP4_BITRATE_TYPE_VBR = 2;                          // VBR

        // Bit rate type names
        public static readonly string[] MP4_BITRATE_TYPE = { "Unknown", "CBR", "VBR" };

        private static readonly byte[] ILST_CORE_SIGNATURE = { 0, 0, 0, 8, 105, 108, 115, 116 }; // (int32)8 followed by "ilst" field code

        private const string ZONE_MP4_NOMETA = "nometa";        // When the whole 'meta' atom is missing
        private const string ZONE_MP4_ILST = "ilst";            // When editing a file with an existing 'meta' atom
        private const string ZONE_MP4_CHPL = "chpl";            // Nero chapters
        private const string ZONE_MP4_PHYSICAL_CHUNK = "chunk"; // Physical audio chunk referenced from stco or co64

        // Mapping between MP4 frame codes and ATL frame codes
        private static Dictionary<string, byte> frameMapping_mp4 = new Dictionary<string, byte>() {
            { "©nam", TagData.TAG_FIELD_TITLE },
            { "titl", TagData.TAG_FIELD_TITLE },
            { "©alb", TagData.TAG_FIELD_ALBUM },
            { "©ART", TagData.TAG_FIELD_ARTIST },
            { "©art", TagData.TAG_FIELD_ARTIST },
            { "©cmt", TagData.TAG_FIELD_COMMENT },
            { "©day", TagData.TAG_FIELD_RECORDING_YEAR_OR_DATE },
            { "©gen", TagData.TAG_FIELD_GENRE },
            { "gnre", TagData.TAG_FIELD_GENRE },
            { "trkn", TagData.TAG_FIELD_TRACK_NUMBER_TOTAL },
            { "disk", TagData.TAG_FIELD_DISC_NUMBER_TOTAL },
            { "rtng", TagData.TAG_FIELD_RATING },
            { "rate", TagData.TAG_FIELD_RATING },
            { "©wrt", TagData.TAG_FIELD_COMPOSER },
            { "desc", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
            { "cprt", TagData.TAG_FIELD_COPYRIGHT },
            { "aART", TagData.TAG_FIELD_ALBUM_ARTIST },
            { "©lyr", TagData.TAG_FIELD_LYRICS_UNSYNCH },
            { "----:com.apple.iTunes:CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR }
        };

        // Mapping between MP4 frame codes and frame classes that aren't class 1 (UTF-8 text)
        private static Dictionary<string, byte> frameClasses_mp4 = new Dictionary<string, byte>() {
            { "gnre", 0 },
            { "trkn", 0 },
            { "disk", 0 },
            { "rtng", 21 },
            { "tmpo", 21 },
            { "cpil", 21 },
            { "stik", 21 },
            { "pcst", 21 },
            { "purl", 0 },
            { "egid", 0 },
            { "tvsn", 21 },
            { "tves", 21 },
            { "pgap", 21 }
        };

        private class MP4Sample
        {
            public double Duration;
            public uint Size;
            public uint ChunkIndex;                     // 1-based index
            public long ChunkOffset;
            public long RelativeOffset;
        }

        private byte headerTypeID;
        private byte bitrateTypeID;
        private double bitrate;
        private double calculatedDuration;
        private int sampleRate;
        private ChannelsArrangement channelsArrangement;

        private AudioDataManager.SizeInfo sizeInfo;
        private readonly string fileName;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR
        {
            get { return (MP4_BITRATE_TYPE_VBR == bitrateTypeID); }
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public double BitRate
        {
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }
        public string FileName
        {
            get { return fileName; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            // NB : MP4/M4A theoretically doesn't support ID3v2
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2) || (metaDataType == MetaDataIOFactory.TAG_APE) || (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }
        public override byte FieldCodeFixedLength
        {
            get { return 4; }
        }
        protected override bool isLittleEndian
        {
            get { return false; }
        }
        protected override byte ratingConvention
        {
            get { return RC_APE; }
        }
        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            if (frameMapping_mp4.ContainsKey(ID)) supportedMetaId = frameMapping_mp4[ID];

            return supportedMetaId;
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            headerTypeID = MP4_HEADER_TYPE_UNKNOWN;
            bitrateTypeID = MP4_BITRATE_TYPE_UNKNOWN;

            bitrate = 0;
            sampleRate = 0;
            calculatedDuration = 0;
        }

        public MP4(string fileName)
        {
            this.fileName = fileName;
            resetData();
        }

        // ********************** Private functions & procedures *********************

        private static void addFrameClass(string frameCode, byte frameClass)
        {
            if (!frameClasses_mp4.ContainsKey(frameCode)) frameClasses_mp4.Add(frameCode, frameClass);
        }

        // Calculate duration time
        private double getDuration()
        {
            if (headerTypeID == MP4_HEADER_TYPE_MP4)
            {
                return calculatedDuration;
            }
            else
            {
                if (0 == bitrate)
                    return 0;
                else
                    return 8.0 * (sizeInfo.FileSize - sizeInfo.TotalTagSize) * 1000 / bitrate;
            }
        }

        // Get header type of the file
        private byte recognizeHeaderType(BinaryReader Source)
        {
            byte result;
            string headerStr;

            result = MP4_HEADER_TYPE_UNKNOWN;
            Source.BaseStream.Seek(sizeInfo.ID3v2Size + 4, SeekOrigin.Begin);
            headerStr = Utils.Latin1Encoding.GetString(Source.ReadBytes(4)); // bytes 4 to 8
            if ("ftyp".Equals(headerStr)) result = MP4_HEADER_TYPE_MP4;

            return result;
        }

        private void readQTChapters(BinaryReader source, IList<MP4Sample> chapterTrackSamples)
        {
            tagExists = true;
            if (null == tagData.Chapters) tagData.Chapters = new List<ChapterInfo>(); else tagData.Chapters.Clear();
            double cumulatedDuration = 0;

            foreach (MP4Sample sample in chapterTrackSamples)
            {
                if (sample.ChunkOffset > 0)
                {
                    ChapterInfo chapter = new ChapterInfo();

                    source.BaseStream.Seek(sample.ChunkOffset + sample.RelativeOffset, SeekOrigin.Begin);
                    ushort strDataSize = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

                    chapter.Title = Encoding.UTF8.GetString(source.ReadBytes(strDataSize));
                    chapter.StartTime = (uint)Math.Round(cumulatedDuration);
                    cumulatedDuration += sample.Duration * 1000;
                    chapter.EndTime = (uint)Math.Round(cumulatedDuration);

                    tagData.Chapters.Add(chapter);
                }
            }
        }

        /// <summary>
        /// Read MP4 header data
        /// http://www.jiscdigitalmedia.ac.uk/guide/aac-audio-and-the-mp4-media-format
        /// http://atomicparsley.sourceforge.net/mpeg-4files.html
        /// - Metadata is located in the moov/udta/meta/ilst atom
        /// - Physical information are located in the moov/trak atom (to be confirmed ?)
        /// - Binary physical data are located in the mdat atom
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="readTagParams">Reading parameters</param>
        private void readMP4(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            long moovPosition;
            int globalTimeScale;

            uint atomSize;

            byte[] data32 = new byte[4];

            IList<MP4Sample> chapterTrackSamples = new List<MP4Sample>();
            IDictionary<int, IList<int>> chapterTrackIndexes = new Dictionary<int, IList<int>>(); // Key is track index (1-based); lists are chapter tracks indexes (1-based)


            // TODO PERF - try and cache the whole tree structure to optimize browsing through nodes

            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // FTYP atom
            source.BaseStream.Read(data32, 0, 4);
            atomSize = StreamUtils.DecodeBEUInt32(data32);
            source.BaseStream.Seek(atomSize - 4, SeekOrigin.Current);

            // MOOV atom
            uint moovSize = lookForMP4Atom(source.BaseStream, "moov"); // === Physical data
            if (0 == moovSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "moov atom could not be found; aborting read");
                return;
            }

            moovPosition = source.BaseStream.Position;

            // === Physical data header
            if (0 == lookForMP4Atom(source.BaseStream, "mvhd"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mvhd atom could not be found; aborting read");
                return;
            }
            byte version = source.ReadByte();
            source.BaseStream.Seek(3, SeekOrigin.Current); // 3-byte flags
            if (1 == version) source.BaseStream.Seek(16, SeekOrigin.Current); else source.BaseStream.Seek(8, SeekOrigin.Current);

            globalTimeScale = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            long timeLengthPerSec;
            if (1 == version) timeLengthPerSec = StreamUtils.DecodeBEInt64(source.ReadBytes(8)); else timeLengthPerSec = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            calculatedDuration = timeLengthPerSec * 1000.0 / globalTimeScale;

            source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            byte currentTrakIndex = 0;
            long trakSize = 0;

            // Loop through tracks
            do
            {
                if (-1 == trakSize)
                {
                    currentTrakIndex = 0; // Convention to start reading from index 1 again
                    source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
                }
                trakSize = readTrack(source, readTagParams, ++currentTrakIndex, chapterTrackSamples, chapterTrackIndexes);
            }
            while (trakSize > 0);

            // QT chapters have been detected while browsing tracks
            if (chapterTrackSamples.Count > 0) readQTChapters(source, chapterTrackSamples);

            // Read user data which contains metadata and Nero chapters
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_NOMETA);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_ILST);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_CHPL);
            }
            source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            readUserData(source, readTagParams);

            // Seek the generic padding atom
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            uint initialPaddingSize = lookForMP4Atom(source.BaseStream, "free");
            if (initialPaddingSize > 0) tagData.PaddingSize = initialPaddingSize;

            if (readTagParams.PrepareForWriting)
            {
                // Padding atom found
                if (initialPaddingSize > 0)
                {
                    structureHelper.AddZone(source.BaseStream.Position - 8, (int)initialPaddingSize, PADDING_ZONE_NAME);
                    structureHelper.AddSize(source.BaseStream.Position - 8, (int)initialPaddingSize, PADDING_ZONE_NAME);
                }
                else // Padding atom not found
                {
                    if (Settings.AddNewPadding) // Create a virtual position to insert a new padding zone
                    {
                        structureHelper.AddZone(source.BaseStream.Position - 8, 0, PADDING_ZONE_NAME);
                        structureHelper.AddSize(source.BaseStream.Position - 8, 0, PADDING_ZONE_NAME);
                    }
                }
            }

            // Seek audio data segment to calculate mean bitrate 
            // NB : This figure is closer to truth than the "average bitrate" recorded in the esds/m4ds header
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            uint mdatSize = lookForMP4Atom(source.BaseStream, "mdat"); // === Audio binary data
            if (0 == mdatSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mdat atom could not be found; aborting read");
                return;
            }
            bitrate = (int)Math.Round(mdatSize * 8 / calculatedDuration * 1000.0, 0);
        }

        private long readTrack(
            BinaryReader source,
            MetaDataIO.ReadTagParams readTagParams,
            int currentTrakIndex,
            IList<MP4Sample> chapterTrackSamples,
            IDictionary<int, IList<int>> chapterTrackIndexes)
        {
            long trakPosition;
            int mediaTimeScale = 1000;

            long atomPosition;
            uint atomSize;

            uint int32Data = 0;
            byte[] data32 = new byte[4];
            byte[] data64 = new byte[8];

            bool isCurrentTrackFirstChapterTrack = false;

            uint trakSize = lookForMP4Atom(source.BaseStream, "trak");
            if (0 == trakSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "trak atom " + currentTrakIndex + " could not be found; aborting reading through tracks");
                return 0;
            }

            trakPosition = source.BaseStream.Position - 8;

            // Look for "chap" atom to detect QT chapters for current track
            if (lookForMP4Atom(source.BaseStream, "tref") > 0 && 0 == chapterTrackIndexes.Count)
            {
                bool parsePreviousTracks = false;
                uint chapSize = lookForMP4Atom(source.BaseStream, "chap");
                if (chapSize > 8)
                {
                    IList<int> thisTrackIndexes = new List<int>();
                    for (int i = 0; i < (chapSize - 8) / 4; i++)
                    {
                        thisTrackIndexes.Add(StreamUtils.DecodeBEInt32(source.ReadBytes(4)));
                    }
                    chapterTrackIndexes.Add(currentTrakIndex, thisTrackIndexes);

                    foreach (int i in thisTrackIndexes)
                    {
                        if (i < currentTrakIndex)
                        {
                            parsePreviousTracks = true;
                            break;
                        }
                    }
                }

                // If current track has declared a chapter track located at a previous index, come back to read it
                if (parsePreviousTracks)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_INFO, "detected chapter track located before read cursor; restarting reading tracks from track 1");
                    return -1;
                }
            }

            source.BaseStream.Seek(trakPosition + 8, SeekOrigin.Begin);
            if (0 == lookForMP4Atom(source.BaseStream, "mdia"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }

            long mdiaPosition = source.BaseStream.Position;
            if (chapterTrackIndexes.Count > 0)
            {
                if (0 == lookForMP4Atom(source.BaseStream, "mdhd"))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.mdhd atom could not be found; aborting read on track " + currentTrakIndex);
                    source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                    return trakSize;
                }

                byte mdhdVersion = source.ReadByte();
                source.BaseStream.Seek(3, SeekOrigin.Current); // Flags

                if (0 == mdhdVersion) source.BaseStream.Seek(8, SeekOrigin.Current); else source.BaseStream.Seek(16, SeekOrigin.Current); // Creation and modification date

                mediaTimeScale = StreamUtils.DecodeBEInt32(source.ReadBytes(4));

                source.BaseStream.Seek(mdiaPosition, SeekOrigin.Begin);
            }

            if (0 == lookForMP4Atom(source.BaseStream, "hdlr"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.hdlr atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
            source.BaseStream.Seek(4, SeekOrigin.Current); // Quicktime type
            string mediaType = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

            // Check if current track is the 1st chapter track
            // NB : Per convention, we will admit that the 1st track referenced in the 'chap' atom
            // contains the chapter names (as opposed to chapter URLs or chapter images)
            if ("text".Equals(mediaType) && chapterTrackIndexes.Count > 0)
            {
                foreach (IList<int> list in chapterTrackIndexes.Values)
                {
                    if (currentTrakIndex == list[0])
                    {
                        isCurrentTrackFirstChapterTrack = true;
                        break;
                    }
                }
            }

            source.BaseStream.Seek(mdiaPosition, SeekOrigin.Begin);
            if (0 == lookForMP4Atom(source.BaseStream, "minf"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.minf atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            if (0 == lookForMP4Atom(source.BaseStream, "stbl"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.minf.stbl atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            long stblPosition = source.BaseStream.Position;

            // Look for sample rate
            if (0 == lookForMP4Atom(source.BaseStream, "stsd"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "stsd atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            uint nbDescriptions = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

            for (int i = 0; i < nbDescriptions; i++)
            {
                int32Data = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // 4-byte description length
                string descFormat = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if (descFormat.Equals("mp4a") || descFormat.Equals("enca") || descFormat.Equals("samr") || descFormat.Equals("sawb"))
                {
                    source.BaseStream.Seek(6, SeekOrigin.Current); // SampleEntry / 6-byte reserved zone set to zero
                    source.BaseStream.Seek(2, SeekOrigin.Current); // SampleEntry / Data reference index

                    source.BaseStream.Seek(8, SeekOrigin.Current); // AudioSampleEntry / 8-byte reserved zone

                    ushort channels = StreamUtils.DecodeBEUInt16(source.ReadBytes(2)); // Channel count
                    channelsArrangement = GuessFromChannelNumber(channels);

                    source.BaseStream.Seek(2, SeekOrigin.Current); // Sample size
                    source.BaseStream.Seek(/*4*/2, SeekOrigin.Current); // Quicktime stuff (should be length 4, but sampleRate doesn't work if so...)

                    sampleRate = (int)StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                }
                else
                {
                    source.BaseStream.Seek(int32Data - 4, SeekOrigin.Current);
                }
            }

            // Read Quicktime chapters
            if (isCurrentTrackFirstChapterTrack)
            {
                source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
                if (0 == lookForMP4Atom(source.BaseStream, "stts"))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stts atom could not be found; aborting read on track " + currentTrakIndex);
                    source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                    return trakSize;
                }
                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                int32Data = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // Number of table entries
                if (int32Data > 0)
                {
                    uint frameCount, sampleDuration;
                    chapterTrackSamples.Clear();

                    for (int i = 0; i < int32Data; i++)
                    {
                        source.Read(data32, 0, 4);
                        frameCount = StreamUtils.DecodeBEUInt32(data32);
                        source.Read(data32, 0, 4);
                        sampleDuration = StreamUtils.DecodeBEUInt32(data32);
                        for (int j = 0; j < frameCount; j++)
                        {
                            MP4Sample sample = new MP4Sample();
                            sample.Duration = sampleDuration * 1.0 / mediaTimeScale;
                            chapterTrackSamples.Add(sample);
                        }
                    }
                }

                source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
                if (0 == lookForMP4Atom(source.BaseStream, "stsc"))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stsc atom could not be found; aborting read on track " + currentTrakIndex);
                    source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                    return trakSize;
                }
                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                int32Data = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // Number of table entries

                uint samplesPerChunk;
                int cumulatedSampleIndex = 0;
                uint chunkIndex = 0;
                uint previousChunkIndex = 0;
                uint previousSamplesPerChunk = 0;
                bool first = true;

                for (int i = 0; i < int32Data; i++)
                {
                    source.Read(data32, 0, 4);
                    chunkIndex = StreamUtils.DecodeBEUInt32(data32);
                    source.Read(data32, 0, 4);
                    samplesPerChunk = StreamUtils.DecodeBEUInt32(data32);
                    source.BaseStream.Seek(4, SeekOrigin.Current); // Sample description ID

                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        for (uint j = previousChunkIndex; j < chunkIndex; j++)
                        {
                            for (int k = 0; k < previousSamplesPerChunk; k++)
                            {
                                if (cumulatedSampleIndex < chapterTrackSamples.Count)
                                {
                                    chapterTrackSamples[cumulatedSampleIndex].ChunkIndex = j;
                                    cumulatedSampleIndex++;
                                }
                            }
                        }
                    }

                    previousChunkIndex = chunkIndex;
                    previousSamplesPerChunk = samplesPerChunk;
                }

                int remainingChunks = (int)Math.Ceiling((chapterTrackSamples.Count - cumulatedSampleIndex) * 1.0 / previousSamplesPerChunk);
                // Fill the rest of the in-memory table with the last pattern
                for (int j = 0; j < remainingChunks; j++)
                {
                    for (int k = 0; k < previousSamplesPerChunk; k++)
                    {
                        if (cumulatedSampleIndex < chapterTrackSamples.Count)
                        {
                            chapterTrackSamples[cumulatedSampleIndex].ChunkIndex = chunkIndex;
                            cumulatedSampleIndex++;
                        }
                    }
                    chunkIndex++;
                }
            } // End read Quicktime chapters

            source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);

            // VBR detection : if the gap between the smallest and the largest sample size is no more than 1%, we can consider the file is CBR; if not, VBR
            atomSize = lookForMP4Atom(source.BaseStream, "stsz");
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stsz atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            atomPosition = source.BaseStream.Position;
            source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            uint blocByteSizeForAll = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            if (0 == blocByteSizeForAll) // If value other than 0, same size everywhere => CBR
            {
                uint nbSizes = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                uint max = 0;
                uint min = UInt32.MaxValue;

                for (int i = 0; i < nbSizes; i++)
                {
                    source.Read(data32, 0, 4);
                    int32Data = StreamUtils.DecodeBEUInt32(data32);
                    min = Math.Min(min, int32Data);
                    max = Math.Max(max, int32Data);

                    if (isCurrentTrackFirstChapterTrack) chapterTrackSamples[i].Size = int32Data;
                }

                if ((min * 1.01) < max)
                {
                    bitrateTypeID = MP4_BITRATE_TYPE_VBR;
                }
                else
                {
                    bitrateTypeID = MP4_BITRATE_TYPE_CBR;
                }
            }
            else
            {
                bitrateTypeID = MP4_BITRATE_TYPE_CBR;
                if (isCurrentTrackFirstChapterTrack) for (int i = 0; i < chapterTrackSamples.Count; i++) chapterTrackSamples[i].Size = blocByteSizeForAll;
            }

            // Adjust individual sample offsets using their size for those that are in position > 0 in the same chunk
            if (isCurrentTrackFirstChapterTrack)
            {
                uint currentChunkIndex = uint.MaxValue;
                uint cumulatedChunkOffset = 0;

                for (int i = 0; i < chapterTrackSamples.Count; i++)
                {
                    if (chapterTrackSamples[i].ChunkIndex == currentChunkIndex)
                    {
                        chapterTrackSamples[i].RelativeOffset = cumulatedChunkOffset;
                    }
                    else
                    {
                        currentChunkIndex = chapterTrackSamples[i].ChunkIndex;
                        cumulatedChunkOffset = 0;
                    }
                    cumulatedChunkOffset += chapterTrackSamples[i].Size;
                }
            }

            source.BaseStream.Seek(atomPosition + atomSize - 8, SeekOrigin.Begin); // -8 because the header has already been read
                                                                                   // "Physical" audio chunks are referenced by position (offset) in  moov.trak.mdia.minf.stbl.stco / co64
                                                                                   // => They have to be rewritten if the position (offset) of the 'mdat' atom changes
            if (readTagParams.PrepareForWriting || isCurrentTrackFirstChapterTrack)
            {
                atomPosition = source.BaseStream.Position;
                byte nbBytes = 0;
                uint nbChunkOffsets = 0;
                object valueObj;
                long valueLong;

                // Chunk offsets
                if (lookForMP4Atom(source.BaseStream, "stco") > 0)
                {
                    nbBytes = 4;
                }
                else
                {
                    source.BaseStream.Seek(atomPosition, SeekOrigin.Begin);
                    if (lookForMP4Atom(source.BaseStream, "co64") > 0)
                    {
                        nbBytes = 8;
                    }
                    else
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "neither stco, not co64 atoms could not be found; aborting read on track " + currentTrakIndex);
                        source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                        return trakSize;
                    }
                }

                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                nbChunkOffsets = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                for (int i = 0; i < nbChunkOffsets; i++)
                {
                    if (4 == nbBytes)
                    {
                        source.Read(data32, 0, 4);
                        valueLong = StreamUtils.DecodeBEUInt32(data32);
                        valueObj = (uint)valueLong;

                    }
                    else
                    {
                        source.Read(data64, 0, 8);
                        valueLong = StreamUtils.DecodeBEInt64(data64);
                        valueObj = valueLong;
                    }

                    if (isCurrentTrackFirstChapterTrack) // Use the offsets to find position for QT chapter titles
                    {
                        for (int j = 0; j < chapterTrackSamples.Count; j++)
                        {
                            if (chapterTrackSamples[j].ChunkIndex == i + 1)
                            {
                                chapterTrackSamples[j].ChunkOffset = valueLong;
                            }
                        }
                    }

                    string zoneName = ZONE_MP4_PHYSICAL_CHUNK + "." + currentTrakIndex + "." + i;
                    structureHelper.AddZone(valueLong, 0, zoneName, false);
                    structureHelper.AddIndex(source.BaseStream.Position - nbBytes, valueObj, false, zoneName);
                } // Chunk offsets
            }

            source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);

            return trakSize;
        }

        private void readUserData(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            long atomPosition, udtaPosition;
            uint atomSize;

            byte[] data32 = new byte[4];
            byte[] data64 = new byte[8];


            atomSize = lookForMP4Atom(source.BaseStream, "udta");
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "udta atom could not be found");
                return;
            }

            udtaPosition = source.BaseStream.Position;
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_NOMETA);
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_ILST);
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_CHPL);
            }

            // Look for Nero chapters
            atomPosition = source.BaseStream.Position;
            atomSize = lookForMP4Atom(source.BaseStream, "chpl");
            if (atomSize > 0)
            {
                tagExists = true;
                structureHelper.AddZone(source.BaseStream.Position - 8, (int)atomSize, new byte[0], ZONE_MP4_CHPL);

                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                source.BaseStream.Seek(1, SeekOrigin.Current); // Reserved byte
                source.BaseStream.Read(data32, 0, 4);
                uint chapterCount = StreamUtils.DecodeBEUInt32(data32);

                if (chapterCount > 0)
                {
                    if (null == tagData.Chapters) tagData.Chapters = new List<ChapterInfo>(); else tagData.Chapters.Clear();
                    byte stringSize;
                    ChapterInfo chapter;

                    for (int i = 0; i < chapterCount; i++)
                    {
                        chapter = new ChapterInfo();
                        tagData.Chapters.Add(chapter);

                        source.BaseStream.Read(data64, 0, 8);
                        chapter.StartTime = (uint)Math.Round(StreamUtils.DecodeBEInt64(data64) / 10000.0);
                        stringSize = source.ReadByte();
                        chapter.Title = Encoding.UTF8.GetString(source.ReadBytes(stringSize));
                    }
                }
            }
            else
            {
                // Allow creating the 'chpl' atom from scratch
                structureHelper.AddZone(atomPosition, 0, ZONE_MP4_CHPL);
            }

            source.BaseStream.Seek(udtaPosition, SeekOrigin.Begin);
            atomSize = lookForMP4Atom(source.BaseStream, "meta");
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "meta atom could not be found");
                // Allow creating the 'meta' atom from scratch
                structureHelper.AddZone(udtaPosition, 0, ZONE_MP4_NOMETA);
            }
            else
            {
                if (readTagParams.PrepareForWriting) structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_ILST);
                source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                if (readTagParams.ReadTag) readTag(source, readTagParams);
            }
        }

        private void readTag(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            long iListSize = 0;
            long iListPosition = 0;
            uint metadataSize = 0;
            byte dataClass = 0;

            ushort int16Data = 0;
            string strData = "";

            uint atomSize;
            long atomPosition;


            atomPosition = source.BaseStream.Position;
            atomSize = lookForMP4Atom(source.BaseStream, "hdlr"); // Metadata handler
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "hdlr atom could not be found; aborting read");
                return;
            }
            long hdlrPosition = source.BaseStream.Position - 8;
            source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            source.BaseStream.Seek(4, SeekOrigin.Current); // Quicktime type
            strData = Utils.Latin1Encoding.GetString(source.ReadBytes(4)); // Meta data type

            if (!strData.Equals("mdir"))
            {
                string errMsg = "ATL does not support ";
                if (strData.Equals("mp7t")) errMsg += "MPEG-7 XML metadata";
                else if (strData.Equals("mp7b")) errMsg += "MPEG-7 binary XML metadata";
                else errMsg = "Unrecognized metadata format";

                throw new NotSupportedException(errMsg);
            }
            source.BaseStream.Seek(atomSize + hdlrPosition, SeekOrigin.Begin); // Reach the end of the hdlr box

            iListSize = lookForMP4Atom(source.BaseStream, "ilst"); // === Metadata list
            if (0 == iListSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "ilst atom could not be found");
                // TODO handle the case where 'meta' exists, but not 'ilst'
                return;
            }
            structureHelper.AddZone(source.BaseStream.Position - 8, (int)iListSize, ILST_CORE_SIGNATURE, ZONE_MP4_ILST);

            if (8 == Size) // Core minimal size
            {
                tagExists = false;
                return;
            }
            else
            {
                tagExists = true;
            }

            string atomHeader;
            // Browse all metadata
            while (iListPosition < iListSize - 8)
            {
                atomSize = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                atomHeader = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if ("----".Equals(atomHeader)) // Custom text metadata
                {
                    metadataSize = lookForMP4Atom(source.BaseStream, "mean"); // "issuer" of the field
                    if (0 == metadataSize)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mean atom could not be found; aborting read");
                        return;
                    }
                    source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                    atomHeader += ":" + Utils.Latin1Encoding.GetString(source.ReadBytes((int)metadataSize - 8 - 4));

                    metadataSize = lookForMP4Atom(source.BaseStream, "name"); // field type
                    if (0 == metadataSize)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "name atom could not be found; aborting read");
                        return;
                    }
                    source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                    atomHeader += ":" + Utils.Latin1Encoding.GetString(source.ReadBytes((int)metadataSize - 8 - 4));
                }

                // Having a 'data' header here means we're still on the same field, with a 2nd value
                // (e.g. multiple embedded pictures)
                if (!"data".Equals(atomHeader))
                {
                    metadataSize = lookForMP4Atom(source.BaseStream, "data");
                    if (0 == metadataSize)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "data atom could not be found; aborting read");
                        return;
                    }
                    atomPosition = source.BaseStream.Position - 8;
                }
                else
                {
                    metadataSize = atomSize;
                }

                // We're only looking for the last byte of the flag
                source.BaseStream.Seek(3, SeekOrigin.Current);
                dataClass = source.ReadByte();

                // 4-byte NULL space
                source.BaseStream.Seek(4, SeekOrigin.Current);

                addFrameClass(atomHeader, dataClass);

                if (1 == dataClass) // UTF-8 Text
                {
                    strData = Encoding.UTF8.GetString(source.ReadBytes((int)metadataSize - 16));
                    SetMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                }
                else if (21 == dataClass) // uint8
                {
                    int16Data = source.ReadByte();
                    //                        Source.BaseStream.Seek(atomPosition+metadataSize, SeekOrigin.Begin); // The rest are padding bytes
                    SetMetaField(atomHeader, int16Data.ToString(), readTagParams.ReadAllMetaFrames);
                }
                else if (13 == dataClass || 14 == dataClass || (0 == dataClass && "covr".Equals(atomHeader))) // Picture
                {
                    PictureInfo.PIC_TYPE picType = PictureInfo.PIC_TYPE.Generic; // TODO - to check : this seems to prevent ATL from detecting multiple images from the same type, as for a file with two "Front Cover" images; only one image will be detected

                    int picturePosition;
                    addPictureToken(picType);
                    picturePosition = takePicturePosition(picType);

                    if (readTagParams.ReadPictures)
                    {
                        PictureInfo picInfo = PictureInfo.fromBinaryData(source.BaseStream, (int)(metadataSize - 16), picType, getImplementedTagType(), dataClass, picturePosition);
                        tagData.Pictures.Add(picInfo);
                    }
                    // else - Other unhandled cases ?
                }
                else if (0 == dataClass) // Special cases : gnre, trkn, disk
                {
                    if ("trkn".Equals(atomHeader) || "disk".Equals(atomHeader))
                    {
                        source.BaseStream.Seek(2, SeekOrigin.Current);
                        ushort number = StreamUtils.DecodeBEUInt16(source.ReadBytes(2)); // Current track/disc number
                        ushort total = StreamUtils.DecodeBEUInt16(source.ReadBytes(2)); // Total number of tracks/discs
                        SetMetaField(atomHeader, number.ToString() + "/" + total.ToString(), readTagParams.ReadAllMetaFrames);
                    }
                    else if ("gnre".Equals(atomHeader)) // ©gen is a text field and doesn't belong here
                    {
                        int16Data = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

                        strData = "";
                        if (int16Data < ID3v1.MAX_MUSIC_GENRES) strData = ID3v1.MusicGenre[int16Data - 1];

                        SetMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                    }
                    // else - Other unhandled cases ?
                }
                // else - Other unhandled cases ?

                source.BaseStream.Seek(atomPosition + metadataSize, SeekOrigin.Begin);
                iListPosition += atomSize;
            }
        }

        /// <summary>
        /// Looks for the atom segment starting with the given key, at the current atom level
        /// Returns with Source positioned right after the atom header, on the 1st byte of data
        /// 
        /// Warning : stream must be positioned at the end of a previous atom before being called
        /// </summary>
        /// <param name="Source">Source to read from</param>
        /// <param name="atomKey">Atom key to look for (e.g. "udta")</param>
        /// <returns>If atom found : raw size of the atom (including the already-read 8-byte header);
        /// If atom not found : 0</returns>
        private uint lookForMP4Atom(Stream Source, string atomKey)
        {
            uint atomSize = 0;
            string atomHeader;
            bool first = true;
            int iterations = 0;
            byte[] data = new byte[4];

            do
            {
                if (!first) Source.Seek(atomSize - 8, SeekOrigin.Current);
                Source.Read(data, 0, 4);
                atomSize = StreamUtils.DecodeBEUInt32(data);
                Source.Read(data, 0, 4);
                atomHeader = Utils.Latin1Encoding.GetString(data);

                if (first) first = false;
                if (++iterations > 100) return 0;
            } while (!atomKey.Equals(atomHeader) && Source.Position + atomSize - 16 < Source.Length);

            if (Source.Position + atomSize - 16 > Source.Length) return 0;

            return atomSize;
        }


        // Read data from file
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;

            ResetData();

            headerTypeID = recognizeHeaderType(source);
            // Read header data
            if (MP4_HEADER_TYPE_MP4 == headerTypeID) readMP4(source, readTagParams);

            result = true;

            return result;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            long tagSizePos;
            int result = 0;

            if (zone.StartsWith(ZONE_MP4_NOMETA)) // Create a META atom from scratch
            {
                // Keep position in mind to calculate final size and come back here to write it
                long metaSizePos = w.BaseStream.Position;
                w.Write(0);
                w.Write(Utils.Latin1Encoding.GetBytes("meta"));
                w.Write(0); // version and flags

                // Handler
                w.Write(StreamUtils.EncodeBEUInt32(33));
                w.Write(Utils.Latin1Encoding.GetBytes("hdlr"));
                w.Write(0); // version and flags
                w.Write(0); // quicktime type
                w.Write(Utils.Latin1Encoding.GetBytes("mdir")); // quicktime subtype = "APPLE meta data iTunes reader"
                w.Write(Utils.Latin1Encoding.GetBytes("appl")); // manufacturer
                w.Write(0); // component flags
                w.Write(0); // component flags mask
                w.Write((byte)0); // component name string end (no name here -> end byte follows flags mask)

                long ilstSizePos = w.BaseStream.Position;
                w.Write(ILST_CORE_SIGNATURE);

                result = writeFrames(tag, w);

                // Record final size of tag into "tag size" fields of header
                long finalTagPos = w.BaseStream.Position;

                w.BaseStream.Seek(metaSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - metaSizePos)));

                w.BaseStream.Seek(ilstSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - ilstSizePos)));

                w.BaseStream.Seek(finalTagPos, SeekOrigin.Begin);

            }
            else if (zone.StartsWith(ZONE_MP4_ILST)) // Edit an existing ILST atom
            {
                // Keep position in mind to calculate final size and come back here to write it
                tagSizePos = w.BaseStream.Position;
                w.Write(ILST_CORE_SIGNATURE);

                result = writeFrames(tag, w);

                // Record final size of tag into "tag size" field of header
                long finalTagPos = w.BaseStream.Position;
                w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - tagSizePos)));
                w.BaseStream.Seek(finalTagPos, SeekOrigin.Begin);
            }
            else if (zone.StartsWith(ZONE_MP4_CHPL)) // Nero chapters
            {
                result = writeNeroChapters(w, Chapters);
            }
            else if (PADDING_ZONE_NAME.Equals(zone)) // Padding
            {
                Zone z = structureHelper.GetZone(zone);
                long paddingSizeToWrite;
                if (tag.PaddingSize > -1) paddingSizeToWrite = tag.PaddingSize;
                else paddingSizeToWrite = TrackUtils.ComputePaddingSize(z.Offset, z.Size, -tag.DataSizeDelta);

                if (paddingSizeToWrite > 0)
                {
                    // Placeholder; size is written by FileStructureHelper
                    w.Write(0);
                    w.Write(Utils.Latin1Encoding.GetBytes("free"));
                    for (int i = 0; i < paddingSizeToWrite - 8; i++) w.Write((byte)0);
                    result = 1;
                }
            }
            else if (zone.StartsWith(ZONE_MP4_PHYSICAL_CHUNK)) // Audio chunks
            {
                result = 1; // Needs to appear active in case the headers need to be rewritten
            }

            return result;
        }

        private int writeFrames(TagData tag, BinaryWriter w)
        {
            int counter = 0;
            bool doWritePicture;

            IDictionary<byte, String> map = tag.ToMap();

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach (string s in frameMapping_mp4.Keys)
                {
                    if (frameType == frameMapping_mp4[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);

                            writeTextFrame(w, s, value);
                            counter++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion)
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    counter++;
                }
            }

            // Picture fields
            bool firstPic = true;
            foreach (PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat, firstPic);
                    counter++;
                    firstPic = false;
                }
            }

            return counter;
        }

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            long frameSizePos1, frameSizePos2, finalFramePos;
            int frameFlags = 0;

            // == METADATA HEADER ==
            frameSizePos1 = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines
            if (frameCode.StartsWith("----")) // Specific metadata
            {
                string[] frameCodeComponents = frameCode.Split(':');
                if (3 == frameCodeComponents.Length)
                {
                    writer.Write(Utils.Latin1Encoding.GetBytes("----"));

                    writer.Write(StreamUtils.EncodeBEInt32(frameCodeComponents[1].Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("mean"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(frameCodeComponents[1]));

                    writer.Write(StreamUtils.EncodeBEInt32(frameCodeComponents[2].Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("name"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(frameCodeComponents[2]));
                }
            }
            else
            {
                writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            }

            // == METADATA VALUE ==
            frameSizePos2 = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines
            writer.Write("data".ToCharArray());

            int frameClass = 1;
            if (frameClasses_mp4.ContainsKey(frameCode)) frameClass = frameClasses_mp4[frameCode];

            writer.Write(StreamUtils.EncodeBEInt32(frameClass));
            writer.Write(frameFlags);

            if (0 == frameClass) // Special cases : gnre, trkn, disk
            {
                byte[] int16data;
                if (frameCode.Equals("trkn") || frameCode.Equals("disk"))
                {
                    int16data = new byte[2] { 0, 0 };
                    writer.Write(int16data);
                    int16data = StreamUtils.EncodeBEUInt16(TrackUtils.ExtractTrackNumber(text));
                    writer.Write(int16data);
                    int16data = StreamUtils.EncodeBEUInt16(TrackUtils.ExtractTrackTotal(text));
                    writer.Write(int16data);
                    if (frameCode.Equals("trkn")) writer.Write(int16data); // trkn field always has two more bytes than disk field....
                }
                else if (frameCode.Equals("gnre"))
                {
                    int16data = StreamUtils.EncodeBEUInt16(Convert.ToUInt16(text));
                    writer.Write(int16data);
                }
            }
            else if (1 == frameClass) // UTF-8 text
            {
                writer.Write(Encoding.UTF8.GetBytes(text));
            }
            else if (21 == frameClass) // uint8
            {
                writer.Write(Convert.ToByte(text));
            }

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos1, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos1)));
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, bool firstPicture)
        {
            long frameSizePos1 = 0;
            long frameSizePos2;
            long finalFramePos;

            int frameFlags = 0;

            // == METADATA HEADER ==
            if (firstPicture) // If multiples pictures are embedded, the 'covr' atom is not repeated; the 'data' atom is
            {
                frameSizePos1 = writer.BaseStream.Position;
                writer.Write(0); // Frame size placeholder to be rewritten in a few lines
                writer.Write(Utils.Latin1Encoding.GetBytes("covr"));
            }

            // == METADATA VALUE ==
            frameSizePos2 = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines
            writer.Write("data".ToCharArray());

            int frameClass;
            if (picFormat.Equals(ImageFormat.Jpeg)) frameClass = 13;
            else if (picFormat.Equals(ImageFormat.Png)) frameClass = 14;
            else frameClass = 0;

            writer.Write(StreamUtils.EncodeBEInt32(frameClass));
            writer.Write(frameFlags);

            writer.Write(pictureData);

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
            if (firstPicture)
            {
                writer.BaseStream.Seek(frameSizePos1, SeekOrigin.Begin);
                writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos1)));
            }
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private int writeNeroChapters(BinaryWriter w, IList<ChapterInfo> chapters)
        {
            int result = 0;
            long frameSizePos, finalFramePos;

            if (chapters != null && chapters.Count > 0)
            {
                result = chapters.Count;

                frameSizePos = w.BaseStream.Position;
                w.Write(0); // To be rewritten at the end of the method
                w.Write(Utils.Latin1Encoding.GetBytes("chpl"));

                w.Write(new byte[5] { 1, 0, 0, 0, 0 }); // Version, flags and reserved byte
                w.Write(StreamUtils.EncodeBEInt32(chapters.Count));

                byte[] strData;
                byte strDataLength;

                foreach (ChapterInfo chapter in chapters)
                {
                    w.Write(StreamUtils.EncodeBEUInt64((ulong)chapter.StartTime * 10000));
                    strData = Encoding.UTF8.GetBytes(chapter.Title);
                    strDataLength = (byte)Math.Min(255, strData.Length);
                    w.Write(strDataLength);
                    w.Write(strData, 0, strDataLength);
                }

                // Go back to frame size locations to write their actual size 
                finalFramePos = w.BaseStream.Position;
                w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - frameSizePos)));
            }

            return result;
        }
    }
}