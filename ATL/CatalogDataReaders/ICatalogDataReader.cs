using System;
using System.Collections;
using System.Collections.Generic;

namespace ATL.CatalogDataReaders
{
	/// <summary>
	/// Description r¨¦sum¨¦e de CatalogDataReader.
	/// </summary>
	public interface ICatalogDataReader
	{
		String Path
		{
			get;
		}

		String BaseFilePath
		{
			get;
		}

		String Title
		{
			get;
		}

		String Artist
		{
			get;
		}

		List<ATL.Track> Tracks
		{
			get;
		}
	}
}
