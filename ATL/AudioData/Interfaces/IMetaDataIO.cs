using ATL.AudioData.IO;
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
        [Obsolete("Use Popularity")]
        ushort Rating
        {
            get;
        }
        /// <summary>
        /// Rating of the track, from 0% to 100%
        /// </summary>
        float Popularity
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
        /// Title of the original album
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
        ///     PictureInfo.PIC_TYPE : internal, normalized picture type
        ///     PictureInfo.NativePicCode : native picture code (useful when exploiting the UNSUPPORTED picture type)
        ///     NB : PictureInfo.PictureData (raw binary picture data) is _not_ valued here; see EmbeddedPictures field
        /// </summary>
        IList<PictureInfo> PictureTokens
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
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        IDictionary<string, string> AdditionalFields
        {
            get;
        }
        /// <summary>
        /// Contains any other metadata field that is not represented by a getter in the above interface
        /// </summary>
        IList<ChapterInfo> Chapters
        {
            get;
        }

        /// <summary>
        /// List of pictures stored in the tag
        /// NB : PictureInfo.PictureData (raw binary picture data) is valued
        /// </summary>
        IList<PictureInfo> EmbeddedPictures
        {
            get;
        }

        /// <summary>
        /// Set metadata to be written using the given embedder
        /// </summary>
        /// <param name="embedder">Metadata embedder to be used to write current metadata</param>
        void SetEmbedder(IMetaDataEmbedder embedder);

        /// <summary>
        /// Parse binary data read from the given stream
        /// </summary>
        /// <param name="source">Reader to parse data from</param>
        /// <param name="readTagParams">Tag reading parameters</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams);

        /// <summary>
        /// Add the specified information to current tag information :
        ///   - Any existing field is overwritten
        ///   - Any non-specified field is kept as is
        /// </summary>
        /// <param name="r">Reader to the resource to edit</param>
        /// <param name="w">Writer to the resource to edit</param>
        /// <param name="tag">Tag information to be added</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Write(BinaryReader r, BinaryWriter w, TagData tag);

        /// <summary>
        /// Remove current tag
        /// </summary>
        /// <param name="w">Writer to the resource to edit</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Remove(BinaryWriter w);

        /// <summary>
        /// Clears all metadata
        /// </summary>
        void Clear();
    }
}
