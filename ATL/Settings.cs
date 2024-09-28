using ATL.AudioData;
using System.Text;

namespace ATL
{
#pragma warning disable S2223 // Non-constant static fields should not be visible
#pragma warning disable S1104 // Fields should not have public accessibility

    /// <summary>
    /// Global settings for the behaviour of the library
    /// </summary>
    public static class Settings
    {
        /*
         * ========= GENERIC TECHNICAL SETTINGS
         */

        /// <summary>
        /// Buffer size used for I/O operations, in bytes.
        /// Default : 512 bytes
        /// 
        /// A higher value will consume more RAM but will allow faster I/O on large files.
        /// Warning : The app will crash if you set a value higher than the file size itself.
        /// </summary>
        public static int FileBufferSize = 512;

        /// <summary>
        /// Force high-level I/O operations to be performed without zone buffering,
        /// resulting in higher disk usage, but lower RAM usage
        /// Default : false
        /// </summary>
        public static bool ForceDiskIO = false;

        /// <summary>
        /// Use null instead of default values to represent absent fields in <see cref="ATL.Track"/>
        /// Default : false
        /// </summary>
        public static bool NullAbsentValues = false;

        /// <summary>
        /// Write stacktraces to stdout
        /// NB : Regardless of this setting, stacktraces are aways logged to ATL's logging system
        /// Default : true
        /// </summary>
        public static bool OutputStacktracesToConsole = true;


        /*
         * ========= GENERIC FUNCTIONAL SETTINGS
         */
        /// <summary>
        /// Add padding to files that don't have it
        /// Default : false
        /// </summary>
        public static bool AddNewPadding = false;

        /// <summary>
        /// Size of the initial padding to add; size of max padding to use
        /// Default : 2048
        /// </summary>
        public static int PaddingSize = 2048;

        /// <summary>
        /// Value separator character
        /// Read-only; for ATL internal use only (don't use it in your client app)
        /// </summary>
        internal static readonly char InternalValueSeparator = '˵';       // Some obscure unicode character that hopefully won't be used anywhere in an actual tag

        /// <summary>
        /// Value separator character used to display and write multiple values within one field
        /// Default : ';'
        /// </summary>
        public static char DisplayValueSeparator = ';';

        /// <summary>
        /// If true, default Track behaviour reads all metadata frames, including those not described by IMetaDataIO
        /// Default : true
        /// </summary>
        public static bool ReadAllMetaFrames = true;

        /// <summary>
        /// Text encoding to use to handle text data where official specs don't set any standard
        /// Default : UTF-8
        /// </summary>
        public static Encoding DefaultTextEncoding = Encoding.UTF8; // Could also be set to Encoding.Default for system default

        /// <summary>
        /// Tag editing preferences : what tagging systems to use when audio file has no metadata ?
        /// NB1 : If more than one item, _all_ of them will be written
        /// NB2 : If Native tagging is not indicated here, it will _not_ be used
        /// NB3 : "Recommended" will choose the most compatible metadata formats according to the target audio format
        /// 
        /// Default : Recommended
        /// </summary>
        public static MetaDataIOFactory.TagType[] DefaultTagsWhenNoMetadata = { MetaDataIOFactory.TagType.RECOMMENDED };

        /// <summary>
        /// If true, file name (without the extension) will go to the Title field if metadata contains no title
        /// Default : true
        /// </summary>
        public static bool UseFileNameWhenNoTitle = true;

        /// <summary>
        /// If true, automatically detects dates in AdditionalFields and write them according to the expected format
        /// Dates should be set using MetaDataHolder.DATETIME_PREFIX + DateTime.ToFileTime()
        /// Default : true
        /// </summary>
        public static bool AutoFormatAdditionalDates = true;

        /// <summary>
        /// Determines the way ATL writes file paths in playlists
        /// If true, use absolute file paths
        /// If false, use relative file paths when track files are under the same folder as the playlist file
        /// Default : false
        /// </summary>
        public static bool PlaylistWriteAbsolutePath = false;


        //
        // Behaviour related to leading zeroes when formatting Disc and Track fields (ID3v2, Vorbis, APE)
        //

        /// <summary>
        /// If true, use leading zeroes; number of digits is aligned on TOTAL fields or 2 digits if no total field
        /// Default : false
        /// </summary>
        public static bool UseLeadingZeroes = false;

        /// <summary>
        /// If true, UseLeadingZeroes is always _applied_ regardless of the format of the original file; if false, formatting of the original file prevails
        /// Default : false
        /// </summary>
        public static bool OverrideExistingLeadingZeroesFormat = false;


        /*
         * ========= FORMAT-SPECIFIC SETTINGS
         */

        /// <summary>
        /// Use tag formats defined in Settings.DefaultTagsWhenNoMetadata when writing to a file tagged with ID3v1 only
        /// Rationale : the ID3v1 format has very limited capabilities. Adding richer metadata to files tagged with ID3v1 only should be easily feasible
        /// without too many client-side checks
        /// </summary>
        public static bool EnrichID3v1 = true;

        /// <summary>
        /// ID3v2 : Always write CTOC frame when metadata contain at least one chapter
        /// Default : false
        /// </summary>
        public static bool ID3v2_useExtendedHeaderRestrictions = false;

        /// <summary>
        /// ID3v2 : Always write CTOC frame when metadata contain at least one chapter
        /// Default : true
        /// </summary>
        public static bool ID3v2_alwaysWriteCTOCFrame = true;

        /// <summary>
        /// ID3v2 : Write metadata in ID3v2.[ID3v2_tagSubVersion] format
        /// Only 3 and 4 are supported so far - resp. ID3v2.3 and ID3v2.4
        /// Default : 4 (ID3v2.4)
        /// </summary>
        public static byte ID3v2_tagSubVersion = 4;

        /// <summary>
        /// ID3v2 : Force the encoding of the APIC frame to ISO-8859-1/Latin-1 for Windows to be able to display the cover picture
        /// Disable it to write picture descriptions using non-western characters (japanese, cyrillic...)
        /// Default : true
        /// </summary>
        public static bool ID3v2_forceAPICEncodingToLatin1 = true;

        /// <summary>
        /// ID3v2 : Set to true to force unsynchronization when writing ID3v2.3 or ID3v2.4 tags
        /// Default : false
        /// </summary>
        public static bool ID3v2_forceUnsynchronization = false;

        /// <summary>
        /// ID3v2 : Set to false not to write the data length indicator (flag p, as per ID3v2.4 specs) on picture frames
        /// Disabling this might help showing covers on certain softwares (e.g. Spotify)
        /// Default : true
        /// </summary>
        public static bool ID3v2_writePictureDataLengthIndicator = true;

        /// <summary>
        /// ID3v2.2, ID3v2.3 : Set to false to keep raw data in fields that have multiple values (e.g. TP1, TCM, TCOM...)
        /// Default : true
        /// </summary>
        public static bool ID3v2_separatev2v3Values = true;

        /// <summary>
        /// MP4 : Set to true to always create chapters in Nero format (chpl)
        /// Default : true
        /// </summary>
        public static bool MP4_createNeroChapters = true;

        /// <summary>
        /// MP4 : Set to true to limit the max number of Nero chapters (chpl) to 255
        /// to ensure better compatiblity with certain apps
        /// Default : true
        /// </summary>
        public static bool MP4_capNeroChapters = true;

        /// <summary>
        /// MP4 : Set to true to always create chapters in Quicktime format (chap)
        /// Default : true
        /// </summary>
        public static bool MP4_createQuicktimeChapters = true;

        /// <summary>
        /// MP4 : Set to true to keep existing chapters (i.e. Nero or Quicktime)
        /// regardless of the other chapter creation options
        /// </summary>
        public static bool MP4_keepExistingChapters = true;

        /// <summary>
        /// MP4 : Set to read chapters :
        ///   - 0 : From Quicktime format (chap), except if Nero chapters have more entries
        ///   - 1 : From Quicktime format only
        ///   - 2 : From Nero format (chpl), except if Quicktime chapters have more entries
        ///   - 3 : From Nero format only
        /// 
        /// Warning : Using 1 or 3 while updating files may delete the tag you've not chosen to read
        /// Default : 0
        /// </summary>
        public static int MP4_readChaptersFormat = 0;

        /// <summary>
        /// ASF/WMA : Keep non-"WM" fields when removing the tag
        /// Default : false
        /// </summary>
        public static bool ASF_keepNonWMFieldsWhenRemovingTag = false;

        /// <summary>
        /// GYM and VGM : Force playback rate (Hz) to calculate track duration
        /// Set 0 to adjust to song properties
        /// Default : 0
        /// </summary>
        public static int GYM_VGM_playbackRate = 0;

        /// <summary>
        /// MP3 : Set to true to calculate the exact duration of the track (parsing the file will be longer)
        /// Default : false (fast parsing)
        /// </summary>
        public static bool MP3_parseExactDuration = false;

        /// <summary>
        /// M3U : Use extended format to write the playlist
        /// Default : true
        /// </summary>
        public static bool M3U_useExtendedFormat = true;
    }

#pragma warning restore S1104 // Fields should not have public accessibility
#pragma warning restore S2223 // Non-constant static fields should not be visible
}
