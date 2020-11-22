using System.Collections.Generic;

#pragma warning disable S125 // Sections of code should not be commented out
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


                // Nothing;
            }
        }

        /// Mandatory override to IPlaylistIO
        public IList<string> FilePaths
        {
            get => new List<string>();
            set
            {
                // Nothing;
            }
        }

        /// Mandatory override to IPlaylistIO
        public IList<Track> Tracks
        {
            get => new List<Track>();
            set
            {
                // Nothing;
            }
        }

        /// Mandatory override to IPlaylistIO
        public PlaylistFormat.LocationFormatting LocationFormatting
        {
            get => PlaylistFormat.LocationFormatting.Undefined;
            set
            {
                // Nothing;
            }
        }

        /// Mandatory override to IPlaylistIO
        public PlaylistFormat.FileEncoding Encoding
        {
            get => PlaylistFormat.FileEncoding.Undefined;
            set
            {
                // Nothing;
            }
        }
    }
}
#pragma warning restore S125 // Sections of code should not be commented out