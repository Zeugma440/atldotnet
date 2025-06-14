using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ATL.Logging;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Matroska Audio files manipulation (extension : .MKA)
    /// 
    /// Implementation notes
    /// - Padding Elements are not supported
    /// - Chapters : Multiple EditionEntries are not supported; only 1st default, non-hidden is used
    /// - Chapters : Nested ChapterAtoms are not supported; only 1st level enabled, non-hidden are used
    /// - Cues : CuePoints are not part of readable not writable metadata
    /// - Tags : Nested tags are not supported
    /// 
    /// </summary>
    internal partial class MKA : MetaDataIO, IAudioDataIO
    {
        private const uint EBML_MAGIC_NUMBER = 0x1A45DFA3; // EBML header
        private const uint EBML_VERSION = 0x4286;
        private const uint EBML_READVERSION = 0x42F7;
        private const uint EBML_MAXIDLENGTH = 0x42F2;
        private const uint EBML_MAXSIZELENGTH = 0x42F3;
        private const uint EBML_DOCTYPE = 0x4282;
        private const uint EBML_DOCTYPEVERSION = 0x4287;
        private const uint EBML_DOCTYPEREADVERSION = 0x4285;

        private const uint EBML_PADDING = 0xEC;

        // Matroska element IDs
        private const uint ID_SEGMENT = 0x18538067;

        private const uint ID_INFO = 0x1549A966;
        private const uint ID_TRACKS = 0x1654AE6B;
        private const uint ID_CLUSTER = 0x1F43B675;
        private const uint ID_TIMESTAMP = 0xE7;
        private const uint ID_SIMPLEBLOCK = 0xA3;
        private const uint ID_BLOCKGROUP = 0xA0;
        private const uint ID_BLOCK = 0xA1;
        private const uint ID_CUES = 0x1C53BB6B;

        private const uint ID_ATTACHMENTS = 0x1941A469;
        private const uint ID_ATTACHEDFILE = 0x61A7;
        private const uint ID_FILEDESCRIPTION = 0x467E;
        private const uint ID_FILENAME = 0x466E;
        private const uint ID_FILEMEDIATYPE = 0x4660;
        private const uint ID_FILEDATA = 0x465C;
        private const uint ID_FILEUID = 0x46AE;

        private const uint ID_SEEKHEAD = 0x114D9B74;
        private const uint ID_SEEK = 0x4DBB;
        private const uint ID_SEEKID = 0x53AB;
        private const uint ID_SEEKPOSITION = 0x53AC;


        private const uint ID_TAGS = 0x1254C367;

        private const uint ID_TAG = 0x7373;
        private const uint ID_TARGETS = 0x63C0;
        private const uint ID_TARGETTYPEVALUE = 0x68CA;

        private const uint ID_SIMPLETAG = 0x67C8;
        private const uint ID_TAGNAME = 0x45A3;
        private const uint ID_TAGSTRING = 0x4487;
        private const uint ID_TAGLANGUAGE = 0x447A;
        private const uint ID_TAGDEFAULT = 0x4484;

        private const uint ID_CHAPTERS = 0x1043A770;
        private const ushort ID_EDITIONENTRY = 0x45B9;
        private const uint ID_EDITIONUID = 0x45BC;
        private const uint ID_EDITIONFLAGDEFAULT = 0x45DB;
        private const uint ID_EDITIONFLAGHIDDEN = 0x45BD;
        private const uint ID_EDITIONDISPLAY = 0x4520;
        private const uint ID_EDITIONSTRING = 0x4521;
        private const uint ID_CHAPTERATOM = 0xB6;
        private const uint ID_CHAPTERUID = 0x73C4;
        private const uint ID_CHAPTERSTRINGUID = 0x5654;
        private const uint ID_CHAPTERFLAGENABLED = 0x4598;
        private const uint ID_CHAPTERFLAGHIDDEN = 0x98;
        private const uint ID_CHAPTERTIMESTART = 0x91;
        private const uint ID_CHAPTERTIMEEND = 0x92;
        private const uint ID_CHAPTERDISPLAY = 0x80;
        private const uint ID_CHAPTERSTRING = 0x85;
        private const uint ID_CHAPTERLANGUAGE = 0x437C;


        /// == Tags

        // TargetTypeValue Codes
        private const int TYPE_SHOT = 10;
        private const int TYPE_MOVEMENT = 20;
        private const int TYPE_TRACK = 30;
        private const int TYPE_SESSION = 40;
        private const int TYPE_ALBUM = 50;
        private const int TYPE_VOLUME = 60;
        private const int TYPE_COLLECTION = 70;

        // TargetTypeValue Codes => prefixes mapping
        private static readonly Dictionary<int, string> targetTypePrefixesMapping = new Dictionary<int, string>
        {
            { TYPE_SHOT, "shot"},
            { TYPE_MOVEMENT, "movement"},
            { TYPE_TRACK, "track"},
            { TYPE_SESSION, "session"},
            { TYPE_ALBUM, "album"},
            { TYPE_VOLUME, "volume"},
            { TYPE_COLLECTION, "collection"},
        };


        private const int TRACKTYPE_AUDIO = 2;

        // Mapping between MKV format ID and ATL format IDs
        private static readonly Dictionary<string, int> codecsMappingCodes = new Dictionary<string, int>
        {
            { "A_MPEG/L3", AudioDataIOFactory.CID_MPEG },
            { "A_MPEG/L2", AudioDataIOFactory.CID_MPEG },
            { "A_MPEG/L1", AudioDataIOFactory.CID_MPEG },
            { "A_PCM/INT/BIG", AudioDataIOFactory.CID_WAV },
            { "A_PCM/INT/LIT", AudioDataIOFactory.CID_WAV },
            { "A_PCM/FLOAT/IEEE", AudioDataIOFactory.CID_WAV },
            { "A_MPC", AudioDataIOFactory.CID_MPC },
            { "A_AC3", AudioDataIOFactory.CID_AC3 },
            { "A_AC3/BSID9", AudioDataIOFactory.CID_AC3 },
            { "A_AC3/BSID10", AudioDataIOFactory.CID_AC3 },
            // No support for ALAC
            { "A_DTS", AudioDataIOFactory.CID_DTS },
            { "A_DTS/EXPRESS", AudioDataIOFactory.CID_DTS },
            { "A_DTS/LOSSLESS", AudioDataIOFactory.CID_DTS },
            { "A_VORBIS", AudioDataIOFactory.CID_OGG },
            { "A_FLAC", AudioDataIOFactory.CID_FLAC },
            // No support for RealMedia
            // No support for MS ACM
            { "A_AAC/MPEG2/MAIN", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/LC", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/LC/SBR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/SSR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/MAIN", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LC", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LC/SBR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/SSR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LTP", AudioDataIOFactory.CID_AAC },
            // No support for QuickTime audio (though MP4 might be close)
            { "A_TTA1", AudioDataIOFactory.CID_TTA },
            { "A_WAVPACK4", AudioDataIOFactory.CID_WAVPACK },
            // No support for ATRAC1
            { "A_OPUS", AudioDataIOFactory.CID_OGG }
        };

        // Mapping between MKV format ID and ATL formats
        private static readonly Dictionary<string, Format> codecsMapping = new Dictionary<string, Format>();

        // Mapping between MKV tag names and ATL frame codes
        private static readonly Dictionary<string, Field> frameMapping = new Dictionary<string, Field>()
        {
            { "track.description", Field.GENERAL_DESCRIPTION },
            { "track.title", Field.TITLE },
            { "track.artist", Field.ARTIST },
            { "album.composer", Field.COMPOSER },
            { "track.composer", Field.COMPOSER },
            { "track.comment", Field.COMMENT },
            { "track.genre", Field.GENRE },
            { "album.title", Field.ALBUM },
            { "track.part_number", Field.TRACK_NUMBER },
            { "track.total_parts", Field.TRACK_TOTAL },
            { "album.part_number", Field.DISC_NUMBER },
            { "album.total_parts", Field.DISC_TOTAL },
            { "track.rating", Field.RATING },
            { "track.copyright", Field.COPYRIGHT },
            { "album.artist", Field.ALBUM_ARTIST },
            { "track.publisher", Field.PUBLISHER },
            { "album.publisher", Field.PUBLISHER },
            { "track.conductor", Field.CONDUCTOR },
            { "album.conductor", Field.CONDUCTOR },
            { "track.lyrics", Field.LYRICS_UNSYNCH },
            { "album.date_released", Field.PUBLISHING_DATE },
            { "album.catalog_number", Field.CATALOG_NUMBER },
            { "track.bpm", Field.BPM },
            { "track.encoded_by", Field.ENCODED_BY },
            { "track.encoder", Field.ENCODER },
            { "album.isrc", Field.ISRC },
            { "album.purchase_item", Field.AUDIO_SOURCE_URL },
            { "album.lyricist", Field.LYRICIST },
            { "album.date_recorded", Field.RECORDING_DATE }
        };

        private const string ZONE_EBML_HEADER = "ebmlHeader";
        private const string ZONE_SEGMENT_SIZE = "segmentSize";
        private const string ZONE_SEEKHEAD = "seekHead";
        private const string ZONE_TAGS = "tags";
        private const string ZONE_ATTACHMENTS = "attachments";
        private const string ZONE_CHAPTERS = "chapters";


        // == Private declarations 

        // Metadata
        private AudioFormat audioFormat;

        private string docType; // EBML docType
        private ulong editionEntryUid;

        // Parsing
        private long segmentOffset; // Segment offset
        private readonly List<List<Tuple<long, ulong>>> seekHeads = new List<List<Tuple<long, ulong>>>();


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public AudioFormat AudioFormat
        {
            get
            {
                if (!audioFormat.Name.Contains('/'))
                {
                    audioFormat = new AudioFormat(audioFormat);
                    var containerFormat = AudioDataIOFactory.GetInstance().getFormat(audioFormat.ContainerId);
                    if (null == containerFormat) audioFormat.Name = audioFormat.DataFormat.ShortName;
                    else audioFormat.Name = containerFormat.Name + " / " + audioFormat.DataFormat.ShortName;
                    audioFormat.ComputeId();
                }

                return audioFormat;
            }
        }

        public bool IsVBR { get; private set; }
        public int CodecFamily { get; private set; }
        public string FileName { get; }
        public double BitRate { get; private set; }
        public int BitDepth { get; private set; }
        public double Duration { get; private set; }
        public int SampleRate { get; private set; }
        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas() => new List<MetaDataIOFactory.TagType>
            { MetaDataIOFactory.TagType.NATIVE };

        protected override int getDefaultTagOffset() => TO_BUILTIN;

        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;

        /// <inheritdoc/>
        public bool IsNativeMetadataRich => true;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        protected override bool isLittleEndian => false;

        public override string EncodeDate(DateTime date)
        {
            return TrackUtils.FormatISOTimestamp(date).Replace("T", " ");
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        static MKA()
        {
            var formats = AudioDataIOFactory.GetInstance().getFormats();
            foreach (var cmc in codecsMappingCodes)
            {
                var format = formats.Where(f => f.ID == cmc.Value).ToList();
                if (format.Count > 0)
                {
                    var formatFinal = format[0];
                    formatFinal = cmc.Key switch
                    {
                        "A_OPUS" => new AudioFormat(formatFinal) { Name = "Opus", ShortName = "Opus" },
                        "A_VORBIS" => new AudioFormat(formatFinal) { Name = "Vorbis", ShortName = "Vorbis" },
                        _ => formatFinal
                    };
                    codecsMapping.Add(cmc.Key, formatFinal);
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MKA(string filePath, AudioFormat format)
        {
            FileName = filePath;
            this.audioFormat = format;
            resetData();
        }

        protected void resetData()
        {
            SampleRate = 0;
            Duration = 0;
            BitRate = 0;
            IsVBR = false;
            CodecFamily = 0;
            BitDepth = 0;
            ChannelsArrangement = null;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            docType = "";
            editionEntryUid = 0;

            segmentOffset = 0;
            seekHeads.Clear();
        }

        public static bool IsValidHeader(byte[] data) => EBML_MAGIC_NUMBER == StreamUtils.DecodeBEUInt32(data);


        // ---------- SUPPORT METHODS

        private void indexSilentZones(EBMLReader reader)
        {
            indexSilentZone(reader, ID_INFO);
            indexSilentZone(reader, ID_TRACKS);
            indexSilentZone(reader, ID_CUES);
            indexSilentZone(reader, ID_CLUSTER);
        }

        private void indexSilentZone(EBMLReader reader, long id)
        {
            reader.seek(segmentOffset);
            int index = 0;
            foreach (var offset in reader.seekElements(id))
            {
                structureHelper.AddZone(offset - 4, 0, id + "." + index, false, false);
                index++;
            }
        }

        private bool readEbmlHeader(EBMLReader reader, ReadTagParams readTagParams)
        {
            if (!reader.enterContainer(EBML_MAGIC_NUMBER))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "File is not a valid EBML file");
                return false;
            }
            long rootOffset = reader.Position;
            long headerSize = reader.readVint();
            long dataOffset = reader.Position;
            reader.seek(rootOffset);

            if (readTagParams.PrepareForWriting)
                structureHelper.AddZone(0, dataOffset + headerSize, ZONE_EBML_HEADER, false);

            if (reader.seekElement(EBML_DOCTYPE))
            {
                docType = reader.readString();
                if (!docType.Equals("matroska") && !docType.Equals("webm"))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "File is not a valid Matroska nor WebM file");
                    return false;
                }
            }
            reader.seek(dataOffset + headerSize);
            return true;
        }

        private bool readPhysicalData(EBMLReader reader)
        {
            if (!reader.seekElement(ID_TRACKS)) return false;
            long audioOffset = -1;

            // Find proper TrackEntry
            var crits = new HashSet<Tuple<long, int>>
            {
                new Tuple<long, int>(0x83, TRACKTYPE_AUDIO), // TrackType
                new Tuple<long, int>(0xB9, 1), // FlagEnabled
                new Tuple<long, int>(0x88, 1) // FlagDefault
            };
            var res = reader.seekElement(0xAE, crits); // TrackEntry
            if (res == EBMLReader.SeekResult.FOUND_MATCH)
            {
                var trackEntryOffset = reader.Position;

                var codecId = "";
                if (reader.seekElement(0x86)) codecId = reader.readString().ToUpper(); // CodecID
                if (codecsMapping.TryGetValue(codecId, out var value)) audioFormat.DataFormat = new Format(value);

                reader.seek(trackEntryOffset);
                if (reader.seekElement(0xE1)) audioOffset = reader.Position; // Audio
            }
            else
            {
                audioFormat.DataFormat = Format.UNKNOWN_FORMAT;
            }

            // Find AudioDataOffset using Clusters' timecodes
            reader.seek(segmentOffset);
            // Seek Cluster with Timecode 0
            crits = new HashSet<Tuple<long, int>>
            {
                new Tuple<long, int>(ID_TIMESTAMP, 0), // Timestamp
            };
            res = reader.seekElement(ID_CLUSTER, crits);
            if (res != EBMLReader.SeekResult.FOUND_MATCH)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Couldn't locate Cluster for timestamp 0");
                // Seek first cluster instead
                reader.seek(segmentOffset);
                if (!reader.seekElement(ID_CLUSTER))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Couldn't locate any Cluster!");
                    return false;
                }
            }

            long firstClusterOffset = reader.Position;

            long blockAudioSize = -1;
            while (-1 == AudioDataOffset)
            {
                if (reader.seekElement(ID_BLOCKGROUP)) // BlockGroup
                {
                    var loopOffset = reader.Position;
                    if (reader.seekElement(ID_BLOCK)) // Block
                    {
                        var blockSize = reader.readVint(); // size
                        var blockOffset = reader.Position;
                        reader.readVint(); // trackId
                        if (0 == StreamUtils.DecodeBEUInt16(reader.readBytes(2))) // Timestamp zero
                        {
                            AudioDataOffset = reader.Position + 1; // Ignore flags
                            blockAudioSize = blockSize - AudioDataOffset + blockOffset;
                        }
                    }

                    if (-1 == AudioDataOffset) reader.seek(loopOffset);
                }
                else break;
            }

            reader.seek(firstClusterOffset);
            while (-1 == AudioDataOffset)
            {
                if (reader.seekElement(ID_SIMPLEBLOCK)) // SimpleBlock
                {
                    var loopOffset = reader.Position;
                    var blockSize = reader.readVint(); // size
                    var blockOffset = reader.Position;
                    reader.readVint(); // trackId
                    if (0 == StreamUtils.DecodeBEUInt16(reader.readBytes(2))) // Timestamp zero
                    {
                        AudioDataOffset = reader.Position + 1; // Ignore flags
                        // TODO adjust offset according to lacing mode
                        blockAudioSize = blockSize - AudioDataOffset + blockOffset;
                    }

                    if (-1 == AudioDataOffset) reader.seek(loopOffset);
                }
                else break;
            }

            if (AudioDataOffset > -1)
            {
                AudioDataSize = reader.BaseStream.Length - AudioDataOffset; // Approximate

                // Try getting physical properties using the actual audio data header
                try
                {
                    if (audioFormat.DataFormat.ID != Format.UNKNOWN_FORMAT.ID)
                    {
                        // Copy block to MemoryStream
                        // TODO find a way to optimize memory by clamping the raw stream to the block's limits
                        reader.seek(AudioDataOffset);
                        using var memStream = new MemoryStream((int)blockAudioSize);
                        StreamUtils.CopyStream(reader.BaseStream, memStream, blockAudioSize);
                        memStream.Seek(0, SeekOrigin.Begin);

                        IAudioDataIO audioData = AudioDataIOFactory.GetInstance().GetFromStream(memStream);
                        if (audioData.AudioFormat.ID != Format.UNKNOWN_FORMAT.ID && audioData.Read(memStream,
                                new AudioDataManager.SizeInfo(), new ReadTagParams()))
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_INFO, "Reading physical attributes from audio data");
                            CodecFamily = audioData.CodecFamily;
                            IsVBR = audioData.IsVBR;
                            BitRate = audioData.BitRate;
                            if (!Utils.ApproxEquals(audioData.Duration, 0)) Duration = audioData.Duration;
                            SampleRate = audioData.SampleRate;
                            ChannelsArrangement = audioData.ChannelsArrangement;
                            BitDepth = audioData.BitDepth;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Couldn't parse inner audio data : " + e.Message);
                }
            }

            // Try getting Duration from MKA metadata
            long trackScale = 0;
            if (Utils.ApproxEquals(Duration, 0))
            {
                reader.seek(segmentOffset);
                if (reader.seekElement(ID_INFO))
                {
                    long infoOffset = reader.Position;
                    double duration = 0.0;

                    if (reader.seekElement(0x4489)) duration = reader.readFloat(); // Duration

                    reader.seek(infoOffset);
                    if (reader.seekElement(0x2AD7B1)) trackScale = (long)reader.readUint(); // TimestampScale

                    // Convert ns to ms
                    Duration = duration * trackScale / 1000000.0;
                }
            }

            // Try getting Duration by reading Cluster and Block timecodes
            long farthestClusterOffset = -1;
            if (Utils.ApproxEquals(Duration, 0))
            {
                // Seek the Cluster with the highest timestamp
                reader.seek(segmentOffset);
                var clusterOffsets = reader.seekElements(ID_CLUSTER);
                ulong highestTimestamp = 0;
                foreach (var clusterOffset in clusterOffsets)
                {
                    reader.seek(clusterOffset);
                    if (!reader.seekElement(ID_TIMESTAMP)) continue;
                    var timestamp = reader.readUint();
                    if (timestamp < highestTimestamp) continue;
                    highestTimestamp = timestamp;
                    farthestClusterOffset = clusterOffset;
                }

                // Within that cluster, seek the Block with the highest timestamp
                reader.seek(farthestClusterOffset);
                var blockGroups = reader.seekElements(ID_BLOCKGROUP);
                // Assume the last BlockGroup is the one marking the last bit of audio data
                var lastBgOffset = blockGroups.LastOrDefault();
                if (lastBgOffset != 0)
                {
                    reader.seek(lastBgOffset);
                    if (reader.seekElement(ID_BLOCK)) // Block
                    {
                        reader.readVint(); // size
                        reader.readVint(); // trackId
                        Duration = StreamUtils.DecodeBEUInt16(reader.readBytes(2)) * trackScale / 1000000.0;
                    }
                }
            }

            // Try getting Duration by reading Cluster and SimpleBlocks timecodes
            if (Utils.ApproxEquals(Duration, 0))
            {
                // Within the farthest cluster, seek the Simple with the highest timestamp
                reader.seek(farthestClusterOffset);
                var simpleBlocks = reader.seekElements(ID_SIMPLEBLOCK);
                // Assume the last BlockGroup is the one marking the last bit of audio data
                var lastSbOffset = simpleBlocks.LastOrDefault();
                if (lastSbOffset != 0)
                {
                    reader.seek(lastSbOffset);
                    reader.readVint(); // size
                    reader.readVint(); // trackId
                    Duration = StreamUtils.DecodeBEUInt16(reader.readBytes(2)) * trackScale / 1000000.0;
                }
            }

            // Approximate bitrate
            if (Utils.ApproxEquals(BitRate, 0)) BitRate = AudioDataSize / Duration;

            if (audioOffset > -1 && (0 == SampleRate || null == ChannelsArrangement || UNKNOWN == ChannelsArrangement))
            {
                reader.seek(audioOffset);
                if (0 == SampleRate && reader.seekElement(0xB5)) // SamplingFrequency
                    SampleRate = (int)reader.readFloat();

                reader.seek(audioOffset);
                int nbChannels = 0;
                if (reader.seekElement(0x9F)) nbChannels = (int)reader.readUint(); // Channels

                ChannelsArrangement ??= GuessFromChannelNumber(nbChannels);
            }

            return true;
        }

        private void readSeekHeads(EBMLReader reader)
        {
            foreach (long offset in reader.seekElements(ID_SEEKHEAD))
            {
                reader.seek(offset);
                var size = reader.readVint(); // Size of the Element minus ID and size descriptor
                size += reader.Position - offset + 4; // Entire size of the element, header included
                structureHelper.AddZone(offset - 4, size, ZONE_SEEKHEAD + "." + seekHeads.Count, false);

                reader.seek(offset);
                seekHeads.Add(readSeekHead(reader));
            }
        }

        private static List<Tuple<long, ulong>> readSeekHead(EBMLReader reader)
        {
            List<Tuple<long, ulong>> result = new List<Tuple<long, ulong>>();
            foreach (long offset in reader.seekElements(ID_SEEK))
            {
                reader.seek(offset);
                result.Add(readSeek(reader));
            }

            return result;
        }

        private static Tuple<long, ulong> readSeek(EBMLReader reader)
        {
            var seekOffset = reader.Position;

            long id = 0;
            if (reader.seekElement(ID_SEEKID))
            {
                reader.readVint(); // Size; unused here
                id = reader.readVint(true); // EBML ID is a VINT
            }

            ulong position = 0;
            reader.seek(seekOffset);
            if (reader.seekElement(ID_SEEKPOSITION)) position = reader.readUint();

            return new Tuple<long, ulong>(id, position);
        }

        private bool attachZone(EBMLReader reader, ReadTagParams readTagParams, uint id, string zoneName)
        {
            if (!reader.seekElement(id))
            {
                // Create empty region to add the given element to an empty file
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(reader.BaseStream.Length, 0, zoneName);
                }

                return false;
            }

            if (!readTagParams.PrepareForWriting) return true;

            var eltOffset = reader.Position - 4;
            var size = reader.readVint(); // Size of the Element minus ID and size descriptor
            size += reader.Position - eltOffset; // Entire size of the element, header included
            structureHelper.AddZone(eltOffset, size, zoneName);
            reader.seek(eltOffset + 4);

            return true;
        }

        private void readTags(EBMLReader reader, ReadTagParams readTagParams)
        {
            if (!attachZone(reader, readTagParams, ID_TAGS, ZONE_TAGS)) return;

            foreach (long offset in reader.seekElements(ID_TAG))
            {
                reader.seek(offset);
                readTag(reader);
            }
        }

        private void readTag(EBMLReader reader)
        {
            var tagOffset = reader.Position;
            int targetTypeValue = TYPE_TRACK; // Default when no data
            if (reader.seekElement(ID_TARGETS) && reader.seekElement(ID_TARGETTYPEVALUE))
            {
                targetTypeValue = (int)reader.readUint();
            }

            reader.seek(tagOffset);
            foreach (long offset in reader.seekElements(ID_SIMPLETAG))
            {
                reader.seek(offset);
                readSimpleTags(targetTypePrefixesMapping[targetTypeValue], reader);
            }
        }

        private void readSimpleTags(string prefix, EBMLReader reader)
        {
            var simpleTagOffset = reader.Position;

            string name = "";
            if (reader.seekElement(ID_TAGNAME)) name = reader.readUtf8String();

            string value = "";
            reader.seek(simpleTagOffset);
            if (reader.seekElement(ID_TAGSTRING)) value = reader.readUtf8String();

            SetMetaField(prefix + "." + name.ToLower(), value, true);
        }

        private void readAttachments(EBMLReader reader, ReadTagParams readTagParams)
        {
            if (!attachZone(reader, readTagParams, ID_ATTACHMENTS, ZONE_ATTACHMENTS)) return;

            foreach (long offset in reader.seekElements(ID_ATTACHEDFILE))
            {
                reader.seek(offset);
                readAttachedFile(reader);
            }
        }

        private void readAttachedFile(EBMLReader reader)
        {
            byte[] data = null;
            var rootOffset = reader.Position;
            if (reader.seekElement(ID_FILEDATA)) data = reader.readBinary();
            if (data == null || 0 == data.Length) return;

            var description = "";
            reader.seek(rootOffset);
            if (reader.seekElement(ID_FILEDESCRIPTION)) description = reader.readUtf8String();

            var name = "";
            reader.seek(rootOffset);
            if (reader.seekElement(ID_FILENAME)) name = reader.readUtf8String();

            var picType = PictureInfo.PIC_TYPE.Generic;
            if (name.Contains("cover", StringComparison.InvariantCultureIgnoreCase))
                picType = PictureInfo.PIC_TYPE.Front;
            else
            {
                foreach (PictureInfo.PIC_TYPE type in Enum.GetValues(typeof(PictureInfo.PIC_TYPE)))
                {
                    if (name.StartsWith(type.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    {
                        picType = type;
                        break;
                    }
                }
            }


            var pic = PictureInfo.fromBinaryData(data, picType, MetaDataIOFactory.TagType.NATIVE, 0);
            pic.NativePicCodeStr = name;
            pic.Description = description;
            tagData.Pictures.Add(pic);
        }

        private void readChapters(EBMLReader reader, ReadTagParams readTagParams)
        {
            if (!attachZone(reader, readTagParams, ID_CHAPTERS, ZONE_CHAPTERS)) return;

            // Find proper EditionEntry
            var crits = new HashSet<Tuple<long, int>>
            {
                new Tuple<long, int>(ID_EDITIONFLAGDEFAULT, 1),
                new Tuple<long, int>(ID_EDITIONFLAGHIDDEN, 0)
            };
            var res = reader.seekElement(ID_EDITIONENTRY, crits);
            if (res == EBMLReader.SeekResult.FOUND_MATCH) readChapterAtoms(reader);
        }

        private void readChapterAtoms(EBMLReader reader)
        {
            long rootOffset = reader.Position;

            if (reader.seekElement(ID_EDITIONUID)) editionEntryUid = reader.readUint();

            reader.seek(rootOffset);
            if (reader.seekElement(ID_EDITIONDISPLAY) && reader.seekElement(ID_EDITIONSTRING))
                tagData.IntegrateValue(Field.CHAPTERS_TOC_DESCRIPTION, reader.readUtf8String());

            reader.seek(rootOffset);
            foreach (long offset in reader.seekElements(ID_CHAPTERATOM))
            {
                reader.seek(offset);
                readChapterAtom(reader);
            }
        }

        // Only reads 1st level chapters (not nested ChapterAtoms)
        private void readChapterAtom(EBMLReader reader)
        {
            long rootOffset = reader.Position;

            ulong data = 0;
            if (reader.seekElement(ID_CHAPTERFLAGENABLED)) data = reader.readUint();
            if (0 == data) return;

            data = 0;
            reader.seek(rootOffset);
            if (reader.seekElement(ID_CHAPTERFLAGHIDDEN)) data = reader.readUint();
            if (1 == data) return;

            ulong timeStart = 0;
            reader.seek(rootOffset);
            if (reader.seekElement(ID_CHAPTERTIMESTART)) timeStart = reader.readUint();

            ulong timeEnd = 0;
            reader.seek(rootOffset);
            if (reader.seekElement(ID_CHAPTERTIMEEND)) timeEnd = reader.readUint();

            var result = new ChapterInfo((uint)(timeStart / 1000000.0));
            if (timeEnd > 0) result.EndTime = (uint)(timeEnd / 1000000.0);

            reader.seek(rootOffset);
            if (reader.seekElement(ID_CHAPTERUID)) result.UniqueNumericID = (uint)reader.readUint();

            reader.seek(rootOffset);
            if (reader.seekElement(ID_CHAPTERSTRINGUID)) result.UniqueID = reader.readUtf8String();

            // Get the first available title
            reader.seek(rootOffset);
            if (!reader.seekElement(ID_CHAPTERDISPLAY)) return;
            if (reader.seekElement(ID_CHAPTERSTRING)) result.Title = reader.readUtf8String();

            tagData.Chapters ??= new List<ChapterInfo>();
            tagData.Chapters.Add(result);
        }

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            ResetData();
            source.Seek(0, SeekOrigin.Begin);

            EBMLReader reader = new EBMLReader(source);

            if (!readEbmlHeader(reader, readTagParams)) return false;

            if (!reader.enterContainer(ID_SEGMENT))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "File is not a valid Matroska file");
                return false;
            }

            segmentOffset = reader.Position;
            if (readTagParams.PrepareForWriting)
            {
                reader.readVint();

                // Add the segment size as a zone on its own
                structureHelper.AddZone(segmentOffset, reader.Position - segmentOffset, ZONE_SEGMENT_SIZE, false);

                // Add each sub-element as a zone to track their movement and index them inside SeekHead
                reader.seek(segmentOffset);
                indexSilentZones(reader);
            }

            // Physical data
            reader.seek(segmentOffset);
            if (!readPhysicalData(reader)) return false;

            // SeekHead
            reader.seek(segmentOffset);
            if (readTagParams.PrepareForWriting) readSeekHeads(reader);

            // TODO fast find using SeekHead

            // Tags
            reader.seek(segmentOffset);
            readTags(reader, readTagParams);

            // Chapters
            reader.seek(segmentOffset);
            readChapters(reader, readTagParams);

            // Embedded pictures
            reader.seek(segmentOffset);
            readAttachments(reader, readTagParams);

            return true;
        }

        protected override void postprocessWrite(Stream s)
        {
            // Write Segment header = final size of the file over 8 bytes
            s.Seek(structureHelper.getCorrectedOffset(segmentOffset), SeekOrigin.Begin);
            s.Write(EBMLHelper.EncodeVint((ulong)(s.Length - 8 - s.Position), false));
        }

        /// <inheritdoc/>
        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;

            if (zone.StartsWith(ZONE_EBML_HEADER))
            {
                writeEbmlHeader(s);
                result = 1;
            }
            else if (zone.StartsWith(ZONE_SEGMENT_SIZE))
            {
                s.Write(StreamUtils.EncodeBEUInt64(0));
                result = 1;
            }
            else if (zone.StartsWith(ZONE_SEEKHEAD))
            {
                string[] parts = zone.Split('.');
                if (parts.Length > 1)
                {
                    var index = int.Parse(parts[1]);
                    writeSeekHead(s, zone, seekHeads[index], tag);
                }
                result = 1;
            }
            else if (zone.StartsWith(ZONE_TAGS))
            {
                result = writeTags(s, tag);
            }
            else if (zone.StartsWith(ZONE_ATTACHMENTS))
            {
                result = writeAttachments(s, tag);
            }
            else if (zone.StartsWith(ZONE_CHAPTERS))
            {
                result = writeChapters(s, tag);
            }

            return result;
        }

        private void writeEbmlHeader(Stream w)
        {
            w.Write(StreamUtils.EncodeBEUInt32(EBML_MAGIC_NUMBER));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            EBMLHelper.WriteElt(w, EBML_VERSION, 1);
            EBMLHelper.WriteElt(w, EBML_READVERSION, 1);
            EBMLHelper.WriteElt(w, EBML_MAXIDLENGTH, 4);
            EBMLHelper.WriteElt(w, EBML_MAXSIZELENGTH, 8);
            EBMLHelper.WriteElt(w, EBML_DOCTYPE, Utils.Latin1Encoding.GetBytes(docType));
            EBMLHelper.WriteElt(w, EBML_DOCTYPEVERSION, 4);
            EBMLHelper.WriteElt(w, EBML_DOCTYPEREADVERSION, 2);

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));
        }

        private void writeSeekHead(Stream w, string zoneName, List<Tuple<long, ulong>> seekHead, TagData tag)
        {
            w.Write(StreamUtils.EncodeBEUInt32(ID_SEEKHEAD));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            ISet<string> foundZones = new SortedSet<string>();
            foreach (var seek in seekHead) writeSeek(w, zoneName, seek.Item1, seek.Item2, tag, foundZones);

            // Add Seek for new elements to the 1st SeekHead
            if (zoneName.EndsWith('0'))
            {
                if (!foundZones.Any(z => z.StartsWith(ZONE_TAGS))) writeSeekTags(w, zoneName, tag);
                if (!foundZones.Any(z => z.StartsWith(ZONE_ATTACHMENTS))) writeSeekAttachments(w, zoneName, tag);
                if (!foundZones.Any(z => z.StartsWith(ZONE_CHAPTERS))) writeSeekChapters(w, zoneName, tag);
            }

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));
        }

        private void writeSeek(Stream w, string shZoneName, long id, ulong position, TagData tag, ISet<string> foundZones)
        {
            // Skip metadata zone if there's nothing to write
            if (ID_TAGS == id && !hasWritableTags(tag)) return;
            if (ID_ATTACHMENTS == id && !hasWritablePics(tag)) return;
            if (ID_CHAPTERS == id && !hasWritableChapters(tag)) return;


            using MemoryStream memStream = new MemoryStream();

            EBMLHelper.WriteElt(memStream, ID_SEEKID, (ulong)id);

            // Make position dynamic
            var firstShOffset = Zones.First(z => z.Name.StartsWith(ZONE_SEEKHEAD)).Offset;
            var zone = Zones.FirstOrDefault(z => z.Offset == (long)position + firstShOffset);
            if (zone != null)
            {
                string dataZoneId = zone.Name;
                FileStructureHelper.Zone dataZone = structureHelper.GetZone(dataZoneId);
                foundZones.Add(dataZoneId);

                structureHelper.AddPostProcessingIndex(w.Position + memStream.Position + 6, (uint)dataZone.Offset, true, dataZoneId, shZoneName, "", firstShOffset);
            }
            EBMLHelper.WriteElt32(memStream, ID_SEEKPOSITION, position);

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_SEEK, memStream);
        }

        private bool hasWritableTags(TagData tag)
        {
            return tag.HasUsableField() || tag.AdditionalFields.Any(f => f.Value.Length > 0 && isMetaFieldWritable(f));
        }

        private bool hasWritablePics(TagData tag)
        {
            return tag.Pictures.Any(isPictureWritable);
        }

        private static bool hasWritableChapters(TagData tag)
        {
            return tag.Chapters != null && tag.Chapters.Count > 0;
        }

        private void writeSeekTags(Stream w, string shZoneName, TagData tag)
        {
            if (hasWritableTags(tag)) writeNewSeek(w, shZoneName, ID_TAGS, ZONE_TAGS);
        }

        private void writeSeekAttachments(Stream w, string shZoneName, TagData tag)
        {
            if (hasWritablePics(tag)) writeNewSeek(w, shZoneName, ID_ATTACHMENTS, ZONE_ATTACHMENTS);
        }

        private void writeSeekChapters(Stream w, string shZoneName, TagData tag)
        {
            if (hasWritableChapters(tag)) writeNewSeek(w, shZoneName, ID_CHAPTERS, ZONE_CHAPTERS);
        }

        private void writeNewSeek(Stream w, string shZoneName, ulong id, string targetZone)
        {
            // Make position dynamic
            var zone = Zones.FirstOrDefault(z => z.Name.StartsWith(targetZone));
            if (zone == null) return;

            using MemoryStream memStream = new MemoryStream();

            EBMLHelper.WriteElt(memStream, ID_SEEKID, id);

            var firstShOffset = Zones.First(z => z.Name.StartsWith(ZONE_SEEKHEAD)).Offset;
            structureHelper.AddPostProcessingIndex(w.Position + memStream.Position + 6, (uint)zone.Offset, true, zone.Name, shZoneName, "", firstShOffset);
            EBMLHelper.WriteElt32(memStream, ID_SEEKPOSITION, (uint)(zone.Offset - firstShOffset));

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_SEEK, memStream);
        }

        private int writeTags(Stream w, TagData data)
        {
            int result = 0;

            IDictionary<Field, string> map = data.ToMap();
            var writtenFieldCodes = new HashSet<string>();

            IDictionary<string, int> targetTypeInverted = new Dictionary<string, int>();
            foreach (var metaType in targetTypePrefixesMapping) targetTypeInverted[metaType.Value] = metaType.Key;

            IDictionary<int, ISet<Tuple<string, string>>>
                tagFields = new Dictionary<int, ISet<Tuple<string, string>>>();
            foreach (Field frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    var parts = s.Split('.');
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, data, map);
                            var field = new Tuple<string, string>(parts[1].ToUpper(), value);

                            var metaType = targetTypeInverted[parts[0]];
                            if (!tagFields.ContainsKey(metaType)) tagFields.Add(metaType, new HashSet<Tuple<string, string>>());

                            var metaSet = tagFields[metaType];
                            metaSet.Add(field);

                            writtenFieldCodes.Add(s.ToUpper());
                            result++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in data.AdditionalFields.Where(isMetaFieldWritable))
            {
                var fieldCode = fieldInfo.NativeFieldCode.ToLower().Trim();
                var parts = fieldCode.Split('.');

                int metaType = TYPE_TRACK; // Default to track if no TargetTypeValue is explicitely set
                if (parts.Length > 1)
                {
                    metaType = targetTypeInverted[parts[0]];
                    fieldCode = parts[1];
                }

                var reforgedFieldCode = targetTypePrefixesMapping[metaType] + "." + fieldCode;
                if (writtenFieldCodes.Contains(reforgedFieldCode.ToUpper())) continue; // Setting a supported value trough AdditionalFields is illegal

                if (!tagFields.ContainsKey(metaType))
                    tagFields.Add(metaType, new HashSet<Tuple<string, string>>());

                var metaSet = tagFields[metaType];
                metaSet.Add(new Tuple<string, string>(fieldCode, FormatBeforeWriting(fieldInfo.Value)));

                result++;
            }

            if (0 == result) return 0;

            w.Write(StreamUtils.EncodeBEUInt32(ID_TAGS));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            // Actually write values
            foreach (var tagSet in tagFields)
            {
                if (tagSet.Value.Count > 0) writeTag(w, tagSet.Key, tagSet.Value);
            }

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));

            return result;
        }

        private static void writeTag(Stream w, int typeCode, ISet<Tuple<string, string>> fields)
        {
            using MemoryStream tagStream = new MemoryStream();

            using MemoryStream targetStream = new MemoryStream();
            EBMLHelper.WriteElt(targetStream, ID_TARGETTYPEVALUE, typeCode); // Mandatory info
            targetStream.Position = 0;
            EBMLHelper.WriteElt(tagStream, ID_TARGETS, targetStream);
            foreach (var field in fields) writeSimpleTag(tagStream, field.Item1, field.Item2);

            tagStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_TAG, tagStream);
        }

        private static void writeSimpleTag(Stream w, string code, string value)
        {
            using MemoryStream memStream = new MemoryStream();

            EBMLHelper.WriteElt(memStream, ID_TAGNAME, Encoding.UTF8.GetBytes(code));
            EBMLHelper.WriteElt(memStream, ID_TAGSTRING, Encoding.UTF8.GetBytes(value));
            EBMLHelper.WriteElt(memStream, ID_TAGLANGUAGE,
                Utils.Latin1Encoding.GetBytes("und")); // "und" for undefined; mandatory info
            EBMLHelper.WriteElt(memStream, ID_TAGDEFAULT, 1); // Mandatory info

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_SIMPLETAG, memStream);
        }

        private int writeAttachments(Stream w, TagData data)
        {
            w.Write(StreamUtils.EncodeBEUInt32(ID_ATTACHMENTS));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            int result = data.Pictures.Where(isPictureWritable).Sum(picInfo => writeAttachedFile(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType, picInfo.Description));

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));

            return result;
        }

        private static int writeAttachedFile(Stream w, byte[] data, string mimeType, PictureInfo.PIC_TYPE type, string description)
        {
            var name = type.Equals(PictureInfo.PIC_TYPE.Front) ? "cover" : type.ToString().ToLower();
            switch (ImageUtils.GetImageFormatFromMimeType(mimeType))
            {
                case ImageFormat.Jpeg:
                    name += ".jpg";
                    break;
                case ImageFormat.Png:
                    name += ".png";
                    break;
                case ImageFormat.Unsupported:
                case ImageFormat.Undefined:
                case ImageFormat.Gif:
                case ImageFormat.Bmp:
                case ImageFormat.Tiff:
                case ImageFormat.Webp:
                default: return 0; // Any other format is forbidden by specs
            }

            using MemoryStream memStream = new MemoryStream();

            EBMLHelper.WriteElt(memStream, ID_FILENAME, Encoding.UTF8.GetBytes(name));
            if (description.Length > 0) EBMLHelper.WriteElt(memStream, ID_FILEDESCRIPTION, Encoding.UTF8.GetBytes(description));
            EBMLHelper.WriteElt(memStream, ID_FILEMEDIATYPE, Utils.Latin1Encoding.GetBytes(mimeType));
            EBMLHelper.WriteElt(memStream, ID_FILEUID, Utils.LongRandom(new Random())); // Spec does say "as random as possible" <.<
            EBMLHelper.WriteElt(memStream, ID_FILEDATA, data);

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_ATTACHEDFILE, memStream);
            return 1;
        }

        private int writeChapters(Stream w, TagData data)
        {
            if (!hasWritableChapters(data)) return 0;

            w.Write(StreamUtils.EncodeBEUInt32(ID_CHAPTERS));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            writeEditionEntry(w, data);

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));

            return 1;
        }

        private void writeEditionEntry(Stream w, TagData data)
        {
            w.Write(StreamUtils.EncodeBEUInt16(ID_EDITIONENTRY));
            var sizeOffset = w.Position;
            // Use 8 bytes to represent size (yes, I am lazy)
            w.Write(StreamUtils.EncodeBEUInt64(0)); // Will be rewritten later

            // Generate a new ID if non-existent
            if (0 == editionEntryUid)
            {
                Random randomGenerator = new Random();
                editionEntryUid = Utils.LongRandom(randomGenerator);
            }
            EBMLHelper.WriteElt(w, ID_EDITIONUID, editionEntryUid);
            EBMLHelper.WriteElt(w, ID_EDITIONFLAGHIDDEN, 0);
            EBMLHelper.WriteElt(w, ID_EDITIONFLAGDEFAULT, 1);

            // Edition display (optional)
            if (data.hasKey(Field.CHAPTERS_TOC_DESCRIPTION))
            {
                using MemoryStream memStream = new MemoryStream();
                EBMLHelper.WriteElt(memStream, ID_EDITIONSTRING, Encoding.UTF8.GetBytes(data[Field.CHAPTERS_TOC_DESCRIPTION]));
                memStream.Position = 0;
                EBMLHelper.WriteElt(w, ID_EDITIONDISPLAY, memStream);
            }

            foreach (ChapterInfo info in data.Chapters) writeChapterAtom(w, info);

            var finalOffset = w.Position;
            w.Seek(sizeOffset, SeekOrigin.Begin);
            w.Write(EBMLHelper.EncodeVint((ulong)(finalOffset - sizeOffset - 8), false));
            w.Seek(finalOffset, SeekOrigin.Begin);
        }

        private static void writeChapterAtom(Stream w, ChapterInfo data)
        {
            using MemoryStream memStream = new MemoryStream();

            // Generate a new ID if non-existent
            if (0 == data.UniqueNumericID)
            {
                Random randomGenerator = new Random();
                data.UniqueNumericID = (uint)Utils.LongRandom(randomGenerator, 0, uint.MaxValue);
            }
            EBMLHelper.WriteElt(w, ID_CHAPTERUID, data.UniqueNumericID);
            if (data.UniqueID.Length > 0)
                EBMLHelper.WriteElt(memStream, ID_CHAPTERSTRINGUID, Encoding.UTF8.GetBytes(data.UniqueID));
            EBMLHelper.WriteElt(memStream, ID_CHAPTERFLAGHIDDEN, 0);
            EBMLHelper.WriteElt(memStream, ID_CHAPTERFLAGENABLED, 1);
            EBMLHelper.WriteElt(memStream, ID_CHAPTERTIMESTART, data.StartTime * 1000000);
            if (data.EndTime > 0) EBMLHelper.WriteElt(memStream, ID_CHAPTERTIMEEND, data.EndTime * 1000000);
            writeChapterDisplay(memStream, data.Title);

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_CHAPTERATOM, memStream);
        }

        private static void writeChapterDisplay(Stream w, string data)
        {
            using MemoryStream memStream = new MemoryStream();

            EBMLHelper.WriteElt(memStream, ID_CHAPTERSTRING, Encoding.UTF8.GetBytes(data));
            EBMLHelper.WriteElt(memStream, ID_CHAPTERLANGUAGE, Utils.Latin1Encoding.GetBytes("eng")); // Mandatory; "eng" is the default according to specs

            memStream.Position = 0;
            EBMLHelper.WriteElt(w, ID_CHAPTERDISPLAY, memStream);
        }


        /// <inheritdoc/>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public override async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            // Overriding this is mandatory as we need SeekHead to be updated after metadata have been removed
            TagData tag = prepareRemove();
            return await WriteAsync(s, tag, args);
        }

        // Create an empty tag for integration
        private TagData prepareRemove()
        {
            TagData result = new TagData();

            foreach (Field b in frameMapping.Values)
            {
                result.IntegrateValue(b, "");
            }

            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo)
                {
                    MarkedForDeletion = true
                };
                result.AdditionalFields.Add(emptyFieldInfo);
            }

            foreach (PictureInfo picInfo in EmbeddedPictures)
            {
                PictureInfo emptyPicInfo = new PictureInfo(picInfo)
                {
                    MarkedForDeletion = true
                };
                result.Pictures.Add(emptyPicInfo);
            }

            return result;
        }
    }
}