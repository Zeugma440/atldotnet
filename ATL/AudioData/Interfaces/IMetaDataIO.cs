using System.IO;
using System.Threading.Tasks;
using static ATL.AudioData.IO.MetaDataIO;

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
        /// <param name="source">Stream to parse data from</param>
        /// <param name="readTagParams">Tag reading parameters</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Read(Stream source, ReadTagParams readTagParams);

        /// <summary>
        /// Add the specified information to current tag information (direct call variant)
        ///   - Any existing field is overwritten
        ///   - Any non-specified field is kept as is
        /// </summary>
        /// <param name="s">Stream for the resource to edit</param>
        /// <param name="tag">Tag information to be added</param>
        /// <param name="args">Parameters to use</param>
        /// <param name="writeProgress">Progress to be updated during write operations</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Write(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null);

        /// <summary>
        /// Add the specified information to current tag information (async variant)
        ///   - Any existing field is overwritten
        ///   - Any non-specified field is kept as is
        /// </summary>
        /// <param name="s">Stream for the resource to edit</param>
        /// <param name="tag">Tag information to be added</param>
        /// <param name="args">Parameters to use</param>
        /// <param name="writeProgress">Progress to be updated during write operations</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        Task<bool> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null);

        /// <summary>
        /// Remove current tag (direct call variant)
        /// </summary>
        /// <param name="s">Stream for the resource to edit</param>
        /// <param name="args">Parameters to use</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        bool Remove(Stream s, WriteTagParams args);

        /// <summary>
        /// Remove current tag (async variant)
        /// </summary>
        /// <param name="s">Stream for the resource to edit</param>
        /// <param name="args">Parameters to use</param>
        /// <returns>true if the operation suceeded; false if not</returns>
        Task<bool> RemoveAsync(Stream s, WriteTagParams args);

        /// <summary>
        /// Clear all metadata
        /// </summary>
        void Clear();
    }
}
