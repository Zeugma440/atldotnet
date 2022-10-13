
using System.IO;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData
{
    /// <summary>
    /// Describes an audio file that embeds standard metadata (e.g. ID3v2) in a native structure instead of keeping it at beginning/end of file as per standard use
    /// Currently used for AIFF/AIFC, DSF and WAV embedded ID3v2
    /// </summary>
    public interface IMetaDataEmbedder
    {
        /// <summary>
        /// Indicates if file format has an embedded ID3v2 tag
        /// Return values
        ///     -1 : Allowed by file format; status unknown because file has not been read yet
        ///     0  : Allowed by file format, but not detected on this particular file
        ///     >0 : Offset of detected embedded ID3v2
        /// </summary>
        long HasEmbeddedID3v2
        {
            get;
        }

        /// <summary>
        /// Size of the native header that precedes the ID3v2 embedded tag, if any (0 if no header)
        /// </summary>
        uint ID3v2EmbeddingHeaderSize
        {
            get;
        }

        /// <summary>
        /// Zone containing the ID3v2 tag
        /// </summary>
        Zone Id3v2Zone
        {
            get;
        }

        /// <summary>
        /// Writes the native header that immediately precedes the ID3v2 embedded tag in the given stream, using the given tag size
        /// </summary>
        /// <param name="s">Stream to write the header to</param>
        /// <param name="tagSize">Size of the written metadata</param>
        void WriteID3v2EmbeddingHeader(Stream s, long tagSize);

        /// <summary>
        /// Writes the native footer that immediately follows the ID3v2 embedded tag in the given stream, using the given tag size
        /// </summary>
        /// <param name="s">Stream to write the header to</param>
        /// <param name="tagSize">Size of the written metadata</param>
        void WriteID3v2EmbeddingFooter(Stream s, long tagSize);
    }
}
