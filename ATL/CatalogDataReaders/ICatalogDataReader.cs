using System.Collections.Generic;

namespace ATL.CatalogDataReaders
{
    /// <summary>
    /// Reads all tracks described in a container (e.g. : CUE sheet)
    /// </summary>
    public interface ICatalogDataReader
    {
        /// <summary>
        /// Absolute path of the container file
        /// </summary>
        string Path
        {
            get;
            set;
        }

        /// <summary>
        /// Title of the container
        /// </summary>
		string Title
        {
            get;
        }

        /// <summary>
        /// Artist of the container
        /// </summary>
		string Artist
        {
            get;
        }

        /// <summary>
        /// Comments of the container
        /// </summary>
		string Comments
        {
            get;
        }

        /// <summary>
        /// List of the tracks described in the container
        /// </summary>
		IList<Track> Tracks
        {
            get;
        }
    }
}
