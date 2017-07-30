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
		string Title
		{
			get;
		}
		/// <summary>
		/// Artist
		/// </summary>
		string Artist
		{
			get;
		}
        /// <summary>
        /// Composer
        /// </summary>
        string Composer
        {
            get;
        }
		/// <summary>
		/// Comments
		/// </summary>
		string Comment
		{
			get;
		}
		/// <summary>
		/// Genre
		/// </summary>
		string Genre
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
		string Year
		{
			get;
		}
		/// <summary>
		/// Title of the album
		/// </summary>
		string Album
		{
			get;
		}
        /// <summary>
        /// Rating of the track, from 1 to 5
        /// </summary>
        ushort Rating
        {
            get;
        }
        /// <summary>
        /// Copyright
        /// </summary>
        string Copyright
        {
            get;
        }
        /// <summary>
        /// Original artist
        /// </summary>
        string OriginalArtist
        {
            get;
        }
        /// <summary>
        /// Original album
        /// </summary>
        string OriginalAlbum
        {
            get;
        }
        /// <summary>
        /// General description
        /// </summary>
        string GeneralDescription
        {
            get;
        }
        /// <summary>
        /// Publisher
        /// </summary>
        string Publisher
        {
            get;
        }
        /// <summary>
        /// Album Artist
        /// </summary>
        string AlbumArtist
        {
            get;
        }
        /// <summary>
        /// Conductor
        /// </summary>
        string Conductor
        {
            get;
        }

        /// <summary>
        /// List of picture IDs stored in the tag
        ///     TagData.PIC_TYPE : internal, normalized picture type
        ///     byte : native picture code (useful when exploiting the UNSUPPORTED picture type)
        /// </summary>
        IList<TagData.PictureInfo> PictureTokens
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
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        IDictionary<string, string> AdditionalFields
        {
            get;
        }

        /// <summary>
        /// Parses the binary data read from the given reader
        /// </summary>
        /// <param name="source">Reader to parse data from</param>
        /// <param name="readTagParams">Tag reading parameters</param>
        /// <returns></returns>
        bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams);

        /// <summary>
        /// Add the specified information to current tag information :
        ///   - Any existing field is overwritten
        ///   - Any non-specified field is kept as is
        /// </summary>
        /// <param name="r">Reader to the resource to edit</param>
        /// <param name="w">Writer to the resource to edit</param>
        /// <param name="tag">Tag information to be added</param>
        /// <returns></returns>
        long Write(BinaryReader r, BinaryWriter w, TagData tag);
    }
}
