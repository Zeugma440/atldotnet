namespace ATL.Playlist
{
    /// <summary>
    /// Defines the format of a playlist
    /// </summary>
    public class PlaylistFormat : Format
    {
        /// <summary>
        /// Formatting of the track locations (file paths) within the playlist
        /// </summary>
        public enum LocationFormatting
        {
            /// <summary>
            /// Undefined formatting
            /// </summary>
            Undefined = -1,
            /// <summary>
            /// File path (e.g. C:\the folder\theFile.mp3)
            /// </summary>
            FilePath = 0,
            /// <summary>
            /// Winamp convention (e.g. file:C:\the folder\theFile.mp3)
            /// </summary>
            Winamp_URI = 1,
            /// <summary>
            /// Microsoft URI (e.g. file://C:\the folder\theFile.mp3)
            /// </summary>
            MS_URI = 2,
            /// <summary>
            /// RFC URI (e.g. file:///C:/the%20folder/theFile.mp3)
            /// </summary>
            RFC_URI = 3
        };

        /// <summary>
        /// String encoding used within the playlist file
        /// </summary>
        public enum FileEncoding
        {
            /// <summary>
            /// Undefined encoding
            /// </summary>
            Undefined = -1,
            /// <summary>
            /// UTF-8 with file BOM
            /// </summary>
            UTF8_BOM = 0,
            /// <summary>
            /// UTF-8 without file BOM
            /// </summary>
            UTF8_NO_BOM = 1,
            /// <summary>
            /// ANSI (use at your own risk)
            /// </summary>
            ANSI = 2
        };

        /// <summary>
        /// Formatting of the track locations (file paths) within the playlist file
        /// </summary>
        public LocationFormatting LocationFormat { get; set; }

        /// <summary>
        /// String encoding used within the playlist file
        /// </summary>
        public FileEncoding Encoding { get; set; }

        /// <summary>
        /// Instanciate a new PlaylistFormat using the given arguments
        /// </summary>
        /// <param name="ID">Format ID</param>
        /// <param name="iName">Format name</param>

        public PlaylistFormat(int ID, string iName) : base(ID, iName) { }

        /// <summary>
        /// Instanciate a new PlaylistFormat by copying the given PlaylistFormat's attributes
        /// </summary>
        /// <param name="f">PlaylistFormat object to copy attributes from</param>
        public PlaylistFormat(PlaylistFormat f) : base(f) { }

        /// <summary>
        /// Copy the attributes of the given Format object
        /// </summary>
        /// <param name="iFormat">Format object to copy attributes from</param>
        protected override void copyFrom(Format iFormat)
        {
            base.copyFrom(iFormat);
            LocationFormat = ((PlaylistFormat)iFormat).LocationFormat;
            Encoding = ((PlaylistFormat)iFormat).Encoding;
        }

        /// <summary>
        /// Initialize the current object with the given ID and Name
        /// </summary>
        /// <param name="id">Format ID</param>
        /// <param name="name">Format name</param>
        protected override void init(int id, string name)
        {
            base.init(id, name);
            LocationFormat = LocationFormatting.FilePath;
            Encoding = FileEncoding.UTF8_BOM;
        }
    }
}
