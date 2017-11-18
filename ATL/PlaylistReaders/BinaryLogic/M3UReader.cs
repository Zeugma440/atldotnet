using System;
using System.IO;
using System.Collections;
using System.Text;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.PlaylistReaders.BinaryLogic
{
	/// <summary>
    /// M3U/M3U8 playlist reader
	/// </summary>
	public class M3UReader : PlaylistReader
	{

        public override void GetFiles(FileStream fs, IList<String> result)
		{
			TextReader source = null;

			Encoding encoding = null;
			if (System.IO.Path.GetExtension(FFileName).ToLower().Equals(".m3u8"))
			{
				encoding = System.Text.Encoding.UTF8;
			}

			if (null == encoding)
			{
				encoding = StreamUtils.GetEncodingFromFileBOM(fs);
			}

            using (source = new StreamReader(fs, encoding))
            {
                String s = source.ReadLine();
                while (s != null)
                {
                    // If the read line isn't a metadata, it's a file path
                    if ((s.Length > 0) && (s[0] != '#'))
                    {
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
