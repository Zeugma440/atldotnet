using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Commons;
using ATL.Logging;
using static ATL.TagData;

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
    ///     3. Unsynchronization and Unicode
    ///     
    ///     Little-endian UTF-16 BOM's are caught by the unsynchronization encoding, which "breaks" most tag readers.
    ///     Hence unsycnhronization is force-disabled when text encoding is Unicode.
    ///
    ///     4. Unsynchronization at frame level
    ///     
    ///     Even though ID3v2.4 allows it, ATL does not support "individual" unsynchronization at frame level
    ///     => Either the whole tag (all frames) is unsynchronized, or none is
    ///     
    ///     5. Prepended tag
    ///     
    ///     Even though specs allow ID3v2.4 tags to be located at the end of the file, I have yet to find a valid sample.
    ///     => Prepended tags are not supported until someone asks for it.
    ///     
    ///     6. Extended support for GEOB fields
    ///     
    ///     Current support for General Encapsulated Object (GEOB) fields are simplified that way :
    ///     - MIME-type is always "application/octet-stream" (read time + write time)
    ///     - File name is always empty (read time + write time)
    ///     - Frame encoding is always Latin-1 (write-time only)
    /// </summary>
    public class ID3v2 : MetaDataIO
    {
        /// <summary>
        /// ID3v2.2
        /// </summary>
        public const byte TAG_VERSION_2_2 = 2;

        /// <summary>
        /// ID3v2.3
        /// </summary>
        public const byte TAG_VERSION_2_3 = 3;

        /// <summary>
        /// ID3v2.4
        /// </summary>
        public const byte TAG_VERSION_2_4 = 4;

        private TagInfo tagHeader;

        // Technical 'shortcut' data
        private static readonly byte[] BOM_UTF16_LE = { 0xFF, 0xFE };
        private static readonly byte[] BOM_UTF16_BE = { 0xFE, 0xFF };
        private static readonly byte[] BOM_NONE = Array.Empty<byte>();

        private static readonly byte[] NULLTERMINATOR = { 0x00 };
        private static readonly byte[] NULLTERMINATOR_2 = { 0x00, 0x00 };

        // Tag flags
        private const byte FLAG_TAG_UNSYNCHRONIZED = 0b10000000;
        private const byte FLAG_TAG_HAS_EXTENDED_HEADER = 0b01000000;
        private const byte FLAG_TAG_HAS_FOOTER = 0b00010000;

        // Supported frame flags
        private const short FLAG_FRAME_24_UNSYNCHRONIZED = 0b0000000000000010; // ID3v2.4 only
        private const short FLAG_FRAME_24_HAS_DATA_LENGTH_INDICATOR = 0b0000000000000001; // ID3v2.4 only

        // Value separators
        private const char VALUE_SEPARATOR_22 = '/';
        private const char VALUE_SEPARATOR_24 = '\0';

        // ID3v2 tag ID
        private const string ID3V2_ID = "ID3";

        // Mapping between revisions and allowed encodings
        private static readonly IDictionary<int, ISet<Encoding>> allowedEncodings = new Dictionary<int, ISet<Encoding>>
            {
                { TAG_VERSION_2_2, new HashSet<Encoding> { Utils.Latin1Encoding, Encoding.Unicode } },
                { TAG_VERSION_2_3, new HashSet<Encoding> { Utils.Latin1Encoding, Encoding.Unicode } },
                { TAG_VERSION_2_4, new HashSet<Encoding> { Utils.Latin1Encoding, Encoding.Unicode, Encoding.UTF8, Encoding.BigEndianUnicode } }
            };

        // List of standard fields

#pragma warning disable S125 // Sections of code should not be commented out
        //private static readonly ICollection<string> standardFrames_v22 = new List<string>() { "BUF", "CNT", "COM", "CRA", "CRM", "ETC", "EQU", "GEO", "IPL", "LNK", "MCI", "MLL", "PIC", "POP", "REV", "RVA", "SLT", "STC", "TAL", "TBP", "TCM", "TCO", "TCR", "TDA", "TDY", "TEN", "TFT", "TIM", "TKE", "TLA", "TLE", "TMT", "TOA", "TOF", "TOL", "TOR", "TOT", "TP1", "TP2", "TP3", "TP4", "TPA", "TPB", "TRC", "TRD", "TRK", "TSI", "TSS", "TT1", "TT2", "TT3", "TXT", "TXX", "TYE", "UFI", "ULT", "WAF", "WAR", "WAS", "WCM", "WCP", "WPB", "WXX" };
#pragma warning restore S125 // Sections of code should not be commented out
        private static readonly ICollection<string> standardFrames_v23 = new HashSet<string> { "AENC", "APIC", "COMM", "COMR", "ENCR", "EQUA", "ETCO", "GEOB", "GRID", "IPLS", "LINK", "MCDI", "MLLT", "OWNE", "PRIV", "PCNT", "POPM", "POSS", "RBUF", "RVAD", "RVRB", "SYLT", "SYTC", "TALB", "TBPM", "TCOM", "TCON", "TCOP", "TDAT", "TDLY", "TENC", "TEXT", "TFLT", "TIME", "TIT1", "TIT2", "TIT3", "TKEY", "TLAN", "TLEN", "TMED", "TOAL", "TOFN", "TOLY", "TOPE", "TORY", "TOWN", "TPE1", "TPE2", "TPE3", "TPE4", "TPOS", "TPUB", "TRCK", "TRDA", "TRSN", "TRSO", "TSIZ", "TSRC", "TSSE", "TYER", "TXXX", "UFID", "USER", "USLT", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB", "WXXX", "CHAP", "CTOC" };
        private static readonly ICollection<string> standardFrames_v24 = new HashSet<string> { "AENC", "APIC", "ASPI", "COMM", "COMR", "ENCR", "EQU2", "ETCO", "GEOB", "GRID", "LINK", "MCDI", "MLLT", "OWNE", "PRIV", "PCNT", "POPM", "POSS", "RBUF", "RVA2", "RVRB", "SEEK", "SIGN", "SYLT", "SYTC", "TALB", "TBPM", "TCOM", "TCON", "TCOP", "TDEN", "TDLY", "TDOR", "TDRC", "TDRL", "TDTG", "TENC", "TEXT", "TFLT", "TIPL", "TIT1", "TIT2", "TIT3", "TKEY", "TLAN", "TLEN", "TMCL", "TMED", "TMOO", "TOAL", "TOFN", "TOLY", "TOPE", "TORY", "TOWN", "TPE1", "TPE2", "TPE3", "TPE4", "TPOS", "TPRO", "TPUB", "TRCK", "TRSN", "TRSO", "TSOA", "TSOP", "TSOT", "TSRC", "TSSE", "TSST", "TXXX", "UFID", "USER", "USLT", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB", "WXXX", "CHAP", "CTOC",
          "RGAD", "TCMP", "TSO2", "TSOC", "XRVA"  // Unoffical fields used by modern apps like itunes & Musicbrainz - see "Unofficial Frames Seen in the Wild" on ID3.org
        };

        // Field codes that need to be persisted in a COMMENT field
        private static readonly ISet<string> commentsFields = new HashSet<string> { "iTunNORM", "iTunSMPB", "iTunPGAP" };

        // Fields where text encoding descriptor byte is not required
        private static readonly ISet<string> noTextEncodingFields = new HashSet<string> { "POPM", "WCOM", "WCOP", "WOAF", "WOAR", "WOAS", "WORS", "WPAY", "WPUB" };

        // Fields which ID3v2.4 size descriptor is known to be misencoded by some implementation
        private static readonly ISet<string> misencodedSizev4Fields = new HashSet<string> { "CTOC" };

        // Fields allowed to have multiple values according to ID3v2.2-3 specs
        private static readonly ISet<string> multipleValuev23Fields = new HashSet<string> { "TP1", "TCM", "TXT", "TLA", "TOA", "TOL", "TCOM", "TEXT", "TOLY", "TOPE", "TPE1" };

        // Note on date field identifiers
        //
        // Original release date
        //   ID3v2.2 : TOR (year only)
        //   ID3v2.3 : TORY (year only)
        //   ID3v2.4 : TDOR (timestamp according to spec)
        //
        // Release date
        //   ID3v2.2 : no standard
        //   ID3v2.3 : no standard
        //   ID3v2.4 : TDRL (timestamp according to spec; actual content may vary)
        //
        // Recording date <== de facto standard behind the "date" field on most taggers
        //   ID3v2.2 : TYE (year), TDA (day & month - DDMM), TIM (hour & minute - HHMM)
        //   ID3v2.3 : TYER (year), TDAT (day & month - DDMM), TIME (hour & minute - HHMM)
        //   NB : Some loose implementations actually use TDRC inside ID3v2.3 headers (MediaMonkey, I'm looking at you...)
        //   ID3v2.4 : TDRC (timestamp)

        // Mapping between standard fields and ID3v2.2 identifiers
        private static readonly IDictionary<string, Field> frameMapping_v22 = new Dictionary<string, Field>
            {
                { "TT1", Field.GROUP },
                { "TT2", Field.TITLE },
                { "TT3", Field.GENERAL_DESCRIPTION },
                { "TP1", Field.ARTIST },
                { "TP2", Field.ALBUM_ARTIST },  // De facto standard, regardless of spec
                { "TP3", Field.CONDUCTOR },
                { "TOA", Field.ORIGINAL_ARTIST },
                { "TAL", Field.ALBUM },
                { "TOT", Field.ORIGINAL_ALBUM },
                { "TRK", Field.TRACK_NUMBER_TOTAL },
                { "TPA", Field.DISC_NUMBER_TOTAL },
                { "TYE", Field.RECORDING_YEAR },
                { "TDA", Field.RECORDING_DAYMONTH },
                { "TIM", Field.RECORDING_TIME },
                { "COM", Field.COMMENT },
                { "TCM", Field.COMPOSER },
                { "POP", Field.RATING },
                { "TCO", Field.GENRE },
                { "TCR", Field.COPYRIGHT },
                { "TPB", Field.PUBLISHER },
                { "TBP", Field.BPM },
                { "TEN", Field.ENCODED_BY },
                { "TOR", Field.ORIG_RELEASE_YEAR },
                { "TSS", Field.ENCODER },
                { "TLA", Field.LANGUAGE },
                { "TRC", Field.ISRC },
                { "WAS", Field.AUDIO_SOURCE_URL },
                { "TXT", Field.LYRICIST },
                { "IPL", Field.INVOLVED_PEOPLE }
            };

        // Mapping between standard fields and ID3v2.3 identifiers
        private static readonly IDictionary<string, Field> frameMapping_v23 = new Dictionary<string, Field>
            {
                { "TIT2", Field.TITLE },
                { "TPE1", Field.ARTIST },
                { "TPE2", Field.ALBUM_ARTIST }, // De facto standard, regardless of spec
                { "TPE3", Field.CONDUCTOR },
                { "TOPE", Field.ORIGINAL_ARTIST },
                { "TALB", Field.ALBUM },
                { "TOAL", Field.ORIGINAL_ALBUM },
                { "TRCK", Field.TRACK_NUMBER_TOTAL },
                { "TPOS", Field.DISC_NUMBER_TOTAL },
                { "TYER", Field.RECORDING_YEAR },
                { "TDAT", Field.RECORDING_DAYMONTH },
                { "TDRC", Field.RECORDING_DATE }, // Not part of ID3v2.3 standard, but sometimes found there anyway (MediaMonkey, I'm looking at you...)
                { "TIME", Field.RECORDING_TIME },
                { "COMM", Field.COMMENT },
                { "TCOM", Field.COMPOSER },
                { "POPM", Field.RATING },
                { "TCON", Field.GENRE },
                { "TCOP", Field.COPYRIGHT },
                { "TPUB", Field.PUBLISHER },
                { "CTOC", Field.CHAPTERS_TOC_DESCRIPTION },
                { "TSOA", Field.SORT_ALBUM },
                { "TSO2", Field.SORT_ALBUM_ARTIST }, // Not part of ID3v2.3 standard
                { "TSOP", Field.SORT_ARTIST },
                { "TSOT", Field.SORT_TITLE },
                { "TIT1", Field.GROUP },
                { "MVIN", Field.SERIES_PART}, // Not part of ID3v2.3 standard
                { "MVNM", Field.SERIES_TITLE }, // Not part of ID3v2.3 standard
                { "TDES", Field.LONG_DESCRIPTION }, // Not part of ID3v2.3 standard
                { "TIT3", Field.GENERAL_DESCRIPTION},
                { "TBPM", Field.BPM },
                { "TENC", Field.ENCODED_BY },
                { "TORY", Field.ORIG_RELEASE_YEAR},
                { "TSSE", Field.ENCODER },
                { "TLAN", Field.LANGUAGE },
                { "TSRC", Field.ISRC },
                { "CATALOGNUMBER", Field.CATALOG_NUMBER },
                { "WOAS", Field.AUDIO_SOURCE_URL },
                { "TEXT", Field.LYRICIST },
                { "IPLS", Field.INVOLVED_PEOPLE }
            };

        // Mapping between standard fields and ID3v2.4 identifiers
        private static readonly IDictionary<string, Field> frameMapping_v24 = new Dictionary<string, Field>
            {
                { "TIT2", Field.TITLE },
                { "TPE1", Field.ARTIST },
                { "TPE2", Field.ALBUM_ARTIST }, // De facto standard, regardless of spec
                { "TPE3", Field.CONDUCTOR },
                { "TOPE", Field.ORIGINAL_ARTIST },
                { "TALB", Field.ALBUM },
                { "TOAL", Field.ORIGINAL_ALBUM },
                { "TRCK", Field.TRACK_NUMBER_TOTAL },
                { "TPOS", Field.DISC_NUMBER_TOTAL },
                { "TDRC", Field.RECORDING_DATE },
                { "COMM", Field.COMMENT },
                { "TCOM", Field.COMPOSER },
                { "POPM", Field.RATING },
                { "TCON", Field.GENRE },
                { "TCOP", Field.COPYRIGHT },
                { "TPUB", Field.PUBLISHER },
                { "CTOC", Field.CHAPTERS_TOC_DESCRIPTION },
                { "TDRL", Field.PUBLISHING_DATE },
                { "TSOA", Field.SORT_ALBUM },
                { "TSO2", Field.SORT_ALBUM_ARTIST }, // Not part of ID3v2.4 standard
                { "TSOP", Field.SORT_ARTIST },
                { "TSOT", Field.SORT_TITLE },
                { "TIT1", Field.GROUP },
                { "MVIN", Field.SERIES_PART}, // Not part of ID3v2.4 standard
                { "MVNM", Field.SERIES_TITLE }, // Not part of ID3v2.4 standard
                { "TDES", Field.LONG_DESCRIPTION }, // Not part of ID3v2.4 standard
                { "TIT3", Field.GENERAL_DESCRIPTION},
                { "TBPM", Field.BPM },
                { "TENC", Field.ENCODED_BY },
                { "TDOR", Field.ORIG_RELEASE_DATE },
                { "TSSE", Field.ENCODER },
                { "TLAN", Field.LANGUAGE},
                { "TSRC", Field.ISRC},
                { "CATALOGNUMBER", Field.CATALOG_NUMBER },
                { "WOAS", Field.AUDIO_SOURCE_URL},
                { "TEXT", Field.LYRICIST },
                { "TIPL", Field.INVOLVED_PEOPLE }
            };

        // Mapping between ID3v2.2/3 fields and ID3v2.4 fields not included in frameMapping_v2x, and that have changed between versions
        private static readonly IDictionary<string, string> frameMapping_v22_4 = new Dictionary<string, string>
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
        private static readonly IDictionary<string, string> frameMapping_v23_4 = new Dictionary<string, string>
            {
                { "EQUA", "EQU2" },
                { "IPLS", "TIPL" },
                { "RVAD", "RVA2" },
                { "TORY", "TDOR" } // yyyy is a valid timestamp
                // TYER, TDAT and TIME are converted on the fly when writing
            };

        // Mapping between ID3v2.2/4 fields and ID3v2.3 fields not included in frameMapping_v2x, and that have changed between versions (populated at runtime)
        private static readonly IDictionary<string, string> frameMapping_v22_3 = new Dictionary<string, string>();
        private static readonly IDictionary<string, string> frameMapping_v24_3 = new Dictionary<string, string>();

        // Frame header (universal)
        private sealed class FrameHeader
        {
            public string ID;                           // Frame ID
            public int Size;                            // Size excluding header
            public ushort Flags;                        // Flags
        }

        // ID3v2 header data - for internal use
        private sealed class TagInfo
        {
            // Real structure of ID3v2 header
            public byte[] ID = new byte[3];                            // Always "ID3"
            public byte Version;                                     // Version number
            public byte Revision;                                   // Revision number
            public byte Flags;                                         // Flags of tag
            public byte[] Size = new byte[4];             // Tag size excluding header
            // Extended data
            public long FileSize;                                 // File size (bytes)
            public long HeaderEnd;                           // End position of header
            public long PaddingOffset = -1;
            public long ActualEnd;     // End position of entire tag (including all padding bytes)

            // Extended header flags
            public int ExtendedHeaderSize;
            public int ExtendedFlags;
            public int CRC = -1;
            public int TagRestrictions = -1;


            // **** BASE HEADER PROPERTIES ****
            public bool UsesUnsynchronisation => (Flags & FLAG_TAG_UNSYNCHRONIZED) > 0;

            public bool HasExtendedHeader => (Flags & FLAG_TAG_HAS_EXTENDED_HEADER) > 0 && Version > TAG_VERSION_2_2; // Determinated from flags; indicates if tag has an extended header (ID3v2.3+)

            private bool HasFooter => (Flags & FLAG_TAG_HAS_FOOTER) > 0; // Determinated from flags; indicates if tag has a footer (ID3v2.4+)

            public int GetSize(bool includeFooter = true)
            {
                // Get total tag size
                int result = StreamUtils.DecodeSynchSafeInt32(Size) + 10; // 10 being the size of the header

                if (includeFooter && HasFooter) result += 10; // Indicates the presence of a footer (ID3v2.4+)

                if (result > FileSize) result = 0;

                return result;
            }
            public long GetPaddingSize()
            {
                return ActualEnd - PaddingOffset;
            }


            // **** EXTENDED HEADER PROPERTIES ****
#pragma warning disable S2437 // Silly bit operations should not be performed
            public int TagFramesRestriction
            {
                get
                {
                    return ((TagRestrictions & 0xC0) >> 6) switch
                    {
                        0 => 128,
                        1 => 64,
                        2 => 32,
                        3 => 32,
                        _ => -1
                    };
                }
            }
            public int TagSizeRestrictionKB
            {
                get
                {
                    return ((TagRestrictions & 0xC0) >> 6) switch
                    {
                        0 => 1024,
                        1 => 128,
                        2 => 40,
                        3 => 4,
                        _ => -1
                    };
                }
            }

            // public bool HasTextEncodingRestriction => (TagRestrictions & 0x20) >> 5 > 0;

            public int TextFieldSizeRestriction
            {
                get
                {
                    return ((TagRestrictions & 0x18) >> 3) switch
                    {
                        0 => -1,
                        1 => 1024,
                        2 => 128,
                        3 => 30,
                        _ => -1
                    };
                }
            }
            public bool HasPictureEncodingRestriction => (TagRestrictions & 0x04) >> 2 > 0;

            public int PictureSizeRestriction
            {
                get
                {
                    return (TagRestrictions & 0x03) switch
                    {
                        0 => -1,    // No restriction
                        1 => 256,   // 256x256 or less
                        2 => 63,    // 64x64 or less
                        3 => 64,    // Exactly 64x64
                        _ => -1
                    };
                }
            }
        }
#pragma warning restore S2437 // Silly bit operations should not be performed

        // Unicode BOM properties
        private sealed class BomProperties
        {
            public bool Found = false;          // BOM found
            public int Size = 0;                // Size of BOM
            public Encoding Encoding;           // Corresponding encoding
        }

        private sealed class RichStructure
        {
            // Comments and unsynch'ed lyrics
            public string LanguageCode;
            public string ContentDescriptor;
            public int Size;
            // Synch'ed lyrics
            public byte TimestampFormat;
            public byte ContentType;
        }


        // --------------- OPTIONAL INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                Format format = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("id3v2")[0]);
                format.Name = format.Name + "." + m_tagVersion;
                format.ID += m_tagVersion;
                return new List<Format>(new[] { format });
            }
        }


        // --------------- MANDATORY INFORMATIVE OVERRIDES

        /// <inheritdoc/>
        protected override int getDefaultTagOffset()
        {
            return TO_BOF; // Specs allow the ID3v2 tag to be located at the end of the audio (see §5 "Tag location" of specs)
        }

        /// <inheritdoc/>
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.ID3V2;
        }

        /// <inheritdoc/>
        // Actually 3 or 4 when strictly applying ID3v2.3 / ID3v2.4 specs, but thanks to TXXX fields, any code is supported
        public override byte FieldCodeFixedLength => 0;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;
        /// <inheritdoc/>
        protected override bool supportsSynchronizedLyrics => true;
        /// <inheritdoc/>
        protected override bool supportsPictures => true;


        // ********************* Auxiliary functions & voids ********************

#pragma warning disable S3963 // "static" fields should be initialized inline; removed because this initialization is dynamic and can't be done inline as there already are inline initializers
        static ID3v2()
        {
            foreach (string s in frameMapping_v22_4.Keys)
            {
                frameMapping_v22_3.Add(s, frameMapping_v22_4[s]);
            }

            foreach (string s in frameMapping_v23_4.Keys)
            {
                frameMapping_v24_3.Add(frameMapping_v23_4[s], s);
                if (frameMapping_v22_3.ContainsKey(frameMapping_v23_4[s])) frameMapping_v22_3[frameMapping_v23_4[s]] = s;
            }
        }
#pragma warning restore S3963 // "static" fields should be initialized inline

        /// <summary>
        /// Constructor
        /// </summary>
        public ID3v2()
        {
            ResetData();
        }

        /// <inheritdoc/>
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            if (ID.Length < 5) ID = ID.ToUpper(); // Preserve the case of non-standard ID3v2 fields -- TODO : use the TagData.Origin property !

            // Finds the ATL field identifier according to the ID3v2 version
            switch (tagVersion)
            {
                case TAG_VERSION_2_2:
                    if (frameMapping_v22.TryGetValue(ID, out var value)) supportedMetaId = value;
                    break;
                case TAG_VERSION_2_3:
                    if (frameMapping_v23.TryGetValue(ID, out var value1)) supportedMetaId = value1;
                    break;
                case TAG_VERSION_2_4:
                    if (frameMapping_v24.TryGetValue(ID, out var value2)) supportedMetaId = value2;
                    break;
            }

            return supportedMetaId;
        }

        /// <inheritdoc/>
        protected override bool canHandleNonStandardField(string code, string value)
        {
            return true; // Will be transformed to a TXXX field
        }

        internal static bool IsValidHeader(byte[] data)
        {
            if (data.Length < 3) return false;
            return Utils.Latin1Encoding.GetString(data).StartsWith(ID3V2_ID);
        }

        private static bool readHeader(BufferedBinaryReader SourceFile, TagInfo Tag, long offset)
        {
            // Reads mandatory (base) header
            SourceFile.Seek(offset, SeekOrigin.Begin);
            Tag.ID = SourceFile.ReadBytes(3);

            if (!IsValidHeader(Tag.ID)) return false;

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
                if ((Tag.ExtendedFlags & 32) > 0) // CRC present
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

            return true;
        }

        private static RichStructure readCommentStructure(
            BufferedBinaryReader source,
            int tagVersion,
            int encodingCode,
            Encoding encoding,
            int dataSize)
        {
            RichStructure result = new RichStructure();
            long initialPos = source.Position;

            // Langage ID
            result.LanguageCode = Utils.Latin1Encoding.GetString(source.ReadBytes(3));

            // Content description
            Encoding contentDescriptionEncoding = encoding;
            if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode || 2 == encodingCode))
            {
                BomProperties bom = readBOM(source);
                if (bom.Found) contentDescriptionEncoding = bom.Encoding;
            }
            result.ContentDescriptor = StreamUtils.ReadNullTerminatedString(source, contentDescriptionEncoding);
            result.Size = (int)(source.Position - initialPos);

            // Ignore malformed comment
            if (result.Size >= dataSize)
            {
                // Get to the physical end of the frame
                source.Position = initialPos + dataSize;
                result.Size = dataSize;
            }

            return result;
        }

        private static RichStructure readSynchedLyricsStructure(BufferedBinaryReader source, int tagVersion, int encodingCode, Encoding encoding)
        {
            RichStructure result = new RichStructure();
            long initialPos = source.Position;

            // Langage ID
            result.LanguageCode = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
            result.TimestampFormat = source.ReadByte();
            result.ContentType = source.ReadByte();

            // Content description
            Encoding contentDescriptionEncoding = encoding;
            if (tagVersion > TAG_VERSION_2_2 && (1 == encodingCode))
            {
                BomProperties bom = readBOM(source);
                if (bom.Found) contentDescriptionEncoding = bom.Encoding;
            }
            result.ContentDescriptor = StreamUtils.ReadNullTerminatedString(source, contentDescriptionEncoding);
            result.Size = (int)(source.Position - initialPos);

            return result;
        }

        private static LyricsInfo.LyricsPhrase readLyricsPhrase(BufferedBinaryReader source, Encoding encoding)
        {
            // Skip the newline char positioned by SYLT Editor
            if (10 != source.ReadByte()) source.Position--;

            string text = StreamUtils.ReadNullTerminatedString(source, encoding);
            int timestamp = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            return new LyricsInfo.LyricsPhrase(timestamp, text);
        }

        private bool readFrame(
        BufferedBinaryReader source,
        TagInfo tag,
        ReadTagParams readTagParams,
        ref IList<MetaFieldInfo> comments,
        bool inChapter = false)
        {
            FrameHeader frame = new FrameHeader();
            byte encodingCode;

            long initialTagPos = source.Position;

            ChapterInfo chapter = null;
            MetaFieldInfo comment = null;

            bool inLyrics = false;


            // Read frame header and check frame ID
            frame.ID = TAG_VERSION_2_2 == m_tagVersion ? Utils.Latin1Encoding.GetString(source.ReadBytes(3)) : Utils.Latin1Encoding.GetString(source.ReadBytes(4));

            if (!char.IsLetter(frame.ID[0]) || !char.IsUpper(frame.ID[0]))
            {
                // We might be at the beginning of a padding frame
                if (0 == frame.ID[0] + frame.ID[1] + frame.ID[2])
                {
                    tag.PaddingOffset = initialTagPos;
                    tag.ActualEnd = readTagParams.ExtraID3v2PaddingDetection ? StreamUtils.TraversePadding(source) :
                    Math.Min(tag.GetSize(false), StreamUtils.TraversePadding(source));
                }
                else // If not, we're in the wrong place
                {
                    LogDelegator.GetLogDelegate()(Log.LV_ERROR, "Valid frame not found where expected; parsing interrupted");
                    source.Seek(initialTagPos - tag.HeaderEnd + tag.GetSize(false), SeekOrigin.Begin);
                    return false;
                }
            }

            if (tag.ActualEnd > -1) return false;

            /*
            Frame size measures number of bytes between end of flag and end of payload 

            Frame size encoding conventions
            ID3v2.2 : 3 bytes
            ID3v2.3 : 4 bytes (plain integer)
            ID3v2.4 : synch-safe Int32
            */
            switch (m_tagVersion)
            {
                case TAG_VERSION_2_2:
                    frame.Size = StreamUtils.DecodeBEInt24(source.ReadBytes(3));
                    break;
                case TAG_VERSION_2_3:
                    frame.Size = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                    break;
                case TAG_VERSION_2_4:
                    long sizePosition = source.Position;
                    byte[] sizeDescriptor = source.ReadBytes(4);
                    frame.Size = StreamUtils.DecodeSynchSafeInt32(sizeDescriptor);
                    // Important: Certain implementation of ID3v2.4 still use the ID3v2.3 size descriptor (e.g.FFMpeg 4.3.1 for the CTOC frame).
                    // => We have to test reading the frame to "guess" which convention its size descriptor uses
                    // when its size is described on more than 1 byte
                    //
                    // If the size descriptor, read as a plain integer, is larger than the whole tag size, we should keep it as a synch safe int
                    if (sizeDescriptor[2] + sizeDescriptor[1] + sizeDescriptor[0] > 0
                        && misencodedSizev4Fields.Contains(frame.ID)
                        && StreamUtils.DecodeBEInt32(sizeDescriptor) < tag.GetSize(false))
                    {
                        // Check if the end of the frame is immediately followed by 4 uppercase chars or by padding chars
                        // If not, try again by reading frame size as a plain integer
                        source.Seek(sizePosition + 6 + frame.Size, SeekOrigin.Begin);
                        string frameId = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                        if (!isUpperAlpha(frameId) && !frameId.Equals("\0\0\0\0")) frame.Size = StreamUtils.DecodeBEInt32(sizeDescriptor);
                        source.Seek(sizePosition + 4, SeekOrigin.Begin);
                    }
                    break;
            }

            frame.Flags = TAG_VERSION_2_2 == m_tagVersion ? (ushort)0 : StreamUtils.DecodeBEUInt16(source.ReadBytes(2));

            var dataSize = frame.Size;

            // Skips data size indicator if signaled by the flag
            if ((frame.Flags & 1) > 0)
            {
                source.Seek(4, SeekOrigin.Current);
                dataSize -= 4;
            }

            // Detect frame-level settings
            //var isCompressed = (frame.Flags & 8) > 0; Unused for now
            var isUnsynchronized = tag.UsesUnsynchronisation || (frame.Flags & 2) > 0;

            if (!noTextEncodingFields.Contains(frame.ID))
            {
                dataSize--; // Minus encoding byte
                encodingCode = source.ReadByte();
            }
            else
            {
                encodingCode = 0; // Latin-1; default according to spec
            }
            var frameEncoding = decodeID3v2CharEncoding(encodingCode);

            // COMM and USLT/ULT fields contain :
            //   a 3-byte langage ID
            //   a "short content description", as an encoded null-terminated string
            //   the actual data (i.e. comment or lyrics), as an encoded, null-terminated string
            // => lg lg lg (BOM) (encoded description) 00 (00) (BOM) encoded text 00 (00)
            if (frame.ID.StartsWith("COM") || frame.ID.StartsWith("USL") || frame.ID.StartsWith("ULT"))
            {
                RichStructure structure = readCommentStructure(source, m_tagVersion, encodingCode, frameEncoding, dataSize);

                if (frame.ID.StartsWith("COM"))
                {
                    comments ??= new List<MetaFieldInfo>();
                    comment = new MetaFieldInfo(getImplementedTagType(), "")
                    {
                        Language = structure.LanguageCode,
                        NativeFieldCode = structure.ContentDescriptor
                    };
                }
                else if (frame.ID.StartsWith("USL") || frame.ID.StartsWith("ULT"))
                {
                    tagData.Lyrics ??= new List<LyricsInfo>();
                    LyricsInfo info = new LyricsInfo();
                    tagData.Lyrics.Add(info);
                    info.LanguageCode = structure.LanguageCode;
                    info.Description = structure.ContentDescriptor;
                    info.Format = LyricsInfo.LyricsFormat.UNSYNCHRONIZED;
                    inLyrics = true;
                }

                dataSize -= structure.Size;
            }
            else if (frame.ID.StartsWith("SYL")) // Synch'ed lyrics
            {
                RichStructure structure = readSynchedLyricsStructure(source, m_tagVersion, encodingCode, frameEncoding);
                tagData.Lyrics ??= new List<LyricsInfo>();
                LyricsInfo info = new LyricsInfo();
                tagData.Lyrics.Add(info);
                info.LanguageCode = structure.LanguageCode;
                info.Description = structure.ContentDescriptor;
                info.ContentType = (LyricsInfo.LyricsType)structure.ContentType;
                inLyrics = true;

                dataSize -= structure.Size;
            }

            // A $01 "Unicode" encoding flag means the presence of a BOM (Byte Order Mark)
            // NB : Even if it's not part of the spec, BOMs may appear on ID3v2.2 tags
            if (1 == encodingCode)
            {
                BomProperties bom = readBOM(source);
                if (bom.Found)
                {
                    frameEncoding = bom.Encoding;
                    dataSize -= bom.Size;
                }
            }

            // If encoding > 3, we might have caught an actual character, which means there is no encoding flag => switch to default encoding
            if (encodingCode > 3)
            {
                frameEncoding = decodeID3v2CharEncoding(0);
                source.Seek(-1, SeekOrigin.Current);
                dataSize++;
            }

            // General Encapsulated Object
            if (frame.ID.StartsWith("GEO"))
            {
                long geoStartOffset = source.Position;
                StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding); // Mime-type; unused
                StreamUtils.ReadNullTerminatedString(source, frameEncoding); // File name; unused
                string cDesc = StreamUtils.ReadNullTerminatedString(source, frameEncoding); // Content description
                frame.ID += "." + cDesc;
                dataSize -= (int)(source.Position - geoStartOffset);
                frameEncoding = Utils.Latin1Encoding; // Decode encapsulated object using Latin-1
            }

            // General Encapsulated Object
            if (frame.ID.StartsWith("PRIV"))
            {
                long privStartOffset = source.Position;
                string owner = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);
                frame.ID += "." + owner;
                dataSize -= (int)(source.Position - privStartOffset);
                frameEncoding = Utils.Latin1Encoding; // Decode private data using Latin-1
            }


            // == READ ACTUAL FRAME DATA
            var dataOffset = source.Position;

            if (dataSize >= 0 && dataSize < source.Length)
            {
                if (!("PIC".Equals(frame.ID) || "APIC".Equals(frame.ID))) // Not a picture frame
                {
                    string strData;

                    // Specific to Popularitymeter : Rating data has to be extracted from the POPM block
                    if (frame.ID.StartsWith("POP"))
                    {
                        // ID3v2.2 : According to spec (see paragraph3.2), encoding should actually be ISO-8859-1
                        // ID3v2.3+ : Spec is unclear whether to read as ISO-8859-1 or not. Practice indicates using this convention is safer.
                        strData = readRatingInPopularityMeter(source, Utils.Latin1Encoding).ToString();
                    }
                    else if (frame.ID.StartsWith("TXX"))
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes(dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));

                        string[] tabS = strData.Split('\0');
                        frame.ID = tabS[0];
                        // Handle multiple values (ID3v2.4 only, theoretically)
                        if (tabS.Length > 1) strData = string.Join(Settings.InternalValueSeparator + "", tabS, 1, tabS.Length - 1);
                        else strData = ""; //if the 2nd part of the array isn't there, value is non-existent (TXXX...KEY\0\0 or TXXX...KEY\0)

                        // If unicode is used, there might be BOMs converted to 'ZERO WIDTH NO-BREAK SPACE' character
                        // (pattern : TXXX-stuff-BOM-ID-\0-BOM-VALUE-\0-BOM-VALUE-\0)
                        if (1 == encodingCode) strData = strData.Replace(Utils.UNICODE_INVISIBLE_EMPTY, "");
                    }
                    else if (frame.ID.StartsWith("CTO")) // Chapters table of contents -> store chapter description
                    {
                        StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding); // Skip element ID
                        source.Seek(1, SeekOrigin.Current); // Skip flags
                        int entryCount = source.ReadByte();
                        for (int i = 0; i < entryCount; i++) StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding); // Skip chapter element IDs
                                                                                                                                 // There's an optional header here
                        if (source.Position - dataOffset < frame.Size && "TIT2".Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(4)), StringComparison.OrdinalIgnoreCase))
                        {
                            source.Seek(6, SeekOrigin.Current); // Skip size and flags
                            encodingCode = source.ReadByte();
                            Encoding encoding = decodeID3v2CharEncoding(encodingCode);
                            if (m_tagVersion > TAG_VERSION_2_2 && (1 == encodingCode || 2 == encodingCode)) readBOM(source);
                            strData = StreamUtils.ReadNullTerminatedStringFixed(source, encoding, (int)(dataOffset + frame.Size - source.Position));
                        }
                        else
                        {
                            strData = "";
                        }
                    }
                    else if (frame.ID.StartsWith("CHA")) // Chapters
                    {
                        tagData.Chapters ??= new List<ChapterInfo>();
                        chapter = new ChapterInfo();
                        tagData.Chapters.Add(chapter);

                        long initPos = source.Position;
                        chapter.UniqueID = StreamUtils.ReadNullTerminatedString(source, frameEncoding);

                        chapter.StartTime = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.EndTime = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.StartOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));
                        chapter.EndOffset = StreamUtils.DecodeBEUInt32(source.ReadBytes(4));

                        chapter.UseOffset = chapter.StartOffset != uint.MaxValue;

                        long remainingData = dataSize - (source.Position - initPos);
                        while (remainingData > 0)
                        {
                            if (!readFrame(source, tag, readTagParams, ref comments, true)) break;
                            remainingData = dataSize - (source.Position - initPos);
                        } // End chapter frames loop

                        strData = "";
                    }
                    else if (frame.ID.StartsWith("SYL")) // Synch'ed lyrics
                    {
                        long initPos = source.Position;
                        long remainingData = dataSize - (source.Position - initPos);

                        LyricsInfo info = tagData.Lyrics[^1];
                        info.Format = LyricsInfo.LyricsFormat.SYNCHRONIZED;
                        while (remainingData > 0)
                        {
                            info.SynchronizedLyrics.Add(readLyricsPhrase(source, frameEncoding));
                            remainingData = dataSize - (source.Position - initPos);
                        }
                        strData = "";
                    }
                    else if (frame.ID.StartsWith("WXX")) // Custom URL
                    {
                        var terminator = getNullTerminatorFromEncoding(frameEncoding);
                        // Find the separator
                        if (StreamUtils.FindSequence(source, terminator, dataSize))
                        {
                            var secondPartOffset = source.Position;
                            var firstPartSize = (int)(secondPartOffset - terminator.Length - dataOffset);
                            source.Seek(dataOffset, SeekOrigin.Begin);
                            byte[] bData = source.ReadBytes(firstPartSize);
                            // Description encoded with current encoding
                            strData = frameEncoding.GetString(bData) + Settings.InternalValueSeparator;
                            var secondPartSize = dataSize - firstPartSize - terminator.Length;
                            if (secondPartSize > 0)
                            {
                                source.Seek(secondPartOffset, SeekOrigin.Begin);
                                bData = source.ReadBytes(secondPartSize);
                                // URL encoded in ISO-8859-1
                                strData += Utils.Latin1Encoding.GetString(bData);
                            }
                        }
                        else
                        { // No separator (bad formatting) : take one single string with current encoding
                            byte[] bData = source.ReadBytes(dataSize);
                            strData = frameEncoding.GetString(bData) + Settings.InternalValueSeparator;
                        }
                    }
                    else
                    {
                        // Read frame data and set tag item if frame supported
                        byte[] bData = source.ReadBytes(dataSize);
                        strData = Utils.StripEndingZeroChars(frameEncoding.GetString(bData));

                        // Parse GENRE frame
                        if (isGenreField(frame.ID, m_tagVersion)) strData = extractGenreFromID3v2Code(strData);
                        // Parse Involved People frame for ID3v2.2-2.3 (IPL/IPLS) where value separator is a \0
                        else if (frame.ID.StartsWith("IPL"))
                        {
                            string[] parts = strData.Trim().Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                            // Individual parts of that field may have a BOM
                            if (1 == encodingCode)
                            {
                                string bomAsStr = frameEncoding.GetString(getBomFromEncoding(frameEncoding));
                                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Replace(bomAsStr, "");
                            }
                            strData = string.Join(Settings.InternalValueSeparator + "", parts);
                        }
                        // Handle multiple values on text information frames
                        else if (frame.ID.StartsWith('T'))
                        {
                            string[] parts = null;
                            if (TAG_VERSION_2_4 == m_tagVersion) // All text information frames may contain multiple values on ID3v2.4
                            {
                                parts = strData.Trim().Split(new char[] { VALUE_SEPARATOR_24 }, StringSplitOptions.RemoveEmptyEntries);
                            }
                            // Only specific text information frames may contain multiple values on ID3v2.2-3
                            else if (multipleValuev23Fields.Contains(frame.ID) && Settings.ID3v2_separatev2v3Values)
                            {
                                parts = strData.Trim().Split(new char[] { VALUE_SEPARATOR_22 }, StringSplitOptions.RemoveEmptyEntries);
                            }
                            if (parts != null) strData = string.Join(Settings.InternalValueSeparator + "", parts);
                        }
                    }

                    if (inLyrics)
                    {
                        if (strData.Length > 0) setMetaField(Field.LYRICS_UNSYNCH, strData);
                    }
                    else if (null == comment && null == chapter) // We're in a non-Comment, non-Chapter field => directly store value
                    {
                        if (!inChapter) SetMetaField(frame.ID, strData, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                        else
                        {
                            chapter = tagData.Chapters[^1];
                            switch (frame.ID)
                            {
                                case "TIT2": chapter.Title = strData; break;
                                case "TIT3": chapter.Subtitle = strData; break;
                                case "WXXX":
                                    string[] parts = strData.Split(Settings.InternalValueSeparator);
                                    chapter.Url = new ChapterInfo.UrlInfo(parts[0], parts[1]);
                                    break;
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

                    if (TAG_VERSION_2_2 == m_tagVersion) source.Seek(dataOffset + dataSize, SeekOrigin.Begin);
                }
                else // Picture frame
                {
                    long position = source.Position;
                    if (TAG_VERSION_2_2 == m_tagVersion)
                    {
                        // Image format
                        source.Seek(3, SeekOrigin.Current);
                    }
                    else
                    {
                        // mime-type always coded in ASCII
                        if (1 == encodingCode) source.Seek(-1, SeekOrigin.Current);
                        // Mime-type
                        StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);
                    }

                    byte picCode = source.ReadByte();
                    // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                    PictureInfo.PIC_TYPE picType = DecodeID3v2PictureType(picCode);

                    int picturePosition = 0;
                    if (!inChapter)
                    {
                        if (picType.Equals(PictureInfo.PIC_TYPE.Unsupported))
                        {
                            picturePosition = takePicturePosition(MetaDataIOFactory.TagType.ID3V2, picCode);
                        }
                        else
                        {
                            picturePosition = takePicturePosition(picType);
                        }
                    }

                    // Image description
                    // Description may be coded with another convention
                    if (m_tagVersion > TAG_VERSION_2_2 && (1 == encodingCode)) readBOM(source);
                    string description = StreamUtils.ReadNullTerminatedString(source, frameEncoding);

                    if (readTagParams.ReadPictures || inChapter)
                    {
                        int picSize = dataSize - (int)(source.Position - position);

                        byte[] data;
                        if (isUnsynchronized)
                        {
                            data = decodeUnsynchronizedStream(source, picSize);
                        }
                        else
                        {
                            using MemoryStream outStream = new MemoryStream(picSize);
                            StreamUtils.CopyStream(source, outStream, picSize);
                            data = outStream.ToArray();
                        }
                        PictureInfo picInfo = PictureInfo.fromBinaryData(data, picType, getImplementedTagType(), picCode, picturePosition);
                        picInfo.Description = description;

                        if (!inChapter)
                        {
                            tagData.Pictures.Add(picInfo);
                        }
                        else
                        {
                            tagData.Chapters[^1].Picture = picInfo;
                        }
                    }
                } // Picture frame
                source.Seek(dataOffset + dataSize, SeekOrigin.Begin);
            }
            else // Data size <= 0 or larger than file
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Frame " + frame.ID + " has an invalid size : " + dataSize);
                return false;
            }

            return true;
        }

        // Get information from frames (universal)
        private void readFrames(BufferedBinaryReader source, TagInfo tag, long offset, ReadTagParams readTagParams)
        {
            long streamLength = source.Length;

            tag.PaddingOffset = -1;
            tag.ActualEnd = -1;

            IList<MetaFieldInfo> comments = new List<MetaFieldInfo>();

            source.Seek(tag.HeaderEnd, SeekOrigin.Begin);
            long streamPos = source.Position;

            while (streamPos - offset < tag.GetSize(false) && streamPos < streamLength)
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
                    string commentDescription = comm.NativeFieldCode.Trim().Replace(Utils.UNICODE_INVISIBLE_EMPTY, "");
                    if (commentDescription.Length > 0 && !commentDescription.Equals("comment", StringComparison.OrdinalIgnoreCase) && !commentDescription.Equals("no description", StringComparison.OrdinalIgnoreCase) && !commentDescription.Equals("description", StringComparison.OrdinalIgnoreCase))
                    {
                        // Processed as an additional field
                        SetMetaField(commentDescription, comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version, 0, comm.Language);
                        continue;
                    }

                    // Processed as a "classic" Comment
                    if (m_tagVersion > TAG_VERSION_2_2) SetMetaField("COMM", comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                    else SetMetaField("COM", comm.Value, readTagParams.ReadAllMetaFrames, FileStructureHelper.DEFAULT_ZONE_NAME, tag.Version);
                }
            }

            if (-1 == tag.ActualEnd) // No padding frame has been detected so far
            {
                // Prod to see if there's padding after the end of the tag
                if (readTagParams.ExtraID3v2PaddingDetection && streamPos + 4 < source.Length && 0 == source.ReadInt32())
                {
                    tag.PaddingOffset = streamPos;
                    tag.ActualEnd = StreamUtils.TraversePadding(source);
                }
                else
                {
                    tag.ActualEnd = streamPos;
                }
            }
        }

        // ********************** Public functions & voids **********************

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            return Read(source, readTagParams.Offset, readTagParams);
        }

        /// <summary>
        /// Read ID3v2 data
        /// </summary>
        /// <param name="source">Reader object to read ID3v2 data from</param>
        /// <param name="offset">ID3v2 header offset (mostly 0, except for specific audio containers such as AIFF or DSF)</param>
        /// <param name="readTagParams">Reading parameters</param>
        /// <returns>True if the reading operation has succeeded; false if not</returns>
        public bool Read(Stream source, long offset, ReadTagParams readTagParams)
        {
            tagHeader = new TagInfo();

            BufferedBinaryReader reader = new BufferedBinaryReader(source);

            // Reset data and load header from file to variable
            ResetData();
            bool result = readHeader(reader, tagHeader, offset);

            tagData.PaddingSize = tagHeader.GetPaddingSize();

            // Process data if loaded and header valid
            if (result && IsValidHeader(tagHeader.ID))
            {
                // Fill properties with header data
                m_tagVersion = tagHeader.Version;

                // Get information from frames if version supported
                if (TAG_VERSION_2_2 <= m_tagVersion && m_tagVersion <= TAG_VERSION_2_4 && tagHeader.GetSize() > 0)
                {
                    readFrames(reader, tagHeader, offset, readTagParams);
                    structureHelper.AddZone(offset, (int)(tagHeader.ActualEnd - offset));
                }
                else
                {
                    if (m_tagVersion < TAG_VERSION_2_2 || m_tagVersion > TAG_VERSION_2_4) LogDelegator.GetLogDelegate()(Log.LV_ERROR, "ID3v2 tag version unknown : " + m_tagVersion + "; parsing interrupted");
                    if (0 == Size) LogDelegator.GetLogDelegate()(Log.LV_ERROR, "ID3v2 size is zero; parsing interrupted");
                }
            }

            return result;
        }

        /// <summary>
        /// Writes the given tag into the given Writer using ID3v2.4 conventions
        /// </summary>
        /// <param name="tag">Tag information to be written</param>
        /// <param name="s">Stream to write tag information to</param>
        /// <param name="zone">Name of the zone that is currently being written</param>
        /// <returns>True if writing operation succeeded; false if not</returns>
        protected override int write(TagData tag, Stream s, string zone)
        {
            using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
            return write(tag, w);
        }

        private int write(TagData tag, BinaryWriter w)
        {
            if (Settings.ID3v2_tagSubVersion < 3 || Settings.ID3v2_tagSubVersion > 4)
                throw new NotImplementedException("Writing metadata with ID3v2." + Settings.ID3v2_tagSubVersion + " convention is not supported");

            w.Write(Utils.Latin1Encoding.GetBytes(ID3V2_ID));

            // Version 2.4.0
            if (3 == Settings.ID3v2_tagSubVersion) w.Write(TAG_VERSION_2_3);
            else if (4 == Settings.ID3v2_tagSubVersion) w.Write(TAG_VERSION_2_4);
            w.Write((byte)0);

            Encoding tagEncoding = Settings.DefaultTextEncoding;

            // Fallback to Unicode when target encoding isn't supported
            if (!allowedEncodings[Settings.ID3v2_tagSubVersion].Contains(tagEncoding)) tagEncoding = Encoding.Unicode;

            // Flags : keep initial flags unless unsynchronization if forced
            if (Settings.ID3v2_forceUnsynchronization) tagHeader.Flags = (byte)(tagHeader.Flags | FLAG_TAG_UNSYNCHRONIZED);

            // Little-endian UTF-16 BOM's are caught by the unsynchronization encoding, which "breaks" most tag readers
            // Hence unsycnhronization is force-disabled when the encoding is Unicode
            if (tagHeader.UsesUnsynchronisation && tagEncoding.Equals(Encoding.Unicode)) tagHeader.Flags = (byte)(tagHeader.Flags & ~FLAG_TAG_UNSYNCHRONIZED);

            // NB : Writing ID3v2.4 flags on an ID3v2.3 tag won't have any effect since v2.4 specific bits won't be exploited

            w.Write(tagHeader.Flags);
            // Keep position in mind to calculate final size and come back here to write it
            var tagSizePos = w.BaseStream.Position;
            w.Write(0); // Tag size placeholder to be rewritten in a few lines

            writeExtHeader(w);
            long headerEnd = w.BaseStream.Position;
            var result = writeFrames(tag, w, tagEncoding);

            // PADDING MANAGEMENT
            // TODO - if footer support is added, don't write padding since they are mutually exclusive (see specs)
            long paddingSizeToWrite;
            if (tag.PaddingSize > -1) paddingSizeToWrite = tag.PaddingSize;
            else paddingSizeToWrite = TrackUtils.ComputePaddingSize(
                tagHeader.PaddingOffset,                        // Initial tag size
                tagHeader.GetPaddingSize(),                     // Initial padding offset
                tagHeader.PaddingOffset - tagHeader.HeaderEnd,  // Initial padding size
                w.BaseStream.Position - headerEnd);             // Current tag size
            if (paddingSizeToWrite > 0)
            {
                if (3 == Settings.ID3v2_tagSubVersion) // Write size of padding
                {
                    long tmpPos = w.BaseStream.Position;
                    w.BaseStream.Seek(headerEnd - 4, SeekOrigin.Begin);
                    w.Write(StreamUtils.EncodeBEInt32((int)paddingSizeToWrite));
                    w.BaseStream.Seek(tmpPos, SeekOrigin.Begin);
                }

                for (long l = 0; l < paddingSizeToWrite; l++) w.Write((byte)0);
            }


            // Record final(*) size of tag into "tag size" field of header
            // (*) : Spec clearly states that the tag final size is tag size after unsynchronization
            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            var tagSize = (int)(finalTagPos - tagSizePos - 4);
            w.Write(StreamUtils.EncodeSynchSafeInt32(tagSize)); // Synch-safe int32 since ID3v2.3

            if (4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_useExtendedHeaderRestrictions && tagSize / 1024 > tagHeader.TagSizeRestrictionKB)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Tag is too large (" + tagSize / 1024 + "KB) according to ID3v2 restrictions (" + tagHeader.TagSizeRestrictionKB + ") !");
            }

            return result;
        }


        // TODO : Write ID3v2.4 footer
        // if footer support is added, don't write padding since they are mutually exclusive (see specs)

        private void writeExtHeader(BinaryWriter w)
        {
            // Rewrites extended header as is
            if (!tagHeader.HasExtendedHeader) return;

            w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.ExtendedHeaderSize, 4));

            switch (Settings.ID3v2_tagSubVersion)
            {
                case 4:
                    {
                        w.Write((byte)1); // Number of flag bytes; always 1 according to spec
                        w.Write(tagHeader.ExtendedFlags);
                        // A new CRC should be calculated according to actual tag contents instead of rewriting CRC as is -- NB : CRC perimeter definition given by specs is unclear
                        if (tagHeader.CRC > 0) w.Write(StreamUtils.EncodeSynchSafeInt(tagHeader.CRC, 5));
                        if (tagHeader.TagRestrictions > 0) w.Write(tagHeader.TagRestrictions);

                        /* TODO - to be reimplemented and tested with a proper unit test
                        if (4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_useExtendedHeaderRestrictions && tagHeader.HasTextEncodingRestriction && (!(tagEncoding.BodyName.Equals("iso-8859-1") || tagEncoding.BodyName.Equals("utf-8"))))
                        {
                            // Force default encoding if encoding restriction is enabled and current encoding is not among authorized types
                            tagEncoding = Settings.DefaultTextEncoding;
                        }
                        */
                        break;
                    }
                case 3:
                    w.Write(tagHeader.ExtendedFlags);
                    w.Write((byte)0); // Always 0 according to spec
                    w.Write(0);  // Size of padding
                    break;
            }
        }

        private int writeFrames(TagData tag, BinaryWriter w, Encoding tagEncoding)
        {
            int nbFrames = 0;

            // === ID3v2 FRAMES ===
            IDictionary<Field, string> map = tag.ToMap(true);

            // 1st pass to gather date information

            // "Recording date" fields are a bit tricky, since there is no 1-to-1 mapping between ID3v2.2/3 and ID3v2.4
            //   ID3v2.2 : TYE (year), TDA (day & month - DDMM), TIM (hour & minute - HHMM)
            //   ID3v2.3 : TYER (year), TDAT (day & month - DDMM), TIME (hour & minute - HHMM)
            //   ID3v2.4 : TDRC (timestamp)
            string recordingYear = "";
            string recordingDayMonth = "";
            string recordingTime = "";
            string recordingDate = "";

            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length <= 0) continue; // No frame with empty value

                switch (frameType)
                {
                    case Field.RECORDING_YEAR:
                        recordingYear = map[frameType];
                        break;
                    case Field.RECORDING_DAYMONTH:
                        recordingDayMonth = map[frameType];
                        break;
                    case Field.RECORDING_TIME:
                        recordingTime = map[frameType];
                        break;
                    case Field.RECORDING_DATE:
                        recordingDate = map[frameType];
                        break;
                }
            }

            if (4 == Settings.ID3v2_tagSubVersion && recordingYear.Length > 0)
            {
                // Make sure we don't erase an existing, same date with less detailed (year only) information
                if (0 == recordingDate.Length || !recordingDate.StartsWith(recordingYear))
                    map[Field.RECORDING_DATE] = TrackUtils.FormatISOTimestamp(recordingYear, recordingDayMonth, recordingTime);
            }
            else if (3 == Settings.ID3v2_tagSubVersion && recordingDate.Length > 3 && 0 == recordingYear.Length)
            {
                // Recording date valued for ID3v2.3 (possibly a migration from ID3v2.4 to ID3v2.3)
                map[Field.RECORDING_YEAR] = recordingDate[..4];
                if (recordingDate.Length > 9)
                {
                    map[Field.RECORDING_DAYMONTH] = recordingDate.Substring(8, 2) + recordingDate.Substring(5, 2);
                    if (recordingDate.Length > 15)
                    {
                        map[Field.RECORDING_TIME] = recordingDate.Substring(11, 2) + recordingDate.Substring(14, 2);
                    }
                }
                map.Remove(Field.RECORDING_DATE);
            }

            // "Original release date" fields are tricky too for the same reason
            //   ID3v2.2 : TOR (year)
            //   ID3v2.3 : TORY (year)
            //   ID3v2.4 : TDOR (timestamp)
            string origReleaseYear = "";
            string origReleaseDate = "";

            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length <= 0) continue; // No frame with empty value

                switch (frameType)
                {
                    case Field.ORIG_RELEASE_YEAR:
                        origReleaseYear = map[frameType];
                        break;
                    case Field.ORIG_RELEASE_DATE:
                        origReleaseDate = map[frameType];
                        break;
                }
            }
            if (4 == Settings.ID3v2_tagSubVersion && origReleaseDate.Length > 0)
            {
                // Make sure we don't erase an existing, same date with less detailed (year only) information
                if (0 == origReleaseDate.Length || !origReleaseDate.StartsWith(origReleaseYear))
                    map[Field.ORIG_RELEASE_DATE] = TrackUtils.FormatISOTimestamp(origReleaseYear, "0101", "");
            }
            else if (3 == Settings.ID3v2_tagSubVersion && origReleaseDate.Length > 3 && 0 == origReleaseYear.Length)
            {
                // Original release date valued for ID3v2.3 (possibly a migration from ID3v2.4 to ID3v2.3)
                map[Field.ORIG_RELEASE_YEAR] = origReleaseDate[..4];
                map.Remove(Field.ORIG_RELEASE_DATE);
            }



            IDictionary<string, Field> mapping = frameMapping_v24;
            if (3 == Settings.ID3v2_tagSubVersion) mapping = frameMapping_v23;
            // Keep these in memory to prevent setting them twice using AdditionalFields
            var writtenFieldCodes = new HashSet<string>();

            foreach (Field frameType in map.Keys)
            {
                if (map[frameType].Length <= 0) continue; // No frame with empty value
                foreach (string s in mapping.Keys)
                {
                    if (frameType != mapping[s]) continue;
                    if (s.Equals("CTOC")) continue; // CTOC (table of contents) is handled by writeChapters                            

                    string value = formatBeforeWriting(frameType, tag, map);
                    writeTextFrame(w, s, value, tagEncoding);
                    writtenFieldCodes.Add(s.ToUpper());
                    nbFrames++;
                    break;
                }
            }

            // Chapters
            if (tag.Chapters != null && tag.Chapters.Count > 0)
            {
                nbFrames += writeChapters(w, tag, tagEncoding);
            }

            // Lyrics
            if (tag.Lyrics != null)
            {
                foreach (LyricsInfo lyricsInfo in tag.Lyrics)
                    nbFrames += writeLyrics(w, lyricsInfo, tagEncoding);
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
            {
                if (!writtenFieldCodes.Contains(fieldInfo.NativeFieldCode.ToUpper()))
                {
                    var fieldCode = fieldInfo.NativeFieldCode;
                    if (fieldCode.Equals(VorbisTag.VENDOR_METADATA_ID)) continue; // Specific mandatory field exclusive to VorbisComment

                    // We're writing with ID3v2.4 standard. Some standard frame codes have to be converted from ID3v2.2/3 to ID3v4
                    if (4 == Settings.ID3v2_tagSubVersion)
                    {
                        if (TAG_VERSION_2_2 == m_tagVersion)
                        {
                            if (frameMapping_v22_4.TryGetValue(fieldCode, out var value)) fieldCode = value;
                        }
                        else if (TAG_VERSION_2_3 == m_tagVersion && frameMapping_v23_4.TryGetValue(fieldCode, out var value))
                        {
                            fieldCode = value;
                        }
                    }
                    else if (3 == Settings.ID3v2_tagSubVersion)
                    {
                        if (TAG_VERSION_2_2 == m_tagVersion)
                        {
                            if (frameMapping_v22_3.TryGetValue(fieldCode, out var value)) fieldCode = value;
                        }
                        else if (TAG_VERSION_2_4 == m_tagVersion && frameMapping_v24_3.TryGetValue(fieldCode, out var value))
                        {
                            fieldCode = value;
                        }
                    }

                    writeTextFrame(w, fieldCode, FormatBeforeWriting(fieldInfo.Value), tagEncoding, fieldInfo.Language);
                    nbFrames++;
                }
            }

            foreach (PictureInfo picInfo in tag.Pictures.Where(isPictureWritable))
            {
                writePictureFrame(w, picInfo.PictureData, picInfo.MimeType, picInfo.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)picInfo.NativePicCode : EncodeID3v2PictureType(picInfo.PicType), picInfo.Description, tagEncoding);
                nbFrames++;
            }

            if (4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_useExtendedHeaderRestrictions && nbFrames > tagHeader.TagFramesRestriction)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Tag has too many frames (" + nbFrames + ") according to ID3v2 restrictions (" + tagHeader.TagFramesRestriction + ") !");
            }

            return nbFrames;
        }

        private static void writeFrameHeader(BinaryWriter w, string frameCode, bool useUnsynchronization, bool useDataSize = false)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            w.Write(0);

            short flags = 0;
            if (useDataSize) flags |= FLAG_FRAME_24_HAS_DATA_LENGTH_INDICATOR;
            if (useUnsynchronization) flags |= FLAG_FRAME_24_UNSYNCHRONIZED;
            w.Write(StreamUtils.EncodeBEInt16(flags));
        }

        private int writeChapters(BinaryWriter writer, TagData tag, Encoding tagEncoding)
        {
            int result;

            if (tagHeader.UsesUnsynchronisation)
            {
                MemoryStream s = new MemoryStream((int)Size);
                using BinaryWriter w = new BinaryWriter(s, tagEncoding);
                result = writeChaptersInternal(writer, w, tag, tagEncoding, writer.BaseStream.Position);
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
            }
            else
            {
                result = writeChaptersInternal(writer, writer, tag, tagEncoding, 0);
            }

            return result;
        }

        private int writeChaptersInternal(BinaryWriter fileWriter, BinaryWriter frameWriter, TagData tag, Encoding tagEncoding, long frameOffset)
        {
            Random randomGenerator = null;
            long frameSizePos, frameDataPos, finalFramePos;
            IList<ChapterInfo> chapters = tag.Chapters;
            int result = 0;

            // Write a "flat" table of contents, if any CTOC is present in tag
            // NB : Hierarchical table of contents is not supported; see implementation notes in the header
            if (Settings.ID3v2_alwaysWriteCTOCFrame && chapters.Count > 0)
            {
                frameSizePos = frameWriter.BaseStream.Position + 4; // Frame size location to be rewritten in a few lines (NB : Always + 4 because all frame codes are 4 chars long)
                writeFrameHeader(frameWriter, "CTOC", tagHeader.UsesUnsynchronisation);

                // Default unique toc ID
                frameDataPos = frameWriter.BaseStream.Position;
                frameWriter.Write(Utils.Latin1Encoding.GetBytes("toc"));
                frameWriter.Write('\0');

                // CTOC flags : no parents; chapters are in order
                frameWriter.Write((byte)3);


                // Entry count
                frameWriter.Write((byte)chapters.Count);

                foreach (var c in chapters)
                {
                    // Generate a chapter ID if none has been given
                    if (0 == c.UniqueID.Length)
                    {
                        randomGenerator ??= new Random();
                        c.UniqueID = randomGenerator.Next().ToString();
                    }
                    frameWriter.Write(Utils.Latin1Encoding.GetBytes(c.UniqueID));
                    frameWriter.Write('\0');
                }

                // CTOC description
                if (Utils.ProtectValue(tag[Field.CHAPTERS_TOC_DESCRIPTION]).Length > 0)
                    writeTextFrame(frameWriter, "TIT2", tag[Field.CHAPTERS_TOC_DESCRIPTION], tagEncoding, "", "", true);

                // Go back to frame size location to write its actual size 
                finalFramePos = frameWriter.BaseStream.Position;
                frameWriter.BaseStream.Seek(frameOffset + frameSizePos, SeekOrigin.Begin);
                int size = (int)(finalFramePos - frameDataPos - frameOffset);
                if (4 == Settings.ID3v2_tagSubVersion) fileWriter.Write(StreamUtils.EncodeSynchSafeInt32(size));
                else if (3 == Settings.ID3v2_tagSubVersion) fileWriter.Write(StreamUtils.EncodeBEInt32(size));
                frameWriter.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

                result++;
            }

            // Write individual chapters
            foreach (ChapterInfo chapter in chapters)
            {
                frameSizePos = frameWriter.BaseStream.Position + 4; // Frame size location to be rewritten in a few lines (NB : Always + 4 because all frame codes are 4 chars long)
                writeFrameHeader(frameWriter, "CHAP", tagHeader.UsesUnsynchronisation);

                frameDataPos = frameWriter.BaseStream.Position;

                // Generate a chapter ID if none has been given
                if (0 == chapter.UniqueID.Length)
                {
                    if (null == randomGenerator) randomGenerator = new Random();
                    chapter.UniqueID = randomGenerator.Next().ToString();
                }

                frameWriter.Write(Utils.Latin1Encoding.GetBytes(chapter.UniqueID));
                frameWriter.Write('\0');

                uint startValue = chapter.StartTime;
                uint endValue = chapter.EndTime;
                // As per specs, unused value should be encoded as 0xFF to be ignored
                if (0 == startValue + endValue)
                {
                    startValue = uint.MaxValue;
                    endValue = uint.MaxValue;
                }
                frameWriter.Write(StreamUtils.EncodeBEUInt32(startValue));
                frameWriter.Write(StreamUtils.EncodeBEUInt32(endValue));

                startValue = chapter.StartOffset;
                endValue = chapter.EndOffset;
                // As per specs, unused value should be encoded as 0xFF to be ignored
                if (0 == startValue + endValue)
                {
                    startValue = uint.MaxValue;
                    endValue = uint.MaxValue;
                }
                frameWriter.Write(StreamUtils.EncodeBEUInt32(startValue));
                frameWriter.Write(StreamUtils.EncodeBEUInt32(endValue));

                if (!string.IsNullOrEmpty(chapter.Title))
                {
                    // NB : FFmpeg uses Latin-1
                    writeTextFrame(frameWriter, "TIT2", chapter.Title, tagEncoding, "", "", true);
                }
                if (!string.IsNullOrEmpty(chapter.Subtitle))
                {
                    writeTextFrame(frameWriter, "TIT3", chapter.Subtitle, tagEncoding, "", "", true);
                }
                if (chapter.Url != null)
                {
                    writeTextFrame(frameWriter, "WXXX", chapter.Url.ToString(), tagEncoding, "", "", true);
                }
                if (chapter.Picture != null && chapter.Picture.PictureData != null && chapter.Picture.PictureData.Length > 0)
                {
                    writePictureFrame(frameWriter, chapter.Picture.PictureData, chapter.Picture.MimeType, chapter.Picture.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? (byte)chapter.Picture.NativePicCode : EncodeID3v2PictureType(chapter.Picture.PicType), chapter.Picture.Description, tagEncoding, true);
                }

                // Go back to frame size location to write its actual size 
                finalFramePos = frameWriter.BaseStream.Position;
                frameWriter.BaseStream.Seek(frameOffset + frameSizePos, SeekOrigin.Begin);
                int size = (int)(finalFramePos - frameDataPos - frameOffset);
                if (4 == Settings.ID3v2_tagSubVersion) fileWriter.Write(StreamUtils.EncodeSynchSafeInt32(size));
                else if (3 == Settings.ID3v2_tagSubVersion) fileWriter.Write(StreamUtils.EncodeBEInt32(size));
                frameWriter.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            }

            result += chapters.Count;
            return result;
        }

        private int writeLyrics(BinaryWriter writer, LyricsInfo lyrics, Encoding tagEncoding)
        {
            int result = 0;

            string unsychData = null;
            if (lyrics.UnsynchronizedLyrics != null && lyrics.UnsynchronizedLyrics.Length > 0) unsychData = lyrics.UnsynchronizedLyrics;
            else if (lyrics.Format != LyricsInfo.LyricsFormat.SYNCHRONIZED) unsychData = lyrics.FormatSynch();

            if (unsychData != null)
            {
                writeTextFrame(writer, "USLT", unsychData, tagEncoding, lyrics.LanguageCode, lyrics.Description);
                result++;
            }
            else if (lyrics.SynchronizedLyrics != null && lyrics.SynchronizedLyrics.Count > 0)
            {
                writeSynchedLyrics(writer, lyrics, tagEncoding);
                result++;
            }

            return result;
        }

        private void writeSynchedLyrics(BinaryWriter w, LyricsInfo lyrics, Encoding tagEncoding)
        {
            var frameSizePos = w.BaseStream.Position + 4; // Frame size location to be rewritten in a few lines (NB : Always + 4 because all frame codes are 4 chars long)
            writeFrameHeader(w, "SYLT", tagHeader.UsesUnsynchronisation);

            var frameDataPos = w.BaseStream.Position;

            // Encoding according to ID3v2 specs
            w.Write(encodeID3v2CharEncoding(tagEncoding));

            // Language ID (ISO-639-2)
            w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(lyrics.LanguageCode, 3, '\0')));

            // Timestamp format (ATL : always absolute milliseconds)
            w.Write((byte)2);

            // Content type
            w.Write((byte)lyrics.ContentType);

            // Short content description
            w.Write(Utils.Latin1Encoding.GetBytes(lyrics.Description));
            w.Write(getNullTerminatorFromEncoding(tagEncoding));

            // Lyrics
            foreach (LyricsInfo.LyricsPhrase phrase in lyrics.SynchronizedLyrics)
            {
                w.Write((byte)10); // Emulate SyltEdit's behaviour that seems to be the de facto standard
                w.Write(tagEncoding.GetBytes(phrase.Text));
                w.Write(getNullTerminatorFromEncoding(tagEncoding));
                w.Write(StreamUtils.EncodeBEInt32(phrase.TimestampStart));
            }

            // Go back to frame size location to write its actual size 
            var finalFramePos = w.BaseStream.Position;
            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            int size = (int)(finalFramePos - frameDataPos);
            if (4 == Settings.ID3v2_tagSubVersion) w.Write(StreamUtils.EncodeSynchSafeInt32(size));
            else if (3 == Settings.ID3v2_tagSubVersion) w.Write(StreamUtils.EncodeBEInt32(size));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writeTextFrame(
            BinaryWriter writer,
            string frameCode,
            string value,
            Encoding tagEncoding,
            string language = "",
            string description = "",
            bool isInsideUnsynch = false)
        {
            long frameOffset;
            bool isCommentCode = false; // True if we're adding a frame that is only supported through Comment field (see commentsFields list)

            bool writeValue = true;
            bool isExplicitLatin1Encoding = noTextEncodingFields.Contains(frameCode);
            bool writeTextEncoding = !isExplicitLatin1Encoding;
            bool writeNullTermination = true; // Required by specs; see paragraph 4, concerning $03 encoding

            ICollection<string> standardFrames = standardFrames_v24;
            if (3 == Settings.ID3v2_tagSubVersion) standardFrames = standardFrames_v23;

            BinaryWriter w;
            MemoryStream s = null;

            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
            {
                s = new MemoryStream((int)Size);
                w = new BinaryWriter(s, tagEncoding);
                frameOffset = writer.BaseStream.Position;
            }
            else
            {
                w = writer;
                frameOffset = 0;
            }

            if (4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_useExtendedHeaderRestrictions && value.Length > tagHeader.TextFieldSizeRestriction)
            {
                LogDelegator.GetLogDelegate()(Log.LV_INFO, frameCode + " field value (" + value + ") is longer than authorized by ID3v2 restrictions; reducing to " + tagHeader.TextFieldSizeRestriction + " characters");

                value = value[..tagHeader.TextFieldSizeRestriction];
            }

            if (frameCode.Length < 5) frameCode = frameCode.ToUpper(); // Only capitalize standard ID3v2 fields -- TODO : Use TagData.Origin property !
            var actualFrameCode = frameCode; // Used for writing TXXX frames

            // If frame is only supported through Comment field, it has to be added through COMM frame
            if (commentsFields.Contains(frameCode))
            {
                frameCode = "COMM";
                isCommentCode = true;
            }
            // If frame is a General Encapsulated object or a Private Frame,
            // its code has to be broken down ("GEOB.FileName" / "PRIV.Owner")
            else if (frameCode.StartsWith("GEO", StringComparison.OrdinalIgnoreCase)
                     || frameCode.StartsWith("PRIV", StringComparison.OrdinalIgnoreCase))
            {
                int dotIdx = frameCode.IndexOf('.');
                if (dotIdx > -1)
                {
                    actualFrameCode = frameCode[(dotIdx + 1)..];
                    frameCode = frameCode[..dotIdx];
                }
            }
            // If frame is not standard, it has to be added through TXXX frame ("user-defined text information frame")
            else if (!standardFrames.Contains(frameCode))
            {
                frameCode = "TXXX";
            }

            var frameSizePos = w.BaseStream.Position + 4; // Frame size location to be rewritten in a few lines (NB : Always + 4 because all frame codes are 4 chars long)
            writeFrameHeader(w, frameCode, tagHeader.UsesUnsynchronisation);

            var frameDataPos = w.BaseStream.Position;
            // Comments frame specifics
            if (frameCode.StartsWith("COM"))
            {
                // Encoding according to ID3v2 specs
                w.Write(encodeID3v2CharEncoding(tagEncoding));

                // Language ID (ISO-639-2)
                if (language != null) language = Utils.BuildStrictLengthString(language, 3, '\0');
                w.Write(Utils.Latin1Encoding.GetBytes(language));

                // Short content description
                w.Write(getBomFromEncoding(tagEncoding));
                if (isCommentCode)
                {
                    w.Write(Utils.Latin1Encoding.GetBytes(actualFrameCode));
                }
                // NB : ATL doesn't support custom content description yet
                w.Write(getNullTerminatorFromEncoding(tagEncoding));

                writeTextEncoding = false;
            }
            else if (frameCode.StartsWith("USL")) // Unsynched lyrics frame specifics
            {
                // Encoding according to ID3v2 specs
                w.Write(encodeID3v2CharEncoding(tagEncoding));

                // Language ID (ISO-639-2)
                if (language != null) language = Utils.BuildStrictLengthString(language, 3, '\0');
                w.Write(Utils.Latin1Encoding.GetBytes(language));

                // Short content description
                w.Write(Utils.Latin1Encoding.GetBytes(description));
                w.Write(getNullTerminatorFromEncoding(tagEncoding));

                writeTextEncoding = false;
            }
            else if (frameCode.StartsWith("POP")) // Rating frame specifics
            {
                // User e-mail
                w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically

                w.Write(byte.Parse(value));

                // Play count
                w.Write(0); // TODO : handle this field dynamically. Warning : may be longer than 32 bits (see specs)

                writeValue = false;
            }
            else if (frameCode.StartsWith("TXX")) // User-defined text frame specifics
            {
                if (writeTextEncoding) w.Write(encodeID3v2CharEncoding(tagEncoding));
                w.Write(getBomFromEncoding(tagEncoding));
                w.Write(tagEncoding.GetBytes(actualFrameCode));
                w.Write(getNullTerminatorFromEncoding(tagEncoding));
                value = value.Replace(Settings.DisplayValueSeparator, '\0');

                writeTextEncoding = false;
                writeNullTermination = true; // Seems to be the de facto standard; however, it isn't written like that in the specs
            }
            else if (frameCode.StartsWith("WXX")) // User-defined URL
            {
                byte[] desc;
                byte[] url;
                // Case 1 : Field read by ATL from a file
                string[] parts = value.Split(Settings.InternalValueSeparator);
                // Case 2 : User-defined value with the ID3v2 specs separator
                // NB : Keeps a remaining null char when the desc is encoded with UTF-16, which is rare
                if (1 == parts.Length) parts = value.Split('\0');
                // Case 3 : User-defined value without separator
                if (1 == parts.Length)
                {
                    desc = Array.Empty<byte>();
                    url = Utils.Latin1Encoding.GetBytes(parts[0]);
                }
                else
                {
                    desc = Utils.Latin1Encoding.GetBytes(parts[0]);
                    url = Utils.Latin1Encoding.GetBytes(parts[1]);
                }

                // Write the field
                w.Write(encodeID3v2CharEncoding(Utils.Latin1Encoding));     // ISO-8859-1 seems to be the de facto norm, although spec allows fancier encodings
                //w.Write(getBomFromEncoding(tagEncoding));                 // No BOM for ISO-8859-1
                w.Write(desc);
                w.Write(getNullTerminatorFromEncoding(Utils.Latin1Encoding));
                w.Write(url);

                writeValue = false;
            }
            else if (frameCode.StartsWith("GEO")) // General encapsulated object
            {
                Encoding geoEncoding = Utils.Latin1Encoding;
                w.Write(encodeID3v2CharEncoding(geoEncoding));
                w.Write(Utils.Latin1Encoding.GetBytes("application/octet-stream\0")); // MIME-type; unsupported for now
                w.Write((byte)0); // File name; unsupported for now
                w.Write(getBomFromEncoding(geoEncoding));
                w.Write(geoEncoding.GetBytes(actualFrameCode + '\0')); // Content description
                w.Write(getBomFromEncoding(geoEncoding));

                isExplicitLatin1Encoding = true; // Writing something else than Latin-1 is unsupported for now
                writeTextEncoding = false;
                writeNullTermination = true;
            }
            else if (frameCode.StartsWith("PRIV")) // Private frame
            {
                w.Write(Utils.Latin1Encoding.GetBytes(actualFrameCode + '\0')); // Owner

                isExplicitLatin1Encoding = true; // Writing something else than Latin-1 is unsupported for now
                writeTextEncoding = false;
                writeNullTermination = true;
            }
            else if (value.Contains(Settings.DisplayValueSeparator + ""))
            {
                if (isGenreField(frameCode, Settings.ID3v2_tagSubVersion)) // Genre
                {
                    // Separating values with \0 is actually specific to ID3v2.4 but seems to have become the de facto standard
                    // If something ever goes wrong with multiples values in ID3v2.3, remember their spec separates values with ()'s
                    value = value.Replace(Settings.DisplayValueSeparator, '\0');
                }
                else if (frameCode.StartsWith("IPL")) // // Involved People frame for ID3v2.2-2.3 (IPL/IPLS)
                {
                    // Value separator is a \0 and every value has a BOM
                    string[] parts = value.Split(Settings.DisplayValueSeparator, StringSplitOptions.RemoveEmptyEntries);
                    string bomAsStr = tagEncoding.GetString(getBomFromEncoding(tagEncoding));
                    if (bomAsStr.Length > 0)
                    {
                        for (int i = 0; i < parts.Length; i++) parts[i] = bomAsStr + parts[i];
                    }
                    value = string.Join("\0", parts);
                }
                else if (frameCode.StartsWith('T')) // Text information frame
                {
                    if (4 == Settings.ID3v2_tagSubVersion) // All text information frames may contain multiple values on ID3v2.4
                    {
                        value = value.Replace(Settings.DisplayValueSeparator, VALUE_SEPARATOR_24);
                    }
                    else if (multipleValuev23Fields.Contains(frameCode)) // Only specific text information frames may contain multiple values on ID3v2.2-3
                    {
                        value = value.Replace(Settings.DisplayValueSeparator, VALUE_SEPARATOR_22);
                    }
                }
            }

            if (writeValue)
            {
                Encoding localEncoding = isExplicitLatin1Encoding ? Utils.Latin1Encoding : tagEncoding;
                if (writeTextEncoding) w.Write(encodeID3v2CharEncoding(localEncoding)); // Encoding according to ID3v2 specs
                w.Write(getBomFromEncoding(localEncoding));
                w.Write(localEncoding.GetBytes(value));
                if (writeNullTermination) w.Write(getNullTerminatorFromEncoding(localEncoding));
            }


            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
            {
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
                w.Close();
            }

            // Go back to frame size location to write its actual size 
            var finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameOffset + frameSizePos, SeekOrigin.Begin);
            int size = (int)(finalFramePos - frameDataPos - frameOffset);
            if (4 == Settings.ID3v2_tagSubVersion) writer.Write(StreamUtils.EncodeSynchSafeInt32(size));
            else if (3 == Settings.ID3v2_tagSubVersion) writer.Write(StreamUtils.EncodeBEInt32(size));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(BinaryWriter writer, byte[] pictureData, string mimeType, byte pictureTypeCode, string picDescription, Encoding tagEncoding, bool isInsideUnsynch = false)
        {
            // Binary tag writing management
            long frameOffset;
            long dataSizePos = 0;
            // Data length indicator (specific to ID3v2.4)
            bool useDataSize = 4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_writePictureDataLengthIndicator;
            Encoding usedTagEncoding = Settings.ID3v2_forceAPICEncodingToLatin1 ? Utils.Latin1Encoding : tagEncoding;

            // Unsynchronization management
            BinaryWriter w;
            MemoryStream s = null;


            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
            {
                s = new MemoryStream((int)Size);
                w = new BinaryWriter(s, usedTagEncoding);
                frameOffset = writer.BaseStream.Position;
            }
            else
            {
                w = writer;
                frameOffset = 0;
            }

            var frameSizePos = w.BaseStream.Position + 4; // Frame size location to be rewritten in a few lines (NB : Always + 4 because all frame codes are 4 chars long)
            writeFrameHeader(w, "APIC", tagHeader.UsesUnsynchronisation, useDataSize);

            var frameDataPos = w.BaseStream.Position;

            if (useDataSize)
            {
                dataSizePos = w.BaseStream.Position; // Data length, as indicated by the flag we just set
                w.Write(0);
            }

            // Beginning of APIC frame data
            w.Write(encodeID3v2CharEncoding(usedTagEncoding));

            // Application of ID3v2 extended header restrictions
            if (4 == Settings.ID3v2_tagSubVersion && Settings.ID3v2_useExtendedHeaderRestrictions)
            {
                // Force JPEG if encoding restriction is enabled and mime-type is not among authorized types
                // TODO : make target format customizable (JPEG or PNG)
                if (tagHeader.HasPictureEncodingRestriction && (!(mimeType.ToLower().Equals("image/jpeg") || mimeType.ToLower().Equals("image/png"))))
                {
                    LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + mimeType + ") does not respect ID3v2 restrictions (jpeg or png required)");
                }

                // Force picture dimensions if a size restriction is enabled
                if (tagHeader.PictureSizeRestriction > 0)
                {
                    ImageProperties props = ImageUtils.GetImageProperties(pictureData);

                    if (256 == tagHeader.PictureSizeRestriction && (props.Height > 256 || props.Width > 256)) // 256x256 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (256x256 or less)");
                    }
                    else if (63 == tagHeader.PictureSizeRestriction && (props.Height > 64 || props.Width > 64)) // 64x64 or less
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (64x64 or less)");
                    }
                    else if (64 == tagHeader.PictureSizeRestriction && (props.Height != 64 && props.Width != 64)) // exactly 64x64
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_INFO, "Embedded picture format (" + props.Width + "x" + props.Height + ") does not respect ID3v2 restrictions (exactly 64x64)");
                    }
                }
            }

            // Null-terminated string = mime-type encoded in ISO-8859-1
            w.Write(Utils.Latin1Encoding.GetBytes(mimeType)); // Force ISO-8859-1 format for mime-type
            w.Write('\0'); // String should be null-terminated

            w.Write(pictureTypeCode);

            // Picture description
            w.Write(getBomFromEncoding(usedTagEncoding));
            if (picDescription.Length > 0) w.Write(usedTagEncoding.GetBytes(picDescription));
            w.Write(getNullTerminatorFromEncoding(usedTagEncoding)); // Description should be null-terminated

            w.Write(pictureData);

            var finalFramePosRaw = w.BaseStream.Position;

            if (tagHeader.UsesUnsynchronisation && !isInsideUnsynch)
            {
                s.Seek(0, SeekOrigin.Begin);
                encodeUnsynchronizedStreamTo(s, writer);
                w.Close();
            }

            // Go back to frame size location to write its actual size
            var finalFramePos = writer.BaseStream.Position;

            writer.BaseStream.Seek(frameSizePos + frameOffset, SeekOrigin.Begin);
            int size = (int)(finalFramePos - frameDataPos - frameOffset);
            if (4 == Settings.ID3v2_tagSubVersion) writer.Write(StreamUtils.EncodeSynchSafeInt32(size));
            else if (3 == Settings.ID3v2_tagSubVersion) writer.Write(StreamUtils.EncodeBEInt32(size));

            if (useDataSize) // ID3v2.4 only
            {
                writer.BaseStream.Seek(dataSizePos + frameOffset, SeekOrigin.Begin);
                size = (int)(finalFramePosRaw - frameDataPos - 4); // Data size doesn't take the data size header into account (breaks certain softwares like MusicBee)
                writer.Write(StreamUtils.EncodeSynchSafeInt32(size));
            }

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
        private static string extractGenreFromID3v2Code(string iGenre)
        {
            if (string.IsNullOrEmpty(iGenre)) return "";

            ISet<string> genres = new HashSet<string>();

            // Handle ID3v2.4 separators and ID3v2.2-3 parenthesis
            // If unicode is used, there might be BOMs converted to 'ZERO WIDTH NO-BREAK SPACE' character before each genre
            string[] parts = iGenre.Trim().Replace(Utils.UNICODE_INVISIBLE_EMPTY, "").Split(new char[] { '\0', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                // Handle numerical index of the genre, as per ID3v1 convention
                if (Utils.IsNumeric(part, true))
                {
                    int.TryParse(part, out var genreIndex);
                    if (genreIndex > -1 && genreIndex < ID3v1.MusicGenre.Length)
                    {
                        genres.Add(ID3v1.MusicGenre[genreIndex]);
                        continue;
                    }
                }
                genres.Add(part);
            }
            return string.Join(Settings.InternalValueSeparator + "", genres);
        }

        // Specific to ID3v2 : extract numeric rating from POP/POPM block containing other useless/obsolete data
        private static int readRatingInPopularityMeter(BufferedBinaryReader Source, Encoding encoding)
        {
            // Skip the e-mail, which is a null-terminated string
            StreamUtils.ReadNullTerminatedString(Source, encoding);

            // Returns the rating, contained in the following byte
            return Source.ReadByte();
        }

        /// <summary>
        /// Read Unicode BOM and return the corresponding encoding
        /// NB : This implementation _only_ works with UTF-16 BOMs defined in the ID3v2 specs ($FF FE 00 00 or $FE FF 00 00)
        /// </summary>
        /// <param name="source">Source stream</param>
        /// <returns>Properties of the BOM.
        /// If it has been found, stream is positioned on the next byte just after the BOM.
        /// If not, stream is positioned on its initial position before calling readBOM.
        /// </returns>
        private static BomProperties readBOM(Stream source)
        {
            BomProperties result = new BomProperties();
            long initialPos = source.Position;
            result.Size = 1;
            result.Encoding = Encoding.Unicode;

            byte[] b = new byte[1];

            source.Read(b, 0, 1);
            bool first = true;
            bool foundFE = false;
            bool foundFF = false;

            while (0 == b[0] || 0xFF == b[0] || 0xFE == b[0])
            {
                if (result.Size > 3) break; // Useful (unsynchronized) BOM can't be longer than 3 chars => detection failed

                // All UTF-16 BOMs either start or end with 0xFE or 0xFF
                // => Having them both read means that the entirety of the UTF-16 BOM has been read
                foundFE = foundFE || 0xFE == b[0];
                foundFF = foundFF || 0xFF == b[0];
                if (foundFE && foundFF)
                {
                    result.Found = true;
                    break;
                }

                if (first && b[0] > 0)
                {
                    // 0xFE first means data is coded in Big Endian
                    if (0xFE == b[0]) result.Encoding = Encoding.BigEndianUnicode;
                    first = false;
                }

                source.Read(b, 0, 1);
                result.Size++;
            }

            if (!result.Found) source.Position = initialPos;
            else result.Size = (int)(source.Position - initialPos);

            return result;
        }

        /// <summary>
        /// Returns the ATL picture type corresponding to the given ID3v2 picture code
        /// </summary>
        /// <param name="picCode">ID3v2 picture code</param>
        /// <returns>ATL picture type corresponding to the given ID3v2 picture code; PIC_TYPE.Unsupported by default</returns>
        public static PictureInfo.PIC_TYPE DecodeID3v2PictureType(int picCode)
        {
            return picCode switch
            {
                0 => PictureInfo.PIC_TYPE.Generic, // Spec calls it "Other"
                0x02 => PictureInfo.PIC_TYPE.Icon,
                0x03 => PictureInfo.PIC_TYPE.Front,
                0x04 => PictureInfo.PIC_TYPE.Back,
                0x05 => PictureInfo.PIC_TYPE.Leaflet,
                0x06 => PictureInfo.PIC_TYPE.CD,
                0x07 => PictureInfo.PIC_TYPE.LeadArtist,
                0x08 => PictureInfo.PIC_TYPE.Artist,
                0x09 => PictureInfo.PIC_TYPE.Conductor,
                0x0A => PictureInfo.PIC_TYPE.Band,
                0x0B => PictureInfo.PIC_TYPE.Composer,
                0x0C => PictureInfo.PIC_TYPE.Lyricist,
                0x0D => PictureInfo.PIC_TYPE.RecordingLocation,
                0x0E => PictureInfo.PIC_TYPE.DuringRecording,
                0x0F => PictureInfo.PIC_TYPE.DuringPerformance,
                0x10 => PictureInfo.PIC_TYPE.MovieCapture,
                0x11 => PictureInfo.PIC_TYPE.Fishie,
                0x12 => PictureInfo.PIC_TYPE.Illustration,
                0x13 => PictureInfo.PIC_TYPE.BandLogo,
                0x14 => PictureInfo.PIC_TYPE.PublisherLogo,
                _ => PictureInfo.PIC_TYPE.Unsupported
            };
        }

        /// <summary>
        /// Returns the ID3v2 picture code corresponding to the given ATL picture type
        /// </summary>
        /// <param name="picType">ATL picture type</param>
        /// <returns>ID3v2 picture code corresponding to the given ATL picture type; 0 ("Other") by default</returns>
        public static byte EncodeID3v2PictureType(PictureInfo.PIC_TYPE picType)
        {
            return picType switch
            {
                PictureInfo.PIC_TYPE.Icon => 0x02,
                PictureInfo.PIC_TYPE.Front => 0x03,
                PictureInfo.PIC_TYPE.Back => 0x04,
                PictureInfo.PIC_TYPE.Leaflet => 0x05,
                PictureInfo.PIC_TYPE.CD => 0x06,
                PictureInfo.PIC_TYPE.LeadArtist => 0x07,
                PictureInfo.PIC_TYPE.Artist => 0x08,
                PictureInfo.PIC_TYPE.Conductor => 0x09,
                PictureInfo.PIC_TYPE.Band => 0x0A,
                PictureInfo.PIC_TYPE.Composer => 0x0B,
                PictureInfo.PIC_TYPE.Lyricist => 0x0C,
                PictureInfo.PIC_TYPE.RecordingLocation => 0x0D,
                PictureInfo.PIC_TYPE.DuringRecording => 0x0E,
                PictureInfo.PIC_TYPE.DuringPerformance => 0x0F,
                PictureInfo.PIC_TYPE.MovieCapture => 0x10,
                PictureInfo.PIC_TYPE.Fishie => 0x11,
                PictureInfo.PIC_TYPE.Illustration => 0x12,
                PictureInfo.PIC_TYPE.BandLogo => 0x13,
                PictureInfo.PIC_TYPE.PublisherLogo => 0x14,
                _ => 0
            };
        }

        // Copies the stream while cleaning abnormalities due to unsynchronization (Cf. paragraph5 of ID3v2.2 specs; paragraph6 of ID3v2.3+ specs)
        // => every "0xff 0x00" becomes "0xff"
        private static byte[] decodeUnsynchronizedStream(BufferedBinaryReader from, int length)
        {
            const int BUFFER_SIZE = 8192;

            byte[] result = new byte[length];

            bool foundFF = false;

            byte[] readBuffer = new byte[BUFFER_SIZE];
            byte[] writeBuffer = new byte[BUFFER_SIZE];

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
                var written = 0;
                var bytesToRead = Math.Min(remainingBytes, BUFFER_SIZE);

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

        // Copies the stream while unsynchronizing it (Cf. paragraph5 of ID3v2.2 specs; paragraph6 of ID3v2.3+ specs)
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
                if (0xFF == data[0] && (0x00 == data[1] || 0xE0 == (data[1] & 0xE0)))
                {
                    to.Write((byte)0);
                }
                data[0] = data[1];
            }
            to.Write(data[0]);
        }

        /// Returns the .NET Encoding corresponding to the given ID3v2 convention (see below)
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
            return encoding switch
            {
                0 => Utils.Latin1Encoding,      // aka ISO Latin-1
                1 => Encoding.Unicode,          // UTF-16 with BOM (since ID3v2.2)
                2 => Encoding.BigEndianUnicode, // UTF-16 Big Endian without BOM (since ID3v2.4)
                3 => Encoding.UTF8,             // UTF-8 (since ID3v2.4)
                _ => Encoding.Default
            };
        }

        /// <summary>
        /// Returns the ID3v2 encoding byte corresponding to the given .NET Encoding (see above function for the convention)
        /// </summary>
        /// <param name="encoding">.NET Encoding to get the ID3v2 encoding byte for</param>
        /// <returns>ID3v2 encoding byte corresponding to the given Encoding; 0 (ISO-8859-1) by default</returns>
        private static byte encodeID3v2CharEncoding(Encoding encoding)
        {
            if (encoding.Equals(Encoding.Unicode)) return 1;
            if (encoding.Equals(Encoding.BigEndianUnicode)) return 2;
            if (encoding.Equals(Encoding.UTF8)) return 3;
            return 0; // Default = ISO-8859-1 / ISO Latin-1
        }

        /// <summary>
        /// Get the Byte Order Mark (BOM) corresponding to the given Encoding
        /// </summary>
        /// <param name="encoding">Encoding to get the BOM for</param>
        /// <returns>BOM corresponding to the given Encoding; no BOM (ISO-8859-1) by default</returns>
        private static byte[] getBomFromEncoding(Encoding encoding)
        {
            if (encoding.Equals(Encoding.Unicode)) return BOM_UTF16_LE;
            if (encoding.Equals(Encoding.BigEndianUnicode)) return BOM_UTF16_BE;
            if (encoding.Equals(Encoding.UTF8)) return BOM_NONE;
            return BOM_NONE; // Default = ISO-8859-1 / ISO Latin-1
        }

        /// <summary>
        /// Get the null terminator corresponding to the given Encoding
        /// </summary>
        /// <param name="encoding">Encoding to get the null terminator for</param>
        /// <returns>Null terminator corresponding to the given Encoding; single byte (ISO-8859-1) by default</returns>
        private static byte[] getNullTerminatorFromEncoding(Encoding encoding)
        {
            if (encoding.Equals(Encoding.Unicode)) return NULLTERMINATOR_2;
            if (encoding.Equals(Encoding.BigEndianUnicode)) return NULLTERMINATOR_2;
            if (encoding.Equals(Encoding.UTF8)) return NULLTERMINATOR;
            return NULLTERMINATOR; // Default = ISO-8859-1 / ISO Latin-1
        }

        /// <summary>
        /// Indicate if the given string is exclusively composed of upper alphabetic characters
        /// </summary>
        /// <param name="str">String to test</param>
        /// <returns>True if the given string is exclusively composed of upper alphabetic characters; false if not</returns>
        private static bool isUpperAlpha(string str)
        {
            foreach (char c in str)
            {
                if (!char.IsLetterOrDigit(c)) return false;
                if (char.IsLetter(c) && !char.IsUpper(c)) return false;
            }
            return true;
        }

        private static bool isGenreField(string field, int tagVersion)
        {
            if (2 == tagVersion) return field.Equals("TCO", StringComparison.OrdinalIgnoreCase);
            else return field.Equals("TCON", StringComparison.OrdinalIgnoreCase);
        }
    }
}