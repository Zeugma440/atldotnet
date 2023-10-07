using System.Collections.Generic;

namespace ATL.Playlist
{
    /// <summary>
    /// Interface for standard playlist I/O classes
    /// </summary>
    public interface IPlaylistIO
    {
        /// <summary>
        /// Absolute path of the playlist file
        /// </summary>
        string Path
        {
            get;
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
        /// Gets the absolute paths of all files registered in the playlist
        /// NB : The existence of the files is not checked when getting them
        /// </summary>
        /// <returns>Array containing all paths</returns>
        IList<string> FilePaths
        {
            get;
            set;
        }

        /// <summary>
        /// Gest all the tracks registered in the playlist
        /// </summary>
        /// <returns>Array containing all tracks</returns>
        IList<Track> Tracks
        {
            get;
            set;
        }

        /// <summary>
        /// Save playlist to disk
        /// </summary>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        bool Save();
    }
}
