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

        protected LocationFormatting locationFormat;


        public PlaylistFormat(int ID, string iName) : base(ID, iName) { }

        public PlaylistFormat(PlaylistFormat f) : base(f) { }

        protected override void copyFrom(Format iFormat)
        {
            base.copyFrom(iFormat);
            this.locationFormat = ((PlaylistFormat)iFormat).locationFormat;
        }

        protected override void init(int ID, string Name)
        {
            base.init(ID, Name);
            locationFormat = LocationFormatting.FilePath;
        }

        public LocationFormatting LocationFormat
        {
            get { return locationFormat; }
            set { locationFormat = value; }
        }
    }
}
