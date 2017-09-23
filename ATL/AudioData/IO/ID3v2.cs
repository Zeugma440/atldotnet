using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Commons;
using System.Drawing;
using ATL.Logging;
using System.Drawing.Imaging;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for ID3v2.2-2.4 tags manipulation
    /// 
    /// Implementation notes
    /// 
    ///     1. Extended header tags
    /// 
    ///     Due to the rarity of ID3v2 tags with extended headers (on my disk and on the web), 
    ///     implementation of decoding extended header data is still theoretical
    ///     
    ///     2. Comment description
    ///     
    ///     Currently, only the last COMM field is read (and rewritten).
    ///     However, there can be as many COMM fields as long as their language ID and description are different
    ///     Some taggers even seem to use COMM + description the same way as the XXX field (e.g. : COMM_eng_Catalog Number_CTL0992)
    ///     
    ///     ATL does not support multiple COMM fields yet
    /// 
    /// </summary>
    public class ID3v2 : MetaDataIO
    {
        public const byte TAG_VERSION_2_2 = 2;             // Code for ID3v2.2.x tag
        public const byte TAG_VERSION_2_3 = 3;             // Code for ID3v2.3.x tag
        public const byte TAG_VERSION_2_4 = 4;             // Code for ID3v2.4.x tag

        private String FEncoder;
        private String FLanguage;
        private String FLink;

        private TagInfo tagHeader;

        public String Encoder // Encoder
        {
            get { return this.FEncoder; }
            set { FEncoder = value; }
        }
        public String Language // Language
        {
            get { return this.FLanguage; }
            set { FLanguage = value; }
        }
        public String Link // URL link
        {
            get { return this.FLink; }
            set { FLink = value; }
        }

        // ID3v2 tag ID
        private const String ID3V2_ID = "ID3";

        // List of standard fields
        private static ICollection<string> standardFrames_v22;
        private static ICollection<string> standardFrames_v23;
        private static ICollection<string> standardFrames_v24;

        // Mapping between ID3v2 field IDs and ATL fields
        private static IDictionary<string, byte> frameMapping_v22;
        private static IDictionary<string, byte> frameMapping_v23_24;

        // Max. tag size for saving
        private const int ID3V2_MAX_SIZE = 4096;

        // Frame header (universal)
        private class FrameHeader
        {
            public string ID;                           // Frame ID
            public int Size;                            // Size excluding header
            public ushort Flags;				        // Flags
        }

        // ID3v2 header data - for internal use
        private class TagInfo
        {
            // Real structure of ID3v2 header
            public char[] ID = new char[3];                            // Always "ID3"
            public byte Version;                                     // Version number
            public byte Revision;                                   // Revision number
            public byte Flags;                                         // Flags of tag
            public byte[] Size = new byte[4];             // Tag size excluding header
            // Extended data
            public long FileSize;                                 // File size (bytes)
            public long HeaderEnd;                           // End position of header
            public long ActualEnd;     // End position of entire tag (including all padding bytes)

            // Extended header flags
            public int ExtendedHeaderSize = 0;
            public int ExtendedFlags;
            public int CRC = -1;
            public int TagRestrictions = -1;


            // **** BASE HEADER PROPERTIES ****
            public bool UsesUnsynchronisation
            {
                get { return ((Flags & 128) > 0); }
            }
            public bool HasExtendedHeader // Determinated from flags; indicates if tag has an extended header (ID3v2.3+)
            {
                get { return (((Flags & 64) > 0) && (Version > TAG_VERSION_2_2)); }
            }
            public bool IsExperimental // Determinated from flags; indicates if tag is experimental (ID3v2.4+)
            {
                get { return ((Flags & 32) > 0); }
            }
            public bool HasFooter // Determinated from flags; indicates if tag has a footer (ID3v2.4+)
            {
                get { return ((Flags & 0x10) > 0); }
            }

            // **** EXTENDED HEADER PROPERTIES ****
            public int TagFramesRestriction
            {
                get
                {
                    switch ((TagRestrictions & 0xC0) >> 6)
                    {
                        case 0: return 128;
                        case 1: return 64;
                        case 2: return 32;
                        case 3: return 32;
                        default: return -1;
                    }
                }
            }
            public int TagSizeRestrictionKB
            {
                get {
                    switch ((TagRestrictions & 0xC0) >> 6)
                    {
                        case 0: return 1024;
                        case 1: return 128;
                        case 2: return 40;
                        case 3: return 4;
                        default: return -1;
                    }
                }
            }
            public bool HasTextEncodingRestriction
            {
                get { return (((TagRestrictions & 0x20) >> 5) > 0); }
            }
            public int TextFieldSizeRestriction
            {
                get
                {
                    switch ((TagRestrictions & 0x18) >> 3)
                    {
                        case 0: return -1;
                        case 1: return 1024;
                        case 2: return 128;
                        case 3: return 30;
                        default: return -1;
                    }
                }
            }
            public bool HasPictureEncodingRestriction
            {
                get { return (((TagRestrictions & 0x04) >> 2) > 0); }
            }
            public int PictureSizeRestriction
            {
                get
                {
                    switch ((TagRestrictions & 0x03))
                    {
                        case 0: return -1;  // No restriction
                        case 1: return 256; // 256x256 or less
                        case 2: return 63;  // 64x64 or less
                        case 3: return 64;  // Exactly 64x64
                        default: return -1;
                    }
                }
            }
        }

        // Unicode BOM properties
        private class BOMProperties
        {
            public int Size = 0;                // Size of BOM
            public Encoding Encoding;           // Corresponding encoding
        }

        // ********************* Auxiliary functions & voids ********************

        static ID3v2()
        {
            standardFrames_v22 = new List<string>() { "BUF", "CNT", "COM", "CRA", "CRM", "ETC", "EQU", "GEO", "IPL", "LNK", "MCI", "MLL", "PIC", "POP", "REV", "RVA", "SLT", "STC", "TAL", "TBP", "TCM", "TCO", "TCR", "TDA", "TDY", "TEN", "TFT", "TIM", "TKE", "TLA", "TLE", "TMT", "TOA", "TOF", "TOL", "TOR", "TOT", "TP1", "TP2", "TP3", "TP4", "TPA", "TPB", "TRC", "TRD", "TRK", "TSI", "TSS", "TT1", "TT2", "TT3", "TXT", "TXX", "TYE","UFI","ULT","WAF","WAR","WAS","WCM","WCP","WPB","WXX" };
            standardFrames_v23 = new List<string>() { "AENC","APIC","COMM","COMR","ENCR","EQUA","ETCO","GEOB","GRID","IPLS","LINK","MCDI","MLLT","OWNE","PRIV","PCNT","POPM","POSS","RBUF","RVAD","RVRB","SYLT","SYTC","TALB","TBPM","TCOM","TCON","TCOP","TDAT","TDLY","TENC","TEXT","TFLT","TIME","TIT1", "TIT2", "TIT3","TKEY","TLAN","TLEN","TMED","TOAL","TOFN","TOLY","TOPE","TORY","TOWN","TPE1", "TPE2", "TPE3", "TPE4","TPOS","TPUB","TRCK","TRDA","TRSN","TRSO","TSIZ","TSRC","TSSE","TYER","TXXX","UFID","USER","USLT","WCOM","WCOP","WOAF","WOAR","WOAS","WORS","WPAY","WPUB","WXXX" };
            standardFrames_v24 = new List<string>() { "AENC", "APIC", "ASPI","COMM", "COMR", "ENCR", "EQU2", "ETCO", "GEOB", "GRID", "LINK", "MCDI", "MLLT", "OWNE", "PRIV", "PCNT", "POPM", "POSS", "RBUF", "RVA2", "RVRB", "SEEK","SIGN","SYLT", "SYTC", "TALB", "TBPM", "TCOM", "TCON", "TCOP", "TDEN", "TDLY", "TDOR","TDRC","TDRL","TDTG", "TENC", "TEXT", "TFLT", "TIPL", "TIT1", "TIT2", "TIT3", "TKEY", "TLAN", "TLEN", "TMCL","TMED", "TMOO","TOAL", "TOFN", "TOLY", "TOPE", "TORY", "TOWN", "TPE1", "TPE2", "TPE3", "TPE4", "TPOS", "TPRO", "TPUB", "TRCK", "TRSN", "TRSO", "TSOA","TSOP","TSOT", "TSRC", "TSSE", "TSST","TXXX", "UFID", "USER", "USLT", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB", "WXXX" };

            // Note on date field identifiers
            //
            // Original release date
            //   ID3v2.0 : TOR (year only)
            //   ID3v2.3 : TORY (year only)
            //   ID3v2.4 : TDOR (timestamp according to spec; actual content may vary)
            //
            // Release date
            //   ID3v2.0 : no standard
            //   ID3v2.3 : no standard
            //   ID3v2.4 : TDRL (timestamp according to spec; actual content may vary)
            //
            // Recording date <== de facto standard behind the "date" field on most taggers
            //   ID3v2.0 : TYE (year), TDA (day & month - DDMM)
            //   ID3v2.3 : TYER (year), TDAT (day & month - DDMM)
            //   ID3v2.4 : TDRC (timestamp according to spec; actual content may vary)

            // Mapping between standard ATL fields and ID3v2.2 identifiers
            frameMapping_v22 = new Dictionary<string, byte>
            {
                { "TT1", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
                { "TT2", TagData.TAG_FIELD_TITLE },
                { "TP1", TagData.TAG_FIELD_ARTIST },
                { "TP2", TagData.TAG_FIELD_ALBUM_ARTIST },  // De facto standard, regardless of spec
                { "TP3", TagData.TAG_FIELD_CONDUCTOR },
                { "TOA", TagData.TAG_FIELD_ORIGINAL_ARTIST },
                { "TAL", TagData.TAG_FIELD_ALBUM },
                { "TOT", TagData.TAG_FIELD_ORIGINAL_ALBUM },
                { "TRK", TagData.TAG_FIELD_TRACK_NUMBER },
                { "TPA", TagData.TAG_FIELD_DISC_NUMBER },
                { "TYE", TagData.TAG_FIELD_RECORDING_YEAR },
                { "TDA", TagData.TAG_FIELD_RECORDING_DAYMONTH },
                { "COM", TagData.TAG_FIELD_COMMENT },
                { "TCM", TagData.TAG_FIELD_COMPOSER },
                { "POP", TagData.TAG_FIELD_RATING },
                { "TCO", TagData.TAG_FIELD_GENRE },
                { "TCR", TagData.TAG_FIELD_COPYRIGHT },
                { "TPB", TagData.TAG_FIELD_PUBLISHER }
            };

            // Mapping between standard fields and ID3v2.3+ identifiers
            frameMapping_v23_24 = new Dictionary<string, byte>
            {
                { "TIT1", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
                { "TIT2", TagData.TAG_FIELD_TITLE },
                { "TPE1", TagData.TAG_FIELD_ARTIST },
                { "TPE2", TagData.TAG_FIELD_ALBUM_ARTIST }, // De facto standard, regardless of spec
                { "TPE3", TagData.TAG_FIELD_CONDUCTOR },
                { "TOPE", TagData.TAG_FIELD_ORIGINAL_ARTIST },
                { "TALB", TagData.TAG_FIELD_ALBUM },
                { "TOAL", TagData.TAG_FIELD_ORIGINAL_ALBUM },
                { "TRCK", TagData.TAG_FIELD_TRACK_NUMBER },
                { "TPOS", TagData.TAG_FIELD_DISC_NUMBER },
                { "TDRC", TagData.TAG_FIELD_RECORDING_DATE },
                { "TYER", TagData.TAG_FIELD_RECORDING_YEAR },
                { "TDAT", TagData.TAG_FIELD_RECORDING_DAYMONTH },
                { "COMM", TagData.TAG_FIELD_COMMENT },
                { "TCOM", TagData.TAG_FIELD_COMPOSER },
                { "POPM", TagData.TAG_FIELD_RATING },
                { "TCON", TagData.TAG_FIELD_GENRE },
                { "TCOP", TagData.TAG_FIELD_COPYRIGHT },
                { "TPUB", TagData.TAG_FIELD_PUBLISHER }
            };
        }

        public ID3v2()
        {
            ResetData();
        }

        private bool readHeader(BufferedBinaryReader SourceFile, TagInfo Tag, long offset)
        {
            bool result = true;

            // Reads mandatory (base) header
            SourceFile.Seek(offset, SeekOrigin.Begin);
            Tag.ID = Utils.Latin1Encoding.GetChars(SourceFile.ReadBytes(3));

            if (!StreamUtils.StringEqualsArr(ID3V2_ID, tagHeader.ID)) return false;

            Tag.Version = SourceFile.ReadByte();
            Tag.Revision = SourceFile.ReadByte();
            Tag.Flags = SourceFile.ReadByte();
            
            // ID3v2 tag size
            Tag.Size = SourceFile.ReadBytes(4);

            // Reads optional (extended) header
            if (Tag.HasExtendedHeader)
            {
                Tag.ExtendedHeaderSize = StreamUtils.DecodeSynchSafeInt(SourceFile.ReadBytes(4)); // Extended header size
                SourceFile.Seek(1, SeekOrigin.Current); // Number of flag bytes; always 1 according to spec

                Tag.ExtendedFlags = SourceFile.ReadByte();

                if ((Tag.ExtendedFlags & 64) > 0) // Tag is an update
                {
                    // This flag is informative and has no corresponding data
                }
                if ( (Tag.ExtendedFlags & 32) > 0) // CRC present
                {
                    Tag.CRC = StreamUtils.DecodeSynchSafeInt(SourceFile.ReadBytes(5));
                }
                if ((Tag.ExtendedFlags & 16) > 0) // Tag has at least one restriction
                {
                    Tag.TagRestrictions = SourceFile.ReadByte();
                }
            }
            Tag.HeaderEnd = SourceFile.Position;

            // File size
            Tag.FileSize = SourceFile.Length;

            return result;
        }

        // ---------------------------------------------------------------------------

        private int getTagSize(TagInfo Tag, bool includeFooter = true)
        {
            // Get total tag size
            int result = StreamUtils.DecodeSynchSafeInt32(Tag.Size) + 10; // 10 being the size of the header

            if (includeFooter && Tag.HasFooter) result += 10; // Indicates the presence of a footer (ID3v2.4+)

            if (result > Tag.FileSize) result = 0;

            return result;
        }

        // ---------------------------------------------------------------------------

        private void setMetaField(String ID, String Data, TagInfo Tag, bool readAllMetaFrames)
        {
            byte supportedMetaId = 255;
            ID = ID.ToUpper();

            // Finds the ATL field identifier according to the ID3v2 version
            if (Tag.Version > TAG_VERSION_2_2)
            {
                if (frameMapping_v23_24.ContainsKey(ID)) supportedMetaId = frameMapping_v23_24[ID];
            } else
            {
                if (frameMapping_v22.ContainsKey(ID)) supportedMetaId = frameMapping_v22[ID];
            }

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                if (TagData.TAG_FIELD_GENRE == supportedMetaId)
                {
                    tagData.IntegrateValue(supportedMetaId, extractGenreFromID3v2Code(Data));
                }
                else
                {
                    tagData.IntegrateValue(supportedMetaId, Data);
                }
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                fieldInfo = new TagData.MetaFieldInfo(getImplementedTagType(), ID, Data);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Replace current value, since there can be no duplicate fields in ID3v2
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        // Get information from frames (universal)
        private void readFrames(BufferedBinaryReader source, TagInfo tag, long offset, ReadTagParams readTagParams)
        {
            const int PADDING_BUFFER_SIZE = 512;
            FrameHeader Frame = new FrameHeader();
            byte encodingCode;
            long dataSize;
            long dataPosition;
            string strData;
            Encoding frameEncoding;
            long streamPos;
            long initialTagPos = source.Position;
            long streamLength = source.Length;
            int tagSize = getTagSize(tag, false);
            tag.ActualEnd = -1;

            source.Seek(tag.HeaderEnd, SeekOrigin.Begin);
            streamPos = source.Position;

            while ((streamPos - offset < tagSize) && (streamPos < streamLength))
            {
                // Read frame header and check frame ID
                Frame.ID = (TAG_VERSION_2_2 == tagVersion) ? Utils.Latin1Encoding.GetString(source.ReadBytes(3)) : Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if (!char.IsLetter(Frame.ID[0]) || !char.IsUpper(Frame.ID[0]))
                {
                    // We might be at the beginning of a padding frame
                    if (0 == Frame.ID[0] + Frame.ID[1] + Frame.ID[2])
                    {
                        // Read until there's something else than zeroes
                        byte[] data = new byte[PADDING_BUFFER_SIZE];
                        bool endReached = false;
                        long initialPos = source.Position;
                        int read = 0;

                        while (!endReached)
                        {
                            source.Read(data, 0, PADDING_BUFFER_SIZE);
                            for (int i=0;i< PADDING_BUFFER_SIZE; i++)
                            {
                                if (data[i] > 0)
                                {
                                    tag.ActualEnd = initialPos + read + i;
                                    endReached = true;
                                    return;
                                }
                            }
                            if (!endReached) read += PADDING_BUFFER_SIZE;
                        }
                    }
                    else // If not, we're in the wrong place
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Valid frame not found where expected; parsing interrupted");
                        source.Seek(initialTagPos - tag.HeaderEnd + tagSize, SeekOrigin.Begin);
                        streamPos = source.Position;
                        break;
                    }
                }

                // Frame size measures number of bytes between end of flag and end of payload
                /* Frame size encoding conventions
                    ID3v2.2 : 3 byte
                    ID3v2.3 : 4 byte
                    ID3v2.4 : synch-safe Int32
                */
                if (TAG_VERSION_2_2 == tagVersion) Frame.Size = StreamUtils.DecodeBEInt24(source.ReadBytes(3));
                else if (TAG_VERSION_2_3 == tagVersion) Frame.Size = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                else if (TAG_VERSION_2_4 == tagVersion) Frame.Size = StreamUtils.DecodeSynchSafeInt32(source.ReadBytes(4));

                if (TAG_VERSION_2_2 == tagVersion) Frame.Flags = 0;
                else Frame.Flags = StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

                dataSize = Frame.Size;

                // Skips data size indicator if signaled by the flag
                if ((Frame.Flags & 1) > 0)
                {
                    source.Seek(4, SeekOrigin.Current);
                    dataSize = dataSize - 4;
                }

                dataSize = dataSize - 1; // Minus encoding byte
                encodingCode = source.ReadByte();
                frameEncoding = decodeID3v2CharEncoding(encodingCode);

                // COMM fields contain :
                //   a 3-byte langage ID
                //   a "short content description", as an encoded null-terminated string
                //   the actual comment, as an encoded, null-terminated string
                // => lg lg lg (BOM) (encoded description) 00 (00) (BOM) encoded text 00 (00)
                if ("COM".Equals(Frame.ID.Substring(0, 3)))
                {
                    long initialPos = source.Position;

                    // Skip langage ID
                    source.Seek(3, SeekOrigin.Current);

                    BOMProperties contentDescriptionBOM = new BOMProperties();
                    // Skip BOM if ID3v2.3+ and UTF-16 with BOM present
                    if ( tagVersion > TAG_VERSION_2_2 && (1 == encodingCode) )
                    {
                        contentDescriptionBOM = readBOM(source);
                    }

                    if (contentDescriptionBOM.Size <= 3)
                    {
                        // Skip content description
                        StreamUtils.ReadNullTerminatedString(source, frameEncoding);
                    }
                    else
                    {
                        // If content description BOM > 3 bytes, there might not be any BOM
                        // for content description, and the algorithm might have bumped into
                        // the comment BOM => backtrack just after langage tag
                        source.Seek(initialPos + 3, SeekOrigin.Begin);
                    }

                    dataSize = dataSize - (source.Position - initialPos);
                }

                // A $01 "Unicode" encoding flag means the presence of a BOM (Byte Order Mark) if version > 2.2
                // http://en.wikipedia.org/wiki/Byte_order_mark
                //    3-byte BOM : FF 00 FE
                //    2-byte BOM : FE FF (UTF-16 Big Endian)
                //    2-byte BOM : FF FE (UTF-16 Little Endian)
                //    Other variants...
                if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode))
                {
                    long initialPos = source.Position;
                    BOMProperties bom = readBOM(source);

                    // A BOM has been read, but it lies outside the current frame
                    // => Backtrack and directly read data without BOM
                    if (bom.Size > dataSize)
                    {
                        source.Seek(initialPos, SeekOrigin.Begin);
                    }
                    else
                    {
                        frameEncoding = bom.Encoding;
                        dataSize = dataSize - bom.Size;
                    }
                }
                
                // If encoding > 3, we might have caught an actual character, which means there is no encoding flag => switch to default encoding
                if (encodingCode > 3)
                {
                    frameEncoding = decodeID3v2CharEncoding(0);
                    source.Seek(-1, SeekOrigin.Current);
                    dataSize++;
                }


                // == READ ACTUAL FRAME DATA

                dataPosition = source.Position;
                if ((dataSize > 0) && (dataSize < 500))
                {
                    // Specific to Popularitymeter : Rating data has to be extracted from the POPM block
                    if ("POP".Equals(Frame.ID.Substring(0,3)))
                    {
                        /*
                         * ID3v2.0 : According to spec (see §3.2), encoding should actually be ISO-8859-1
                         * ID3v2.3+ : Spec is unclear wether to read as ISO-8859-1 or not. Practice indicates using this convention is safer.
                         */
                        strData = readRatingInPopularityMeter(source, Utils.Latin1Encoding).ToString();
                    }
                    else if ("TXX".Equals(Frame.ID.Substring(0,3)))
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes((int)dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));

                        string[] tabS = strData.Split('\0');
                        Frame.ID = tabS[0];
                        strData = tabS[1];
                    }
                    else
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes((int)dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));
                    }

                    setMetaField(Frame.ID, strData, tag, readTagParams.ReadAllMetaFrames);

                    if (TAG_VERSION_2_2 == tagVersion) source.Seek(dataPosition + dataSize, SeekOrigin.Begin);
                }
                else if (dataSize > 0) // Size > 500 => Probably an embedded picture
                {
                    long position = source.Position;
                    if ("PIC".Equals(Frame.ID) || "APIC".Equals(Frame.ID))
                    {
                        ImageFormat imgFormat;
                        if (TAG_VERSION_2_2 == tagVersion)
                        {
                            // Image format
                            string imageFormat = Utils.Latin1Encoding.GetString(source.ReadBytes(3)).ToUpper();

                            if ("BMP".Equals(imageFormat)) imgFormat = ImageFormat.Bmp;
                            else if ("PNG".Equals(imageFormat)) imgFormat = ImageFormat.Png;
                            else if ("GIF".Equals(imageFormat)) imgFormat = ImageFormat.Gif;
                            else imgFormat = ImageFormat.Jpeg;
                        }
                        else
                        {
                            // mime-type always coded in ASCII
                            if (1 == encodingCode) source.Seek(-1, SeekOrigin.Current);
                            // Mime-type
                            String mimeType = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);
                            imgFormat = Utils.GetImageFormatFromMimeType(mimeType);
                        }

                        byte picCode = source.ReadByte();
                        // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                        TagData.PIC_TYPE picType = DecodeID3v2PictureType(picCode);

                        int picturePosition;
                        if (picType.Equals(TagData.PIC_TYPE.Unsupported))
                        {
                            addPictureToken(MetaDataIOFactory.TAG_ID3V2,picCode);
                            picturePosition = takePicturePosition(MetaDataIOFactory.TAG_ID3V2, picCode);
                        }
                        else
                        {
                            addPictureToken(picType);
                            picturePosition = takePicturePosition(picType);
                        }

                        // Image description (unused)
                        // Description can be coded with another convention
                        if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode)) readBOM(source);
                        StreamUtils.ReadNullTerminatedString(source, frameEncoding);

                        if (readTagParams.PictureStreamHandler != null)
                        {
                            int picSize = (int)(dataSize - (source.Position - position));
                            MemoryStream mem = new MemoryStream(picSize);

                            if (tag.UsesUnsynchronisation)
                            {
                                decodeUnsynchronizedStreamTo(source, mem, picSize);
                            }
                            else
                            {
                                StreamUtils.CopyStream(source, mem, picSize);
                            }

                            mem.Seek(0, SeekOrigin.Begin);

                            readTagParams.PictureStreamHandler(ref mem, picType, imgFormat, MetaDataIOFactory.TAG_ID3V2, picCode, picturePosition);

                            mem.Close();
                        }
                    }
                } // End picture frame

                source.Seek(dataPosition + dataSize, SeekOrigin.Begin);

                streamPos = source.Position;
            } // End frames loop

            if (-1 == tag.ActualEnd) // No padding frame has been detected so far
            {
                if (streamPos + 4 < source.Length)
                {
                    int test = source.ReadInt32();
                    // See if there's padding after the end of the tag
                    if (0 == test)
                    {
                        // Read until there's something else than zeroes
                        byte[] data = new byte[PADDING_BUFFER_SIZE];
                        bool endReached = false;
                        long initialPos = source.Position;
                        int read = 0;

                        while (!endReached)
                        {
                            source.Read(data, 0, PADDING_BUFFER_SIZE);
                            for (int i = 0; i < PADDING_BUFFER_SIZE; i++)
                            {
                                if (data[i] > 0)
                                {
                                    tag.ActualEnd = initialPos + read + i;
                                    endReached = true;
                                    break;
                                }
                            }
                            if (!endReached) read += PADDING_BUFFER_SIZE;
                        }
                    } else
                    {
                        tag.ActualEnd = streamPos;
                    }
                } else
                {
                    tag.ActualEnd = streamPos;
                }
            }
        }

        // ********************** Public functions & voids **********************

        public override bool Read(BinaryReader source, ReadTagParams readTagParams)
        {
            return Read(source, readTagParams.offset, readTagParams);
        }

        /// <summary>
        /// Reads ID3v2 data
        /// </summary>
        /// <param name="source">Reader object from where to read ID3v2 data</param>
        /// <param name="pictureStreamHandler">If not null, handler that will be triggered whenever a supported embedded picture is read</param>
        /// <param name="offset">ID3v2 header offset (mostly 0, except for specific audio containers such as AIFF or DSF)</param>
        /// <param name="storeUnsupportedMetaFields">Indicates wether unsupported fields should be read and stored in memory (optional; default = false)</param>
        /// <returns></returns>
        public bool Read(BinaryReader source, long offset, ReadTagParams readTagParams)
        {
            tagHeader = new TagInfo();

            BufferedBinaryReader reader = new BufferedBinaryReader(source.BaseStream);

            // Reset data and load header from file to variable
            ResetData();
            bool result = readHeader(reader, tagHeader, offset);

            // Process data if loaded and header valid
            if (result && StreamUtils.StringEqualsArr(ID3V2_ID, tagHeader.ID))
            {
                tagExists = true;
                // Fill properties with header data
                tagVersion = tagHeader.Version;

                // Get information from frames if version supported
                if ((TAG_VERSION_2_2 <= tagVersion) && (tagVersion <= TAG_VERSION_2_4) && (getTagSize(tagHeader) > 0))
                {
                    readFrames(reader, tagHeader, offset, readTagParams);
                    structureHelper.AddZone(offset, (int)(tagHeader.ActualEnd - offset));
                }
                else
                {
                    if ( (tagVersion < TAG_VERSION_2_2) ||  (tagVersion > TAG_VERSION_2_4) ) LogDelegator.GetLogDelegate()(Log.LV_ERROR, "ID3v2 tag version unknown : " + tagVersion  + "; parsing interrupted");
                    if (0 == Size) LogDelegator.GetLogDelegate()(Log.LV_ERROR, "ID3v2 size is zero; parsing interrupted");
                }
            }

            return result;
        }

        protected override int getDefaultTagOffset()
        {
            return TO_BOF;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_ID3V2;
        }

        public override byte FieldCodeFixedLength
        {
            get { return 4; }
        }


        // Writes tag info using ID3v2.4 conventions
        // TODO much later : support ID3v2.3- conventions

        internal int writeInternal(TagData tag, BinaryWriter w, string zone)
        {
            return write(tag, w, zone);
        }

        /// <summary>
        /// Writes the given tag into the given Writer using ID3v2.4 conventions
        /// </summary>
        /// <param name="tag">Tag information to be written</param>
        /// <param name="w">Stream to write tag information to</param>
        /// <returns>True if writing operation succeeded; false if not</returns>
        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result;
            long tagSizePos;
            int tagSize;

            w.Write(ID3V2_ID.ToCharArray());

            // Version 2.4.0
            w.Write(TAG_VERSION_2_4);
            w.Write((byte)0);

            // Flags : keep initial flags
            w.Write(tagHeader.Flags);
            // Keep position in mind to calculate final size and come back here to write it
            tagSizePos = w.BaseStream.Position;
            w.Write((int)0); // Tag size placeholder to be rewritten in a few lines

            result = writeExtHeaderAndFrames(tag, w);

            // Record final(*) size of tag into "tag size" field of header
            // (*) : Spec clearly states that the tag final size is tag size after unsynchronization
            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            tagSize = (int)(finalTagPos - tagSizePos - 4);
            w.Write(StreamUtils.EncodeSynchSafeInt32(tagSize));

            if (ID3v2_useExtendedHeaderRestrictions)
            {
                if (tagSize/1024 > tagHeader.TagSizeRestrictionKB)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Tag is too large (" + tagSize/1024 + "KB) according to ID3v2 restrictions (" + tagHeader.TagSizeRestrictionKB + ") !");
                }
            }

            return result;
        }


        // TODO : Write ID3v2.4 footer
        // TODO : check date field format (YYYY, DDMM, timestamp)

        private int writeExtHeaderAndFrames(TagData tag, BinaryWriter w)
        {
            int nbFrames = 0;
            bool doWritePicture;
            Encoding tagEncoding = Encoding.UTF8; // TODO make this customizable

            // Rewrites extended header as is
            if (tagHeader.HasExtendedHeader)
            {
                w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.ExtendedHeaderSize,4));
                w.Write((byte)1); // Number of flag bytes; always 1 according to spec
                w.Write(tagHeader.ExtendedFlags);
                // TODO : calculate a new CRC according to actual tag contents instead of rewriting CRC as is
                if (tagHeader.CRC > 0) w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.CRC, 5));
                if (tagHeader.TagRestrictions > 0) w.Write(tagHeader.TagRestrictions);

                if (ID3v2_useExtendedHeaderRestrictions)
                {
                    // Force UTF-8 if encoding restriction is enabled and current encoding is not among authorized types
                    // TODO : make target format customizable (UTF-8 or ISO-8859-1)
                    if (tagHeader.HasTextEncodingRestriction)
                    {
                        if (!(tagEncoding.BodyName.Equals("iso-8859-1") || tagEncoding.BodyName.Equals("utf-8")))
                        {
                            tagEncoding = Encoding.UTF8;
                        }
                    }
                }
            }

            // === ID3v2 FRAMES ===
            IDictionary<byte, String> map = tag.ToMap();

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                if (map[frameType].Length > 0) // No frame with empty value
                {
                    foreach (string s in frameMapping_v23_24.Keys)
                    {
                        if (frameType == frameMapping_v23_24[s])
                        {
                            writeTextFrame(w, s, map[frameType], tagEncoding);
                            nbFrames++;
                            break;
                        }
                    }
                }
            }

            // Other textual fields
            foreach (TagData.MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if (fieldInfo.TagType.Equals(getImplementedTagType()) && !fieldInfo.MarkedForDeletion)
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value, tagEncoding);
                    nbFrames++;
                }
            }

            foreach (TagData.PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(TagData.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture =  (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat, Utils.GetMimeTypeFromImageFormat(picInfo.NativeFormat), picInfo.PicType.Equals(TagData.PIC_TYPE.Unsupported) ? (byte)picInfo.NativePicCode : EncodeID3v2PictureType(picInfo.PicType), "", tagEncoding);
                    nbFrames++;
                }
            }

            if (ID3v2_useExtendedHeaderRestrictions)
            {
                if (nbFrames > tagHeader.TagFramesRestriction)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Tag has too many frames ("+ nbFrames +") according to ID3v2 restrictions ("+ tagHeader.TagFramesRestriction +") !");
                }
            }

            return nbFrames;
        }

        private void writeTextFrame(BinaryWriter writer, String frameCode, String text, Encoding tagEncoding)
        {
            string actualFrameCode; // Used for writing TXXX frames
            long frameSizePos;
            long finalFramePos;
            long frameOffset;
            int frameHeaderSize = 6; // 4-byte size + 2-byte flags

            bool writeFieldValue = true;
            bool writeFieldEncoding = true;
            bool writeNullTermination = true; // Required by specs; see §4, concerning $03 encoding

            BinaryWriter w;
            MemoryStream s = null;

            if (tagHeader.UsesUnsynchronisation)
            {
                s = new MemoryStream(Size);
                w = new BinaryWriter(s, tagEncoding);
                frameOffset = writer.BaseStream.Position;
            } else {
                w = writer;
                frameOffset = 0;
            }

            if (ID3v2_useExtendedHeaderRestrictions)
            {
                if (text.Length > tagHeader.TextFieldSizeRestriction)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_INFO, frameCode + " field value (" + text + ") is longer than authorized by ID3v2 restrictions; reducing to " + tagHeader.TextFieldSizeRestriction + " characters");

                    text = text.Substring(0, tagHeader.TextFieldSizeRestriction);
                }
            }

            frameCode = frameCode.ToUpper();
            actualFrameCode = frameCode;

            // If frame is not standard, it has to be added through TXXX frame ("user-defined text information frame")
            if (!standardFrames_v24.Contains(frameCode))
            {
                frameCode = "TXXX";
            }

            w.Write(frameCode.ToCharArray());
            frameSizePos = w.BaseStream.Position;
            w.Write((int)0); // Frame size placeholder to be rewritten in a few lines

            // TODO : handle frame flags (See ID3v2.4 spec; §4.1)
            UInt16 flags = 0;
            if (tagHeader.UsesUnsynchronisation)
            {
                flags = 2;
            }
            w.Write(StreamUtils.ReverseUInt16(flags));

            // Comments frame specifics
            if (frameCode.Substring(0, 3).Equals("COM"))
            {
                // Encoding according to ID3v2 specs
                w.Write(encodeID3v2CharEncoding(tagEncoding));
                // Language ID (ISO-639-2)
                w.Write("eng".ToCharArray()); // TODO : handle this field dynamically
                                              // Short content description
                w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically

                writeFieldEncoding = false;
            }
            else if (frameCode.Substring(0,3).Equals("POP")) // Rating frame specifics
            {
                // User e-mail
                w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically
                               // ID3v2 rating : scale 0 to 255
                w.Write((byte)Math.Max(255, Int32.Parse(text) * 51));
                // Play count
                w.Write((int)0); // TODO : handle this field dynamically. Warning : may be longer than 32 bits (see specs)

                writeFieldValue = false;
            }
            else if (frameCode.Substring(0, 3).Equals("TXX")) // User-defined text frame specifics
            {
                if (writeFieldEncoding) w.Write(encodeID3v2CharEncoding(tagEncoding)); // Encoding according to ID3v2 specs
                w.Write(actualFrameCode.ToCharArray());
                w.Write('\0');

                writeFieldEncoding = false;
                writeNullTermination = true;
            }

            if (writeFieldValue)
            {
                if (writeFieldEncoding) w.Write(encodeID3v2CharEncoding(tagEncoding)); // Encoding according to ID3v2 specs
                w.Write(text.ToCharArray());
                if (writeNullTermination) w.Write('\0');
            }


            if (tagHeader.UsesUnsynchronisation)
            {
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
                w.Close();
            }

            // Go back to frame size location to write its actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameOffset+frameSizePos, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - frameOffset - frameHeaderSize)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, string mimeType, byte pictureTypeCode, string picDescription, Encoding tagEncoding)
        {
            // Binary tag writing management
            long frameOffset;
            long frameSizePos;
            long frameSizePos2;
            long finalFramePos;
            long finalFramePosRaw;
            int dataSizeModifier = 0;

            // Picture operations management
            Image picture = null;

            // Unsynchronization management
            BinaryWriter w;
            MemoryStream s = null;


            if (tagHeader.UsesUnsynchronisation)
            {
                s = new MemoryStream(Size);
                w = new BinaryWriter(s, tagEncoding);
                frameOffset = writer.BaseStream.Position;
            }
            else
            {
                w = writer;
                frameOffset = 0;
            }

            w.Write("APIC".ToCharArray());

            frameSizePos = w.BaseStream.Position;
            w.Write((int)0); // Temp frame size placeholder; will be rewritten at the end of the routine

            // TODO : handle frame flags (See ID3v2.4 spec; §4.1)
            // Data size is the "natural" size of the picture (i.e. without unsynchronization) + the size of the headerless APIC frame (i.e. starting from the "text encoding" byte)
            /*
            w.Write('\0');
            w.Write('\0');
            */
            UInt16 flags = 1;
            if (tagHeader.UsesUnsynchronisation) flags = 3;
            w.Write(StreamUtils.ReverseUInt16(flags));
            dataSizeModifier += 2;

            frameSizePos2 = w.BaseStream.Position;
            w.Write((int)0);
            dataSizeModifier += 4;

            // Beginning of APIC frame data

            w.Write(encodeID3v2CharEncoding(tagEncoding));

            // Application of ID3v2 extended header restrictions
            if (ID3v2_useExtendedHeaderRestrictions)
            {
                // Force JPEG if encoding restriction is enabled and mime-type is not among authorized types
                // TODO : make target format customizable (JPEG or PNG)
                if (tagHeader.HasPictureEncodingRestriction)
                {
                    if (!(mimeType.ToLower().Equals("image/jpeg") || mimeType.ToLower().Equals("image/png")))
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format ("+ mimeType +") does not respect ID3v2 restrictions; switching to JPEG");

                        picture = Image.FromStream(new MemoryStream(pictureData));

                        mimeType = "image/jpeg";
                        picFormat = ImageFormat.Jpeg;
                    }
                }

                // Force picture dimensions if a size restriction is enabled
                if (tagHeader.PictureSizeRestriction > 0)
                {
                    if ( (256 == tagHeader.PictureSizeRestriction) && ((picture.Height > 256) || (picture.Width > 256))) // 256x256 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format ("+ picture.Width + "x" + picture.Height +") does not respect ID3v2 restrictions (256x256 or less); resizing");

                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(256, 256), true);
                    }
                    else if ((64 == tagHeader.PictureSizeRestriction) && ((picture.Height > 64) || (picture.Width > 64))) // 64x64 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + picture.Width + "x" + picture.Height + ") does not respect ID3v2 restrictions (64x64 or less); resizing");

                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(64, 64), true);
                    }
                    else if ((63 == tagHeader.PictureSizeRestriction) && ((picture.Height != 64) && (picture.Width != 64))) // exactly 64x64
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + picture.Width + "x" + picture.Height + ") does not respect ID3v2 restrictions (exactly 64x64); resizing");

                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(64, 64), false);
                    }
                }
            }

            // Null-terminated string = mime-type encoded in ISO-8859-1
            w.Write(Utils.Latin1Encoding.GetBytes(mimeType.ToCharArray())); // Force ISO-8859-1 format for mime-type
            w.Write('\0'); // String should be null-terminated

            w.Write(pictureTypeCode);

            if (picDescription.Length > 0) w.Write(picDescription); // Picture description
            w.Write('\0'); // Description should be null-terminated

            if (picture != null) // Picture has been somehow modified when checking against extended header restrictions
            {
                picture.Save(w.BaseStream, picFormat);
            } else
            {
                w.Write(pictureData);
            }

            finalFramePosRaw = w.BaseStream.Position;

            if (tagHeader.UsesUnsynchronisation)
            {
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
                w.Close();
            }

            // Go back to frame size location to write its actual size
            finalFramePos = writer.BaseStream.Position;

            writer.BaseStream.Seek(frameSizePos + frameOffset, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - frameOffset - dataSizeModifier)));

            writer.BaseStream.Seek(frameSizePos2 + frameOffset, SeekOrigin.Begin);
            writer.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePosRaw - frameSizePos2 - dataSizeModifier)));

            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        // ---------------------------------------------------------------------------
        // ID3v2-SPECIFIC FEATURES
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Extract genre name from String according to ID3 conventions
        /// </summary>
        /// <param name="iGenre">String representation of genre according to various ID3v1/v2 conventions</param>
        /// <returns>Genre name</returns>
        private static String extractGenreFromID3v2Code(String iGenre)
        {
            if (null == iGenre) return "";

            string result = Utils.StripZeroChars(iGenre.Trim());
            int genreIndex = -1;
            int openParenthesisIndex = -1;

            for (int i=0;i < result.Length;i++)
            {
                if ('(' == result[i]) openParenthesisIndex = i;
                else if (')' == result[i] && openParenthesisIndex > -1)
                {
                    if (int.TryParse(result.Substring(openParenthesisIndex + 1, i - openParenthesisIndex - 1), out genreIndex))
                    {
                        // Delete genre index string from the tag value
                        result = result.Remove(0, i + 1);
                        break;
                    }
                }
            }

            /* Regexes are too expensive
            // Any number between parenthesis
            Regex regex = new Regex(@"(?<=\()\d+?(?=\))");

            Match match = regex.Match(result);
            // First match is directly returned
            if (match.Success)
            {
                genreIndex = Int32.Parse(match.Value);
                // Delete genre index string from the tag value
                result = result.Remove(0, result.LastIndexOf(')') + 1);
            }
            */

            if (("" == result) && (genreIndex != -1) && (genreIndex < ID3v1.MusicGenre.Length)) result = ID3v1.MusicGenre[genreIndex];

            return result;
        }

        // Specific to ID3v2 : extract numeric rating from POP/POPM block containing other useless/obsolete data
        private static int readRatingInPopularityMeter(BufferedBinaryReader Source, Encoding encoding)
        {
            // Skip the e-mail, which is a null-terminated string
            StreamUtils.ReadNullTerminatedString(Source, encoding);

            // Returns the rating, contained in the following byte
            return Source.ReadByte();
        }

        // Specific to ID3v2 : read Unicode BOM and return the corresponding encoding
        // NB : This implementation only works with UTF-16 BOMs (i.e. UTF-8 and UTF-32 BOMs will not be detected)
        private BOMProperties readBOM(BufferedBinaryReader fs)
        {
            BOMProperties result = new BOMProperties();
            result.Size = 1;
            result.Encoding = Encoding.Unicode;

            byte[] b = new byte[1];

            fs.Read(b, 0, 1);
            bool first = true;
            bool foundFE = false;
            bool foundFF = false;

            while (0 == b[0] || 0xFF == b[0] || 0xFE == b[0])
            {
                // All UTF-16 BOMs either start or end with 0xFE or 0xFF
                // => Having them both read means that the entirety of the UTF-16 BOM has been read
                foundFE = foundFE || (0xFE == b[0]);
                foundFF = foundFF || (0xFF == b[0]);
                if (foundFE & foundFF) break;

                if (first && b[0] > 0)
                {
                    // 0xFE first means data is coded in Big Endian
                    if (0xFE == b[0]) result.Encoding = Encoding.BigEndianUnicode;
                    first = false;
                }

                fs.Read(b, 0, 1);
                result.Size++;
            }

            return result;
        }

        public static TagData.PIC_TYPE DecodeID3v2PictureType(int picCode)
        {
            if (0 == picCode) return TagData.PIC_TYPE.Generic;      // Spec calls it "Other"
            else if (3 == picCode) return TagData.PIC_TYPE.Front;
            else if (4 == picCode) return TagData.PIC_TYPE.Back;
            else if (6 == picCode) return TagData.PIC_TYPE.CD;
            else return TagData.PIC_TYPE.Unsupported;
        }

        public static byte EncodeID3v2PictureType(TagData.PIC_TYPE picCode)
        {
            if (TagData.PIC_TYPE.Front.Equals(picCode)) return 3;
            else if (TagData.PIC_TYPE.Back.Equals(picCode)) return 4;
            else if (TagData.PIC_TYPE.CD.Equals(picCode)) return 6;
            else return 0;
        }

        // Copies the stream while cleaning abnormalities due to unsynchronization (Cf. §5 of ID3v2.0 specs; §6 of ID3v2.3+ specs)
        // => every "0xff 0x00" becomes "0xff"
        private static void decodeUnsynchronizedStreamTo(BufferedBinaryReader from, Stream to, long length)
        {
            const int BUFFER_SIZE = 8192;

            int bytesToRead;
            bool foundFF = false;

            byte[] readBuffer = new byte[BUFFER_SIZE];
            byte[] writeBuffer = new byte[BUFFER_SIZE];

            int written;
            int remainingBytes;
            if (length > 0)
            {
                remainingBytes = (int)Math.Min(length, from.Length - from.Position);
            } else
            {
                remainingBytes = (int)(from.Length - from.Position);
            }

            while (remainingBytes > 0)
            {
                written = 0;
                bytesToRead = Math.Min(remainingBytes, BUFFER_SIZE);

                from.Read(readBuffer, 0, bytesToRead);

                for (int i = 0; i < bytesToRead; i++)
                {
                    if (0xff == readBuffer[i]) foundFF = true;
                    else if (0x00 == readBuffer[i] && foundFF)
                    {
                        foundFF = false;
                        continue; // i.e. do not write 0x00 to output stream
                    }
                    else if (foundFF) foundFF = false;

                    writeBuffer[written++] = readBuffer[i];
                }
                to.Write(writeBuffer, 0, written);

                remainingBytes -= bytesToRead;
            }
        }

        // Copies the stream while unsynchronizing it (Cf. §5 of ID3v2.0 specs; §6 of ID3v2.3+ specs)
        // => every "0xff 0xex" becomes "0xff 0x00 0xex"; every "0xff 0x00" becomes "0xff 0x00 0x00"
        private static void encodeUnsynchronizedStreamTo(Stream mFrom, BinaryWriter w)
        {
            // TODO PERF : profile using BinaryReader.ReadByte & BinaryWriter.Write(byte) vs. Stream.ReadByte & Stream.WriteByte

            BinaryReader r = new BinaryReader(mFrom); // This reader shouldn't be closed at the end of the function, else the stream closes as well and becomes inaccessible
            
            long initialPosition;
            byte b1,b2;

            initialPosition = r.BaseStream.Position;

            b1 = r.ReadByte();
            while (r.BaseStream.Position < initialPosition + r.BaseStream.Length && r.BaseStream.Position < r.BaseStream.Length)
            {
                b2 = r.ReadByte();
                w.Write(b1);
                if (0xFF == b1 && ( (0x00 == b2) || (0xE0 == (b2 & 0xE0))))
                {
                    w.Write((byte)0);
                }
                b1 = b2;
            }
            w.Write(b1);
        }

        /// Returns the .NET Encoding corresponding to the ID3v2 convention (see below)
        ///
        /// Default encoding should be "ISO-8859-1"
        /// 
        /// Warning : due to approximative implementations, some tags seem to be coded
        /// with the default encoding of the OS they have been tagged on
        /// 
        ///  $00    ISO-8859-1 [ISO-8859-1]. Terminated with $00.
        ///  $01    UTF-16 [UTF-16] encoded Unicode [UNICODE], with BOM if version > 2.2
        ///         All strings in the same frame SHALL have the same byteorder.
        ///         Terminated with $00 00.
        ///  $02    UTF-16BE [UTF-16] encoded Unicode [UNICODE] without BOM.
        ///         Terminated with $00 00.
        ///  $03    UTF-8 [UTF-8] encoded Unicode [UNICODE]. Terminated with $00.
        private static Encoding decodeID3v2CharEncoding(byte encoding)
        {
            if (0 == encoding) return Utils.Latin1Encoding;                 // aka ISO Latin-1
            else if (1 == encoding) return Encoding.Unicode;                // UTF-16 with BOM if version > 2.2
            else if (2 == encoding) return Encoding.BigEndianUnicode;       // UTF-16 Big Endian without BOM (since ID3v2.4)
            else if (3 == encoding) return Encoding.UTF8;                   // UTF-8 (since ID3v2.4)
            else return Encoding.Default;
        }

        private static byte encodeID3v2CharEncoding(Encoding encoding)
        {
            if (encoding.Equals(Encoding.Unicode)) return 1;
            else if (encoding.Equals(Encoding.BigEndianUnicode)) return 2;
            else if (encoding.Equals(Encoding.UTF8)) return 3;
            else return 0; // Default = ISO-8859-1 / ISO Latin-1
        }

    }
}