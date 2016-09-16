using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio metadata information
	/// </summary>
	public interface IMetaDataIO
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
        IList<MetaDataIOFactory.PIC_CODE> Pictures
        {
            get;
        }
        /// <summary>
        /// Physical size of the tag (bytes)
        /// </summary>
        int Size
        {
            get;
        }
        /// <summary>
        /// Physical offset of the tag on its host file (bytes)
        /// </summary>
        long Offset
        {
            get;
        }

        /// <summary>
        /// Parses the binary data read from the given reader
        /// </summary>
        /// <param name="source">Reader to parse data from</param>
        /// <param name="pictureStreamHandler">Delegate to use when reading picture data</param>
        /// <returns></returns>
        bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler);

        // TODO Doc
        bool Write(BinaryReader r, BinaryWriter w, TagData tag);
    }
}
