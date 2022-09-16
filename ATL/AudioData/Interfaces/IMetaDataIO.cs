using ATL.AudioData.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ATL.AudioData
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio metadata information
	/// </summary>
	public interface IMetaDataIO : IMetaData
	{
        /// <summary>
        /// Returns true if this kind of metadata exists in the file, false if not
        /// </summary>
        bool Exists
        {
            get;
        }

        /// <summary>
        /// List of picture IDs stored in the tag
        ///     PictureInfo.PIC_TYPE : internal, normalized picture type
        ///     PictureInfo.NativePicCode : native picture code (useful when exploiting the UNSUPPORTED picture type)
        ///     NB : PictureInfo.PictureData (raw binary picture data) is _not_ valued here; see EmbeddedPictures field
        /// </summary>
        [Obsolete("Use PictureInfo instead", false)]
        IList<PictureInfo> PictureTokens
        {
            get;
        }

        /// <summary>
        /// Size of padding area, if any
        /// </summary>
        long PaddingSize
        {
            get;
        }

        /// <summary>
        /// Physical size of the tag (bytes)
        /// </summary>
        long Size
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
        /// <param name="writeProgress">Progress to be updated during write operations</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Write(BinaryReader r, Stream w, TagData tag, IProgress<float> writeProgress = null);

        Task<bool> WriteAsync(BinaryReader r, Stream w, TagData tag, IProgress<float> writeProgress = null);

        /// <summary>
        /// Remove current tag
        /// </summary>
        /// <param name="w">Writer to the resource to edit</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Remove(Stream s);

        /// <summary>
        /// Clear all metadata
        /// </summary>
        void Clear();
    }
}
