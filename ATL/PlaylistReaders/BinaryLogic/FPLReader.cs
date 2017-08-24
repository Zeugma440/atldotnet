using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ATL.PlaylistReaders.BinaryLogic
{
	/// <summary>
	/// Foobar2000 EXPERIMENTAL playlist reader
    /// Since the format is not open and can be subject to change by
    /// fb2k developers at any time, this reader is experimental : it is not guaranteed 
    /// to work with all versions of FPL files
	/// </summary>
	public class FPLReader : PlaylistReader
	{
        private static byte[] FILE_IDENTIFIER =  new byte[7] {102,105,108,101,58,47,47}; // "file://"


		public override void GetFiles(FileStream fs, ref IList<string> result)
		{
            string filePath;
            string playlistPath = System.IO.Path.GetDirectoryName(fs.Name) + System.IO.Path.DirectorySeparatorChar;


            BinaryReader source = new BinaryReader(fs);
            while (StreamUtils.FindSequence(source, FILE_IDENTIFIER))
            {
                filePath = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                if (!System.IO.Path.IsPathRooted(filePath)) filePath = playlistPath + filePath;
                result.Add(filePath);
            }
		}
	}
}
