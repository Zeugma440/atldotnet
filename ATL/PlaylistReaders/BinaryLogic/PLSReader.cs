using System;
using System.IO;
using System.Text;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.PlaylistReaders.BinaryLogic
{
	/// <summary>
    /// PLS playlist reader
	/// </summary>
	public class PLSReader : PlaylistReader
	{
        public override void GetFiles(FileStream fs, IList<String> result)
		{
            Encoding encoding = StreamUtils.GetEncodingFromFileBOM(fs);

            using (StreamReader source = new StreamReader(fs, encoding))
            {
                String s = source.ReadLine();
                int equalIndex;
                while (s != null)
                {
                    // If the read line isn't a metadata, it's a file path
                    if ("FILE" == s.Substring(0, 4).ToUpper())
                    {
                        equalIndex = s.IndexOf("=") + 1;
                        s = s.Substring(equalIndex, s.Length - equalIndex);
                        if (!System.IO.Path.IsPathRooted(s))
                        {
                            s = System.IO.Path.GetDirectoryName(FFileName) + System.IO.Path.DirectorySeparatorChar + s;
                        }
                        result.Add(System.IO.Path.GetFullPath(s));
                    }
                    s = source.ReadLine();
                }
            }
		}
	}
}
