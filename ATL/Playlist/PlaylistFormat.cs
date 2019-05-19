namespace ATL.Playlist
{
    public class PlaylistFormat : Format
    {
        public enum LocationFormatting
        {
            Undefined = -1,
            FilePath = 0, // C:\the folder\theFile.mp3
            Winamp_URI = 1, // file:C:\the folder\theFile.mp3
            MS_URI = 2, // file://C:\the folder\theFile.mp3
            RFC_URI = 3 // file:///C:/the%20folder/theFile.mp3
        };

        public enum FileEncoding
        {
            Undefined = -1,
            UTF8_BOM = 0, // UTF-8 with file BOM
            UTF8_NO_BOM = 1, // UTF-8 without file BOM
            ANSI = 2 // ANSI (use at your own risk)
        };


        public LocationFormatting LocationFormat { get; set; }

        public FileEncoding Encoding { get; set; }


        public PlaylistFormat(int ID, string iName) : base(ID, iName) { }

        public PlaylistFormat(PlaylistFormat f) : base(f) { }


        protected override void copyFrom(Format iFormat)
        {
            base.copyFrom(iFormat);
            LocationFormat = ((PlaylistFormat)iFormat).LocationFormat;
            Encoding = ((PlaylistFormat)iFormat).Encoding;
        }

        protected override void init(int ID, string Name)
        {
            base.init(ID, Name);
            LocationFormat = LocationFormatting.FilePath;
            Encoding = FileEncoding.UTF8_BOM;
        }
    }
}
