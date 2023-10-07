using System.Collections.Generic;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// Dummy playlist management class
    /// </summary>
    public class DummyIO : IPlaylistIO
    {
        /// <inheritdoc/>
        public string Path => "";

        /// <inheritdoc/>
        public IList<string> FilePaths
        {
            get => new List<string>();
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// <inheritdoc/>
        public IList<Track> Tracks
        {
            get => new List<Track>();
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// <inheritdoc/>
        public bool Save()
        {
            return true;
        }

        /// <inheritdoc/>
        public PlaylistFormat.LocationFormatting LocationFormatting
        {
            get => PlaylistFormat.LocationFormatting.Undefined;
            set
            {
                // Nothing here, it's a dummy method
            }
        }

        /// <inheritdoc/>
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