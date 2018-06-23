using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Commons;
using ATL.Logging;

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
    ///     implementation of decoding extended header data has been tested on _forged_ files. Implementation might not be 100% real-world proof.
    ///     
    ///     2. Hierarchical table of contents (CTOC)
    ///     
    ///     ID3v2 chapters specification allows multiple CTOC frames in the tag, in order to describe a multiple-level table of contents.
    ///     (see informal standard  id3v2-chapters-1.0.html)
    ///     
    ///     This feature is currently not supported. If any CTOC is detected while reading, ATL will "blindly" write a flat CTOC containing
    ///     all chapters. Any hierarchical table of contents will be lost while rewriting.
    /// 
    /// </summary>
    public class ID3v2 : MetaDataIO
    {
        public const byte TAG_VERSION_2_2 = 2;             // Code for ID3v2.2.x tag
        public const byte TAG_VERSION_2_3 = 3;             // Code for ID3v2.3.x tag
        public const byte TAG_VERSION_2_4 = 4;             // Code for ID3v2.4.x tag

        private TagInfo tagHeader;


        // ID3v2 tag ID
        private const String ID3V2_ID = "ID3";

        // List of standard fields
        private static ICollection<string> standardFrames_v22;
        private static ICollection<string> standardFrames_v23;
        private static ICollection<string> standardFrames_v24;

        // Field codes that need to be persisted in a COMMENT field
        private static ICollection<string> commentsFields;

        // Fields where text encoding descriptor byte is not required
        private static ICollection<string> noTextEncodingFields;

        // Mapping between ID3v2 field IDs and ATL fields
        private static IDictionary<string, byte> frameMapping_v22;
        private static IDictionary<string, byte> frameMapping_v23;
        private static IDictionary<string, byte> frameMapping_v24;

        // Mapping between ID3v2.2/3 fields and ID3v2.4 fields not included in frameMapping_v2x, and that have changed between versions
        private static IDictionary<string, string> frameMapping_v22_4;
        private static IDictionary<string, string> frameMapping_v23_4;


        // Max. tag size for saving
        private const int ID3V2_MAX_SIZE = 4096;
        
        // Buffer size to use to parse through padding frames
        private const int PADDING_BUFFER_SIZE = 512;

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
            standardFrames_v23 = new List<string>() { "AENC","APIC","COMM","COMR","ENCR","EQUA","ETCO","GEOB","GRID","IPLS","LINK","MCDI","MLLT","OWNE","PRIV","PCNT","POPM","POSS","RBUF","RVAD","RVRB","SYLT","SYTC","TALB","TBPM","TCOM","TCON","TCOP","TDAT","TDLY","TENC","TEXT","TFLT","TIME","TIT1", "TIT2", "TIT3","TKEY","TLAN","TLEN","TMED","TOAL","TOFN","TOLY","TOPE","TORY","TOWN","TPE1", "TPE2", "TPE3", "TPE4", "TPOS", "TPUB", "TRCK", "TRDA", "TRSN", "TRSO", "TSIZ", "TSRC", "TSSE", "TYER", "TXXX", "UFID", "USER", "USLT", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB", "WXXX", "CHAP", "CTOC" };
            standardFrames_v24 = new List<string>() { "AENC", "APIC", "ASPI","COMM", "COMR", "ENCR", "EQU2", "ETCO", "GEOB", "GRID", "LINK", "MCDI", "MLLT", "OWNE", "PRIV", "PCNT", "POPM", "POSS", "RBUF", "RVA2", "RVRB", "SEEK","SIGN","SYLT", "SYTC", "TALB", "TBPM", "TCOM", "TCON", "TCOP", "TDEN", "TDLY", "TDOR","TDRC","TDRL","TDTG", "TENC", "TEXT", "TFLT", "TIPL", "TIT1", "TIT2", "TIT3", "TKEY", "TLAN", "TLEN", "TMCL","TMED", "TMOO","TOAL", "TOFN", "TOLY", "TOPE", "TORY", "TOWN", "TPE1", "TPE2", "TPE3", "TPE4", "TPOS", "TPRO", "TPUB", "TRCK", "TRSN", "TRSO", "TSOA","TSOP","TSOT", "TSRC", "TSSE", "TSST","TXXX", "UFID", "USER", "USLT", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB", "WXXX", "CHAP", "CTOC" };

            commentsFields = new List<string>() { "iTunNORM", "iTunSMPB", "iTunPGAP" };

            noTextEncodingFields = new List<string>() { "POPM", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB" };

            // Note on date field identifiers
            //
            // Original release date
            //   ID3v2.0 : TOR (year only)
            //   ID3v2.3 : TORY (year only)
            //   ID3v2.4 : TDOR (timestamp according to spec)
            //
            // Release date
            //   ID3v2.0 : no standard
            //   ID3v2.3 : no standard
            //   ID3v2.4 : TDRL (timestamp according to spec; actual content may vary)
            //
            // Recording date <== de facto standard behind the "date" field on most taggers
            //   ID3v2.0 : TYE (year), TDA (day & month - DDMM), TIM (hour & minute - HHMM)
            //   ID3v2.3 : TYER (year), TDAT (day & month - DDMM), TIME (hour & minute - HHMM)
            //   ID3v2.4 : TDRC (timestamp)

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
                { "TIM", TagData.TAG_FIELD_RECORDING_TIME },
                { "COM", TagData.TAG_FIELD_COMMENT },
                { "TCM", TagData.TAG_FIELD_COMPOSER },
                { "POP", TagData.TAG_FIELD_RATING },
                { "TCO", TagData.TAG_FIELD_GENRE },
                { "TCR", TagData.TAG_FIELD_COPYRIGHT },
                { "TPB", TagData.TAG_FIELD_PUBLISHER }
            };

            frameMapping_v22_4 = new Dictionary<string, string>
            {
                { "BUF", "RBUF" },
                { "CNT", "PCNT" },
                { "CRA", "AENC" },
                // CRM / Encrypted meta frame field has been droppped
                { "ETC", "ETCO" },
                { "EQU", "EQU2" },
                { "GEO", "GEOB" },
                { "IPL", "TIPL" },
                { "LNK", "LINK" },
                { "MCI", "MCDI" },
                { "MLL", "MLLT" },
                { "REV", "RVRB" },
                { "RVA", "RVA2" },
                { "SLT", "SYLT" },
                { "STC", "SYTC" },
                { "TBP", "TBPM" },
                { "TDY", "TDLY" },
                { "TEN", "TENC" },
                { "TFT", "TFLT" },
                { "TKE", "TKEY" },
                { "TLA", "TLAN" },
                { "TLE", "TLEN" },
                { "TMT", "TMED" },
                { "TOF", "TOFN" },
                { "TOL", "TOLY" },
                { "TP4", "TPE4" },
                { "TPA", "TPOS" },
                { "TRC", "TSRC" },
                //{ "TRD", "" } no direct equivalent
                // TSI / Size field has been dropped
                { "TSS", "TSSE" },
                { "TT3", "TIT3" },
                { "TXT", "TEXT" },
                { "TXX", "TXXX" },
                { "UFI", "UFID" },
                { "ULT", "USLT" },
                { "WAF", "WOAF" },
                { "WAR", "WOAR" },
                { "WAS", "WOAS" },
                { "WCM", "WCOM" },
                { "WCP", "WCOP" },
                { "WPB", "WPUB" },
                { "WXX", "WXXX" }
                // TYE, TDA and TIM are converted on the fly when writing
        };

            frameMapping_v23_4 = new Dictionary<string, string>
            {
                { "EQUA", "EQU2" },
                { "IPLS", "TIPL" },
                { "RVAD", "RVA2" },
                { "TORY", "TDOR" } // yyyy is a valid timestamp
                // TYER, TDAT and TIME are converted on the fly when writing
            };

            // Mapping between standard fields and ID3v2.3 identifiers
            frameMapping_v23 = new Dictionary<string, byte>
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
                { "TYER", TagData.TAG_FIELD_RECORDING_YEAR },
                { "TDAT", TagData.TAG_FIELD_RECORDING_DAYMONTH },
                { "TIME", TagData.TAG_FIELD_RECORDING_TIME },
                { "COMM", TagData.TAG_FIELD_COMMENT },
                { "TCOM", TagData.TAG_FIELD_COMPOSER },
                { "POPM", TagData.TAG_FIELD_RATING },
                { "TCON", TagData.TAG_FIELD_GENRE },
                { "TCOP", TagData.TAG_FIELD_COPYRIGHT },
                { "TPUB", TagData.TAG_FIELD_PUBLISHER }
            };

            // Mapping between standard fields and ID3v2.4 identifiers
            frameMapping_v24 = new Dictionary<string, byte>
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

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            if (ID.Length < 5) ID = ID.ToUpper(); // Preserve the case of non-standard ID3v2 fields -- TODO : use the TagData.Origin property !

            // Finds the ATL field identifier according to the ID3v2 version
            switch (tagVersion)
            {
                case TAG_VERSION_2_2: if (frameMapping_v22.ContainsKey(ID)) supportedMetaId = frameMapping_v22[ID]; break;
                case TAG_VERSION_2_3: if (frameMapping_v23.ContainsKey(ID)) supportedMetaId = frameMapping_v23[ID]; break;
                case TAG_VERSION_2_4: if (frameMapping_v24.ContainsKey(ID)) supportedMetaId = frameMapping_v24[ID]; break;
            }

            return supportedMetaId;
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

        private int getTagSize(TagInfo Tag, bool includeFooter = true)
        {
            // Get total tag size
            int result = StreamUtils.DecodeSynchSafeInt32(Tag.Size) + 10; // 10 being the size of the header

            if (includeFooter && Tag.HasFooter) result += 10; // Indicates the presence of a footer (ID3v2.4+)

            if (result > Tag.FileSize) result = 0;

            return result;
        }

        private bool readFrame(
            BufferedBinaryReader source,
            TagInfo tag,
            ReadTagParams readTagParams,
            ref IList<MetaFieldInfo> comments,
            bool inChapter = false)
        {
            FrameHeader Frame = new FrameHeader();
            byte encodingCode;
            Encoding frameEncoding;
            int dataSize;
            long dataPosition;

            long initialTagPos = source.Position;

            ChapterInfo chapter = null;
            MetaFieldInfo comment = null;


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
                }
                else // If not, we're in the wrong place
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Valid frame not found where expected; parsing interrupted");
                    source.Seek(initialTagPos - tag.HeaderEnd + getTagSize(tag, false), SeekOrigin.Begin);
                    return false;
                }
            }

            if (tag.ActualEnd > -1) return false;

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

            if (!noTextEncodingFields.Contains(Frame.ID))
            {
                dataSize = dataSize - 1; // Minus encoding byte
                encodingCode = source.ReadByte();
            } else
            {
                encodingCode = 0; // Latin-1; default according to spec
            }
            frameEncoding = decodeID3v2CharEncoding(encodingCode);

            // COMM fields contain :
            //   a 3-byte langage ID
            //   a "short content description", as an encoded null-terminated string
            //   the actual comment, as an encoded, null-terminated string
            // => lg lg lg (BOM) (encoded description) 00 (00) (BOM) encoded text 00 (00)
            if ("COM".Equals(Frame.ID.Substring(0, 3)))
            {
                long initialPos = source.Position;
                if (null == comments) comments = new List<MetaFieldInfo>();

                comment = new MetaFieldInfo(getImplementedTagType(), "");

                // Langage ID
                comment.Language = Utils.Latin1Encoding.GetString(source.ReadBytes(3));

                BOMProperties contentDescriptionBOM = new BOMProperties();
                // Skip BOM if ID3v2.3+ and UTF-16 with BOM present
                if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode))
                {
                    contentDescriptionBOM = readBOM(source);
                }

                if (contentDescriptionBOM.Size <= 3)
                {
                    // Skip content description
                    comment.NativeFieldCode = StreamUtils.ReadNullTerminatedString(source, frameEncoding);
                }
                else
                {
                    // If content description BOM > 3 bytes, there might not be any BOM
                    // for content description, and the algorithm might have bumped into
                    // the comment BOM => backtrack just after langage tag
                    source.Seek(initialPos + 3, SeekOrigin.Begin);
                }

                dataSize = dataSize - (int)(source.Position - initialPos);
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

            if (dataSize > 0)
            {
                if (!("PIC".Equals(Frame.ID) || "APIC".Equals(Frame.ID))) // Not a picture frame
                {
                    string strData;
                    string shortFrameId = Frame.ID.Substring(0, 3);

                    // Specific to Popularitymeter : Rating data has to be extracted from the POPM block
                    if ("POP".Equals(shortFrameId))
                    {
                        /*
                         * ID3v2.0 : According to spec (see §3.2), encoding should actually be ISO-8859-1
                         * ID3v2.3+ : Spec is unclear wether to read as ISO-8859-1 or not. Practice indicates using this convention is safer.
                         */
                        strData = readRatingInPopularityMeter(source, Utils.Latin1Encoding).ToString();
                    }
                    else if ("TXX".Equals(shortFrameId))
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes(dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));

                        string[] tabS = strData.Split('\0');

                        Frame.ID = tabS[0];
                        if (tabS.Length > 1) strData = tabS[1]; else strData = ""; // If the 2nd part of the array isn't there, value is non-existent (TXXX...KEY\0\0 or TXXX...KEY\0)

                        // If unicode is used, there might be BOMs converted to 'ZERO WIDTH NO-BREAK SPACE' character
                        // (pattern : TXXX-stuff-BOM-ID-\0-BOM-VALUE-\0-BOM-VALUE-\0)
                        if (1 == encodingCode) strData = strData.Replace(Utils.UNICODE_INVISIBLE_EMPTY, "");
                    }
                    else if ("CHA".Equals(shortFrameId)) // Chapters
                    {
                        if (null == tagData.Chapters) tagData.Chapters= new List<ChapterInfo>();
                        chapter = new ChapterInfo();
                        tagData.Chapters.Add(chapter);

                        long initPos = source.Position;
                        chapter.UniqueID = StreamUtils.ReadNullTerminatedString(source, frameEncoding);

                        chapter.StartTime = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.EndTime = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.StartOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.EndOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                        chapter.UseOffset = (!(chapter.StartOffset == UInt32.MaxValue));

                        long remainingData = dataSize - (source.Position - initPos);
                        while (remainingData > 0)
                        {
                            if (!readFrame(source, tag, readTagParams, ref comments, true)) break;
                            remainingData = dataSize - (source.Position - initPos);
                        } // End chapter frames loop

                        strData = "";
                    }
                    else
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes(dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));

                        // Parse GENRE frame
                        if ("TCO".Equals(shortFrameId)) strData = extractGenreFromID3v2Code(strData);
                    }

                    if (null == comment && null == chapter) // We're in a non-Comment, non-Chapter field => directly store value
                    {
                        if (!inChapter) SetMetaField(Frame.ID, strData, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                        else
                        {
                            chapter = tagData.Chapters[tagData.Chapters.Count - 1];
                            switch (Frame.ID)
                            {
                                case "TIT2": chapter.Title = strData; break;
                                case "TIT3": chapter.Subtitle = strData; break;
                                case "WXXX": chapter.Url = strData; break;
                            }
                        }
                    }
                    else if (comment != null) // We're in a Comment field => store value in temp Comment structure
                    {
                        bool found = false;

                        // Comment of the same field if already exists
                        foreach (MetaFieldInfo com in comments)
                        {
                            if (com.NativeFieldCode.Equals(comment.NativeFieldCode))
                            {
                                com.Value += Settings.InternalValueSeparator + strData;
                                found = true;
                            }
                        }

                        // Else brand new comment
                        if (!found)
                        {
                            comment.Value = strData;
                            comments.Add(comment);
                        }
                    }

                    if (TAG_VERSION_2_2 == tagVersion) source.Seek(dataPosition + dataSize, SeekOrigin.Begin);
                }
                else // Picture frame
                {
                    long position = source.Position;
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
                        imgFormat = ImageUtils.GetImageFormatFromMimeType(mimeType);
                    }

                    byte picCode = source.ReadByte();
                    // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                    PictureInfo.PIC_TYPE picType = DecodeID3v2PictureType(picCode);

                    int picturePosition = 0;
                    if (!inChapter)
                    {
                        if (picType.Equals(PictureInfo.PIC_TYPE.Unsupported))
                        {
                            addPictureToken(MetaDataIOFactory.TAG_ID3V2, picCode);
                            picturePosition = takePicturePosition(MetaDataIOFactory.TAG_ID3V2, picCode);
                        }
                        else
                        {
                            addPictureToken(picType);
                            picturePosition = takePicturePosition(picType);
                        }
                    }

                    // Image description (unused)
                    // Description may be coded with another convention
                    if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode)) readBOM(source);
                    string description = StreamUtils.ReadNullTerminatedString(source, frameEncoding);

                    if (readTagParams.ReadPictures || readTagParams.PictureStreamHandler != null)
                    {
                        int picSize = dataSize - (int)(source.Position - position);

                        PictureInfo picInfo = new PictureInfo(imgFormat, picType, getImplementedTagType(), picCode, picturePosition);
                        picInfo.Description = description;

                        if (tag.UsesUnsynchronisation)
                        {
                            picInfo.PictureData = decodeUnsynchronizedStream(source, picSize);
                        }
                        else
                        {
                            picInfo.PictureData = new byte[picSize];
                            source.Read(picInfo.PictureData, 0, picSize);
                        }

                        if (!inChapter)
                        {
                            tagData.Pictures.Add(picInfo);

                            if (readTagParams.PictureStreamHandler != null)
                            {
                                MemoryStream mem = new MemoryStream(picInfo.PictureData);
                                readTagParams.PictureStreamHandler(ref mem, picInfo.PicType, picInfo.NativeFormat, picInfo.TagType, picInfo.NativePicCode, picInfo.Position);
                                mem.Close();
                            }
                        } else
                        {
                            tagData.Chapters[tagData.Chapters.Count - 1].Picture = picInfo;
                        }
                    }
                } // Picture frame
            } // Data size > 0

            source.Seek(dataPosition + dataSize, SeekOrigin.Begin);

            return true;
        }

        // Get information from frames (universal)
        private void readFrames(BufferedBinaryReader source, TagInfo tag, long offset, ReadTagParams readTagParams)
        {
            long streamPos;
            long streamLength = source.Length;
            int tagSize = getTagSize(tag, false);

            tag.ActualEnd = -1;

            IList<MetaFieldInfo> comments = new List<MetaFieldInfo>();

            source.Seek(tag.HeaderEnd, SeekOrigin.Begin);
            streamPos = source.Position;

            while ((streamPos - offset < tagSize) && (streamPos < streamLength))
            {
                if (!readFrame(source, tag, readTagParams, ref comments)) break;

                streamPos = source.Position;
            }

            /* Store all comments
             * 
             * - The only comment with a blank or bland description field is a "classic" Comment
             * - Other comments are treated as additional fields, with their description field as their field code
            */
            if (comments != null && comments.Count > 0)
            {
                foreach (MetaFieldInfo comm in comments)
                {
                    string commentDescription = comm.NativeFieldCode.Trim().Replace(Utils.UNICODE_INVISIBLE_EMPTY,"");
                    if (commentDescription.Length > 0) 
                    {
                        if (!commentDescription.Equals("comment", StringComparison.OrdinalIgnoreCase) && !commentDescription.Equals("no description", StringComparison.OrdinalIgnoreCase) && !commentDescription.Equals("description", StringComparison.OrdinalIgnoreCase))
                        {
                            // Processed as an additional field
                            SetMetaField(commentDescription, comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version, 0, comm.Language);
                            continue;
                        }
                    }

                    // Processed as a "classic" Comment
                    if (tagVersion > TAG_VERSION_2_2) SetMetaField("COMM", comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                    else SetMetaField("COM", comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                }
            }

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
                    }
                    else
                    {
                        tag.ActualEnd = streamPos;
                    }
                }
                else
                {
                    tag.ActualEnd = streamPos;
                }
            }
        }

        // ********************** Public functions & voids **********************

        protected override bool read(BinaryReader source, ReadTagParams readTagParams)
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
            get { return 0; } // Actually 4 when strictly applying specs, but thanks to TXXX fields, any code is supported
        }


        // Writes tag info using ID3v2.4 conventions
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

            if (Settings.ID3v2_useExtendedHeaderRestrictions)
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
            Encoding tagEncoding = Settings.DefaultTextEncoding;

            // Rewrites extended header as is
            if (tagHeader.HasExtendedHeader)
            {
                w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.ExtendedHeaderSize,4));
                w.Write((byte)1); // Number of flag bytes; always 1 according to spec
                w.Write(tagHeader.ExtendedFlags);
                // TODO : calculate a new CRC according to actual tag contents instead of rewriting CRC as is -- NB : CRC perimeter definition given by specs is unclear
                if (tagHeader.CRC > 0) w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.CRC, 5));
                if (tagHeader.TagRestrictions > 0) w.Write(tagHeader.TagRestrictions);

                if (Settings.ID3v2_useExtendedHeaderRestrictions)
                {
                    // Force default encoding if encoding restriction is enabled and current encoding is not among authorized types
                    if (tagHeader.HasTextEncodingRestriction)
                    {
                        if (!(tagEncoding.BodyName.Equals("iso-8859-1") || tagEncoding.BodyName.Equals("utf-8")))
                        {
                            tagEncoding = Settings.DefaultTextEncoding;
                        }
                    }
                }
            }

            // === ID3v2 FRAMES ===
            IDictionary<byte, String> map = tag.ToMap();
            string recordingYear = "";
            string recordingDayMonth = "";
            string recordingTime = "";

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                if (map[frameType].Length > 0) // No frame with empty value
                {
                    // "Recording date" fields are a bit tricky, since there is no 1-to-1 mapping between ID3v2.2/3 and ID3v2.4
                    //   ID3v2.0 : TYE (year), TDA (day & month - DDMM), TIM (hour & minute - HHMM)
                    //   ID3v2.3 : TYER (year), TDAT (day & month - DDMM), TIME (hour & minute - HHMM)
                    //   ID3v2.4 : TDRC (timestamp)
                    if (TagData.TAG_FIELD_RECORDING_YEAR == frameType)
                    {
                        recordingYear = map[frameType];
                    }
                    else if (TagData.TAG_FIELD_RECORDING_DAYMONTH == frameType)
                    {
                        recordingDayMonth = map[frameType];
                    }
                    else if (TagData.TAG_FIELD_RECORDING_TIME == frameType)
                    {
                        recordingTime = map[frameType];
                    }
                    else
                    {
                        foreach (string s in frameMapping_v24.Keys)
                        {
                            if (frameType == frameMapping_v24[s])
                            {
                                writeTextFrame(w, s, map[frameType], tagEncoding);
                                nbFrames++;
                                break;
                            }
                        }
                    }
                }
            }

            // Finally write recording date if recording day-month and/or year have been provided
            if (4 == recordingYear.Length && Utils.IsNumeric(recordingYear))
            {
                StringBuilder recordingTimestamp = new StringBuilder(recordingYear);
                if (4 == recordingDayMonth.Length && Utils.IsNumeric(recordingDayMonth)) recordingTimestamp.Append("-" ).Append(recordingDayMonth.Substring(2, 2)).Append("-").Append(recordingDayMonth.Substring(0, 2));
                if (4 == recordingTime.Length && Utils.IsNumeric(recordingTime)) recordingTimestamp.Append("T").Append(recordingTime.Substring(0, 2)).Append(":").Append(recordingTime.Substring(2, 2));

                writeTextFrame(w, "TDRC", recordingTimestamp.ToString(), tagEncoding);
                nbFrames++;
            }

            // Chapters
            if (Chapters.Count > 0)
            {
                nbFrames += writeChapters(w, Chapters, tagEncoding);
            }

            // Other textual fields
            string fieldCode;
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if (( fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion)
                {
                    fieldCode = fieldInfo.NativeFieldCode;

                    if (fieldCode.Equals("CTOC")) continue; // CTOC (table of contents) is handled by writeChapters

                    // We're writing with ID3v2.4 standard. Some standard frame codes have to be converted from ID3v2.2/3 to ID3v4
                    if (TAG_VERSION_2_2 == tagVersion)
                    {
                        if (frameMapping_v22_4.ContainsKey(fieldCode)) fieldCode = frameMapping_v22_4[fieldCode];
                    }
                    else if (TAG_VERSION_2_3 == tagVersion)
                    {
                        if (frameMapping_v23_4.ContainsKey(fieldCode)) fieldCode = frameMapping_v23_4[fieldCode];
                    }

                    writeTextFrame(w, fieldCode, fieldInfo.Value, tagEncoding, fieldInfo.Language);
                    nbFrames++;
                }
            }

            foreach (PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture =  (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(w, picInfo.PictureData, picInfo.NativeFormat, ImageUtils.GetMimeTypeFromImageFormat(picInfo.NativeFormat), picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)picInfo.NativePicCode : EncodeID3v2PictureType(picInfo.PicType), picInfo.Description, tagEncoding);
                    nbFrames++;
                }
            }

            if (Settings.ID3v2_useExtendedHeaderRestrictions)
            {
                if (nbFrames > tagHeader.TagFramesRestriction)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Tag has too many frames ("+ nbFrames +") according to ID3v2 restrictions ("+ tagHeader.TagFramesRestriction +") !");
                }
            }

            return nbFrames;
        }

        private int writeChapters(BinaryWriter writer, IList<ChapterInfo> chapters, Encoding tagEncoding)
        {
            Random randomGenerator = null;
            long frameSizePos, finalFramePos, frameOffset;
            int frameHeaderSize = 6; // 4-byte size + 2-byte flags
            int result = 0;

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

            // Write a "flat" table of contents, if any CTOC is present in tag
            // NB : Hierarchical table of contents is not supported; see implementation notes in the header
            if ((Settings.ID3v2_alwaysWriteCTOCFrame && chapters.Count > 0) || AdditionalFields.ContainsKey("CTOC"))
            {
                w.Write(Utils.Latin1Encoding.GetBytes("CTOC"));
                frameSizePos = w.BaseStream.Position;
                w.Write((int)0); // Frame size placeholder to be rewritten in a few lines

                // TODO : handle frame flags (See ID3v2.4 spec; §4.1)
                ushort flags = 0;
                if (tagHeader.UsesUnsynchronisation)
                {
                    flags = 2;
                }
                w.Write(StreamUtils.EncodeBEUInt16(flags));

                // Default unique toc ID
                w.Write(Utils.Latin1Encoding.GetBytes("toc"));
                w.Write('\0');

                // CTOC flags : no parents; chapters are in order
                w.Write((byte)3);


                // Entry count
                w.Write((byte)chapters.Count);

                for (int i=0; i < chapters.Count; i++)
                {
                    // Generate a chapter ID if none has been given
                    if (0 == chapters[i].UniqueID.Length)
                    {
                        if (null == randomGenerator) randomGenerator = new Random();
                        chapters[i].UniqueID = randomGenerator.Next().ToString();
                    }
                    w.Write(Encoding.UTF8.GetBytes(chapters[i].UniqueID));
                    w.Write('\0');
                }

                // CTOC description not supported

                // Go back to frame size location to write its actual size 
                finalFramePos = w.BaseStream.Position;
                w.BaseStream.Seek(frameOffset + frameSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - frameOffset - frameHeaderSize)));
                w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

                result++;
            }

            // Write individual chapters
            foreach (ChapterInfo chapter in chapters)
            {
                w.Write(Utils.Latin1Encoding.GetBytes("CHAP"));
                frameSizePos = w.BaseStream.Position;
                w.Write((int)0); // Frame size placeholder to be rewritten in a few lines

                // TODO : handle frame flags (See ID3v2.4 spec; §4.1)
                ushort flags = 0;
                if (tagHeader.UsesUnsynchronisation)
                {
                    flags = 2;
                }
                w.Write(StreamUtils.EncodeBEUInt16(flags));

                // Encoding according to ID3v2 specs
                w.Write(encodeID3v2CharEncoding(tagEncoding));

                // Generate a chapter ID if none has been given
                if (0 == chapter.UniqueID.Length)
                {
                    if (null == randomGenerator) randomGenerator = new Random();
                    chapter.UniqueID = randomGenerator.Next().ToString();
                }

                w.Write(Encoding.UTF8.GetBytes(chapter.UniqueID));
                w.Write('\0');

                w.Write(StreamUtils.EncodeBEUInt32(chapter.StartTime));
                w.Write(StreamUtils.EncodeBEUInt32(chapter.EndTime));
                w.Write(StreamUtils.EncodeBEUInt32(chapter.StartOffset));
                w.Write(StreamUtils.EncodeBEUInt32(chapter.EndOffset));

                if (chapter.Title != null && chapter.Title.Length > 0)
                {
                    writeTextFrame(w, "TIT2", chapter.Title, tagEncoding, "", true);
                }
                if (chapter.Subtitle != null && chapter.Subtitle.Length > 0)
                {
                    writeTextFrame(w, "TIT3", chapter.Subtitle, tagEncoding, "", true);
                }
                if (chapter.Url != null && chapter.Url.Length > 0)
                {
                    writeTextFrame(w, "WXXX", chapter.Url, tagEncoding, "", true);
                }
                if (chapter.Picture != null && chapter.Picture.PictureData != null && chapter.Picture.PictureData.Length > 0)
                {
                    writePictureFrame(w, chapter.Picture.PictureData, chapter.Picture.NativeFormat, ImageUtils.GetMimeTypeFromImageFormat(chapter.Picture.NativeFormat), chapter.Picture.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)chapter.Picture.NativePicCode : EncodeID3v2PictureType(chapter.Picture.PicType), chapter.Picture.Description, tagEncoding, true);
                }

                // Go back to frame size location to write its actual size 
                finalFramePos = w.BaseStream.Position;
                w.BaseStream.Seek(frameOffset + frameSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - frameOffset - frameHeaderSize)));
                w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            }

            result += chapters.Count;

            if (tagHeader.UsesUnsynchronisation)
            {
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
                w.Close();
            }

            return result;
        }

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text, Encoding tagEncoding, string language = "", bool isInsideUnsynch = false)
        {
            string actualFrameCode; // Used for writing TXXX frames
            long frameSizePos, finalFramePos, frameOffset;
            int frameHeaderSize = 6; // 4-byte size + 2-byte flags

            bool isCommentCode = false;

            bool writeValue = true;
            bool writeTextEncoding = !noTextEncodingFields.Contains(frameCode);
            bool writeNullTermination = true; // Required by specs; see §4, concerning $03 encoding

            BinaryWriter w;
            MemoryStream s = null;

            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
            {
                s = new MemoryStream(Size);
                w = new BinaryWriter(s, tagEncoding);
                frameOffset = writer.BaseStream.Position;
            } else {
                w = writer;
                frameOffset = 0;
            }

            if (Settings.ID3v2_useExtendedHeaderRestrictions)
            {
                if (text.Length > tagHeader.TextFieldSizeRestriction)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_INFO, frameCode + " field value (" + text + ") is longer than authorized by ID3v2 restrictions; reducing to " + tagHeader.TextFieldSizeRestriction + " characters");

                    text = text.Substring(0, tagHeader.TextFieldSizeRestriction);
                }
            }


            if (frameCode.Length < 5) frameCode = frameCode.ToUpper(); // Only capitalize standard ID3v2 fields -- TODO : Use TagData.Origin property !
            actualFrameCode = frameCode;

            // If frame is only supported through Comment field, it has to be added through COMM frame
            if (commentsFields.Contains(frameCode))
            {
                frameCode = "COMM";
                isCommentCode = true;
            }
            // If frame is not standard, it has to be added through TXXX frame ("user-defined text information frame")
            else if (!standardFrames_v24.Contains(frameCode))
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
            w.Write(StreamUtils.EncodeBEUInt16(flags));

            string shortCode = frameCode.Substring(0, 3);

            // Comments frame specifics
            if (shortCode.Equals("COM"))
            {
                // Encoding according to ID3v2 specs
                w.Write(encodeID3v2CharEncoding(tagEncoding));

                // Language ID (ISO-639-2)
                if (language != null) language = Utils.BuildStrictLengthString(language, 3, '\0');
                w.Write(language.ToCharArray());

                // Short content description
                if (isCommentCode)
                {
                    w.Write(actualFrameCode.ToCharArray());
                }
                w.Write('\0');

                writeTextEncoding = false;
            }
            else if (shortCode.Equals("POP")) // Rating frame specifics
            {
                // User e-mail
                w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically

                w.Write((byte)TrackUtils.EncodePopularity(text, ratingConvention));

                // Play count
                w.Write((int)0); // TODO : handle this field dynamically. Warning : may be longer than 32 bits (see specs)

                writeValue = false;
            }
            else if (shortCode.Equals("TXX")) // User-defined text frame specifics
            {
                if (writeTextEncoding) w.Write(encodeID3v2CharEncoding(tagEncoding)); // Encoding according to ID3v2 specs
                w.Write(actualFrameCode.ToCharArray());
                w.Write('\0');

                writeTextEncoding = false;
                writeNullTermination = true;
            }

            if (writeValue)
            {
                if (writeTextEncoding) w.Write(encodeID3v2CharEncoding(tagEncoding)); // Encoding according to ID3v2 specs
                w.Write(text.ToCharArray());
                if (writeNullTermination) w.Write('\0');
            }


            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
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

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, string mimeType, byte pictureTypeCode, string picDescription, Encoding tagEncoding, bool isInsideUnsynch = false)
        {
            // Binary tag writing management
            long frameOffset;
            long frameSizePos;
            long frameSizePos2;
            long finalFramePos;
            long finalFramePosRaw;
            int dataSizeModifier = 0;

            // Unsynchronization management
            BinaryWriter w;
            MemoryStream s = null;


            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
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
            if (Settings.ID3v2_useExtendedHeaderRestrictions)
            {
                // Force JPEG if encoding restriction is enabled and mime-type is not among authorized types
                // TODO : make target format customizable (JPEG or PNG)
                if (tagHeader.HasPictureEncodingRestriction)
                {
                    if (!(mimeType.ToLower().Equals("image/jpeg") || mimeType.ToLower().Equals("image/png")))
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format ("+ mimeType +") does not respect ID3v2 restrictions (jpeg or png required)");
                    }
                }

                // Force picture dimensions if a size restriction is enabled
                if (tagHeader.PictureSizeRestriction > 0)
                {
                    ImageProperties props = ImageUtils.GetImageProperties(pictureData);

                    if ( (256 == tagHeader.PictureSizeRestriction) && ((props.Height > 256) || (props.Width > 256))) // 256x256 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format ("+ props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (256x256 or less)");

                        /*
                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(256, 256), true);
                        */
                    }
                    else if ((63 == tagHeader.PictureSizeRestriction) && ((props.Height > 64) || (props.Width > 64))) // 64x64 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (64x64 or less)");

                        /*
                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(64, 64), true);
                        */
                    }
                    else if ((64 == tagHeader.PictureSizeRestriction) && ((props.Height != 64) && (props.Width != 64))) // exactly 64x64
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (exactly 64x64)");

                        /*
                        picture = Image.FromStream(new MemoryStream(pictureData));
                        picture = Utils.ResizeImage(picture, new System.Drawing.Size(64, 64), false);
                        */
                    }
                }
            }

            // Null-terminated string = mime-type encoded in ISO-8859-1
            w.Write(Utils.Latin1Encoding.GetBytes(mimeType.ToCharArray())); // Force ISO-8859-1 format for mime-type
            w.Write('\0'); // String should be null-terminated

            w.Write(pictureTypeCode);

            if (picDescription.Length > 0) w.Write(picDescription); // Picture description
            w.Write('\0'); // Description should be null-terminated

            w.Write(pictureData);

            finalFramePosRaw = w.BaseStream.Position;

            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
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

            string result = iGenre.Trim().Replace("\0","");
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

        public static PictureInfo.PIC_TYPE DecodeID3v2PictureType(int picCode)
        {
            if (0 == picCode) return PictureInfo.PIC_TYPE.Generic;      // Spec calls it "Other"
            else if (3 == picCode) return PictureInfo.PIC_TYPE.Front;
            else if (4 == picCode) return PictureInfo.PIC_TYPE.Back;
            else if (6 == picCode) return PictureInfo.PIC_TYPE.CD;
            else return PictureInfo.PIC_TYPE.Unsupported;
        }

        public static byte EncodeID3v2PictureType(PictureInfo.PIC_TYPE picCode)
        {
            if (PictureInfo.PIC_TYPE.Front.Equals(picCode)) return 3;
            else if (PictureInfo.PIC_TYPE.Back.Equals(picCode)) return 4;
            else if (PictureInfo.PIC_TYPE.CD.Equals(picCode)) return 6;
            else return 0;
        }

        // Copies the stream while cleaning abnormalities due to unsynchronization (Cf. §5 of ID3v2.0 specs; §6 of ID3v2.3+ specs)
        // => every "0xff 0x00" becomes "0xff"
        private static byte[] decodeUnsynchronizedStream(BufferedBinaryReader from, int length)
        {
            const int BUFFER_SIZE = 8192;

            byte[] result = new byte[length];

            int bytesToRead;
            bool foundFF = false;

            byte[] readBuffer = new byte[BUFFER_SIZE];
            byte[] writeBuffer = new byte[BUFFER_SIZE];

            int written;
            int writtenTotal = 0;
            int remainingBytes;
            if (length > 0)
            {
                remainingBytes = (int)Math.Min(length, from.Length - from.Position);
            }
            else
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
                Array.Copy(writeBuffer, 0, result, writtenTotal, written);
                writtenTotal += written;

                remainingBytes -= bytesToRead;
            }

            Array.Resize(ref result, writtenTotal);

            return result;
        }

        // Copies the stream while unsynchronizing it (Cf. §5 of ID3v2.0 specs; §6 of ID3v2.3+ specs)
        // => every "0xff 0xex" becomes "0xff 0x00 0xex"; every "0xff 0x00" becomes "0xff 0x00 0x00"
        private static void encodeUnsynchronizedStreamTo(Stream from, BinaryWriter to)
        {
            /* TODO PERF : profile I/O speed using
             * 
             *   BinaryReader.readByte vs. BufferedBinaryReader.readByte vs. Stream.Read
             *   BinaryWriter.Write vs. Stream.WriteByte
             */
            
            byte[] data = new byte[2];
            long streamLength = from.Length;
            long position = from.Position;

            from.Read(data, 0, 1);
            position++;

            while (position < streamLength)
            {
                from.Read(data, 1, 1);
                position++;

                to.Write(data[0]);
                if (0xFF == data[0] && ( (0x00 == data[1]) || (0xE0 == (data[1] & 0xE0))))
                {
                    to.Write((byte)0);
                }
                data[0] = data[1];
            }
            to.Write(data[0]);
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