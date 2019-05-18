namespace ATL.Playlist
{
    public class PlaylistFormat : Format
    {
        public enum LocationFormatting { Undefined = -1, FilePath = 0, MS_URI = 1, RFC_URI = 2, Winamp_URI = 3 };

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
