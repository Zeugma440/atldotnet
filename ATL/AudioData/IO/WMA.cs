using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Commons;
using static ATL.ChannelsArrangements;
using System.Linq;
using System.Collections.Concurrent;
using static ATL.TagData;
using System.Threading.Tasks;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Windows Media Audio 7,8 and 9 files manipulation (extension : .WMA)
    /// </summary>
    internal partial class WMA : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_CONTENT_DESCRIPTION = "contentDescription";
        private const string ZONE_EXTENDED_CONTENT_DESCRIPTION = "extContentDescription";
        private const string ZONE_EXTENDED_HEADER_METADATA = "extHeaderMeta";
        private const string ZONE_EXTENDED_HEADER_METADATA_LIBRARY = "extHeaderMetaLibrary";


        // Object IDs
        private static readonly byte[] WMA_HEADER_ID = { 48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static readonly byte[] WMA_HEADER_EXTENSION_ID = { 0xB5, 0x03, 0xBF, 0x5F, 0x2E, 0xA9, 0xCF, 0x11, 0x8E, 0xE3, 0x00, 0xc0, 0x0c, 0x20, 0x53, 0x65 };

        private static readonly byte[] WMA_METADATA_OBJECT_ID = { 0xEA, 0xCB, 0xF8, 0xC5, 0xAF, 0x5B, 0x77, 0x48, 0x84, 0x67, 0xAA, 0x8C, 0x44, 0xFA, 0x4C, 0xCA };
        private static readonly byte[] WMA_METADATA_LIBRARY_OBJECT_ID = { 0x94, 0x1C, 0x23, 0x44, 0x98, 0x94, 0xD1, 0x49, 0xA1, 0x41, 0x1D, 0x13, 0x4E, 0x45, 0x70, 0x54 };

        private static readonly byte[] WMA_FILE_PROPERTIES_ID = { 161, 220, 171, 140, 71, 169, 207, 17, 142, 228, 0, 192, 12, 32, 83, 101 };
        private static readonly byte[] WMA_STREAM_PROPERTIES_ID = { 145, 7, 220, 183, 183, 169, 207, 17, 142, 230, 0, 192, 12, 32, 83, 101 };
        private static readonly byte[] WMA_CONTENT_DESCRIPTION_ID = { 51, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static readonly byte[] WMA_EXTENDED_CONTENT_DESCRIPTION_ID = { 64, 164, 208, 210, 7, 227, 210, 17, 151, 240, 0, 160, 201, 94, 168, 80 };

        private static readonly byte[] WMA_LANGUAGE_LIST_OBJECT_ID = { 0xA9, 0x46, 0x43, 0x7C, 0xE0, 0xEF, 0xFC, 0x4B, 0xB2, 0x29, 0x39, 0x3E, 0xDE, 0x41, 0x5C, 0x85 };


        // Format IDs
#pragma warning disable S1144 // Unused private types or members should be removed
        // ReSharper disable UnusedMember.Local
#pragma warning disable IDE0051 // Remove unused private members
        private const int WMA_ID = 0x161;
        private const int WMA_PRO_ID = 0x162;
        private const int WMA_LOSSLESS_ID = 0x163;
        private const int WMA_GSM_CBR_ID = 0x7A21;
        private const int WMA_GSM_VBR_ID = 0x7A22;

        // Max. number of characters in tag field
        private const byte WMA_MAX_STRING_SIZE = 250;
#pragma warning restore IDE0051 // Remove unused private members
        // ReSharper restore UnusedMember.Local
#pragma warning restore S1144 // Unused private types or members should be removed

        // File data - for internal use
        private sealed class FileData
        {
            public long HeaderSize;
            public int FormatTag;                                       // Format ID tag
            public ushort Channels;                                // Number of channels
            public int SampleRate;                                   // Sample rate (hz)

            public uint ObjectCount;                     // Number of high-level objects
            public long ObjectListOffset;       // Offset of the high-level objects list


            public FileData() { Reset(); }

            private void Reset()
            {
                HeaderSize = 0;
                FormatTag = 0;
                Channels = 0;
                SampleRate = 0;
                ObjectCount = 0;
                ObjectListOffset = -1;
            }
        }

        private FileData fileData;

        private bool isLossless;

        // Mapping between WMA frame codes and ATL frame codes
        // NB : WM/TITLE, WM/AUTHOR, WM/COPYRIGHT, WM/DESCRIPTION and WM/RATING are not WMA extended fields; therefore
        // their ID will not appear as is in the WMA header. 
        // Their info is contained in the standard Content Description block at the very beginning of the file
        public static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { "WM/TITLE", Field.TITLE },
            { "WM/AlbumTitle", Field.ALBUM },
            { "WM/AUTHOR", Field.ARTIST },
            { "WM/COPYRIGHT", Field.COPYRIGHT },
            { "WM/DESCRIPTION", Field.COMMENT },
            { "WM/Year", Field.RECORDING_YEAR },
            { "WM/Genre", Field.GENRE },
            { "WM/TrackNumber", Field.TRACK_NUMBER_TOTAL },
            { "WM/PartOfSet", Field.DISC_NUMBER_TOTAL },
            { "WM/RATING", Field.RATING },
            { "WM/SharedUserRating", Field.RATING },
            { "WM/Composer", Field.COMPOSER },
            { "WM/AlbumArtist", Field.ALBUM_ARTIST },
            { "WM/Conductor", Field.CONDUCTOR },
            { "WM/Lyrics", Field.LYRICS_UNSYNCH },
            { "WM/AlbumSortOrder", Field.SORT_ALBUM },
            { "WM/ArtistSortOrder", Field.SORT_ARTIST },
            { "WM/TitleSortOrder", Field.SORT_TITLE },
            { "WM/ContentGroupDescription", Field.GROUP },
            { "WM/BeatsPerMinute", Field.BPM },
            { "WM/EncodedBy", Field.ENCODED_BY },
            { "WM/OriginalReleaseTime", Field.PUBLISHING_DATE }, // De facto standard as specs don't define any dedicated field to store a full date
            { "WM/OriginalReleaseYear", Field.ORIG_RELEASE_YEAR },
            { "WM/ToolName", Field.ENCODER },
            { "WM/Language", Field.LANGUAGE },
            { "WM/ISRC", Field.ISRC },
            { "WM/CatalogNo", Field.CATALOG_NUMBER },
            { "WM/AudioSourceURL", Field.AUDIO_SOURCE_URL },
            { "WM/Writer", Field.LYRICIST }
        };

        public static readonly IDictionary<string, Field> frameMappingLower = new Dictionary<string, Field>();

        public static readonly IDictionary<Field, string> invertedFrameMapping = new Dictionary<Field, string>();

        // Field that are embedded in standard ASF description, and do not need to be written in any other frame
        private static readonly IList<string> embeddedFields = new List<string>
        {
            "WM/TITLE",
            "WM/AUTHOR",
            "WM/COPYRIGHT",
            "WM/DESCRIPTION",
            "WM/RATING"
        };

        // Mapping between WMA frame codes and frame classes that aren't class 0 (Unicode string), or force certain types to their rightful class
        // To be further populated while reading
        private static readonly ConcurrentDictionary<string, ushort> frameClasses = new ConcurrentDictionary<string, ushort>
        {
            ["WM/SharedUserRating"] = 3,
            ["WM/TrackNumber"] = 0,
            ["WM/PartOfSet"] = 0
        };


        private IList<string> languages; // Optional language index described in the WMA header

        private AudioDataManager.SizeInfo m_sizeInfo;

        // Keep these in memory to prevent setting them twice using AdditionalFields
        private readonly ISet<string> m_writtenFieldCodes = new HashSet<string>();


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO

        /// <inheritdoc/>
        public int SampleRate { get; private set; }

        /// <inheritdoc/>
        public bool IsVBR { get; private set; }

        /// <inheritdoc/>
        public AudioFormat AudioFormat { get; }

        /// <inheritdoc/>
        public int CodecFamily => isLossless ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY;

        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public double BitRate { get; private set; }

        /// <inheritdoc/>
        public int BitDepth => 16; // Seems to be constant

        /// <inheritdoc/>
        public double Duration { get; private set; }

        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.APE, MetaDataIOFactory.TagType.ID3V1 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => true;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;

        /// <inheritdoc/>
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier
            if (frameMappingLower.TryGetValue(ID.ToLower(), out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }

        /// <inheritdoc/>
        public long AudioDataSize { get; set; }


        // IMetaDataIO

        /// <inheritdoc/>
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }

        /// <inheritdoc/>
        protected override byte ratingConvention => RC_ASF;


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            SampleRate = 0;
            IsVBR = false;
            isLossless = false;
            BitRate = 0;
            Duration = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        private static void generateLowerMappings()
        {
            foreach (var mapping in frameMapping)
            {
                frameMappingLower[mapping.Key.ToLower()] = mapping.Value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public WMA(string filePath, AudioFormat format)
        {
            FileName = filePath;
            AudioFormat = format;
            resetData();
            generateLowerMappings();
        }


        // ---------- SUPPORT METHODS

        private static void addFrameClass(string frameCode, ushort frameClass)
        {
            frameClasses.TryAdd(frameCode, frameClass);
        }

        private void cacheLanguageIndex(Stream source)
        {
            if (null == languages)
            {
                languages = new List<string>();

                var initialPosition = source.Position;
                source.Seek(fileData.ObjectListOffset, SeekOrigin.Begin);

                BinaryReader r = new BinaryReader(source);

                for (int i = 0; i < fileData.ObjectCount; i++)
                {
                    var position = source.Position;
                    var bytes = r.ReadBytes(16);
                    var objectSize = r.ReadUInt64();

                    // Language index (optional; one only -- useful to map language codes to extended header tag information)
                    if (WMA_LANGUAGE_LIST_OBJECT_ID.SequenceEqual(bytes))
                    {
                        ushort nbLanguages = r.ReadUInt16();

                        for (int j = 0; j < nbLanguages; j++)
                        {
                            var strLen = r.ReadByte();
                            long position2 = source.Position;
                            if (strLen > 2) languages.Add(Utils.StripEndingZeroChars(Encoding.Unicode.GetString(r.ReadBytes(strLen))));
                            source.Seek(position2 + strLen, SeekOrigin.Begin);
                        }
                    }

                    source.Seek(position + (long)objectSize, SeekOrigin.Begin);
                }

                source.Seek(initialPosition, SeekOrigin.Begin);
            }
        }

        private ushort encodeLanguage(Stream source, string languageCode)
        {
            if (null == languages) cacheLanguageIndex(source);

            if (0 == languages!.Count) return 0;
            return (ushort)languages.IndexOf(languageCode);
        }

        private string decodeLanguage(Stream source, ushort languageIndex)
        {
            if (null == languages) cacheLanguageIndex(source);

            if (languages!.Count > 0)
            {
                if (languageIndex < languages.Count) return languages[languageIndex];
                return languages[0]; // Index out of bounds
            }
            return "";
        }

        private void readContentDescription(BufferedBinaryReader source, ReadTagParams readTagParams)
        {
            ushort[] fieldSize = new ushort[5];

            // Read standard field sizes
            for (int i = 0; i < 5; i++) fieldSize[i] = source.ReadUInt16();

            // Read standard field values
            for (int i = 0; i < 5; i++)
            {
                if (fieldSize[i] > 0)
                {
                    // Read field value
                    var fieldValue = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

                    // Set corresponding tag field if supported
                    switch (i)
                    {
                        case 0: SetMetaField("WM/TITLE", fieldValue, readTagParams.ReadAllMetaFrames, ZONE_CONTENT_DESCRIPTION); break;
                        case 1: SetMetaField("WM/AUTHOR", fieldValue, readTagParams.ReadAllMetaFrames, ZONE_CONTENT_DESCRIPTION); break;
                        case 2: SetMetaField("WM/COPYRIGHT", fieldValue, readTagParams.ReadAllMetaFrames, ZONE_CONTENT_DESCRIPTION); break;
                        case 3: SetMetaField("WM/DESCRIPTION", fieldValue, readTagParams.ReadAllMetaFrames, ZONE_CONTENT_DESCRIPTION); break;
                        case 4: SetMetaField("WM/RATING", fieldValue, readTagParams.ReadAllMetaFrames, ZONE_CONTENT_DESCRIPTION); break;
                    }
                }
            }
        }

        private void readHeaderExtended(BufferedBinaryReader source, long sizePosition1, ulong size1, long sizePosition2, ulong size2, ReadTagParams readTagParams)
        {
            source.Seek(16, SeekOrigin.Current); // Reserved field 1
            source.Seek(2, SeekOrigin.Current); // Reserved field 2

            var sizePosition3 = source.Position;
            uint headerExtendedSize = source.ReadUInt32(); // Size of actual data

            // Looping through header extension objects
            var position = source.Position;
            var limit = (ulong)position + headerExtendedSize;
            while ((ulong)position < limit)
            {
                var framePosition = source.Position;
                var headerExtensionObjectId = source.ReadBytes(16);
                var headerExtensionObjectSize = source.ReadUInt64();

                // Additional metadata (Optional frames)
                if (WMA_METADATA_OBJECT_ID.SequenceEqual(headerExtensionObjectId) || WMA_METADATA_LIBRARY_OBJECT_ID.SequenceEqual(headerExtensionObjectId))
                {
                    ushort nbObjects = source.ReadUInt16();
                    bool isLibraryObject = WMA_METADATA_LIBRARY_OBJECT_ID.SequenceEqual(headerExtensionObjectId);

                    string zoneCode = isLibraryObject ? ZONE_EXTENDED_HEADER_METADATA_LIBRARY : ZONE_EXTENDED_HEADER_METADATA;

                    structureHelper.AddZone(framePosition, (int)headerExtensionObjectSize, zoneCode);
                    // Store frame information for future editing, since current frame is optional
                    if (readTagParams.PrepareForWriting)
                    {
                        structureHelper.AddSize(sizePosition1, size1, zoneCode);
                        structureHelper.AddSize(sizePosition2, size2, zoneCode);
                        structureHelper.AddSize(sizePosition3, headerExtendedSize, zoneCode);
                    }

                    for (int i = 0; i < nbObjects; i++)
                    {
                        var languageIndex = source.ReadUInt16();
                        var streamNumber = source.ReadUInt16();
                        // Length (in bytes) of Name field
                        var nameSize = source.ReadUInt16();
                        // Type of data stored in current field
                        var fieldDataType = source.ReadUInt16();
                        // Size of data stored in current field
                        var fieldDataSize = source.ReadInt32();
                        // Name of current field
                        var fieldName = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(nameSize)));

                        var dataPosition = source.Position;
                        readTagField(source, zoneCode, fieldName, fieldDataType, fieldDataSize, readTagParams, true, languageIndex, streamNumber);

                        source.Seek(dataPosition + fieldDataSize, SeekOrigin.Begin);
                    }
                }

                source.Seek(position + (long)headerExtensionObjectSize, SeekOrigin.Begin);
                position = source.Position;
            }

            // Add absent zone definitions for further editing
            if (readTagParams.PrepareForWriting)
            {
                if (!structureHelper.ZoneNames.Contains(ZONE_EXTENDED_HEADER_METADATA))
                {
                    structureHelper.AddZone(source.Position, 0, ZONE_EXTENDED_HEADER_METADATA);
                    structureHelper.AddSize(sizePosition1, size1, ZONE_EXTENDED_HEADER_METADATA);
                    structureHelper.AddSize(sizePosition2, size2, ZONE_EXTENDED_HEADER_METADATA);
                    structureHelper.AddSize(sizePosition3, headerExtendedSize, ZONE_EXTENDED_HEADER_METADATA);
                }
                if (!structureHelper.ZoneNames.Contains(ZONE_EXTENDED_HEADER_METADATA_LIBRARY))
                {
                    structureHelper.AddZone(source.Position, 0, ZONE_EXTENDED_HEADER_METADATA_LIBRARY);
                    structureHelper.AddSize(sizePosition1, size1, ZONE_EXTENDED_HEADER_METADATA_LIBRARY);
                    structureHelper.AddSize(sizePosition2, size2, ZONE_EXTENDED_HEADER_METADATA_LIBRARY);
                    structureHelper.AddSize(sizePosition3, headerExtendedSize, ZONE_EXTENDED_HEADER_METADATA_LIBRARY);
                }
            }
        }

        private void readExtendedContentDescription(BufferedBinaryReader source, ReadTagParams readTagParams)
        {
            // Read extended tag data
            var fieldCount = source.ReadUInt16();
            for (int iterator1 = 0; iterator1 < fieldCount; iterator1++)
            {
                // Read field name
                var dataSize = source.ReadUInt16();
                var fieldName = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(dataSize)));
                // Read value data type
                var dataType = source.ReadUInt16();
                dataSize = source.ReadUInt16();

                var dataPosition = source.Position;
                readTagField(source, ZONE_EXTENDED_CONTENT_DESCRIPTION, fieldName, dataType, dataSize, readTagParams);

                source.Seek(dataPosition + dataSize, SeekOrigin.Begin);
            }
        }

        public void readTagField(BufferedBinaryReader source, string zoneCode, string fieldName, ushort fieldDataType, int fieldDataSize, ReadTagParams readTagParams, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            string fieldValue = "";
            bool setMeta = true;

            addFrameClass(fieldName, fieldDataType);

            switch (fieldDataType)
            {
                // Unicode string
                case 0:
                    fieldValue = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(fieldDataSize)));
                    break;
                // Byte array
                case 1 when fieldName.ToUpper().Equals("WM/PICTURE"):
                    {
                        byte picCode = source.ReadByte();
                        // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                        PictureInfo.PIC_TYPE picType = ID3v2.DecodeID3v2PictureType(picCode);

                        int picturePosition;
                        if (picType.Equals(PictureInfo.PIC_TYPE.Unsupported))
                        {
                            picturePosition = takePicturePosition(MetaDataIOFactory.TagType.NATIVE, picCode);
                        }
                        else
                        {
                            picturePosition = takePicturePosition(picType);
                        }

                        if (readTagParams.ReadPictures)
                        {
                            int picSize = source.ReadInt32();
                            StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode); // MIME type
                            string description = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

                            PictureInfo picInfo = PictureInfo.fromBinaryData(source, picSize, picType, getImplementedTagType(), picCode, picturePosition);
                            picInfo.Description = description;

                            tagData.Pictures.Add(picInfo);
                        }
                        setMeta = false;
                        break;
                    }
                case 1:
                    fieldValue = Utils.Latin1Encoding.GetString(Utils.EncodeTo64(source.ReadBytes(fieldDataSize)));
                    break;
                // 16-bit Boolean (metadata); 32-bit Boolean (extended header)
                case 2:
                    fieldValue = isExtendedHeader ? source.ReadUInt32().ToString() : source.ReadUInt16().ToString();
                    break;
                // 32-bit unsigned integer
                case 3:
                    {
                        uint intValue = source.ReadUInt32();
                        if (fieldName.Equals("WM/GENRE", StringComparison.OrdinalIgnoreCase)) intValue++;
                        fieldValue = intValue.ToString();
                        break;
                    }
                // 64-bit unsigned integer
                case 4:
                    fieldValue = source.ReadUInt64().ToString();
                    break;
                // 16-bit unsigned integer
                case 5:
                    fieldValue = source.ReadUInt16().ToString();
                    break;
                // 128-bit GUID; unused for now
                case 6:
                    fieldValue = Utils.Latin1Encoding.GetString(Utils.EncodeTo64(source.ReadBytes(fieldDataSize)));
                    break;
            }

            if (setMeta) SetMetaField(fieldName.Trim(), fieldValue, readTagParams.ReadAllMetaFrames, zoneCode, 0, streamNumber, decodeLanguage(source, languageIndex));
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, WMA_HEADER_ID);
        }

        private bool readData(Stream source, ReadTagParams readTagParams)
        {
            bool result = false;

            languages?.Clear();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            reader.Seek(m_sizeInfo.ID3v2Size, SeekOrigin.Begin);

            var initialPos = reader.Position;

            // Check for existing header
            var ID = reader.ReadBytes(16);

            // Header (mandatory; one only)
            if (IsValidHeader(ID))
            {
                var sizePosition1 = reader.Position;
                var headerSize = reader.ReadUInt64();
                var countPosition = reader.Position;
                var objectCount = reader.ReadUInt32();
                reader.Seek(2, SeekOrigin.Current); // Reserved data
                fileData.ObjectCount = objectCount;
                fileData.ObjectListOffset = reader.Position;

                // Read all objects in header and get needed data
                for (int i = 0; i < objectCount; i++)
                {
                    var position = reader.Position;
                    ID = reader.ReadBytes(16);
                    var sizePosition2 = reader.Position;
                    var objectSize = reader.ReadUInt64();

                    // File properties (mandatory; one only)
                    if (WMA_FILE_PROPERTIES_ID.SequenceEqual(ID))
                    {
                        reader.Seek(40, SeekOrigin.Current);
                        Duration = reader.ReadUInt64() / 10000.0;       // Play duration (100-nanoseconds)
                        reader.Seek(8, SeekOrigin.Current);  // Send duration; unused for now
                        Duration -= reader.ReadUInt64();                // Preroll duration (ms)
                    }
                    // Stream properties (mandatory; one per stream)
                    else if (WMA_STREAM_PROPERTIES_ID.SequenceEqual(ID))
                    {
                        reader.Seek(54, SeekOrigin.Current);
                        fileData.FormatTag = reader.ReadUInt16();
                        fileData.Channels = reader.ReadUInt16();
                        fileData.SampleRate = reader.ReadInt32();
                    }
                    // Content description (optional; one only)
                    // -> standard, pre-defined metadata
                    else if (WMA_CONTENT_DESCRIPTION_ID.SequenceEqual(ID) && readTagParams.ReadTag)
                    {
                        structureHelper.AddZone(position, (int)objectSize, ZONE_CONTENT_DESCRIPTION);
                        // Store frame information for future editing, since current frame is optional
                        if (readTagParams.PrepareForWriting)
                        {
                            structureHelper.AddSize(sizePosition1, headerSize, ZONE_CONTENT_DESCRIPTION);
                            structureHelper.AddCounter(countPosition, objectCount, ZONE_CONTENT_DESCRIPTION);
                        }
                        readContentDescription(reader, readTagParams);
                    }
                    // Extended content description (optional; one only)
                    // -> extended, dynamic metadata
                    else if (WMA_EXTENDED_CONTENT_DESCRIPTION_ID.SequenceEqual(ID) && readTagParams.ReadTag)
                    {
                        structureHelper.AddZone(position, (int)objectSize, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                        // Store frame information for future editing, since current frame is optional
                        if (readTagParams.PrepareForWriting)
                        {
                            structureHelper.AddSize(sizePosition1, headerSize, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                            structureHelper.AddCounter(countPosition, objectCount, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                        }
                        readExtendedContentDescription(reader, readTagParams);
                    }
                    // Header extension (mandatory; one only)
                    // -> extended, dynamic additional metadata such as additional embedded pictures (any picture after the 1st one stored in extended content)
                    else if (WMA_HEADER_EXTENSION_ID.SequenceEqual(ID) && readTagParams.ReadTag)
                    {
                        readHeaderExtended(reader, sizePosition1, headerSize, sizePosition2, objectSize, readTagParams);
                    }

                    reader.Seek(position + (long)objectSize, SeekOrigin.Begin);
                }

                // Add absent zone definitions for further editing
                if (readTagParams.PrepareForWriting)
                {
                    if (!structureHelper.ZoneNames.Contains(ZONE_CONTENT_DESCRIPTION))
                    {
                        structureHelper.AddZone(reader.Position, 0, ZONE_CONTENT_DESCRIPTION);
                        structureHelper.AddSize(sizePosition1, headerSize, ZONE_CONTENT_DESCRIPTION);
                        structureHelper.AddCounter(countPosition, objectCount, ZONE_CONTENT_DESCRIPTION);
                    }
                    if (!structureHelper.ZoneNames.Contains(ZONE_EXTENDED_CONTENT_DESCRIPTION))
                    {
                        structureHelper.AddZone(reader.Position, 0, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                        structureHelper.AddSize(sizePosition1, headerSize, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                        structureHelper.AddCounter(countPosition, objectCount, ZONE_EXTENDED_CONTENT_DESCRIPTION);
                    }
                }

                result = true;
            }

            fileData.HeaderSize = reader.Position - initialPos;

            AudioDataOffset = reader.Position;
            AudioDataSize = m_sizeInfo.FileSize - AudioDataOffset;

            return result;
        }

        private static bool isValid(FileData Data)
        {
            return Data.Channels > 0 && Data.SampleRate >= 8000 && Data.SampleRate <= 96000;
        }

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            m_sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            fileData = new FileData();

            resetData();
            bool result = readData(source, readTagParams);

            // Process data if loaded and valid
            if (result && isValid(fileData))
            {
                ChannelsArrangement = GuessFromChannelNumber(fileData.Channels);
                SampleRate = fileData.SampleRate;
                BitRate = (m_sizeInfo.FileSize - m_sizeInfo.TotalTagSize - fileData.HeaderSize) * 8.0 / Duration;
                IsVBR = WMA_GSM_VBR_ID == fileData.FormatTag;
                isLossless = WMA_LOSSLESS_ID == fileData.FormatTag;
            }

            return result;
        }

        protected override void preprocessWrite(TagData dataToWrite)
        {
            base.preprocessWrite(dataToWrite);
            m_writtenFieldCodes.Clear();
        }

        /// <inheritdoc/>
        protected override int write(TagData tag, Stream s, string zone)
        {
            using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
            return write(tag, w, zone);
        }

        private int write(TagData tag, BinaryWriter w, string zone)
        {
            computePicturesDestination(tag.Pictures);

            return zone switch
            {
                ZONE_CONTENT_DESCRIPTION => writeContentDescription(tag, m_writtenFieldCodes, w),
                ZONE_EXTENDED_HEADER_METADATA => writeExtendedHeaderMeta(tag, w),
                ZONE_EXTENDED_HEADER_METADATA_LIBRARY => writeExtendedHeaderMetaLibrary(tag, w),
                ZONE_EXTENDED_CONTENT_DESCRIPTION => writeExtendedContentDescription(tag, m_writtenFieldCodes, w),
                _ => 0
            };
        }

        private static int writeContentDescription(TagData tag, ISet<string> writtenFieldCodes, BinaryWriter w)
        {
            var beginPos = w.BaseStream.Position;
            w.Write(WMA_CONTENT_DESCRIPTION_ID);
            var frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method

            string title = "";
            string author = "";
            string copyright = "";
            string comment = "";
            string rating = "";

            IDictionary<Field, string> map = tag.ToMap();
            if (0 == invertedFrameMapping.Count)
            {
                foreach (var kvp in frameMapping)
                {
                    invertedFrameMapping[kvp.Value] = kvp.Key.ToLower();
                }
            }

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length <= 0) continue; // No frame with empty value

                switch (frameType)
                {
                    case Field.TITLE:
                        title = map[frameType];
                        writtenFieldCodes.Add(invertedFrameMapping[frameType]);
                        break;
                    case Field.ARTIST:
                        author = map[frameType];
                        writtenFieldCodes.Add(invertedFrameMapping[frameType]);
                        break;
                    case Field.COPYRIGHT:
                        copyright = map[frameType];
                        writtenFieldCodes.Add(invertedFrameMapping[frameType]);
                        break;
                    case Field.COMMENT:
                        comment = map[frameType];
                        writtenFieldCodes.Add(invertedFrameMapping[frameType]);
                        break;
                    case Field.RATING:
                        rating = map[frameType];
                        writtenFieldCodes.Add(invertedFrameMapping[frameType]);
                        break;
                }
            }

            // Read standard field sizes (+1 for last null characher; x2 for unicode)
            if (title.Length > 0) w.Write((ushort)((title.Length + 1) * 2)); else w.Write((ushort)0);
            if (author.Length > 0) w.Write((ushort)((author.Length + 1) * 2)); else w.Write((ushort)0);
            if (copyright.Length > 0) w.Write((ushort)((copyright.Length + 1) * 2)); else w.Write((ushort)0);
            if (comment.Length > 0) w.Write((ushort)((comment.Length + 1) * 2)); else w.Write((ushort)0);
            if (rating.Length > 0) w.Write((ushort)((rating.Length + 1) * 2)); else w.Write((ushort)0);

            if (title.Length > 0) w.Write(Encoding.Unicode.GetBytes(title + '\0'));
            if (author.Length > 0) w.Write(Encoding.Unicode.GetBytes(author + '\0'));
            if (copyright.Length > 0) w.Write(Encoding.Unicode.GetBytes(copyright + '\0'));
            if (comment.Length > 0) w.Write(Encoding.Unicode.GetBytes(comment + '\0'));
            if (rating.Length > 0) w.Write(Encoding.Unicode.GetBytes(rating + '\0'));

            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return (title.Length > 0 ? 1 : 0) + (author.Length > 0 ? 1 : 0) + (copyright.Length > 0 ? 1 : 0) + (comment.Length > 0 ? 1 : 0) + (rating.Length > 0 ? 1 : 0);
        }

        private int writeExtendedContentDescription(TagData tag, ISet<string> writtenFieldCodes, BinaryWriter w)
        {
            ushort counter = 0;

            var beginPos = w.BaseStream.Position;
            w.Write(WMA_EXTENDED_CONTENT_DESCRIPTION_ID);
            var frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            var counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            IDictionary<Field, string> map = tag.ToMap();

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (!embeddedFields.Contains(s) && frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            string value = formatBeforeWriting(frameType, tag, map);

                            writeTextFrame(w, s, value);
                            writtenFieldCodes.Add(s.ToLower());
                            counter++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if ((ZONE_EXTENDED_CONTENT_DESCRIPTION.Equals(fieldInfo.Zone) || "".Equals(fieldInfo.Zone))
                    && !writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToLower())
                    )
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, FormatBeforeWriting(fieldInfo.Value));
                    counter++;
                }
            }

            // Picture fields
            foreach (PictureInfo picInfo in tag.Pictures)
            {
                if (0 == picInfo.TransientFlag)
                {
                    writePictureFrame(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)picInfo.NativePicCode : ID3v2.EncodeID3v2PictureType(picInfo.PicType), picInfo.Description);
                    counter++;
                }
            }


            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return counter;
        }

        private int writeExtendedHeaderMeta(TagData tag, BinaryWriter w)
        {
            var beginPos = w.BaseStream.Position;
            w.Write(WMA_METADATA_OBJECT_ID);
            var frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            var counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            var counter = writeExtendedMeta(tag, w);

            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return counter;
        }

        private int writeExtendedHeaderMetaLibrary(TagData tag, BinaryWriter w)
        {
            var beginPos = w.BaseStream.Position;
            w.Write(WMA_METADATA_LIBRARY_OBJECT_ID);
            var frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            var counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            var counter = writeExtendedMeta(tag, w, true);

            // Go back to frame size locations to write their actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return counter;
        }

        private ushort writeExtendedMeta(TagData tag, BinaryWriter w, bool isExtendedMetaLibrary = false)
        {
            ushort counter = 0;
            // Supported textual fields : all current supported fields are located in extended content description frame

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TagType.ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion)
                {
                    if ((ZONE_EXTENDED_HEADER_METADATA.Equals(fieldInfo.Zone) && !isExtendedMetaLibrary) || (ZONE_EXTENDED_HEADER_METADATA_LIBRARY.Equals(fieldInfo.Zone) && isExtendedMetaLibrary))
                    {
                        writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value, true, encodeLanguage(w.BaseStream, fieldInfo.Language), fieldInfo.StreamNumber);
                        counter++;
                    }
                }
            }

            // Picture fields
            if (isExtendedMetaLibrary)
            {
                foreach (PictureInfo picInfo in tag.Pictures)
                {
                    if (1 == picInfo.TransientFlag)
                    {
                        writePictureFrame(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)picInfo.NativePicCode : ID3v2.EncodeID3v2PictureType(picInfo.PicType), picInfo.Description, true);
                        counter++;
                    }
                }
            }

            return counter;
        }

        private static void writeTextFrame(BinaryWriter writer, string frameCode, string text, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(frameCode + '\0');
            ushort nameSize = (ushort)nameBytes.Length;

            if (isExtendedHeader)
            {
                writer.Write(languageIndex); // Metadata object : Reserved / Metadata library object : Language list index
                writer.Write(streamNumber); // Corresponding stream number
            }

            // Name length and name
            writer.Write(nameSize);
            if (!isExtendedHeader) writer.Write(nameBytes);

            ushort frameClass = 0;
            if (frameClasses.TryGetValue(frameCode, out var clazz)) frameClass = clazz;
            writer.Write(frameClass);

            var dataSizePos = writer.BaseStream.Position;
            // Data size placeholder to be rewritten in a few lines
            if (isExtendedHeader) writer.Write((uint)0);
            else writer.Write((ushort)0);

            if (isExtendedHeader) writer.Write(nameBytes);

            var dataPos = writer.BaseStream.Position;
            switch (frameClass)
            {
                // Unicode string
                case 0:
                    writer.Write(Encoding.Unicode.GetBytes(text + '\0'));
                    break;
                // Non-picture byte array
                case 1:
                    writer.Write(Utils.DecodeFrom64(Utils.Latin1Encoding.GetBytes(text)));
                    break;
                // 32-bit boolean; 16-bit boolean if in extended header
                case 2 when isExtendedHeader:
                    writer.Write(Utils.ToBoolean(text) ? (ushort)1 : (ushort)0);
                    break;
                case 2:
                    writer.Write(Utils.ToBoolean(text) ? (uint)1 : (uint)0);
                    break;
                // 32-bit unsigned integer
                case 3:
                    writer.Write(Convert.ToUInt32(text));
                    break;
                // 64-bit unsigned integer
                case 4:
                    writer.Write(Convert.ToUInt64(text));
                    break;
                // 16-bit unsigned integer
                case 5:
                    writer.Write(Convert.ToUInt16(text));
                    break;
                // 128-bit GUID
                case 6:
                    writer.Write(Utils.DecodeFrom64(Utils.Latin1Encoding.GetBytes(text)));
                    break;
            }

            // Go back to frame size locations to write their actual size 
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(dataSizePos, SeekOrigin.Begin);
            if (!isExtendedHeader)
            {
                writer.Write(Convert.ToUInt16(finalFramePos - dataPos));
            }
            else
            {
                writer.Write(Convert.ToUInt32(finalFramePos - dataPos));
            }
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private static void writePictureFrame(BinaryWriter writer, byte[] pictureData, string mimeType, byte pictureTypeCode, string description, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes("WM/Picture" + '\0');
            ushort nameSize = (ushort)nameBytes.Length;

            if (isExtendedHeader)
            {
                writer.Write(languageIndex); // Metadata object : Reserved / Metadata library object : Language list index
                writer.Write(streamNumber); // Corresponding stream number
            }

            // Name length and name
            writer.Write(nameSize);
            if (!isExtendedHeader) writer.Write(nameBytes);

            ushort frameClass = 1;
            writer.Write(frameClass);

            var dataSizePos = writer.BaseStream.Position;
            // Data size placeholder to be rewritten in a few lines
            if (isExtendedHeader)
            {
                writer.Write((uint)0);
            }
            else
            {
                writer.Write((ushort)0);
            }

            if (isExtendedHeader)
            {
                writer.Write(nameBytes);
            }
            var dataPos = writer.BaseStream.Position;

            writer.Write(pictureTypeCode);
            writer.Write(pictureData.Length);

            writer.Write(Encoding.Unicode.GetBytes(mimeType + '\0'));
            writer.Write(Encoding.Unicode.GetBytes(description + '\0'));     // Picture description

            writer.Write(pictureData);

            // Go back to frame size locations to write their actual size 
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(dataSizePos, SeekOrigin.Begin);
            if (isExtendedHeader)
            {
                writer.Write(Convert.ToUInt32(finalFramePos - dataPos));
            }
            else
            {
                writer.Write(Convert.ToUInt16(finalFramePos - dataPos));
            }
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        // Specific implementation for conservation of non-WM/xxx fields
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public override async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            if (Settings.ASF_keepNonWMFieldsWhenRemovingTag)
            {
                TagData tag = prepareRemove();
                return await WriteAsync(s, tag, args);
            }
            return await base.RemoveAsync(s, args);
        }

        private TagData prepareRemove()
        {
            TagData result = new TagData();
            foreach (Field b in frameMapping.Values)
            {
                result.IntegrateValue(b, "");
            }

            foreach (var fieldInfo in GetAdditionalFields().Where(fieldInfo => fieldInfo.NativeFieldCode.ToUpper().StartsWith("WM/")))
            {
                MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo)
                {
                    MarkedForDeletion = true
                };
                result.AdditionalFields.Add(emptyFieldInfo);
            }
            return result;
        }

        // Decides whether picture has to be written and set it to their TransientFlag field
        // -1: Nowhere
        //  0: In content description
        //  1: In extended metadata
        private void computePicturesDestination(IEnumerable<PictureInfo> picInfos)
        {
            bool foundFirstContentDescPicture = false;
            foreach (PictureInfo picInfo in picInfos)
            {
                // Picture has either to be supported, or to come from the right tag standard
                bool doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = getImplementedTagType() == picInfo.TagType;
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && !picInfo.MarkedForDeletion;

                if (doWritePicture)
                {
                    if (picInfo.PictureData.Length + 50 <= ushort.MaxValue && !foundFirstContentDescPicture)
                    {
                        picInfo.TransientFlag = 0;
                        foundFirstContentDescPicture = true;
                    }
                    else picInfo.TransientFlag = 1;
                }
                else picInfo.TransientFlag = -1;
            }
        }
    }
}