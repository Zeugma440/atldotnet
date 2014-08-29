using System;
using System.Collections;
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
        String Path
        {
            get;
            set;
        }

        /// <summary>
        /// Title of the container
        /// </summary>
		String Title
		{
			get;
		}

        /// <summary>
        /// Artist of the container
        /// </summary>
		String Artist
		{
			get;
		}

        /// <summary>
        /// List of the tracks described in the container
        /// </summary>
		IList<ATL.Track> Tracks
		{
			get;
		}
	}
}
