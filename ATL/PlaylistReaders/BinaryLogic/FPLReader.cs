using System;
using System.IO;
using System.Text;
using System.Collections;
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


		public override void GetFiles(FileStream fs, ref IList<String> result)
		{
            BinaryReader source = new BinaryReader(fs);
            String filePath;

            while (StreamUtils.FindSequence(ref source, FILE_IDENTIFIER, 0))
            {
                filePath = StreamUtils.ReadNullTerminatedString(source, Encoding.UTF8);
                result.Add(System.IO.Path.GetFullPath(filePath));
            }

            source.Close();
		}
	}
}
