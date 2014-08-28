using System;
using System.Collections;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
	/// <summary>
	/// Reads all file paths registered in a playlist
	/// </summary>
	public interface IPlaylistReader
	{
        /// <summary>
        /// Absolute path of the playlist file
        /// </summary>
        String Path
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the absolute paths of all files registered in a playlist
        /// NB : The existence of the files is not checked
        /// </summary>
        /// <returns>An array containing all paths</returns>
        IList<String> GetFiles();
	}
}
