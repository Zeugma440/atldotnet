namespace ATL
{
    public static class Settings
    {
        // General properties
        public static bool ID3v2_useExtendedHeaderRestrictions = false;
        public static bool ASF_keepNonWMFieldsWhenRemovingTag = false;
        public static bool EnablePadding = false;                        // Used by OGG container; could be used by ID3v2 in the future

        // Used by APE tag
        public static string InternalValueSeparator = "˵"; // Some obscure unicode character that hopefully won't be used anywhere in an actual tag
        public static string DisplayValueSeparator = ";";
        public static string InternalLineSeparator = "˶"; // Some obscure unicode character that hopefully won't be used anywhere in an actual tag
        public static string DisplayLineSeparator = "/";
    }
}
