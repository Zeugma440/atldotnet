
using System.IO;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData
{
    public interface IMetaDataEmbedder
    {
        /// <summary>
        /// Indicates if file format has embedded ID3v2 tag (i.e. not at the beginning of file -as of standard ID3v2-, but within a specific chunk)
        /// Return values
        ///     -1 : Allowed by file format; status unknown because file has not been read yet
        ///     0  : Allowed by file format, but not detected on this particular file
        ///     >0 : Offset of detected embedded ID3v2
        /// </summary>
        long HasEmbeddedID3v2
        {
            get;
        }

        uint TagHeaderSize
        {
            get;
        }

        Zone Id3v2Zone
        {
            get;
        }


        void WriteTagHeader(BinaryWriter w, long tagSize);
    }
}
