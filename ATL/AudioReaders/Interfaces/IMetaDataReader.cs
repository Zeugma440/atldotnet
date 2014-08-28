using System;
using System.Collections.Generic;

namespace ATL.AudioReaders
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio metadata information
	/// </summary>
	public interface IMetaDataReader
	{
		/// <summary>
		/// Returns true if this kind of metadata exists in the file, false if not
		/// </summary>
		bool Exists
		{
			get;
		}
		/// <summary>
		/// Title of the track
		/// </summary>
		String Title
		{
			get;
		}
		/// <summary>
		/// Artist
		/// </summary>
		String Artist
		{
			get;
		}
        /// <summary>
        /// Composer
        /// </summary>
        String Composer
        {
            get;
        }
		/// <summary>
		/// Comments
		/// </summary>
		String Comment
		{
			get;
		}
		/// <summary>
		/// Genre
		/// </summary>
		String Genre
		{
			get;
		}
		/// <summary>
		/// Track number
		/// </summary>
		ushort Track
		{
			get;
		}
		/// <summary>
		/// Disc number
		/// </summary>
		ushort Disc
		{
			get;
		}

		/// <summary>
		/// Year
		/// </summary>
		String Year
		{
			get;
		}
		/// <summary>
		/// Title of the album
		/// </summary>
		String Album
		{
			get;
		}
        /// <summary>
        /// Rating of the track
        /// </summary>
        ushort Rating
        {
            get;
        }
        /// <summary>
        /// List of picture IDs stored in the tag
        /// </summary>
        IList<MetaReaderFactory.PIC_CODE> Pictures
        {
            get;
        }
	}
}
