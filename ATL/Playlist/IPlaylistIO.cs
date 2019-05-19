using System;
using System.Collections.Generic;
using System.Text;

namespace ATL.Playlist
{
    /// <summary>
    /// TODO
    /// </summary>
    public interface IPlaylistIO
    {
        /// <summary>
        /// Absolute path of the playlist file
        /// </summary>
        string Path
        {
            get;
            set;
        }

        /// <summary>
        /// Location formatting to use when writing the file
        /// </summary>
        PlaylistFormat.LocationFormatting LocationFormatting
        {
            get;
            set;
        }

        /// <summary>
        /// Encoding convention to use when writing the file
        /// </summary>
        PlaylistFormat.FileEncoding Encoding
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the absolute paths of all files registered in a playlist
        /// NB : The existence of the files is not checked when getting them
        /// </summary>
        /// <returns>An array containing all paths</returns>
        IList<string> FilePaths
        {
            get;
            set;
        }

        /// <summary>
        /// Tracks extracted from all files registered in a playlist
        /// </summary>
        /// <returns>An array containing all tracks</returns>
        IList<Track> Tracks
        {
            get;
            set;
        }
    }
}
