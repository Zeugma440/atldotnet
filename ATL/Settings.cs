using System.Text;

namespace ATL
{
#pragma warning disable S2223 // Non-constant static fields should not be visible
#pragma warning disable S1104 // Fields should not have public accessibility

    public static class Settings
    {
        /*
         * Technical settings
         */
        public static int FileBufferSize = 512;                         // Buffer size used for I/O operations. Change it at your own risk !

        /*
         * Global settings
         */
        public static bool AddNewPadding = false;                       // Add padding to files that don't have it
        public static int PaddingSize = 2048;                           // Size of the initial padding to add; size of max padding to use

        public static readonly char InternalValueSeparator = '˵';       // Some obscure unicode character that hopefully won't be used anywhere in an actual tag
        public static char DisplayValueSeparator = ';';

        public static bool ReadAllMetaFrames = true; // If true, default Track behaviour reads all metadata frames, including those not described by IMetaDataIO

        public static Encoding DefaultTextEncoding = Encoding.UTF8; // Could also be set to Encoding.Default for system default

        // Tag editing preferences : what tagging systems to use when audio file has no metadata ?
        // NB1 : If more than one item, _all_ of them will be written
        // NB2 : If Native tagging is not indicated here, it will _not_ be used
        public static int[] DefaultTagsWhenNoMetadata = new int[2] { AudioData.MetaDataIOFactory.TAG_ID3V2, AudioData.MetaDataIOFactory.TAG_NATIVE };

        public static bool UseFileNameWhenNoTitle = true;               // If true, file name (without the extension) will go to the Title field if metadata contains no title

        //
        // Behaviour related to leading zeroes when formatting Disc and Track fields (ID3v2, Vorbis, APE)
        //
        public static bool UseLeadingZeroes = false;                    // If true, use leading zeroes; number of digits is aligned on TOTAL fields or 2 digits if no total field
        public static bool OverrideExistingLeadingZeroesFormat = false; // If true, UseLeadingZeroes is always _applied_ regardless of the format of the original file; if false, formatting of the original file prevails

        /*
         * Format-specific settings
         */
        public static bool ID3v2_useExtendedHeaderRestrictions = false;
        public static bool ID3v2_alwaysWriteCTOCFrame = true;           // Always write CTOC frame when metadata contain at least one chapter
        public static byte ID3v2_tagSubVersion = 4;                     // Write metadata in ID3v2.<ID3v2_tagSubVersion> format (only 3 and 4 are supported so far - resp. ID3v2.3 and ID3v2.4)
        public static bool ID3v2_forceAPICEncodingToLatin1 = true;      // Force the encoding of the APIC frame to ISO-8859-1/Latin-1 for Windows to be able to display the cover picture
                                                                        // Disable it to be able to write picture descriptions using non-western characters (japanese, cyrillic...)
        public static bool ID3v2_forceUnsynchronization = false;        // Set to true to force unsynchronization when writing ID3v2.3 or ID3v2.4 tags

        public static bool MP4_createNeroChapters = true;               // Set to true to always create chapters in Nero format (chpl)
        public static bool MP4_createQuicktimeChapters = true;          // Set to true to always create chapters in Quicktime format (chap)
        public static bool MP4_keepExistingChapters = true;             // Set to true to keep existing chapters (i.e. Nero or Quicktime)
                                                                        // regardless of the other chapter creation options

        public static bool ASF_keepNonWMFieldsWhenRemovingTag = false;

        public static int GYM_VGM_playbackRate = 0;                     // Playback rate (Hz) [0 = adjust to song properties]

        public static bool M3U_useExtendedFormat = true;
    }

#pragma warning restore S1104 // Fields should not have public accessibility
#pragma warning restore S2223 // Non-constant static fields should not be visible
}
