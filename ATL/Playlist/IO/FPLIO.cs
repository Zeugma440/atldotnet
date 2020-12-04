using System.IO;
using System.Collections.Generic;
using System;
using Commons;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// Foobar2000 EXPERIMENTAL playlist reader
    /// Since the format is not open and can be subject to change by
    /// fb2k developers at any time, this reader is experimental : it is not guaranteed 
    /// to work with all versions of FPL files
    /// </summary>
#pragma warning disable S101 // Types should be named in PascalCase
    public class FPLIO : PlaylistIO
#pragma warning restore S101 // Types should be named in PascalCase
    {
        private static byte[] FILE_IDENTIFIER = Utils.Latin1Encoding.GetBytes("file://");

        /// <inheritdoc/>
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            while (StreamUtils.FindSequence(fs, FILE_IDENTIFIER))
            {
                string filePath = StreamUtils.ReadNullTerminatedString(fs, UTF8_NO_BOM);
                result.Add(decodeLocation(filePath));
            }
        }

        /// <inheritdoc/>
        protected override void setTracks(FileStream fs, IList<Track> result)
        {
            throw new NotImplementedException("FPL writing not implemented");
        }
    }
}
