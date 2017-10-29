using System.Collections.Generic;

namespace ATL
{
    public static class Settings
    {
        // General properties
        public static bool ID3v2_useExtendedHeaderRestrictions = false;
        public static bool ASF_keepNonWMFieldsWhenRemovingTag = false;

        public static int GYM_VGM_playbackRate = 0;                     // Playback rate (Hz) [0 = adjust to song properties]

        public static bool EnablePadding = false;                       // Used by OGG container; could be used by ID3v2 in the future

        public static readonly string InternalValueSeparator = "˵"; // Some obscure unicode character that hopefully won't be used anywhere in an actual tag
        public static string DisplayValueSeparator = ";";

        public static bool ReadAllMetaFrames = true; // If true, default Track behaviour reads all metadata frames, including those not described by IMetaDataIO

        // Tag editing preferences : what tagging systems to use when audio file has no metadata ?
        // NB1 : If more than one item, _all_ of them will be written
        // NB2 : If Native tagging is not indicated here, it will _not_ be used
        public static int[] DefaultTagsWhenNoMetadata = new int[2] { AudioData.MetaDataIOFactory.TAG_ID3V2, AudioData.MetaDataIOFactory.TAG_NATIVE };
    }
}
