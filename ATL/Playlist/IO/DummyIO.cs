using System.Collections.Generic;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// Dummy playlist management class
    /// </summary>
    public class DummyIO : IPlaylistIO
    {
        /// Mandatory override to IPlaylistIO
        public string Path
        {
            get
            {
                return "";
            }
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// Mandatory override to IPlaylistIO
        public IList<string> FilePaths
        {
            get => new List<string>();
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// Mandatory override to IPlaylistIO
        public IList<Track> Tracks
        {
            get => new List<Track>();
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// Mandatory override to IPlaylistIO
        public PlaylistFormat.LocationFormatting LocationFormatting
        {
            get => PlaylistFormat.LocationFormatting.Undefined;
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// Mandatory override to IPlaylistIO
        public PlaylistFormat.FileEncoding Encoding
        {
            get => PlaylistFormat.FileEncoding.Undefined;
            set
            {
                // Nothing here, it's a dummy method
            }
        }
    }
}