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
	partial class WMA : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_CONTENT_DESCRIPTION = "contentDescription";
        private const string ZONE_EXTENDED_CONTENT_DESCRIPTION = "extContentDescription";
        private const string ZONE_EXTENDED_HEADER_METADATA = "extHeaderMeta";
        private const string ZONE_EXTENDED_HEADER_METADATA_LIBRARY = "extHeaderMetaLibrary";


        // Object IDs
        private static readonly byte[] WMA_HEADER_ID = new byte[16] { 48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static readonly byte[] WMA_HEADER_EXTENSION_ID = new byte[16] { 0xB5, 0x03, 0xBF, 0x5F, 0x2E, 0xA9, 0xCF, 0x11, 0x8E, 0xE3, 0x00, 0xc0, 0x0c, 0x20, 0x53, 0x65 };

        private static readonly byte[] WMA_METADATA_OBJECT_ID = new byte[16] { 0xEA, 0xCB, 0xF8, 0xC5, 0xAF, 0x5B, 0x77, 0x48, 0x84, 0x67, 0xAA, 0x8C, 0x44, 0xFA, 0x4C, 0xCA };
        private static readonly byte[] WMA_METADATA_LIBRARY_OBJECT_ID = new byte[16] { 0x94, 0x1C, 0x23, 0x44, 0x98, 0x94, 0xD1, 0x49, 0xA1, 0x41, 0x1D, 0x13, 0x4E, 0x45, 0x70, 0x54 };

        private static readonly byte[] WMA_FILE_PROPERTIES_ID = new byte[16] { 161, 220, 171, 140, 71, 169, 207, 17, 142, 228, 0, 192, 12, 32, 83, 101 };
        private static readonly byte[] WMA_STREAM_PROPERTIES_ID = new byte[16] { 145, 7, 220, 183, 183, 169, 207, 17, 142, 230, 0, 192, 12, 32, 83, 101 };
        private static readonly byte[] WMA_CONTENT_DESCRIPTION_ID = new byte[16] { 51, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static readonly byte[] WMA_EXTENDED_CONTENT_DESCRIPTION_ID = new byte[16] { 64, 164, 208, 210, 7, 227, 210, 17, 151, 240, 0, 160, 201, 94, 168, 80 };

        private static readonly byte[] WMA_LANGUAGE_LIST_OBJECT_ID = new byte[16] { 0xA9, 0x46, 0x43, 0x7C, 0xE0, 0xEF, 0xFC, 0x4B, 0xB2, 0x29, 0x39, 0x3E, 0xDE, 0x41, 0x5C, 0x85 };


        // Format IDs
#pragma warning disable S1144 // Unused private types or members should be removed
        private const int WMA_ID = 0x161;
        private const int WMA_PRO_ID = 0x162;
        private const int WMA_LOSSLESS_ID = 0x163;
        private const int WMA_GSM_CBR_ID = 0x7A21;
        private const int WMA_GSM_VBR_ID = 0x7A22;

        // Max. number of characters in tag field
        private const byte WMA_MAX_STRING_SIZE = 250;
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

            public void Reset()
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

        private ChannelsArrangement channelsArrangement;
        private int sampleRate;
        private bool isVBR;
        private bool isLossless;
        private double bitrate;
        private double duration;

        // Mapping between WMA frame codes and ATL frame codes
        // NB : WM/TITLE, WM/AUTHOR, WM/COPYRIGHT, WM/DESCRIPTION and WM/RATING are not WMA extended fields; therefore
        // their ID will not appear as is in the WMA header. 
        // Their info is contained in the standard Content Description block at the very beginning of the file
        public static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>()
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
            { "WM/BeatsPerMinute", Field.BPM }
        };
        // Field that are embedded in standard ASF description, and do not need to be written in any other frame
        private static readonly IList<string> embeddedFields = new List<string>
        {
            { "WM/TITLE" },
            { "WM/AUTHOR" },
            { "WM/COPYRIGHT" },
            { "WM/DESCRIPTION" },
            { "WM/RATING" }
        };

        // Mapping between WMA frame codes and frame classes that aren't class 0 (Unicode string)
        private static ConcurrentDictionary<string, ushort> frameClasses = new ConcurrentDictionary<string, ushort>() // To be further populated while reading
        {
            ["WM/SharedUserRating"] = 3
        };


        private IList<string> languages; // Optional language index described in the WMA header

        private AudioDataManager.SizeInfo sizeInfo;
        private readonly string filePath;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate // Sample rate (hz)
        {
            get { return this.sampleRate; }
        }
        public bool IsVBR
        {
            get { return this.isVBR; }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get
            {
                return isLossless ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY;
            }
        }
        public string FileName => filePath;
        public double BitRate => bitrate;
        public int BitDepth => 16; // Seems to be constant
        public double Duration => duration;
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.ID3V1) || (metaDataType == MetaDataIOFactory.TagType.ID3V2) || (metaDataType == MetaDataIOFactory.TagType.APE) || (metaDataType == MetaDataIOFactory.TagType.NATIVE);
        }
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }
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
        protected override byte ratingConvention
        {
            get { return RC_ASF; }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            sampleRate = 0;
            isVBR = false;
            isLossless = false;
            bitrate = 0;
            duration = 0;

            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public WMA(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
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
                long position, initialPosition;
                ulong objectSize;
                byte[] bytes;

                languages = new List<string>();

                initialPosition = source.Position;
                source.Seek(fileData.ObjectListOffset, SeekOrigin.Begin);

                BinaryReader r = new BinaryReader(source);

                for (int i = 0; i < fileData.ObjectCount; i++)
                {
                    position = source.Position;
                    bytes = r.ReadBytes(16);
                    objectSize = r.ReadUInt64();

                    // Language index (optional; one only -- useful to map language codes to extended header tag information)
                    if (WMA_LANGUAGE_LIST_OBJECT_ID.SequenceEqual(bytes))
                    {
                        ushort nbLanguages = r.ReadUInt16();
                        byte strLen;

                        for (int j = 0; j < nbLanguages; j++)
                        {
                            strLen = r.ReadByte();
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

            if (0 == languages.Count)
            {
                return 0;
            }
            else
            {
                return (ushort)languages.IndexOf(languageCode);
            }
        }

        private string decodeLanguage(Stream source, ushort languageIndex)
        {
            if (null == languages) cacheLanguageIndex(source);

            if (languages.Count > 0)
            {
                if (languageIndex < languages.Count)
                {
                    return languages[languageIndex];
                }
                else
                {
                    return languages[0]; // Index out of bounds
                }
            }
            else
            {
                return "";
            }
        }

        private void readContentDescription(BufferedBinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            ushort[] fieldSize = new ushort[5];
            string fieldValue;

            // Read standard field sizes
            for (int i = 0; i < 5; i++) fieldSize[i] = source.ReadUInt16();

            // Read standard field values
            for (int i = 0; i < 5; i++)
            {
                if (fieldSize[i] > 0)
                {
                    // Read field value
                    fieldValue = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

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

        private void readHeaderExtended(BufferedBinaryReader source, long sizePosition1, ulong size1, long sizePosition2, ulong size2, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] headerExtensionObjectId;
            ulong headerExtensionObjectSize = 0;
            long position, framePosition, sizePosition3, dataPosition;
            ulong limit;
            ushort streamNumber, languageIndex;

            source.Seek(16, SeekOrigin.Current); // Reserved field 1
            source.Seek(2, SeekOrigin.Current); // Reserved field 2

            sizePosition3 = source.Position;
            uint headerExtendedSize = source.ReadUInt32(); // Size of actual data

            // Looping through header extension objects
            position = source.Position;
            limit = (ulong)position + headerExtendedSize;
            while ((ulong)position < limit)
            {
                framePosition = source.Position;
                headerExtensionObjectId = source.ReadBytes(16);
                headerExtensionObjectSize = source.ReadUInt64();

                // Additional metadata (Optional frames)
                if (WMA_METADATA_OBJECT_ID.SequenceEqual(headerExtensionObjectId) || WMA_METADATA_LIBRARY_OBJECT_ID.SequenceEqual(headerExtensionObjectId))
                {
                    ushort nameSize;            // Length (in bytes) of Name field
                    ushort fieldDataType;       // Type of data stored in current field
                    int fieldDataSize;          // Size of data stored in current field
                    string fieldName;           // Name of current field
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
                        languageIndex = source.ReadUInt16();
                        streamNumber = source.ReadUInt16();
                        nameSize = source.ReadUInt16();
                        fieldDataType = source.ReadUInt16();
                        fieldDataSize = source.ReadInt32();
                        fieldName = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(nameSize)));

                        dataPosition = source.Position;
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
            long dataPosition;
            ushort fieldCount;
            ushort dataSize;
            ushort dataType;
            string fieldName;

            // Read extended tag data
            fieldCount = source.ReadUInt16();
            for (int iterator1 = 0; iterator1 < fieldCount; iterator1++)
            {
                // Read field name
                dataSize = source.ReadUInt16();
                fieldName = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(dataSize)));
                // Read value data type
                dataType = source.ReadUInt16();
                dataSize = source.ReadUInt16();

                dataPosition = source.Position;
                readTagField(source, ZONE_EXTENDED_CONTENT_DESCRIPTION, fieldName, dataType, dataSize, readTagParams);

                source.Seek(dataPosition + dataSize, SeekOrigin.Begin);
            }
        }

        public void readTagField(BufferedBinaryReader source, string zoneCode, string fieldName, ushort fieldDataType, int fieldDataSize, ReadTagParams readTagParams, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            string fieldValue = "";
            bool setMeta = true;

            addFrameClass(fieldName, fieldDataType);

            if (0 == fieldDataType) // Unicode string
            {
                fieldValue = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(fieldDataSize)));
            }
            else if (1 == fieldDataType) // Byte array
            {
                if (fieldName.ToUpper().Equals("WM/PICTURE"))
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
                        string mimeType = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);
                        string description = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

                        PictureInfo picInfo = PictureInfo.fromBinaryData(source, picSize, picType, getImplementedTagType(), picCode, picturePosition);
                        picInfo.Description = description;

                        tagData.Pictures.Add(picInfo);
                    }
                    setMeta = false;
                }
                else
                {
                    fieldValue = Utils.Latin1Encoding.GetString(Utils.EncodeTo64(source.ReadBytes(fieldDataSize)));
                }
            }
            else if (2 == fieldDataType) // 16-bit Boolean (metadata); 32-bit Boolean (extended header)
            {
                if (isExtendedHeader) fieldValue = source.ReadUInt32().ToString();
                else fieldValue = source.ReadUInt16().ToString();
            }
            else if (3 == fieldDataType) // 32-bit unsigned integer
            {
                uint intValue = source.ReadUInt32();
                if (fieldName.Equals("WM/GENRE", StringComparison.OrdinalIgnoreCase)) intValue++;
                fieldValue = intValue.ToString();
            }
            else if (4 == fieldDataType) // 64-bit unsigned integer
            {
                fieldValue = source.ReadUInt64().ToString();
            }
            else if (5 == fieldDataType) // 16-bit unsigned integer
            {
                fieldValue = source.ReadUInt16().ToString();
            }
            else if (6 == fieldDataType) // 128-bit GUID; unused for now
            {
                fieldValue = Utils.Latin1Encoding.GetString(Utils.EncodeTo64(source.ReadBytes(fieldDataSize)));
            }

            if (setMeta) SetMetaField(fieldName.Trim(), fieldValue, readTagParams.ReadAllMetaFrames, zoneCode, 0, streamNumber, decodeLanguage(source, languageIndex));
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, WMA_HEADER_ID);
        }

        private bool readData(Stream source, ReadTagParams readTagParams)
        {
            byte[] ID;
            uint objectCount;
            ulong headerSize, objectSize;
            long initialPos, position;
            long countPosition, sizePosition1, sizePosition2;
            bool result = false;

            if (languages != null) languages.Clear();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            reader.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            initialPos = reader.Position;

            // Check for existing header
            ID = reader.ReadBytes(16);

            // Header (mandatory; one only)
            if (IsValidHeader(ID))
            {
                sizePosition1 = reader.Position;
                headerSize = reader.ReadUInt64();
                countPosition = reader.Position;
                objectCount = reader.ReadUInt32();
                reader.Seek(2, SeekOrigin.Current); // Reserved data
                fileData.ObjectCount = objectCount;
                fileData.ObjectListOffset = reader.Position;

                // Read all objects in header and get needed data
                for (int i = 0; i < objectCount; i++)
                {
                    position = reader.Position;
                    ID = reader.ReadBytes(16);
                    sizePosition2 = reader.Position;
                    objectSize = reader.ReadUInt64();

                    // File properties (mandatory; one only)
                    if (WMA_FILE_PROPERTIES_ID.SequenceEqual(ID))
                    {
                        reader.Seek(40, SeekOrigin.Current);
                        duration = reader.ReadUInt64() / 10000.0;       // Play duration (100-nanoseconds)
                        reader.Seek(8, SeekOrigin.Current);  // Send duration; unused for now
                        duration -= reader.ReadUInt64();                // Preroll duration (ms)
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
                        tagExists = true;
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
                        tagExists = true;
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
            AudioDataSize = sizeInfo.FileSize - AudioDataOffset;

            return result;
        }

        private bool isValid(FileData Data)
        {
            return (Data.Channels > 0) && (Data.SampleRate >= 8000) && (Data.SampleRate <= 96000);
        }

        public bool Read(Stream source, AudioDataManager.SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            fileData = new FileData();

            resetData();
            bool result = readData(source, readTagParams);

            // Process data if loaded and valid
            if (result && isValid(fileData))
            {
                channelsArrangement = GuessFromChannelNumber(fileData.Channels);
                sampleRate = fileData.SampleRate;
                bitrate = (sizeInfo.FileSize - sizeInfo.TotalTagSize - fileData.HeaderSize) * 8.0 / duration;
                isVBR = (WMA_GSM_VBR_ID == fileData.FormatTag);
                isLossless = (WMA_LOSSLESS_ID == fileData.FormatTag);
            }

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(tag, w, zone);
        }

        private int write(TagData tag, BinaryWriter w, string zone)
        {
            computePicturesDestination(tag.Pictures);

            if (ZONE_CONTENT_DESCRIPTION.Equals(zone)) return writeContentDescription(tag, w);
            else if (ZONE_EXTENDED_HEADER_METADATA.Equals(zone)) return writeExtendedHeaderMeta(tag, w);
            else if (ZONE_EXTENDED_HEADER_METADATA_LIBRARY.Equals(zone)) return writeExtendedHeaderMetaLibrary(tag, w);
            else if (ZONE_EXTENDED_CONTENT_DESCRIPTION.Equals(zone)) return writeExtendedContentDescription(tag, w);
            else return 0;
        }

        private int writeContentDescription(TagData tag, BinaryWriter w)
        {
            long beginPos, frameSizePos, finalFramePos;

            beginPos = w.BaseStream.Position;
            w.Write(WMA_CONTENT_DESCRIPTION_ID);
            frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method

            string title = "";
            string author = "";
            string copyright = "";
            string comment = "";
            string rating = "";

            IDictionary<Field, string> map = tag.ToMap();

            // Supported textual fields
            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length > 0) // No frame with empty value
                {
                    if (TagData.Field.TITLE.Equals(frameType)) title = map[frameType];
                    else if (TagData.Field.ARTIST.Equals(frameType)) author = map[frameType];
                    else if (TagData.Field.COPYRIGHT.Equals(frameType)) copyright = map[frameType];
                    else if (TagData.Field.COMMENT.Equals(frameType)) comment = map[frameType];
                    else if (TagData.Field.RATING.Equals(frameType)) rating = map[frameType];
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
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return ((title.Length > 0) ? 1 : 0) + ((author.Length > 0) ? 1 : 0) + ((copyright.Length > 0) ? 1 : 0) + ((comment.Length > 0) ? 1 : 0) + ((rating.Length > 0) ? 1 : 0);
        }

        private int writeExtendedContentDescription(TagData tag, BinaryWriter w)
        {
            long beginPos, frameSizePos, counterPos, finalFramePos;
            ushort counter = 0;

            beginPos = w.BaseStream.Position;
            w.Write(WMA_EXTENDED_CONTENT_DESCRIPTION_ID);
            frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            IDictionary<Field, String> map = tag.ToMap();

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
                            counter++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TagType.ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion && (ZONE_EXTENDED_CONTENT_DESCRIPTION.Equals(fieldInfo.Zone) || "".Equals(fieldInfo.Zone)))
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
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return counter;
        }

        private int writeExtendedHeaderMeta(TagData tag, BinaryWriter w)
        {
            long beginPos, frameSizePos, counterPos, finalFramePos;
            ushort counter;

            beginPos = w.BaseStream.Position;
            w.Write(WMA_METADATA_OBJECT_ID);
            frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            counter = writeExtendedMeta(tag, w);

            // Go back to frame size locations to write their actual size 
            finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(Convert.ToUInt64(finalFramePos - beginPos));
            w.BaseStream.Seek(counterPos, SeekOrigin.Begin);
            w.Write(counter);
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

            return counter;
        }

        private int writeExtendedHeaderMetaLibrary(TagData tag, BinaryWriter w)
        {
            long beginPos, frameSizePos, counterPos, finalFramePos;
            ushort counter;

            beginPos = w.BaseStream.Position;
            w.Write(WMA_METADATA_LIBRARY_OBJECT_ID);
            frameSizePos = w.BaseStream.Position;
            w.Write((ulong)0); // Frame size placeholder to be rewritten at the end of the method
            counterPos = w.BaseStream.Position;
            w.Write((ushort)0); // Counter placeholder to be rewritten at the end of the method

            counter = writeExtendedMeta(tag, w, true);

            // Go back to frame size locations to write their actual size 
            finalFramePos = w.BaseStream.Position;
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

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            long dataSizePos, dataPos, finalFramePos;
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
            if (frameClasses.ContainsKey(frameCode)) frameClass = frameClasses[frameCode];
            writer.Write(frameClass);

            dataSizePos = writer.BaseStream.Position;
            // Data size placeholder to be rewritten in a few lines
            if (isExtendedHeader)
            {
                writer.Write((uint)0);
            }
            else
            {
                writer.Write((ushort)0);
            }

            if (isExtendedHeader) writer.Write(nameBytes);

            dataPos = writer.BaseStream.Position;
            if (0 == frameClass) // Unicode string
            {
                writer.Write(Encoding.Unicode.GetBytes(text + '\0'));
            }
            else if (1 == frameClass) // Non-picture byte array
            {
                writer.Write(Utils.DecodeFrom64(Utils.Latin1Encoding.GetBytes(text)));
            }
            else if (2 == frameClass) // 32-bit boolean; 16-bit boolean if in extended header
            {
                if (isExtendedHeader) writer.Write(Utils.ToBoolean(text) ? (ushort)1 : (ushort)0);
                else writer.Write(Utils.ToBoolean(text) ? (uint)1 : (uint)0);
            }
            else if (3 == frameClass) // 32-bit unsigned integer
            {
                writer.Write(Convert.ToUInt32(text));
            }
            else if (4 == frameClass) // 64-bit unsigned integer
            {
                writer.Write(Convert.ToUInt64(text));
            }
            else if (5 == frameClass) // 16-bit unsigned integer
            {
                writer.Write(Convert.ToUInt16(text));
            }
            else if (6 == frameClass) // 128-bit GUID
            {
                writer.Write(Utils.DecodeFrom64(Utils.Latin1Encoding.GetBytes(text)));
            }

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
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

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, string mimeType, byte pictureTypeCode, string description, bool isExtendedHeader = false, ushort languageIndex = 0, ushort streamNumber = 0)
        {
            long dataSizePos, dataPos, finalFramePos;
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

            dataSizePos = writer.BaseStream.Position;
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
            dataPos = writer.BaseStream.Position;

            writer.Write(pictureTypeCode);
            writer.Write(pictureData.Length);

            writer.Write(Encoding.Unicode.GetBytes(mimeType + '\0'));
            writer.Write(Encoding.Unicode.GetBytes(description + '\0'));     // Picture description

            writer.Write(pictureData);

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
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
        public override async Task<bool> RemoveAsync(Stream s)
        {
            if (Settings.ASF_keepNonWMFieldsWhenRemovingTag)
            {
                TagData tag = prepareRemove();
                return await WriteAsync(s, tag);
            }
            else
            {
                return await base.RemoveAsync(s);
            }
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
                MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                emptyFieldInfo.MarkedForDeletion = true;
                result.AdditionalFields.Add(emptyFieldInfo);
            }
            return result;
        }

        // Decides whether picture has to be written and set it to their TransientFlag field
        // -1: Nowhere
        //  0: In content description
        //  1: In extended metadata
        private void computePicturesDestination(IList<PictureInfo> picInfos)
        {
            bool foundFirstContentDescPicture = false;
            foreach (PictureInfo picInfo in picInfos)
            {
                // Picture has either to be supported, or to come from the right tag standard
                bool doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

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