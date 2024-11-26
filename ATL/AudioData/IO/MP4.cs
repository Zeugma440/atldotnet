﻿using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.AudioData.FileStructureHelper;
using System.Linq;
using System.Collections.Concurrent;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for MP4 files manipulation (extensions : .MP4, .M4A, .M4B, .M4V, .M4P, .M4R, .AAX)
    /// 
    /// Implementation notes
    ///     - If the UDTA atom is absent as a direct child to the MOOV atom, ATL seeks the first TRAK that has an UDTA atom
    ///     and considers that one as the entire file's metadata
    ///     
    ///     - When removing a Track, physical chunks belonging to the track (i.e. those indexed by 'stco') won't be removed
    ///     
    ///     - When adding the exact same chapter picture to multiple chapters, that picture is written as many times as there are chapters
    ///     instead of being written once and referenced from each chapter
    /// 
    /// </summary>
	class MP4 : MetaDataIO, IAudioDataIO
    {
        // Bit rate type codes
        public const byte MP4_BITRATE_TYPE_UNKNOWN = 0;                      // Unknown
        public const byte MP4_BITRATE_TYPE_CBR = 1;                          // CBR
        public const byte MP4_BITRATE_TYPE_VBR = 2;                          // VBR

        // de facto default namespace for custom fields
        private const string DEFAULT_NAMESPACE = "com.apple.iTunes";

        private static readonly byte[] FILE_HEADER = Utils.Latin1Encoding.GetBytes("ftyp");

        private static readonly byte[] ILST_CORE_SIGNATURE = { 0, 0, 0, 8, 105, 108, 115, 116 }; // (int32)8 followed by "ilst" field code

        private const string ZONE_MP4_NOUDTA = "noudta";                // Placeholder for missing 'udta' atom
        private const string ZONE_MP4_NOMETA = "nometa";                // Placeholder for missing 'meta' atom
        private const string ZONE_MP4_ILST = "ilst";                    // When editing a file with an existing 'meta' atom
        private const string ZONE_MP4_CHPL = "chpl";                    // Nero chapters
        private const string ZONE_MP4_XTRA = "Xtra";                    // Specific fields (e.g. rating) inserted by Microsoft instead of using standard MP4 fields
        private const string ZONE_MP4_QT_CHAP_NOTREF = "qt_notref";     // Placeholder for missing track reference atom
        private const string ZONE_MP4_QT_CHAP_CHAP = "qt_chap_chap";    // Quicktime chapters track reference
        private const string ZONE_MP4_QT_CHAP_TXT_TRAK = "qt_trak_txt"; // Quicktime chapters text track
        private const string ZONE_MP4_QT_CHAP_PIC_TRAK = "qt_trak_pic"; // Quicktime chapters picture track
        private const string ZONE_MP4_QT_CHAP_MDAT = "qt_chap_mdat";    // Quicktime chapters data
        private const string ZONE_MP4_ADDITIONAL_UUIDS = "uuids";       // Zone to write extra UUIDs

        private const string ZONE_MP4_PHYSICAL_CHUNK = "chunk";         // Physical audio chunk referenced from stco or co64

        // Mapping between MP4 frame codes and ATL frame codes
        private static readonly Dictionary<string, Field> frameMapping_mp4 = new Dictionary<string, Field>() {
            { "©nam", Field.TITLE },
            { "titl", Field.TITLE },
            { "©alb", Field.ALBUM },
            { "©ART", Field.ARTIST },
            { "©art", Field.ARTIST },
            { "©cmt", Field.COMMENT },
            { "©day", Field.RECORDING_DATE_OR_YEAR },
            { "©gen", Field.GENRE },
            { "gnre", Field.GENRE },
            { "trkn", Field.TRACK_NUMBER_TOTAL },
            { "disk", Field.DISC_NUMBER_TOTAL },
            { "rtng", Field.RATING },
            { "rate", Field.RATING },
            { "©wrt", Field.COMPOSER },
            { "desc", Field.GENERAL_DESCRIPTION }, // Description
            { "©des", Field.GENERAL_DESCRIPTION }, // Long description
            { "cprt", Field.COPYRIGHT },
            { "aART", Field.ALBUM_ARTIST },
            { "©lyr", Field.LYRICS_UNSYNCH },
            { "©pub", Field.PUBLISHER },
            { "rldt", Field.PUBLISHING_DATE},
            { "prID", Field.PRODUCT_ID},
            { "©con", Field.CONDUCTOR },
            { "CONDUCTOR", Field.CONDUCTOR }, // aka ----:com.apple.iTunes:CONDUCTOR
            { "soal", Field.SORT_ALBUM },
            { "soaa", Field.SORT_ALBUM_ARTIST },
            { "soar", Field.SORT_ARTIST },
            { "sonm", Field.SORT_TITLE },
            { "©grp", Field.GROUP },
            { "©mvi", Field.SERIES_PART},
            { "©mvn", Field.SERIES_TITLE },
            { "ldes", Field.LONG_DESCRIPTION },
            { "tmpo", Field.BPM },
            { "©enc", Field.ENCODED_BY },
            { "©too", Field.ENCODER },
            { "LANGUAGE", Field.LANGUAGE }, // aka ----:com.apple.iTunes:LANGUAGE
            { "©isr", Field.ISRC },
            { "CATALOGNUMBER", Field.CATALOG_NUMBER }, // aka ----:com.apple.iTunes:CATALOGNUMBER
            { "LYRICIST", Field.LYRICIST } // aka ----:com.apple.iTunes:LYRICIST
        };

        // Mapping between MP4 frame codes and frame classes that aren't class 1 (UTF-8 text)
        // 0 = special / 21 = int8-16-24-32 / 22 = uint8-16-24-32
        private static readonly ConcurrentDictionary<string, byte> frameClasses_mp4 = new ConcurrentDictionary<string, byte>()
        {
            ["gnre"] = 0,
            ["trkn"] = 0,
            ["disk"] = 0,
            ["rtng"] = 21,
            ["tmpo"] = 21,
            ["cpil"] = 21,
            ["stik"] = 21,
            ["pcst"] = 21,
            ["purl"] = 0,
            ["egid"] = 0,
            ["tvsn"] = 22,
            ["tves"] = 21,
            ["pgap"] = 21,
            ["shwm"] = 21,
            ["hdvd"] = 21,
            ["©mvc"] = 21,
            ["©mvi"] = 21
        };

        private sealed class MP4Sample
        {
            public double Duration;
            public uint Size;
            public uint ChunkIndex;                     // 1-based index
            public long ChunkOffset;
            public long RelativeOffset;
        }

        private sealed class Uuid
        {
            public long position;
            public uint size;
            public string key = "";
            public string value = "";

            public bool isValid()
            {
                return 32 == key.Length && Utils.IsHex(key);
            }
        }

        // Inner technical information to remember for writing purposes
        private uint globalTimeScale;
        private readonly IDictionary<int, int> trackTimescales = new Dictionary<int, int>();

        private long trackCounterPosition;
        private int qtChapterTextTrackId;
        private int qtChapterPictureTrackId;
        private int maxTrackId;

        private long initialPaddingOffset;
        private uint initialPaddingSize;
        private byte[] chapterTextTrackEdits;
        private byte[] chapterPictureTrackEdits;
        private long udtaOffset;

        private byte bitrateTypeID;
        private double bitrate;
        private double calculatedDurationMs; // Calculated track duration, in milliseconds

        private AudioDataManager.SizeInfo sizeInfo;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR => MP4_BITRATE_TYPE_VBR == bitrateTypeID;

        public AudioFormat AudioFormat { get; }
        public int CodecFamily => AudioDataIOFactory.CF_LOSSY;

        public double BitRate => bitrate / 1000.0;
        public int BitDepth => -1; // Irrelevant for lossy formats
        public double Duration => getDuration();

        public int SampleRate { get; private set; }

        public string FileName { get; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V1 };
        }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // IMetaDataIO
        protected override int getDefaultTagOffset() => TO_BUILTIN;
        protected override MetaDataIOFactory.TagType getImplementedTagType() => MetaDataIOFactory.TagType.NATIVE;
        public override byte FieldCodeFixedLength => 4;

        protected override bool isLittleEndian => false;

        protected override byte ratingConvention => RC_APE;

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            if (frameMapping_mp4.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        /// <inheritdoc/>
        protected override bool canHandleNonStandardField(string code, string value) => true;


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            bitrateTypeID = MP4_BITRATE_TYPE_UNKNOWN;

            globalTimeScale = 0;
            trackTimescales.Clear();
            trackCounterPosition = 0;
            qtChapterTextTrackId = 0;
            qtChapterPictureTrackId = 0;
            maxTrackId = 0;
            initialPaddingSize = 0;
            initialPaddingOffset = -1;
            AudioDataOffset = -1;
            AudioDataSize = 0;
            udtaOffset = 0;

            bitrate = 0;
            SampleRate = 0;
            calculatedDurationMs = 0;

            chapterTextTrackEdits = null;
            chapterPictureTrackEdits = null;

            ResetData();
        }

        public MP4(string fileName, AudioFormat format)
        {
            this.FileName = fileName;
            AudioFormat = format;
            resetData();
        }

        // ********************** Private functions & procedures *********************

        private static void addFrameClass(string frameCode, byte frameClass)
        {
            frameClasses_mp4.TryAdd(frameCode, frameClass);
        }

        // Calculate duration time
        private double getDuration()
        {
            return calculatedDurationMs;
        }

        public static bool IsValidHeader(byte[] data)
        {
            // Examine bytes 4 to 8
            byte[] usefulData = new byte[4];
            Array.Copy(data, 4, usefulData, 0, 4);
            return StreamUtils.ArrBeginsWith(usefulData, FILE_HEADER);
        }

        // Get header type of the file
        private bool recognizeHeaderType(BinaryReader Source)
        {
            Source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            return IsValidHeader(Source.ReadBytes(8));
        }

        private void readQTChapters(BinaryReader source, IList<MP4Sample> chapterTextTrackSamples, IList<MP4Sample> chapterPictureTrackSamples)
        {
            if (3 == Settings.MP4_readChaptersFormat) return;

            if (null == tagData.Chapters) tagData.Chapters = new List<ChapterInfo>(); else tagData.Chapters.Clear();
            double cumulatedDuration = 0;

            // Text chapters are "master data"; picture chapters get attached to them
            for (int i = 0; i < chapterTextTrackSamples.Count; i++)
            {
                MP4Sample textSample = chapterTextTrackSamples[i];
                MP4Sample pictureSample = i < chapterPictureTrackSamples.Count ? chapterPictureTrackSamples[i] : null;
                if (textSample.ChunkOffset > 0)
                {
                    ChapterInfo chapter = new ChapterInfo();

                    source.BaseStream.Seek(textSample.ChunkOffset + textSample.RelativeOffset, SeekOrigin.Begin);
                    ushort strDataSize = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

                    chapter.Title = Encoding.UTF8.GetString(source.ReadBytes(strDataSize));
                    chapter.StartTime = (uint)Math.Round(cumulatedDuration);
                    cumulatedDuration += textSample.Duration * 1000;
                    chapter.EndTime = (uint)Math.Round(cumulatedDuration);

                    if (pictureSample != null && pictureSample.ChunkOffset > 0 && pictureSample.Size > 0)
                    {
                        source.BaseStream.Seek(pictureSample.ChunkOffset + pictureSample.RelativeOffset, SeekOrigin.Begin);
                        byte[] data = new byte[pictureSample.Size];
                        source.Read(data, 0, (int)pictureSample.Size);
                        chapter.Picture = PictureInfo.fromBinaryData(data, PictureInfo.PIC_TYPE.Generic, getImplementedTagType());
                    }

                    tagData.Chapters.Add(chapter);
                }
            }
        }

        /// <summary>
        /// Read MP4 header data
        /// http://www.jiscdigitalmedia.ac.uk/guide/aac-audio-and-the-mp4-media-format
        /// http://atomicparsley.sourceforge.net/mpeg-4files.html
        /// https://developer.apple.com/library/archive/documentation/QuickTime/QTFF/QTFFChap2/qtff2.html
        /// - Metadata is located in the moov/udta/meta/ilst atom
        /// - Physical metadata are located in the moov/trak atoms
        /// - Binary physical data are located in the mdat atoms
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="readTagParams">Reading parameters</param>
        private bool readMP4(BinaryReader source, ReadTagParams readTagParams)
        {
            byte[] data32 = new byte[4];

            IList<long> audioTrackOffsets = new List<long>(); // Offset of all detected audio/video tracks (tracks with a media type of 'soun' or 'vide')
            IList<MP4Sample> chapterTextTrackSamples = new List<MP4Sample>(); // If non-empty, quicktime chapters have been detected
            IList<MP4Sample> chapterPictureTrackSamples = new List<MP4Sample>(); // If non-empty, quicktime chapters have been detected
            IDictionary<int, IList<int>> chapterTrackIndexes = new Dictionary<int, IList<int>>(); // Key is track index (1-based); lists are chapter tracks indexes (1-based)


            // TODO PERF - try and cache the whole tree structure to optimize browsing through nodes

            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // FTYP atom
            source.Read(data32, 0, 4);
            var atomSize = StreamUtils.DecodeBEUInt32(data32);
            source.BaseStream.Seek(atomSize - 4, SeekOrigin.Current);

            // MOOV atom
            uint moovSize = navigateToAtom(source, "moov"); // === Physical data
            if (0 == moovSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "moov atom could not be found; aborting read");
                return false;
            }

            var moovPosition = source.BaseStream.Position;
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_NOUDTA);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_NOMETA);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_ILST);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_XTRA);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_CHPL);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_QT_CHAP_PIC_TRAK);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_QT_CHAP_TXT_TRAK);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_QT_CHAP_NOTREF);
                structureHelper.AddSize(moovPosition - 8, moovSize, ZONE_MP4_QT_CHAP_CHAP);
            }

            // === Physical data header
            if (0 == navigateToAtom(source.BaseStream, "mvhd"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mvhd atom could not be found; aborting read");
                return false;
            }
            byte version = source.ReadByte();
            source.BaseStream.Seek(3, SeekOrigin.Current); // 3-byte flags
            if (1 == version) source.BaseStream.Seek(16, SeekOrigin.Current);
            else source.BaseStream.Seek(8, SeekOrigin.Current);

            globalTimeScale = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            long timeLengthPerSec;
            if (1 == version) timeLengthPerSec = StreamUtils.DecodeBEInt64(source.ReadBytes(8));
            else timeLengthPerSec = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            calculatedDurationMs = timeLengthPerSec * 1000.0 / globalTimeScale;
            trackCounterPosition = source.BaseStream.Position + 76;

            source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            byte currentTrakIndex = 0;
            long trakSize;

            // Loop through tracks
            do
            {
                trakSize = readTrack(source, readTagParams, ++currentTrakIndex, chapterTextTrackSamples, chapterPictureTrackSamples, chapterTrackIndexes, audioTrackOffsets, moovPosition, moovSize);
                if (-1 == trakSize)
                {
                    // TODO do better than that
                    currentTrakIndex = 0; // Convention to start reading from index 1 again
                    source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
                    trakSize = 1;
                }
            }
            while (trakSize > 0);

            if (Utils.ApproxEquals(calculatedDurationMs, 0)) readMoof(source.BaseStream);

            // Look for uuid atoms
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            Uuid uuid;
            do
            {
                uuid = readUuid(source.BaseStream);
                if (uuid.isValid())
                {
                    if (XmpTag.UUID_XMP == uuid.key)
                    {
                        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(uuid.value));
                        XmpTag.FromStream(stream, this, readTagParams, stream.Length);
                    }
                    else
                    {
                        SetMetaField("uuid." + uuid.key, uuid.value, readTagParams.ReadAllMetaFrames);
                    }

                    if (readTagParams.PrepareForWriting)
                    {
                        structureHelper.AddZone(uuid.position, uuid.size, "uuid." + uuid.key);
                    }
                }
            } while (uuid.size > 0);


            // Seek audio data segment to calculate mean bitrate 
            // NB : This figure is closer to truth than the "average bitrate" recorded in the esds/m4ds header

            // == Audio binary data, chapter or subtitle data
            // Per convention, audio binary data always seems to be in the 1st mdat atom of the file
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            var mdatAtomData = navigateToAtomSize(source.BaseStream, "mdat");
            uint mdatSize = mdatAtomData.Item1;
            int mdatHeaderSize = mdatAtomData.Item2;
            if (0 == mdatSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mdat atom could not be found; aborting read");
                return false;
            }
            long mdatOffset = source.BaseStream.Position;
            AudioDataOffset = mdatOffset - mdatHeaderSize;
            AudioDataSize = mdatSize;
            bitrate = (int)Math.Round(mdatSize * 8 / calculatedDurationMs * 1000.0, 0);

            // Set zone for new UUIDs to write at the end of the file
            if (readTagParams.PrepareForWriting) structureHelper.AddZone(AudioDataOffset + AudioDataSize, 0, ZONE_MP4_ADDITIONAL_UUIDS);


            // == Quicktime chapters management

            // No QT chapter track found -> Assign free track ID
            if (0 == qtChapterTextTrackId)
            {
                qtChapterTextTrackId = currentTrakIndex++;
                trackTimescales[qtChapterTextTrackId] = 1000; // Easier to encode base 10 timecodes
            }
            if (0 == qtChapterPictureTrackId)
            {
                qtChapterPictureTrackId = currentTrakIndex;
                trackTimescales[qtChapterPictureTrackId] = 1000; // Easier to encode base 10 timecodes
            }

            // QT chapters have been detected while browsing tracks
            if (chapterTextTrackSamples.Count > 0) readQTChapters(source, chapterTextTrackSamples, chapterPictureTrackSamples);

            // If QT chapters data is missing, reserve zones to write QT chapters
            if (readTagParams.PrepareForWriting)
            {
                // Candidates for chapters MDAT zone
                // NB : limit zone size to the actual size of the chapters
                long chapMdatOffset = -1; // Offset of the MDAT atom hosting chapters
                long chapMdatDataSize = -1; // Size of chapters data inside the MDAT atom
                uint chapMdatChapSize = 0; // Size of the entire MDAT atom (to properly rewrite the zone size header)

                if (Settings.MP4_createQuicktimeChapters && (0 == chapterTextTrackSamples.Count || 0 == chapterPictureTrackSamples.Count))
                {
                    source.BaseStream.Seek(moovPosition, SeekOrigin.Begin); // TRAK before UDTA
                    atomSize = navigateToAtom(source, "udta");
                    if (atomSize > 0)
                    {
                        if (0 == chapterTextTrackSamples.Count) structureHelper.AddZone(source.BaseStream.Position - 8, 0, ZONE_MP4_QT_CHAP_TXT_TRAK);
                        if (0 == chapterPictureTrackSamples.Count) structureHelper.AddZone(source.BaseStream.Position - 8, 0, ZONE_MP4_QT_CHAP_PIC_TRAK);
                    }

                    // By default, attach to-be text and image data to the first MDAT atom
                    chapMdatOffset = AudioDataOffset;
                    chapMdatDataSize = 0;
                    chapMdatChapSize = mdatSize;
                }

                // If QT chapters are present, record the current zone for chapters data
                if (chapterTextTrackSamples.Count > 0 && (Settings.MP4_keepExistingChapters || Settings.MP4_createQuicktimeChapters))
                {
                    long minChapterOffset = chapterTextTrackSamples.Min(sample => sample.ChunkOffset);

                    // Detect if QT chapters are interleaved
                    long previousEndOffset = 0;
                    foreach (MP4Sample sample in chapterTextTrackSamples)
                    {
                        if (0 == previousEndOffset) previousEndOffset = sample.ChunkOffset + sample.RelativeOffset + sample.Size;
                        else if (previousEndOffset == sample.ChunkOffset + sample.RelativeOffset)
                        {
                            previousEndOffset = sample.ChunkOffset + sample.RelativeOffset + sample.Size;
                        }
                        else
                        {
                            structureHelper.RemoveZone(ZONE_MP4_QT_CHAP_NOTREF);
                            structureHelper.RemoveZone(ZONE_MP4_QT_CHAP_CHAP);
                            structureHelper.RemoveZone(ZONE_MP4_QT_CHAP_TXT_TRAK);
                            structureHelper.RemoveZone(ZONE_MP4_QT_CHAP_PIC_TRAK);
                            structureHelper.RemoveZone(ZONE_MP4_QT_CHAP_MDAT);
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "ATL does not support writing non-contiguous (e.g. interleaved with audio data) Quicktime chapters; ignoring Quicktime chapters.");
                            return true;
                        }
                    }

                    // Scan all MDAT atoms starting from the first one to detect the one containing existing chapters
                    source.BaseStream.Seek(mdatOffset, SeekOrigin.Begin);
                    long chapterTextSize = chapterTextTrackSamples.Sum(sample => sample.Size);
                    long chapterPictureSize = chapterPictureTrackSamples.Sum(sample => sample.Size);
                    do
                    {
                        // On some files, there's a single MDAT atom that contains both chapter references and audio data
                        // => limit zone size to the actual size of the chapters
                        // TODO handle non-contiguous chapters (e.g. chapter data interleaved with audio data)
                        if (minChapterOffset >= source.BaseStream.Position && minChapterOffset < source.BaseStream.Position - mdatHeaderSize + mdatSize)
                        {
                            chapMdatOffset = source.BaseStream.Position - mdatHeaderSize;
                            // Zone size = size of chapter data (text and pictures)
                            chapMdatDataSize = chapterTextSize + chapterPictureSize;
                            chapMdatChapSize = mdatSize;
                        }

                        source.BaseStream.Seek(mdatSize - mdatHeaderSize, SeekOrigin.Current);
                        mdatAtomData = navigateToAtomSize(source.BaseStream, "mdat");
                        mdatSize = mdatAtomData.Item1;
                        mdatHeaderSize = mdatAtomData.Item2;
                    } while (mdatSize > 0);
                } // QT chapters are present

                // Memorize the definitive chapter data location as a zone
                if (chapMdatDataSize > -1)
                {
                    structureHelper.AddZone(chapMdatOffset + mdatHeaderSize, chapMdatDataSize, ZONE_MP4_QT_CHAP_MDAT);
                    structureHelper.AddSize(chapMdatOffset, chapMdatChapSize, ZONE_MP4_QT_CHAP_MDAT);
                }
            } // Write mode


            // Read user data which contains metadata and Nero chapters
            readUserData(source, readTagParams, moovPosition, moovSize);

            // == Padding management
            // Seek the generic padding atom
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            initialPaddingSize = navigateToAtom(source.BaseStream, "free");
            if (initialPaddingSize > 0) tagData.PaddingSize = initialPaddingSize;

            if (readTagParams.PrepareForWriting)
            {
                // Padding atom found
                if (initialPaddingSize > 0)
                {
                    initialPaddingOffset = source.BaseStream.Position - 8;
                    structureHelper.AddZone(source.BaseStream.Position - 8, (int)initialPaddingSize, PADDING_ZONE_NAME);
                    structureHelper.AddSize(source.BaseStream.Position - 8, (int)initialPaddingSize, PADDING_ZONE_NAME, PADDING_ZONE_NAME);
                }
                else // Padding atom not found
                {
                    if (Settings.AddNewPadding) // Create a virtual position to insert a new padding zone
                    {
                        structureHelper.AddZone(source.BaseStream.Position - 8, 0, PADDING_ZONE_NAME);
                        structureHelper.AddSize(source.BaseStream.Position - 8, 0, PADDING_ZONE_NAME, PADDING_ZONE_NAME);
                    }
                }
            }

            return true;
        }

        // Try getting duration from moof atoms (fragmented MP4), if they exist
        private void readMoof(Stream s)
        {
            long durationAll = 0;
            s.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            uint moofSize = navigateToAtom(s, "moof");
            while (moofSize > 0)
            {
                var moofOffset = s.Position;
                if (0 == navigateToAtom(s, "traf")) break;
                long trafOffset = s.Position;
                if (0 == navigateToAtom(s, "tfhd")) break;
                s.Seek(1, SeekOrigin.Current); // Version

                byte[] data = new byte[4];
                if (s.Read(data, 0, 3) < 3) break;
                int flags = StreamUtils.DecodeBEInt24(data);
                if (0 == (flags & 0x00000008)) break;

                if (s.Read(data, 0, 4) < 4) break; // Track ID
                if ((flags & 0x00000001) > 0) s.Seek(8, SeekOrigin.Current); // base_data_offset
                if ((flags & 0x00000002) > 0) s.Seek(4, SeekOrigin.Current); // sample_description_index
                if (s.Read(data, 0, 4) < 4) break;
                int defaultSampleDuration = StreamUtils.DecodeBEInt32(data);

                s.Seek(trafOffset, SeekOrigin.Begin);
                uint trunSize = navigateToAtom(s, "trun");
                while (trunSize > 0)
                {
                    var trunOffset = s.Position;
                    s.Seek(1, SeekOrigin.Current); // Version
                    if (s.Read(data, 0, 3) < 3) break;
                    flags = StreamUtils.DecodeBEInt24(data);
                    if (s.Read(data, 0, 4) < 4) break;
                    int sampleCount = StreamUtils.DecodeBEInt32(data);

                    for (int i = 0; i < sampleCount; i++)
                    {
                        if ((flags & 0x00000100) > 0) // Sample has its own duration
                        {
                            if ((flags & 0x00000001) > 0) s.Seek(4, SeekOrigin.Current); // data_offset
                            if ((flags & 0x00000004) > 0) s.Seek(4, SeekOrigin.Current); // first_sample_flags
                            if (s.Read(data, 0, 4) < 4) break;
                            durationAll += StreamUtils.DecodeBEInt32(data);
                        }
                        else durationAll += defaultSampleDuration;
                    }

                    // Seek next trun atom
                    s.Seek(trunOffset + trunSize - 8, SeekOrigin.Begin);
                    if (s.Read(data, 0, 4) < 4) break;
                    trunSize = StreamUtils.DecodeBEUInt32(data);
                    if (s.Read(data, 0, 4) < 4) break;
                    if (!Utils.Latin1Encoding.GetString(data).Equals("trun")) break;
                }

                // Seek next moof atom, skipping the mdat atom
                s.Seek(moofOffset + moofSize - 8, SeekOrigin.Begin);
                if (s.Read(data, 0, 4) < 4) break;
                var mdatSize = StreamUtils.DecodeBEUInt32(data);
                s.Seek(mdatSize - 4, SeekOrigin.Current);
                if (s.Read(data, 0, 4) < 4) break;
                moofSize = StreamUtils.DecodeBEUInt32(data);
                if (s.Read(data, 0, 4) < 4) break;
                var atomName = Utils.Latin1Encoding.GetString(data);
                if (!atomName.Equals("moof")) break;
            }

            calculatedDurationMs = durationAll * 1000.0 / SampleRate;
        }

        private long readTrack(
            BinaryReader source,
            ReadTagParams readTagParams,
            int currentTrakIndex,
            IList<MP4Sample> chapterTextTrackSamples,
            IList<MP4Sample> chapterPictureTrackSamples,
            IDictionary<int, IList<int>> chapterTrackIndexes,
            IList<long> mediaTrackOffsets,
            long moovPosition,
            long moovSize
        )
        {
            int mediaTimeScale = 1000;

            uint int32Data = 0;
            byte[] data32 = new byte[4];
            byte[] data64 = new byte[8];

            bool isCurrentTrackFirstChapterTextTrack = false; // First chapter text track which should contain chapter titles (as opposed to URLs)
            bool isCurrentTrackOtherChapterTrack = false; // Generic marker for other chapter-related text tracks
            bool isCurrentTrackFirstChapterPicturesTrack = false; // First chapter picture track which should contain chapter "covers"
            bool isCurrentTrackFirstAudioTrack = false;

            string trackZoneName = "";

            uint trakSize = navigateToAtom(source.BaseStream, "trak");
            if (0 == trakSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "total tracks found : " + (currentTrakIndex - 1));
                return 0;
            }
            var trakPosition = source.BaseStream.Position - 8;

            // Read track ID
            if (0 == navigateToAtom(source.BaseStream, "tkhd"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "trak.tkhd atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            int intLength = 0 == source.ReadByte() ? 4 : 8;
            source.BaseStream.Seek(3, SeekOrigin.Current); // Flags
            source.BaseStream.Seek(intLength * 2, SeekOrigin.Current); // Creation & Modification Dates

            int trackId = StreamUtils.DecodeBEInt32(source.ReadBytes(4));

            // Detect the track type
            source.BaseStream.Seek(trakPosition + 8, SeekOrigin.Begin);
            if (0 == navigateToAtom(source, "mdia"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }

            long mdiaPosition = source.BaseStream.Position;
            if (chapterTrackIndexes.Count > 0)
            {
                if (0 == navigateToAtom(source, "mdhd"))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.mdhd atom could not be found; aborting read on track " + currentTrakIndex);
                    source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                    return trakSize;
                }

                byte mdhdVersion = source.ReadByte();
                source.BaseStream.Seek(3, SeekOrigin.Current); // Flags

                if (0 == mdhdVersion) source.BaseStream.Seek(8, SeekOrigin.Current);
                else source.BaseStream.Seek(16, SeekOrigin.Current); // Creation and modification date

                mediaTimeScale = StreamUtils.DecodeBEInt32(source.ReadBytes(4));

                source.BaseStream.Seek(mdiaPosition, SeekOrigin.Begin);
            }
            trackTimescales[trackId] = mediaTimeScale;

            if (0 == navigateToAtom(source, "hdlr"))
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
            isCurrentTrackOtherChapterTrack = false;
            if ("text".Equals(mediaType) && chapterTrackIndexes.Count > 0)
            {
                foreach (IList<int> list in chapterTrackIndexes.Values)
                {
                    if (trackId == list[0])
                    {
                        isCurrentTrackFirstChapterTextTrack = true;
                        isCurrentTrackOtherChapterTrack = false;
                        break;
                    }
                    foreach (int index in list)
                    {
                        if (trackId == index)
                        {
                            isCurrentTrackOtherChapterTrack = true;
                            break;
                        }
                    }
                }
            }
            else if ("soun".Equals(mediaType) || "vide".Equals(mediaType))
            {
                mediaTrackOffsets.Add(trakPosition);
                isCurrentTrackFirstAudioTrack = 1 == mediaTrackOffsets.Count;
            }

            if (readTagParams.PrepareForWriting && isCurrentTrackOtherChapterTrack && !isCurrentTrackFirstChapterTextTrack)
            {
                trackZoneName = ZONE_MP4_QT_CHAP_TXT_TRAK + "." + trackId;
                structureHelper.AddZone(trakPosition, (int)trakSize, trackZoneName);
                structureHelper.AddSize(moovPosition - 8, moovSize, trackZoneName);
            }

            source.BaseStream.Seek(mdiaPosition, SeekOrigin.Begin);
            if (0 == navigateToAtom(source, "minf"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.minf atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            if (0 == navigateToAtom(source, "stbl"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "mdia.minf.stbl atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            long stblPosition = source.BaseStream.Position;

            // Look for sample rate
            if (0 == navigateToAtom(source, "stsd"))
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

                // Descriptors for audio
                if (descFormat.Equals("mp4a") || descFormat.Equals("enca") || descFormat.Equals("samr") || descFormat.Equals("sawb") || descFormat.Equals("Opus"))
                {
                    source.BaseStream.Seek(6, SeekOrigin.Current); // SampleEntry / 6-byte reserved zone set to zero
                    source.BaseStream.Seek(2, SeekOrigin.Current); // SampleEntry / Data reference index

                    source.BaseStream.Seek(8, SeekOrigin.Current); // AudioSampleEntry / 8-byte reserved zone

                    ushort channels = StreamUtils.DecodeBEUInt16(source.ReadBytes(2)); // Channel count
                    ChannelsArrangement = GuessFromChannelNumber(channels);

                    source.BaseStream.Seek(2, SeekOrigin.Current); // Sample size
                    source.BaseStream.Seek(2, SeekOrigin.Current); // Quicktime stuff (should be length 4, but sampleRate doesn't work when it is...)

                    SampleRate = (int)StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                }
                else if (descFormat.Equals("jpeg")) // Descriptor for picture (slides / chapter pictures)
                {
                    isCurrentTrackFirstChapterPicturesTrack = chapterTrackIndexes.Values.Any(list => list.Contains(trackId));
                }
                else
                {
                    source.BaseStream.Seek(int32Data - 4, SeekOrigin.Current);
                }
            }

            // Look for "trak.tref.chap" atom to detect QT chapters for current track
            source.BaseStream.Seek(trakPosition + 8, SeekOrigin.Begin);
            uint trefSize = navigateToAtom(source, "tref");
            long trefPosition = source.BaseStream.Position - 8;
            // Existing, non-empty tref atom
            if (trefSize > 8 && 0 == chapterTrackIndexes.Count)
            {
                bool parsePreviousTracks = false;
                uint chapSize = navigateToAtom(source, "chap");
                if (chapSize > 0 && (Settings.MP4_keepExistingChapters || Settings.MP4_createQuicktimeChapters))
                {
                    structureHelper.AddZone(source.BaseStream.Position - 8, (int)chapSize, ZONE_MP4_QT_CHAP_CHAP);
                    structureHelper.AddSize(trakPosition, trakSize, ZONE_MP4_QT_CHAP_CHAP);
                    structureHelper.AddSize(trefPosition, trefSize, ZONE_MP4_QT_CHAP_CHAP);
                }
                if (chapSize > 8)
                {
                    IList<int> thisTrackIndexes = new List<int>();
                    for (int i = 0; i < (chapSize - 8) / 4; i++)
                    {
                        thisTrackIndexes.Add(StreamUtils.DecodeBEInt32(source.ReadBytes(4)));
                    }
                    chapterTrackIndexes.Add(trackId, thisTrackIndexes);

                    foreach (int i in thisTrackIndexes)
                    {
                        if (i < trackId)
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
            else if (isCurrentTrackFirstAudioTrack && Settings.MP4_createQuicktimeChapters) // Only add QT chapters to the 1st detected audio or video track
            {
                if (0 == trefSize) // No atom at all
                {
                    structureHelper.AddZone(trakPosition + trakSize, 0, ZONE_MP4_QT_CHAP_NOTREF);
                    structureHelper.AddSize(trakPosition, trakSize, ZONE_MP4_QT_CHAP_NOTREF);
                }
                else if (trefSize <= 8) // Existing empty atom
                {
                    structureHelper.AddZone(trefPosition, trefSize, ZONE_MP4_QT_CHAP_NOTREF);
                    structureHelper.AddSize(trakPosition, trakSize, ZONE_MP4_QT_CHAP_NOTREF);
                }
            }

            // Read chapters textual data
            if (isCurrentTrackFirstChapterTextTrack)
            {
                uint result = readQtChapter(source, readTagParams, stblPosition, trakPosition, trakSize, trackId, chapterTextTrackSamples, mediaTimeScale, true);
                if (result > 0) return int32Data; // An error has occured
            }

            // Read chapters picture data
            if (isCurrentTrackFirstChapterPicturesTrack)
            {
                uint result = readQtChapter(source, readTagParams, stblPosition, trakPosition, trakSize, trackId, chapterPictureTrackSamples, mediaTimeScale, false);
                if (result > 0) return int32Data; // An error has occured
            }

            source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);

            // Samples analysis
            var atomSize = navigateToAtom(source, "stsz");
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stsz atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            uint blocByteSizeForAll = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
            if (0 == blocByteSizeForAll) // If value other than 0, same size everywhere => CBR
            {
                uint nbSizes = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                uint max = 0;
                uint min = uint.MaxValue;

                for (int i = 0; i < nbSizes; i++)
                {
                    source.Read(data32, 0, 4);
                    int32Data = StreamUtils.DecodeBEUInt32(data32);
                    min = Math.Min(min, int32Data);
                    max = Math.Max(max, int32Data);

                    if (isCurrentTrackFirstChapterTextTrack) chapterTextTrackSamples[i].Size = int32Data;
                    if (isCurrentTrackFirstChapterPicturesTrack) chapterPictureTrackSamples[i].Size = int32Data;
                }

                // VBR detection : if the gap between the smallest and the largest sample size is no more than 1%, we can consider the file is CBR; if not, VBR
                if (isCurrentTrackFirstAudioTrack)
                {
                    bitrateTypeID = min * 1.01 < max ? MP4_BITRATE_TYPE_VBR : MP4_BITRATE_TYPE_CBR;
                }
            }
            else
            {
                if (isCurrentTrackFirstAudioTrack) bitrateTypeID = MP4_BITRATE_TYPE_CBR;
                if (isCurrentTrackFirstChapterTextTrack)
                    foreach (var tt in chapterTextTrackSamples) tt.Size = blocByteSizeForAll;
                if (isCurrentTrackFirstChapterPicturesTrack)
                    foreach (var pt in chapterPictureTrackSamples) pt.Size = blocByteSizeForAll;
            }

            // Adjust individual sample offsets using their size for those that are in position > 0 in the same chunk
            if (isCurrentTrackFirstChapterTextTrack)
            {
                uint currentChunkIndex = uint.MaxValue;
                uint cumulatedChunkOffset = 0;

                foreach (var tt in chapterTextTrackSamples)
                {
                    if (tt.ChunkIndex == currentChunkIndex)
                    {
                        tt.RelativeOffset = cumulatedChunkOffset;
                    }
                    else
                    {
                        currentChunkIndex = tt.ChunkIndex;
                        cumulatedChunkOffset = 0;
                    }
                    cumulatedChunkOffset += tt.Size;
                }
            }
            if (isCurrentTrackFirstChapterPicturesTrack)
            {
                uint currentChunkIndex = uint.MaxValue;
                uint cumulatedChunkOffset = 0;

                foreach (var pt in chapterPictureTrackSamples)
                {
                    if (pt.ChunkIndex == currentChunkIndex)
                    {
                        pt.RelativeOffset = cumulatedChunkOffset;
                    }
                    else
                    {
                        currentChunkIndex = pt.ChunkIndex;
                        cumulatedChunkOffset = 0;
                    }
                    cumulatedChunkOffset += pt.Size;
                }
            }

            if (!isCurrentTrackFirstChapterPicturesTrack && !isCurrentTrackFirstChapterTextTrack)
                maxTrackId = Math.Max(maxTrackId, trackId);

            /*
            * "Physical" audio chunks are referenced by position (offset) in moov.trak.mdia.minf.stbl.stco / co64
            * => They have to be rewritten if the position (offset) of the 'mdat' atom changes
            */
            if (readTagParams.PrepareForWriting || isCurrentTrackFirstChapterTextTrack || isCurrentTrackFirstChapterPicturesTrack)
            {
                source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
                var atomPosition = source.BaseStream.Position;
                byte nbBytes;

                // Chunk offsets
                if (navigateToAtom(source, "stco") > 0)
                {
                    nbBytes = 4;
                }
                else
                {
                    source.BaseStream.Seek(atomPosition, SeekOrigin.Begin);
                    if (navigateToAtom(source, "co64") > 0)
                    {
                        nbBytes = 8;
                    }
                    else
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "neither stco, nor co64 atoms could not be found; aborting read on track " + currentTrakIndex);
                        source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                        return trakSize;
                    }
                }

                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                var nbChunkOffsets = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                for (int i = 0; i < nbChunkOffsets; i++)
                {
                    object valueObj;
                    long valueLong;
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

                    if (isCurrentTrackFirstChapterTextTrack) // Use the offsets to find position for QT chapter titles
                    {
                        foreach (var tt in chapterTextTrackSamples)
                        {
                            if (tt.ChunkIndex == i + 1) tt.ChunkOffset = valueLong;
                        }
                    }
                    else if (isCurrentTrackFirstChapterPicturesTrack)
                    {
                        // Use the offsets to find position for QT chapter pictures
                        foreach (var pt in chapterPictureTrackSamples)
                        {
                            if (pt.ChunkIndex == i + 1) pt.ChunkOffset = valueLong;
                        }
                    }
                    else if (!isCurrentTrackOtherChapterTrack) // Don't need to save chunks for chapters since they are entirely rewritten
                    {
                        string zoneName = ZONE_MP4_PHYSICAL_CHUNK + "." + currentTrakIndex + "." + i;
                        structureHelper.AddZone(valueLong, 0, zoneName, false, false);
                        structureHelper.AddIndex(source.BaseStream.Position - nbBytes, valueObj, false, zoneName);
                    }
                } // Chunk offsets
            }

            source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);

            return trakSize;
        }

        private uint readQtChapter(
            BinaryReader source,
            ReadTagParams readTagParams,
            long stblPosition,
            long trakPosition,
            uint trakSize,
            int currentTrakIndex,
            IList<MP4Sample> chapterTrackSamples,
            int mediaTimeScale,
            bool isText)
        {
            byte[] data32 = new byte[4];

            source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
            if (0 == navigateToAtom(source, "stts"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stts atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
            var int32Data = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // Number of table entries
            if (int32Data > 0)
            {
                if (isText)
                    qtChapterTextTrackId = currentTrakIndex;
                else
                    qtChapterPictureTrackId = currentTrakIndex;

                // Memorize zone
                if (readTagParams.PrepareForWriting && (Settings.MP4_keepExistingChapters || Settings.MP4_createQuicktimeChapters))
                {
                    var zoneName = isText ? ZONE_MP4_QT_CHAP_TXT_TRAK : ZONE_MP4_QT_CHAP_PIC_TRAK;
                    structureHelper.AddZone(trakPosition, (int)trakSize, zoneName);
                    structureHelper.RemoveZonesStartingWith(ZONE_MP4_PHYSICAL_CHUNK + "." + currentTrakIndex); // Remove chunks associated with previously recorded generic track zone
                }

                chapterTrackSamples.Clear();

                for (int i = 0; i < int32Data; i++)
                {
                    source.Read(data32, 0, 4);
                    var frameCount = StreamUtils.DecodeBEUInt32(data32);
                    source.Read(data32, 0, 4);
                    var sampleDuration = StreamUtils.DecodeBEUInt32(data32);
                    for (int j = 0; j < frameCount; j++)
                    {
                        MP4Sample sample = new MP4Sample();
                        sample.Duration = sampleDuration * 1.0 / mediaTimeScale;
                        chapterTrackSamples.Add(sample);
                    }
                }
            }

            source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
            if (0 == navigateToAtom(source, "stsc"))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "stsc atom could not be found; aborting read on track " + currentTrakIndex);
                source.BaseStream.Seek(trakPosition + trakSize, SeekOrigin.Begin);
                return trakSize;
            }
            source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
            int32Data = StreamUtils.DecodeBEUInt32(source.ReadBytes(4)); // Number of table entries

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
                var samplesPerChunk = StreamUtils.DecodeBEUInt32(data32);
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

            // Look for "trak.edts" atom and save it if it exists
            source.BaseStream.Seek(trakPosition + 8, SeekOrigin.Begin);
            uint edtsSize = navigateToAtom(source, "edts");
            if (edtsSize > 0)
            {
                source.BaseStream.Seek(-8, SeekOrigin.Current);
                if (isText)
                    chapterTextTrackEdits = source.ReadBytes((int)edtsSize);
                else
                    chapterPictureTrackEdits = source.ReadBytes((int)edtsSize);
            }
            return 0u;
        }

        private void readUserData(BinaryReader source, ReadTagParams readTagParams, long moovPosition, uint moovSize)
        {
            byte[] data32 = new byte[4];
            byte[] data64 = new byte[8];

            bool udtaFound = false;
            source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            var atomSize = navigateToAtom(source, "udta");
            if (0 == atomSize)
            {
                // If no UDTA has been located in MOOV, look for it into TRAK atoms
                source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
                atomSize = navigateToAtom(source, "trak");
                while (atomSize > 0)
                {
                    var trakPosition = source.BaseStream.Position;
                    atomSize = navigateToAtom(source, "udta");
                    if (atomSize > 0)
                    {
                        udtaFound = true;
                        break;
                    }
                    else
                    {
                        source.BaseStream.Seek(trakPosition, SeekOrigin.Begin);
                        atomSize = navigateToAtom(source, "trak");
                    }
                }
            }
            else
            {
                udtaFound = true;
            }

            if (!udtaFound)
            {
                LogDelegator.GetLogDelegate()(Log.LV_INFO, "udta atom could not be found");
                // Create a placeholder to create a new UDTA atom from scratch, located as a direct child of MOOV
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddSize(moovPosition - 8 + moovSize, atomSize, ZONE_MP4_NOMETA);
                    structureHelper.AddSize(moovPosition - 8 + moovSize, atomSize, ZONE_MP4_ILST);
                    structureHelper.AddSize(moovPosition - 8 + moovSize, atomSize, ZONE_MP4_CHPL);
                    structureHelper.AddSize(moovPosition - 8 + moovSize, atomSize, ZONE_MP4_XTRA);
                    structureHelper.AddZone(moovPosition - 8 + moovSize, 0, ZONE_MP4_NOUDTA);
                }
                return;
            }

            var udtaPosition = source.BaseStream.Position;
            udtaOffset = udtaPosition;
            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_NOMETA);
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_ILST);
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_CHPL);
                structureHelper.AddSize(source.BaseStream.Position - 8, atomSize, ZONE_MP4_XTRA);
            }

            // Look for Nero chapters
            var atomPosition = source.BaseStream.Position;
            atomSize = navigateToAtom(source, "chpl");
            if (atomSize > 0 && (Settings.MP4_keepExistingChapters || Settings.MP4_createNeroChapters))
            {
                structureHelper.AddZone(source.BaseStream.Position - 8, (int)atomSize, Array.Empty<byte>(), ZONE_MP4_CHPL);

                source.BaseStream.Seek(4, SeekOrigin.Current); // Version and flags
                source.BaseStream.Seek(1, SeekOrigin.Current); // Reserved byte
                source.Read(data32, 0, 4);
                uint neroChapterCount = StreamUtils.DecodeBEUInt32(data32);

                if (neroChapterCount > 0)
                {
                    int qtChapterCount = tagData.Chapters?.Count ?? 0;
                    bool isTakeNero = 0 == Settings.MP4_readChaptersFormat && neroChapterCount > qtChapterCount;
                    isTakeNero |= 2 == Settings.MP4_readChaptersFormat && neroChapterCount == qtChapterCount;
                    isTakeNero |= 3 == Settings.MP4_readChaptersFormat;
                    if (isTakeNero)
                    {
                        tagData.Chapters ??= new List<ChapterInfo>();
                        tagData.Chapters.Clear();
                        ChapterInfo previousChapter = null;

                        for (int i = 0; i < neroChapterCount; i++)
                        {
                            var chapter = new ChapterInfo();
                            tagData.Chapters.Add(chapter);

                            source.Read(data64, 0, 8);
                            chapter.StartTime = (uint)Math.Round(StreamUtils.DecodeBEInt64(data64) / 10000.0);
                            if (previousChapter != null) previousChapter.EndTime = chapter.StartTime;
                            var stringSize = source.ReadByte();
                            chapter.Title = Encoding.UTF8.GetString(source.ReadBytes(stringSize));
                            previousChapter = chapter;
                        }
                        if (previousChapter != null) previousChapter.EndTime = Convert.ToUInt32(Math.Floor(calculatedDurationMs));
                    }
                }
            }
            else if (Settings.MP4_createNeroChapters)
            {
                // Allow creating the 'chpl' atom from scratch
                structureHelper.AddZone(atomPosition, 0, ZONE_MP4_CHPL);
            }

            source.BaseStream.Seek(udtaPosition, SeekOrigin.Begin);
            atomSize = navigateToAtom(source, "meta");
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

            // Look for Xtra WMA fields (specific fields -e.g. rating- inserted by Windows instead of using standard MP4 fields)
            source.BaseStream.Seek(udtaPosition, SeekOrigin.Begin);
            atomSize = navigateToAtom(source, "Xtra");
            if (atomSize > 7)
            {
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(source.BaseStream.Position - 8, (int)atomSize, Array.Empty<byte>(), ZONE_MP4_XTRA);
                }
                if (readTagParams.ReadTag) readXtraTag(source, readTagParams, atomSize - 8);
            }
        }

        private void readTag(BinaryReader source, ReadTagParams readTagParams)
        {
            var atomPosition = source.BaseStream.Position;
            var atomSize = navigateToAtom(source, "hdlr"); // Metadata handler
            if (0 == atomSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "hdlr atom could not be found; aborting read");
                return;
            }
            long hdlrPosition = source.BaseStream.Position - 8;
            source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            source.BaseStream.Seek(4, SeekOrigin.Current); // Quicktime type
            string strData = Utils.Latin1Encoding.GetString(source.ReadBytes(4)); // Meta data type

            if (!strData.Equals("mdir"))
            {
                string errMsg = "ATL does not support ";
                if (strData.Equals("mp7t")) errMsg += "MPEG-7 XML metadata";
                else if (strData.Equals("mp7b")) errMsg += "MPEG-7 binary XML metadata";
                else errMsg = "Unrecognized metadata format";

                throw new NotSupportedException(errMsg);
            }
            source.BaseStream.Seek(atomSize + hdlrPosition, SeekOrigin.Begin); // Reach the end of the hdlr box

            long iListSize = navigateToAtom(source, "ilst"); // === Metadata list
            if (0 == iListSize)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "ilst atom could not be found");
                // TODO handle the case where 'meta' exists, but not 'ilst'
                return;
            }
            structureHelper.AddZone(source.BaseStream.Position - 8, (int)iListSize, ILST_CORE_SIGNATURE, ZONE_MP4_ILST);

            // Core minimal size
            if (8 == Size) return;

            StringBuilder atomHeaderBuilder = new StringBuilder();
            // Browse all metadata
            long iListPosition = 0;
            while (iListPosition < iListSize - 8)
            {
                atomHeaderBuilder.Clear();
                atomSize = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                atomHeaderBuilder.Append(Utils.Latin1Encoding.GetString(source.ReadBytes(4)));

                uint metadataSize;
                if ("----".Equals(atomHeaderBuilder.ToString())) // Custom text metadata
                {
                    metadataSize = navigateToAtom(source, "mean"); // "issuer" of the field
                    if (0 == metadataSize)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "mean atom could not be found; aborting read");
                        return;
                    }
                    source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                    string nmeSpace = Utils.Latin1Encoding.GetString(source.ReadBytes((int)metadataSize - 8 - 4));

                    // Only add namespace to the atom name if it's different than the default namespace
                    if (!nmeSpace.Equals(DEFAULT_NAMESPACE, StringComparison.OrdinalIgnoreCase))
                        atomHeaderBuilder.Append(':').Append(nmeSpace).Append(':');
                    else atomHeaderBuilder.Clear();

                    metadataSize = navigateToAtom(source, "name"); // field type
                    if (0 == metadataSize)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "name atom could not be found; aborting read");
                        return;
                    }
                    source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                    atomHeaderBuilder.Append(Utils.Latin1Encoding.GetString(source.ReadBytes((int)metadataSize - 8 - 4)));
                }
                string atomHeader = atomHeaderBuilder.ToString();

                // Having a 'data' header here means we're still on the same field, with a 2nd value
                // (e.g. multiple embedded pictures)
                if (!"data".Equals(atomHeader))
                {
                    metadataSize = navigateToAtom(source, "data");
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
                var dataClass = source.ReadByte();

                // 4-byte NULL space
                source.BaseStream.Seek(4, SeekOrigin.Current);

                addFrameClass(atomHeader, dataClass);

                if (1 == dataClass) // UTF-8 Text
                {
                    var strlen = metadataSize;
                    long lastLocation;
                    var currentData = new List<string>();

                    do
                    {
                        var dataSize = Math.Max(0, (int) strlen - 16);
                        if (dataSize > 0)
                        {
                            strData = Encoding.UTF8.GetString(source.ReadBytes((int)strlen - 16));
                            currentData.Add(strData);
                        }

                        lastLocation = source.BaseStream.Position;
                        strlen = navigateToAtom(source, "data");
                        
                        if (strlen <= 0) continue;
                        
                        source.BaseStream.Seek(3, SeekOrigin.Current); // We're only looking for the last byte of the flag
                        dataClass = source.ReadByte();
                        source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte NULL space
                        metadataSize += strlen;
                    } while (strlen > 0);
                    source.BaseStream.Seek(lastLocation, SeekOrigin.Begin);
                    strData = string.Join(Settings.InternalValueSeparator, currentData);
                    SetMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                }
                else if (21 == dataClass) // int8-16-24-32
                {
                    int intData;
                    uint fieldSize = metadataSize - 16;
                    if (fieldSize > 3) intData = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                    else if (fieldSize > 2) intData = StreamUtils.DecodeBEInt24(source.ReadBytes(3));
                    else if (fieldSize > 1) intData = StreamUtils.DecodeBEInt16(source.ReadBytes(2));
                    else intData = source.ReadByte();
                    SetMetaField(atomHeader, intData.ToString(), readTagParams.ReadAllMetaFrames);
                }
                else
                {
                    uint uIntData;
                    if (22 == dataClass) // uint8-16-24-32
                    {
                        uint fieldSize = metadataSize - 16;
                        if (fieldSize > 3) uIntData = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        else if (fieldSize > 2) uIntData = StreamUtils.DecodeBEUInt24(source.ReadBytes(3));
                        else if (fieldSize > 1) uIntData = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));
                        else uIntData = source.ReadByte();
                        SetMetaField(atomHeader, uIntData.ToString(), readTagParams.ReadAllMetaFrames);
                    }
                    else if (13 == dataClass || 14 == dataClass || (0 == dataClass && "covr".Equals(atomHeader))) // Picture
                    {
                        PictureInfo.PIC_TYPE picType = PictureInfo.PIC_TYPE.Generic; // TODO - to check : this seems to prevent ATL from detecting multiple images from the same type, as for a file with two "Front Cover" images; only one image will be detected

                        uint pictureSize = metadataSize;
                        long lastLocation;

                        do
                        {
                            var picturePosition = takePicturePosition(picType);

                            int dataSize = Math.Max(0, (int)pictureSize - 16);
                            if (readTagParams.ReadPictures)
                            {
                                PictureInfo picInfo = PictureInfo.fromBinaryData(source.BaseStream, dataSize, picType, getImplementedTagType(), dataClass, picturePosition);
                                tagData.Pictures.Add(picInfo);
                            }
                            else
                            {
                                source.BaseStream.Seek(dataSize, SeekOrigin.Current);
                            }

                            // Look for other pictures within 'covr'
                            lastLocation = source.BaseStream.Position;
                            pictureSize = navigateToAtom(source, "data");
                            if (pictureSize > 0)
                            {
                                // We're only looking for the last byte of the flag
                                source.BaseStream.Seek(3, SeekOrigin.Current);
                                dataClass = source.ReadByte();
                                source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte NULL space

                                metadataSize += pictureSize;
                            }
                        } while (pictureSize > 0);
                        source.BaseStream.Seek(lastLocation, SeekOrigin.Begin);
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
                            uIntData = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

                            strData = "";
                            if (uIntData < ID3v1.MAX_MUSIC_GENRES) strData = ID3v1.MusicGenre[uIntData - 1];

                            SetMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                        }
                        // else - Other unhandled cases ?
                    }
                }
                // else - Other unhandled cases ?

                source.BaseStream.Seek(atomPosition + metadataSize, SeekOrigin.Begin);
                iListPosition += atomSize;
            }
        }

        /**
         * Read WMA fields located behind the 'Xtra' atom
         * NB : Called _after_ reading standard MP4 tag
         */
        private void readXtraTag(BinaryReader source, ReadTagParams readTagParams, long atomDataSize)
        {
            IList<KeyValuePair<string, string>> wmaFields = WMAHelper.ReadFields(source.BaseStream, atomDataSize);
            foreach (KeyValuePair<string, string> field in wmaFields)
                setXtraField(field.Key, field.Value, readTagParams.ReadAllMetaFrames || readTagParams.PrepareForWriting);
        }

        /**
         * Set a WMA field
         */
        private void setXtraField(string ID, string data, bool readAllMetaFrames)
        {
            // Finds the ATL field identifier
            Field supportedMetaID = WMAHelper.getAtlCodeForFrame(ID);

            // Hack to format popularity tag with MP4's convention rather than the ASF convention that Xtra uses
            // so that it is parsed properly by MetaDataIO's default mechanisms
            if (Field.RATING == supportedMetaID)
            {
                double? popularity = TrackUtils.DecodePopularity(data, RC_ASF);
                if (popularity.HasValue) data = TrackUtils.EncodePopularity(popularity.Value * 5, ratingConvention) + "";
                else return;
            }

            // If ID has been mapped with an 'classic' ATL field, store it in the dedicated place...
            if (supportedMetaID != Field.NO_FIELD && !tagData.hasKey(supportedMetaID))
            {
                setMetaField(supportedMetaID, data);
            }

            if (readAllMetaFrames && ID.Length > 0) // Store it in the additional fields Dictionary
            {
                MetaFieldInfo fieldInfo = new MetaFieldInfo(getImplementedTagType(), ID, data, 0, "", "");
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Prevent duplicates
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        private static Uuid readUuid(Stream source)
        {
            Uuid result = new Uuid
            {
                size = navigateToAtom(source, "uuid"),
                position = source.Position - 8
            };
            if (result.size >= 16 + 8)
            {
                Span<byte> key = new byte[16];
                if (source.Read(key) < key.Length) return result;
                // Convert key to hex value
                StringBuilder sbr = new StringBuilder();
                for (int i = 0; i < 16; i++) sbr.Append(key[i].ToString("X2"));
                result.key = sbr.ToString();
                // Read remaining data as UTF-8 string
                long dataSize = result.size - 8 - 16;
                if (dataSize > 0)
                {
                    Span<byte> data = new byte[dataSize];
                    if (source.Read(data) < dataSize) return result;
                    result.value = Encoding.UTF8.GetString(data);
                }
            }
            return result;
        }

        /// <summary>
        /// Look for the atom segment starting with the given key, at the current atom level
        /// Returns with Source positioned right after the atom header, on the 1st byte of data
        /// 
        /// Warning : stream must be positioned at the end of a previous atom before being called
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="atomKey">Atom key to look for (e.g. "udta")</param>
        /// <returns>If atom found : raw size of the atom (including the already-read 8-byte header);
        /// If atom not found : 0</returns>
        private static uint navigateToAtom(BinaryReader source, string atomKey)
        {
            return navigateToAtom(source.BaseStream, atomKey);
        }

        private static uint navigateToAtom(Stream source, string atomKey)
        {
            return navigateToAtomSize(source, atomKey).Item1;
        }

        /// <summary>
        /// Look for the atom segment starting with the given key, at the current atom level
        /// Returns with Source positioned right after the atom header, on the 1st byte of data
        /// 
        /// Warning : stream must be positioned at the end of a previous atom before being called
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="atomKey">Atom key to look for (e.g. "udta")</param>
        /// <returns>
        ///  - Item1 : If atom found : raw size of the atom (including the already-read 8 or 16-byte header);
        ///  If atom not found : 0
        ///  - Item2 : Size of the atom header (may vary if using the 64-bit variant)
        /// </returns>
        private static Tuple<uint, int> navigateToAtomSize(Stream source, string atomKey)
        {
            long atomSize = 0;
            string atomHeader;
            bool first = true;
            int iterations = 0;
            int atomHeaderSize = 8;
            byte[] data = new byte[8];

            do
            {
                if (!first) source.Seek(atomSize - atomHeaderSize, SeekOrigin.Current);
                atomHeaderSize = 8; // Default variant where size takes up 32-bit
                if (source.Read(data, 0, 4) < 4) return new Tuple<uint, int>(0, atomHeaderSize);
                atomSize = StreamUtils.DecodeBEUInt32(data);
                if (source.Read(data, 0, 4) < 4) return new Tuple<uint, int>(0, atomHeaderSize);
                atomHeader = Utils.Latin1Encoding.GetString(data, 0, 4);
                if (1 == atomSize) // 64-bit size variant
                {
                    atomHeaderSize += 8;
                    if (source.Read(data, 0, 8) < 8) return new Tuple<uint, int>(0, atomHeaderSize);
                    atomSize = StreamUtils.DecodeBEInt64(data);
                }

                if (first) first = false;
                if (++iterations > 100) return new Tuple<uint, int>(0, atomHeaderSize);
            } while (!atomKey.Equals(atomHeader) && source.Position + atomSize - atomHeaderSize < source.Length);

            if (source.Position + atomSize - atomHeaderSize > source.Length)
            {
                // atom found, but its declared size goes beyond file size
                if (atomKey.Equals(atomHeader))
                {
                    uint actualSize = (uint)(source.Length - source.Position + atomHeaderSize);
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "atom " + atomKey + " has been declared with an incorrect size; using its actual size (" + actualSize + " bytes)");
                    return new Tuple<uint, int>(actualSize, atomHeaderSize);
                }
                // atom not found
                return new Tuple<uint, int>(0, atomHeaderSize);
            }

            var result = atomKey.Equals(atomHeader) ? (uint)atomSize : 0;
            return new Tuple<uint, int>(result, atomHeaderSize);
        }


        // Read data from file
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            if (readTagParams is null) throw new ArgumentNullException(nameof(readTagParams));

            resetData();

            BinaryReader reader = new BinaryReader(source);

            if (recognizeHeaderType(reader)) return readMP4(reader, readTagParams);
            else
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "unknown header type");
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void preprocessWrite(TagData dataToWrite)
        {
            // Scan AdditionalData for the need to create the Xtra zone
            foreach (MetaFieldInfo info in dataToWrite.AdditionalFields)
            {
                // Belongs to the XTRA zone + parent UDTA atom has been located => OK
                if (info.NativeFieldCode.StartsWith("WM/", StringComparison.OrdinalIgnoreCase) && udtaOffset > 0)
                {
                    // Allow creating the 'xtra' atom / zone from scratch
                    structureHelper.AddZone(udtaOffset, 0, ZONE_MP4_XTRA);
                    break;
                }
            }

            // Chapter picture-related QA checks specific to MP4/M4A
            if (dataToWrite.Chapters != null)
            {
                long nbPics = dataToWrite.Chapters.LongCount(c => c.Picture != null);
                if (nbPics > 0)
                {
                    if (dataToWrite.Chapters[0].StartTime > 0)
                        LogDelegator.GetLogDelegate()(Log.LV_WARNING, "First chapter start time is > 0:00 - that might cause chapter picture display issues on some players such as VLC.");

                    if (nbPics < dataToWrite.Chapters.Count)
                        LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Not all chapters have an associated picture - that might cause chapter picture display issues on some players such as VLC.");
                }
            }
        }

        /// <inheritdoc/>
        protected override void postprocessWrite(Stream s)
        {
            int maxTrack = maxTrackId;
            if (Chapters.Count > 0)
            {
                var zones = structureHelper.Zones;
                if (zones.Any(z => z.Name == ZONE_MP4_QT_CHAP_PIC_TRAK)) maxTrack = Math.Max(maxTrack, qtChapterPictureTrackId);
                if (zones.Any(z => z.Name == ZONE_MP4_QT_CHAP_TXT_TRAK)) maxTrack = Math.Max(maxTrack, qtChapterTextTrackId);
            }
            s.Seek(structureHelper.getCorrectedOffset(trackCounterPosition), SeekOrigin.Begin);
            s.Write(StreamUtils.EncodeBEUInt32((uint)maxTrack + 1));
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(tag, w, zone);
        }

        private int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;

            if (zone.StartsWith(ZONE_MP4_NOUDTA)) // Create an UDTA atom from scratch
            {
                // Keep position in mind to calculate final size and come back here to write it
                long udtaSizePos = w.BaseStream.Position;
                w.Write(0);
                w.Write(Utils.Latin1Encoding.GetBytes("udta"));

                result = writeMeta(tag, w);

                // Record final size of tag into "tag size" fields of header
                long finalTagPos = w.BaseStream.Position;

                w.BaseStream.Seek(udtaSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - udtaSizePos)));

            }
            else if (zone.StartsWith(ZONE_MP4_NOMETA)) // Create a META atom from scratch
            {
                result = writeMeta(tag, w);
            }
            else if (zone.StartsWith(ZONE_MP4_ILST)) // Edit an existing ILST atom
            {
                // Keep position in mind to calculate final size and come back here to write it
                var tagSizePos = w.BaseStream.Position;
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
                result = writeNeroChapters(w, tag.Chapters);
            }
            else if (zone.StartsWith(ZONE_MP4_XTRA)) // Extra WMA-like fields written by Windows
            {
                result = writeXtraFrames(tag, w);
            }
            else if (PADDING_ZONE_NAME.Equals(zone)) // Padding
            {
                long paddingSizeToWrite;
                if (tag.PaddingSize > -1) paddingSizeToWrite = tag.PaddingSize;
                else paddingSizeToWrite = TrackUtils.ComputePaddingSize(initialPaddingOffset, initialPaddingSize, structureHelper.GetZone(zone).Offset - structureHelper.getCorrectedOffset(zone));

                if (paddingSizeToWrite > 0)
                {
                    // Placeholder; size is written by FileStructureHelper
                    w.Write(0);
                    w.Write(Utils.Latin1Encoding.GetBytes("free"));
                    for (int i = 0; i < paddingSizeToWrite - 8; i++) w.Write((byte)0);
                    result = 1;
                }
            }
            else if (zone.StartsWith(ZONE_MP4_QT_CHAP_NOTREF)) // Write a new tref atom for quicktime chapters
            {
                result = writeQTChaptersTref(w, qtChapterTextTrackId, qtChapterPictureTrackId, tag.Chapters);
            }
            else if (zone.StartsWith(ZONE_MP4_QT_CHAP_CHAP)) // Reference to Quicktime chapter track from an audio/video track
            {
                result = writeQTChaptersChap(w, qtChapterTextTrackId, qtChapterPictureTrackId, tag.Chapters);
            }
            else if (zone.StartsWith(ZONE_MP4_QT_CHAP_TXT_TRAK)) // Quicktime chapter text track
            {
                if (zone.Equals(ZONE_MP4_QT_CHAP_TXT_TRAK)) // Text track ATL supports -> can be edited
                    result = writeQTChaptersTrack(w, qtChapterTextTrackId, tag.Chapters, globalTimeScale, Convert.ToUInt32(calculatedDurationMs), true);
                else return 1; // Other text track ATL doesn't support; needs to appear active
            }
            else if (zone.StartsWith(ZONE_MP4_QT_CHAP_PIC_TRAK)) // Quicktime chapter picture track
            {
                result = writeQTChaptersTrack(w, qtChapterPictureTrackId, tag.Chapters, globalTimeScale, Convert.ToUInt32(calculatedDurationMs), false);
            }
            else if (zone.StartsWith(ZONE_MP4_QT_CHAP_MDAT)) // Quicktime chapter data (text and picture data)
            {
                result = writeQTChaptersData(w, tag.Chapters);
            }
            else if (zone.StartsWith("uuid.")) // Existing UUID atoms
            {
                result = writeUuidFrame(tag, zone[5..], w);
            }
            else if (zone.StartsWith(ZONE_MP4_ADDITIONAL_UUIDS)) // Extra UUID atoms
            {
                result = writeUuidFrames(tag, w);
            }
            else if (zone.StartsWith(ZONE_MP4_PHYSICAL_CHUNK)) // Audio chunks
            {
                result = 1; // Needs to appear active in case their headers need to be rewritten (e.g. chunk enlarged somewhere -> all physical chunks are X bytes ahead of their initial position)
            }

            return result;
        }

        private int writeMeta(TagData tag, BinaryWriter w)
        {
            // Keep position in mind to calculate final size and come back here to write it
            long metaSizePos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("meta"));
            w.Write(0); // version and flags

            // Handler
            long hdlrPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("hdlr"));
            w.Write(0); // version and flags
            w.Write(0); // quicktime type
            w.Write(Utils.Latin1Encoding.GetBytes("mdir")); // quicktime subtype = "APPLE meta data iTunes reader"
            w.Write(Utils.Latin1Encoding.GetBytes("appl")); // manufacturer
            w.Write(0); // component flags
            w.Write(0); // component flags mask
            w.Write(Utils.Latin1Encoding.GetBytes("Metadata (ilst) handler\0")); // component name

            long ilstSizePos = w.BaseStream.Position;
            w.BaseStream.Seek(hdlrPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(ilstSizePos - hdlrPos)));

            w.BaseStream.Seek(ilstSizePos, SeekOrigin.Begin);
            w.Write(ILST_CORE_SIGNATURE);

            int result = writeFrames(tag, w);

            // Record final size of tag into "tag size" fields of header
            long finalTagPos = w.BaseStream.Position;

            w.BaseStream.Seek(metaSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - metaSizePos)));

            w.BaseStream.Seek(ilstSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalTagPos - ilstSizePos)));

            w.BaseStream.Seek(finalTagPos, SeekOrigin.Begin);

            return result;
        }

        private int writeFrames(TagData tag, BinaryWriter w)
        {
            int counter = 0;
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            IDictionary<Field, string> map = tag.ToMap();

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                foreach (string s in frameMapping_mp4.Keys)
                {
                    if (frameType == frameMapping_mp4[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);

                            writeTextFrame(w, s, value);
                            writtenFieldCodes.Add(s.ToUpper());
                            counter++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (!fieldInfo.NativeFieldCode.StartsWith("uuid.")
                    && !fieldInfo.NativeFieldCode.StartsWith("xmp.")
                    && !writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper())
                    )
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, FormatBeforeWriting(fieldInfo.Value));
                    counter++;
                }
            }

            // Picture fields
            bool firstPic = true;
            bool hasPic = false;
            long picHeaderPos = 0;
            foreach (PictureInfo picInfo in tag.Pictures.Where(isPictureWritable))
            {
                hasPic = true;
                if (firstPic)
                {
                    // If multiples pictures are embedded, the 'covr' atom is not repeated; the 'data' atom is
                    picHeaderPos = w.BaseStream.Position;
                    w.Write(0); // Frame size placeholder to be rewritten in a few lines
                    w.Write(Utils.Latin1Encoding.GetBytes("covr"));
                    firstPic = false;
                }

                writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat);
                counter++;
            }
            if (hasPic)
            {
                long finalPos = w.BaseStream.Position;
                w.BaseStream.Seek(picHeaderPos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalPos - picHeaderPos)));
                w.BaseStream.Seek(finalPos, SeekOrigin.Begin);
            }

            return counter;
        }

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            const int frameFlags = 0;

            // == METADATA HEADER ==
            var frameSizePos1 = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines

            if (!frameCode.StartsWith("WM/", StringComparison.OrdinalIgnoreCase))
            {
                // Non-Microsoft custom metadata
                if (frameCode.Length != FieldCodeFixedLength)
                {
                    string[] frameCodeComponents = frameCode.Split(':');
                    string nmespace = DEFAULT_NAMESPACE;
                    if (2 == frameCodeComponents.Length && !frameCodeComponents[0].StartsWith("--")) nmespace = frameCodeComponents[0];
                    string fieldCode = frameCodeComponents[^1];

                    writer.Write(Utils.Latin1Encoding.GetBytes("----"));

                    writer.Write(StreamUtils.EncodeBEInt32(nmespace.Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("mean"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(nmespace));

                    writer.Write(StreamUtils.EncodeBEInt32(fieldCode.Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("name"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(fieldCode));
                }
                else // Standard-looking metadata
                {
                    writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
                }
            }

            // == METADATA VALUE ==
            var frameSizePos2 = writer.BaseStream.Position;
            writer.Write(0); // Frame size placeholder to be rewritten in a few lines
            writer.Write(Utils.Latin1Encoding.GetBytes("data"));

            int frameClass = 1;
            if (frameClasses_mp4.TryGetValue(frameCode, out var value1)) frameClass = value1;

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
            else if (21 == frameClass) // int8-16-24-32, depending on the value
            {
                if (!Utils.IsNumeric(text, true))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "value " + text + " could not be converted to integer; ignoring");
                    writer.Write(0);
                }
                else
                {
                    int value = Convert.ToInt32(text);
                    if (value > short.MaxValue || value < short.MinValue) writer.Write(StreamUtils.EncodeBEInt32(value));
                    // use int32 instead of int24 because Convert.ToInt24 doesn't exist
                    else if (value > 127 || value < -127) writer.Write(StreamUtils.EncodeBEInt16(Convert.ToInt16(text)));
                    else writer.Write(Convert.ToByte(text));
                }
            }
            else if (22 == frameClass) // uint8-16-24-32, depending on the value
            {
                if (!Utils.IsNumeric(text, true, false))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "value " + text + " could not be converted to unsigned integer; ignoring");
                    writer.Write(0);
                }
                else
                {
                    uint value = Convert.ToUInt32(text);
                    if (value > 0xffff) writer.Write(StreamUtils.EncodeBEUInt32(value));
                    // use int32 instead of int24 because Convert.ToUInt24 doesn't exist
                    else if (value > 0xff) writer.Write(StreamUtils.EncodeBEUInt16(Convert.ToUInt16(text)));
                    else writer.Write(Convert.ToByte(text));
                }
            }


            // Go back to frame size locations to write their actual size 
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos1, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos1)));
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private static void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat)
        {
            const int frameFlags = 0;

            var frameSizePos2 = writer.BaseStream.Position;
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
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private int writeXtraFrames(TagData tag, BinaryWriter w)
        {
            IEnumerable<MetaFieldInfo> xtraTags = tag.AdditionalFields.Where(fi => (fi.TagType.Equals(MetaDataIOFactory.TagType.ANY) || fi.TagType.Equals(getImplementedTagType())) && !fi.MarkedForDeletion && fi.NativeFieldCode.ToLower().StartsWith("wm/", StringComparison.OrdinalIgnoreCase));

            if (!xtraTags.Any()) return 0;

            // Start writing the atom
            var frameSizePos = w.BaseStream.Position;

            w.Write(0); // To be rewritten at the end of the method
            w.Write(Utils.Latin1Encoding.GetBytes("Xtra"));

            // Write all fields
            foreach (MetaFieldInfo fieldInfo in xtraTags)
            {
                // Write the value of the "master" field contained in TagData
                string value = WMAHelper.getValueFromTagData(fieldInfo.NativeFieldCode, tag);
                // if no "master" field is set, write the extra field's own value
                if (string.IsNullOrEmpty(value)) value = fieldInfo.Value;

                bool isNumeric = false;
                // Hack to format popularity tag with the ASF convention rather than the convention that MP4 uses
                // so that it is parsed properly by Windows
                if ("wm/shareduserrating" == fieldInfo.NativeFieldCode.ToLower())
                {
                    double popularity;
                    if (double.TryParse(value, out popularity))
                    {
                        value = TrackUtils.EncodePopularity(popularity * 5, MetaDataIO.RC_ASF) + "";
                        isNumeric = true;
                    }
                    else continue;
                }

                WMAHelper.WriteField(w, fieldInfo.NativeFieldCode, value, isNumeric);
            }

            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - frameSizePos)));

            return xtraTags.Count();
        }

        private static int writeNeroChapters(BinaryWriter w, IList<ChapterInfo> chapters)
        {
            if (null == chapters || 0 == chapters.Count) return 0;

            int result = 0;

            result = chapters.Count;

            var frameSizePos = w.BaseStream.Position;
            w.Write(0); // To be rewritten at the end of the method
            w.Write(Utils.Latin1Encoding.GetBytes("chpl"));

            w.Write(new byte[] { 1, 0, 0, 0, 0 }); // Version, flags and reserved byte

            int maxCount = Settings.MP4_capNeroChapters ? Math.Min(chapters.Count, 255) : chapters.Count;
            w.Write(StreamUtils.EncodeBEInt32(maxCount));

            for (int i = 0; i < maxCount; i++)
            {
                ChapterInfo chapter = chapters[i];
                w.Write(StreamUtils.EncodeBEUInt64((ulong)chapter.StartTime * 10000));
                var strData = Encoding.UTF8.GetBytes(chapter.Title);
                var strDataLength = (byte)Math.Min(255, strData.Length);
                w.Write(strDataLength);
                w.Write(strData, 0, strDataLength);
            }

            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - frameSizePos)));

            return result;
        }

        private static int writeQTChaptersTref(BinaryWriter w, int qtChapterTextTrackNum, int qtChapterPictureTrackNum, IList<ChapterInfo> chapters)
        {
            if (null == chapters || 0 == chapters.Count) return 0;

            long trefPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("tref"));

            writeQTChaptersChap(w, qtChapterTextTrackNum, qtChapterPictureTrackNum, chapters);

            long finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(trefPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - trefPos)));

            return 1;
        }

        private static int writeQTChaptersChap(BinaryWriter w, int qtChapterTextTrackNum, int qtChapterPictureTrackNum, ICollection<ChapterInfo> chapters)
        {
            if (null == chapters || 0 == chapters.Count) return 0;

            long chapPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("chap"));

            if (qtChapterTextTrackNum > 0)
                w.Write(StreamUtils.EncodeBEInt32(qtChapterTextTrackNum));

            int nbActualChapterImages = chapters.Count(ch => ch.Picture != null && ch.Picture.PictureData.Length > 0);
            if (qtChapterPictureTrackNum > 0 && nbActualChapterImages > 0)
                w.Write(StreamUtils.EncodeBEInt32(qtChapterPictureTrackNum)); // As many pictures as there are chapters

            long finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(chapPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - chapPos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return 1;
        }

        private static int writeQTChaptersData(BinaryWriter w, ICollection<ChapterInfo> chapters)
        {
            if (null == chapters || 0 == chapters.Count) return 0;

            foreach (ChapterInfo chapter in chapters)
            {
                byte[] titleBytes = Encoding.UTF8.GetBytes(chapter.Title);
                w.Write(StreamUtils.EncodeBEInt16((short)titleBytes.Length));
                w.Write(titleBytes);
                // Magic sequence (always the same)
                w.Write(StreamUtils.EncodeBEInt32(12));
                w.Write(Utils.Latin1Encoding.GetBytes("encd"));
                w.Write(StreamUtils.EncodeBEInt32(256));
            }

            foreach (var chapter in chapters)
            {
                if (chapter.Picture != null) w.Write(chapter.Picture.PictureData);
                else w.Write(Properties.Resources._1px_black);
            }

            return 1;
        }

        private int writeQTChaptersTrack(BinaryWriter w, int trackNum, IList<ChapterInfo> chapters, uint globalTimeScale, uint trackDurationMs, bool isText)
        {
            if (null == chapters || 0 == chapters.Count) return 0;
            IList<ChapterInfo> workingChapters = chapters;
            if (0 == workingChapters.Count) return 0;

            // Find largest dimensions and color depth among all chapter pictures
            short maxWidth = 0;
            short maxHeight = 0;
            int maxDepth = 0;
            if (!isText)
            {
                foreach (ChapterInfo chapter in workingChapters)
                {
                    byte[] pictureData = chapter.Picture != null
                        ? chapter.Picture.PictureData
                        : Properties.Resources._1px_black;
                    ImageProperties props = ImageUtils.GetImageProperties(pictureData);
                    maxWidth = (short)Math.Min(Math.Max(props.Width, maxWidth), short.MaxValue);
                    maxHeight = (short)Math.Min(Math.Max(props.Height, maxHeight), short.MaxValue);
                    maxDepth = Math.Max(props.ColorDepth, maxDepth);
                }
            }

            // TRACK
            long trakPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("trak"));

            // TRACK HEADER BEGIN
            w.Write(StreamUtils.EncodeBEInt32(92)); // Standard size
            w.Write(Utils.Latin1Encoding.GetBytes("tkhd"));

            w.Write((byte)0); // Version
            w.Write((short)0); // Flags(bytes 2,3)
            w.Write((byte)7); // Flags(byte 1) --> TrackEnabled = 1 ; TrackInMovie = 2 ; TrackInPreview = 4; TrackInPoster = 8

            w.Write(StreamUtils.EncodeBEUInt32(getMacDateNow())); // Creation date
            w.Write(StreamUtils.EncodeBEUInt32(getMacDateNow())); // Modification date

            w.Write(StreamUtils.EncodeBEInt32(trackNum)); // Track number

            w.Write(0); // Reserved

            w.Write(StreamUtils.EncodeBEUInt32(trackDurationMs / 1000 * globalTimeScale)); // Duration (sec)

            w.Write((long)0); // Reserved
            w.Write(StreamUtils.EncodeBEInt16((short)(isText ? 2 : 1))); // Layer
            w.Write((short)0); // Alternate group
            w.Write((short)0); // Volume
            w.Write((short)0); // Reserved

            // Matrix (keep values of sample file)
            w.Write(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x40, 0, 0, 0 });

            w.Write(encodeBEFixedPoint32(maxWidth, 0)); // Width
            w.Write(encodeBEFixedPoint32(maxHeight, 0)); // Height

            // TRACK HEADER END


            // EDITS BEGIN (optional)
            if (isText && chapterTextTrackEdits != null) w.Write(chapterTextTrackEdits);
            if (!isText && chapterPictureTrackEdits != null) w.Write(chapterPictureTrackEdits);
            // EDITS END (optional)


            // MEDIA BEGIN
            long mdiaPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("mdia"));

            // MEDIA HEADER
            w.Write(StreamUtils.EncodeBEInt32(32)); // Standard size
            w.Write(Utils.Latin1Encoding.GetBytes("mdhd"));
            w.Write(0); // Version and flags

            w.Write(StreamUtils.EncodeBEUInt32(getMacDateNow())); // Creation date
            w.Write(StreamUtils.EncodeBEUInt32(getMacDateNow())); // Modification date

            long trackTimescale = trackTimescales[trackNum];
            w.Write(StreamUtils.EncodeBEUInt32((uint)trackTimescale)); // Track timescale

            w.Write(StreamUtils.EncodeBEUInt32((uint)(trackDurationMs / 1000 * trackTimescale))); // Duration (sec)

            w.Write(new byte[] { 0x55, 0xc4 }); // Code for English - TODO : make that dynamic

            w.Write((short)0); // Quicktime quality
            // MEDIA HEADER END

            // MEDIA HANDLER
            long hdlrPos = w.BaseStream.Position;
            w.Write(0); // Temp; will be rewritten later
            w.Write(Utils.Latin1Encoding.GetBytes("hdlr"));
            w.Write(0); // Version and flags

            w.Write(0); // Quicktime type
            if (isText)
                w.Write(Utils.Latin1Encoding.GetBytes("text")); // Subtype
            else
                w.Write(Utils.Latin1Encoding.GetBytes("vide")); // Subtype
            w.Write(0); // Reserved
            w.Write(0); // Reserved
            w.Write(0); // Reserved
            w.Write(Utils.Latin1Encoding.GetBytes(isText ? "Chapter titles\0" : "Chapter pictures\0")); // component name

            long minfPos = w.BaseStream.Position;
            w.BaseStream.Seek(hdlrPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt32(Convert.ToUInt32(minfPos - hdlrPos)));
            // MEDIA HANDLER END

            // MEDIA INFORMATION
            w.BaseStream.Seek(minfPos, SeekOrigin.Begin);
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("minf"));

            // BASE MEDIA INFORMATION HEADER
            if (isText)
            {
                w.Write(StreamUtils.EncodeBEInt32(76)); // Standard size
                w.Write(Utils.Latin1Encoding.GetBytes("gmhd"));

                w.Write(StreamUtils.EncodeBEInt32(24)); // Standard size
                w.Write(Utils.Latin1Encoding.GetBytes("gmin"));
                w.Write(0); // Version and flags
                w.Write(StreamUtils.EncodeBEInt16((short)64)); // Graphics mode
                w.Write(new byte[] { 0x80, 0 }); // Opcolor 1
                w.Write(new byte[] { 0x80, 0 }); // Opcolor 2
                w.Write(new byte[] { 0x80, 0 }); // Opcolor 3
                w.Write(0); // Balance + reserved

                w.Write(StreamUtils.EncodeBEInt32(44)); // Standard size
                if (isText)
                    w.Write(Utils.Latin1Encoding.GetBytes("text")); // Subtype
                else
                    w.Write(Utils.Latin1Encoding.GetBytes("vide")); // Subtype
                // Matrix (keep values of sample file)
                w.Write(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x40, 0, 0, 0 });
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt32(20)); // Standard size
                w.Write(Utils.Latin1Encoding.GetBytes("vmhd"));

                w.Write((byte)0); // Version
                w.Write((short)0); // Flags, bytes 2 and 3
                w.Write((byte)1); // Flags, byte 1
                w.Write((short)0); // Graphics mode
                w.Write((short)0); // OpColor R
                w.Write((short)0); // OpColor G
                w.Write((short)0); // OpColor B
            }
            // END BASE MEDIA INFORMATION HEADER


            // DATA INFORMATION
            w.Write(StreamUtils.EncodeBEInt32(36)); // Predetermined size
            w.Write(Utils.Latin1Encoding.GetBytes("dinf"));

            // DATA REFERENCE
            w.Write(StreamUtils.EncodeBEInt32(28)); // Predetermined size
            w.Write(Utils.Latin1Encoding.GetBytes("dref"));
            w.Write(0); // Version and flags
            w.Write(StreamUtils.EncodeBEInt32(1)); // Number of refs
            w.Write(StreamUtils.EncodeBEInt32(12)); // Entry length
            w.Write(Utils.Latin1Encoding.GetBytes("url ")); // Entry code
            w.Write(StreamUtils.EncodeBEInt32(1)); // Entry data

            // SAMPLE TABLE BEGIN
            long stblPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stbl"));

            // SAMPLE DESCRIPTION
            long stsdPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stsd"));
            w.Write(0); // Version and flags
            w.Write(StreamUtils.EncodeBEInt32(1)); // Number of descriptions

            long subtypePos = w.BaseStream.Position;
            w.Write(0);
            if (isText)
            {
                // General structure
                w.Write(Utils.Latin1Encoding.GetBytes("text")); // Subtype ('text' for text; 'tx3g' for subtitles)
                w.Write(0); // Reserved
                w.Write((short)0); // Reserved
                w.Write(StreamUtils.EncodeBEInt16(1)); // Data reference index (TODO - is that dynamic ?)

                // Text sample properties
                w.Write(StreamUtils.EncodeBEInt32(1)); // Display flags
                w.Write(StreamUtils.EncodeBEInt32(1)); // Text justification
                w.Write(0); // Text background color
                w.Write((short)0); // Text background color
                w.Write((long)0); // Default text box
                w.Write((long)0); // Reserved
                w.Write((short)0); // Font number
                w.Write((short)0); // Font face
                w.Write((byte)0); // Reserved
                w.Write((short)0); // Reserved
                w.Write(0); // Foreground color
                w.Write((short)0); // Foreground color
                                   //            w.Write((byte)0); // No text
            }
            else
            {
                // General structure
                // TODO PNG support
                w.Write(Utils.Latin1Encoding.GetBytes("jpeg")); // Subtype
                w.Write(0); // Reserved
                w.Write((short)0); // Reserved
                w.Write(StreamUtils.EncodeBEInt16(1)); // Data reference index (TODO - is that dynamic ?)

                // Video sample properties
                w.Write((short)0); // Version
                w.Write((short)0); // Revision level
                w.Write(0); // Vendor
                w.Write(0); // Temporal quality
                w.Write(0); // Spatial quality

                w.Write(StreamUtils.EncodeBEInt16(maxWidth)); // Width
                w.Write(StreamUtils.EncodeBEInt16(maxHeight)); // Height

                w.Write(new byte[] { 0, 0x48, 0, 0 }); // Horizontal resolution (32 bits fixed-point; reusing sample file data for now)
                w.Write(new byte[] { 0, 0x48, 0, 0 }); // Vertical resolution (32 bits fixed-point; reusing sample file data for now)
                w.Write(0); // Data size
                w.Write(StreamUtils.EncodeBEInt16(1)); // Frame count
                //w.Write(Utils.Latin1Encoding.GetBytes("jpeg")); // Compressor name
                w.Write(0); // Compressor name
                /*
                w.Write(StreamUtils.EncodeBEInt16((short)Math.Min(maxDepth, short.MaxValue))); // Color depth
                w.Write(StreamUtils.EncodeBEInt16(-1)); // Color table
                */
                // Color depth and table (keep values of sample file)
                w.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x18, 0xFF, 0xFF });
            }
            long finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(stsdPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - stsdPos)));
            w.BaseStream.Seek(subtypePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - subtypePos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            // TIME TO SAMPLE START
            long sttsPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stts"));
            w.Write(0); // Version and flags
            w.Write(StreamUtils.EncodeBEInt32(workingChapters.Count));
            foreach (ChapterInfo chapter in workingChapters)
            {
                w.Write(StreamUtils.EncodeBEUInt32(1));
                w.Write(StreamUtils.EncodeBEUInt32((uint)Math.Ceiling((chapter.EndTime - chapter.StartTime) * trackTimescale / 1000.0)));
            }
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(sttsPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - sttsPos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            // TIME TO SAMPLE END

            // SAMPLE <-> CHUNK START
            long stscPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stsc"));
            w.Write(0); // Version and flags
            w.Write(StreamUtils.EncodeBEInt32(1));
            // Attach all samples to 1st chunk
            w.Write(StreamUtils.EncodeBEInt32(1));
            w.Write(StreamUtils.EncodeBEInt32(workingChapters.Count));
            w.Write(StreamUtils.EncodeBEInt32(1));
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(stscPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - stscPos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            // SAMPLE <-> CHUNK END

            // SAMPLE SIZE START
            long stszPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stsz"));
            w.Write(0); // Version and flags
            w.Write(0); // Different block sizes
            w.Write(StreamUtils.EncodeBEInt32(workingChapters.Count));
            long totalTrackTxtSize = 0;
            foreach (ChapterInfo chapter in chapters)
            {
                long trackTxtSize = 2 + Encoding.UTF8.GetBytes(chapter.Title).Length + 12;
                totalTrackTxtSize += trackTxtSize;
                if (isText) w.Write(StreamUtils.EncodeBEUInt32((uint)trackTxtSize));
            }
            if (!isText)
            {
                foreach (ChapterInfo chapter in workingChapters)
                {
                    byte[] pictureData = chapter.Picture != null
                        ? chapter.Picture.PictureData
                        : Properties.Resources._1px_black;
                    w.Write(StreamUtils.EncodeBEUInt32((uint)pictureData.Length));
                }
            }
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(stszPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - stszPos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            // SAMPLE SIZE END

            // CHUNK OFFSET START
            long stcoPos = w.BaseStream.Position;
            w.Write(0);
            w.Write(Utils.Latin1Encoding.GetBytes("stco"));
            w.Write(0); // Version and flags

            w.Write(StreamUtils.EncodeBEInt32(1));

            // Calculate chunk offset and feed it to FileStructureHelper as a header to the MDAT zone 
            //   - Physically located in the TRAK zone
            //   - Child of the TRAK zone (i.e. won't be useful to process if the TRAK zone is deleted)
            // NB : Only works when QT track is located _before_ QT mdat
            string zoneId = isText ? ZONE_MP4_QT_CHAP_TXT_TRAK : ZONE_MP4_QT_CHAP_PIC_TRAK;
            string dataZoneId = ZONE_MP4_QT_CHAP_MDAT;
            Zone chapMdatZone = structureHelper.GetZone(dataZoneId);

            uint offset = (uint)(chapMdatZone.Offset + (isText ? 0 : totalTrackTxtSize));
            structureHelper.AddPostProcessingIndex(w.BaseStream.Position, offset, false, dataZoneId, zoneId, zoneId);
            w.Write(StreamUtils.EncodeBEUInt32(offset)); // TODO switch to co64 when needed ?

            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(stcoPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - stcoPos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            // SAMPLE <-> CHUNK END

            // SAMPLE TABLE END
            // MEDIA INFORMATION END
            // MEDIA END

            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(stblPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - stblPos)));

            w.BaseStream.Seek(minfPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - minfPos)));

            w.BaseStream.Seek(mdiaPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - mdiaPos)));

            w.BaseStream.Seek(trakPos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - trakPos)));

            return 1;
        }
        private static int writeUuidFrame(TagData tag, string key, BinaryWriter w)
        {
            var keyNominal = key.Replace(" ", "");
            if (keyNominal.Length < 32)
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "uuid key should be 16-bit long");
                return 0;
            }
            if (!Utils.IsHex(keyNominal))
            {
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, "uuid key should be given in hexadecimal format");
                return 0;
            }

            byte[] data;
            if (keyNominal.Equals(XmpTag.UUID_XMP, StringComparison.OrdinalIgnoreCase))
            {
                using var mem = new MemoryStream();
                using var memW = new BinaryWriter(mem);
                XmpTag.ToStream(memW, new TagHolder(tag));
                data = mem.ToArray();
            }
            else
            {
                var info = tag.AdditionalFields.FirstOrDefault(f =>
                    "uuid." + keyNominal == f.NativeFieldCode && !f.MarkedForDeletion);
                if (null == info)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR,
                        "Couldn't find uuid " + keyNominal + " inside additionalFields");
                    return 0;
                }
                data = Encoding.UTF8.GetBytes(info.Value);
            }

            uint size = 8 + 16 + (uint)data.Length;
            w.Write(StreamUtils.EncodeBEUInt32(size));
            w.Write(Utils.Latin1Encoding.GetBytes("uuid"));
            w.Write(Utils.ParseHex(keyNominal));
            w.Write(data);

            return 1;
        }

        private int writeUuidFrames(TagData tag, BinaryWriter w)
        {
            var existingUuids =
                structureHelper.ZoneNames
                    .Where(n => n.StartsWith("uuid.", StringComparison.OrdinalIgnoreCase))
                    .Select(n => n[5..].Replace(" ", "").ToUpper());

            var extraUuids = tag.AdditionalFields
                .Where(f => f.TagType.Equals(MetaDataIOFactory.TagType.ANY) || f.TagType.Equals(getImplementedTagType()))
                .Where(f => !f.MarkedForDeletion)
                .Where(f => f.NativeFieldCode.StartsWith("uuid.", StringComparison.OrdinalIgnoreCase))
                .Where(f => !existingUuids.Contains(f.NativeFieldCode[5..].Replace(" ", "").ToUpper()));

            var written = 0;
            foreach (var data in extraUuids)
            {
                written += writeUuidFrame(tag, data.NativeFieldCode[5..], w);
            }

            // Scan AdditionalData for the need to write an XMP UUID if there's none already set
            if (!existingUuids.Contains(XmpTag.UUID_XMP))
            {
                foreach (MetaFieldInfo info in tag.AdditionalFields)
                {
                    // XMP data => create the corresponding UUID atom to host it
                    if (info.NativeFieldCode.StartsWith("xmp.", StringComparison.OrdinalIgnoreCase))
                    {
                        written += writeUuidFrame(tag, XmpTag.UUID_XMP, w);
                        break;
                    }
                }
            }

            return written;
        }

        private static uint getMacDateNow()
        {
            DateTime date = DateTime.UtcNow;
            DateTime date1904 = DateTime.Parse("1/1/1904 0:00:00 AM");
            return (uint)date.Subtract(date1904).TotalSeconds;
        }

        private static byte[] encodeBEFixedPoint32(short intPart, short decPart)
        {
            return new[] {
                (byte)((intPart & 0xFF00) >> 8), (byte)(intPart & 0x00FF),
                (byte)((decPart & 0xFF00) >> 8), (byte)(decPart & 0x00FF)
            };
        }

        // reduce the useful MDAT to a few Kbs (for dev purposes only)

#pragma warning disable S125 // Sections of code should not be commented out
        /*
                public override bool Remove(Stream s)
                {
                    long chapDataSize = 0;
                    foreach (Zone zone in Zones)
                    {
                        if (zone.Name.Equals(ZONE_MP4_QT_CHAP_MDAT))
                        {
                            chapDataSize = zone.Size;
                            break;
                        }
                    }
                    s.BaseStream.Seek(AudioDataOffset, SeekOrigin.Begin);
                    long newSize = chapDataSize + 32000;
                    StreamUtils.WriteBEInt32(s, (int)newSize);
                    s.BaseStream.Seek(4 + newSize, SeekOrigin.Current);
                    StreamUtils.ShortenStream(s, AudioDataOffset + AudioDataSize, (uint)(AudioDataSize - newSize));

                    return true;
                }
                */
    }
#pragma warning restore S125 // Sections of code should not be commented out
}