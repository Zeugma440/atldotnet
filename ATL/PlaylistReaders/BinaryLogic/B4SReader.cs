using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace ATL.PlaylistReaders.BinaryLogic
{
    /// <summary>
    /// B4S playlist reader
    /// </summary>
    public class B4SReader : PlaylistReader
    {

        public override void GetFiles(FileStream fs, IList<String> result)
        {
            // The following flags indicate if the parser is currently reading
            // the content of the corresponding tag
            bool inPlaylist = false;
            bool inTracklist = false;

            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.Read())
                {
                    switch (source.NodeType)
                    {
                        case XmlNodeType.Element: // Element start
                            if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = true;
                            }
                            else if (inPlaylist && source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase))
                            {
                                inTracklist = true;
                            }
                            else if (inTracklist && source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(getResourceLocation(source));
                            }
                            break;

                        //                    case XmlNodeType.Text:
                        //                        break;

                        case XmlNodeType.EndElement: // Element end
                            if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = false;
                            }
                            else if (inPlaylist && source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase))
                            {
                                inTracklist = false;
                            }
                            break;
                    }
                }
            }
        }

        private String getResourceLocation(XmlReader source)
        {
            String result = "";
            while (source.MoveToNextAttribute()) // Read the attributes.
            {
                if (source.Name.Equals("Playstring", StringComparison.OrdinalIgnoreCase))
                {
                    // Trim the "file:" prefix
                    result = source.Value.Substring(5, source.Value.Length - 5);

                    if (!System.IO.Path.IsPathRooted(result))
                    {
                        result = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FFileName), result);
                    }
                }
            }
            return result;
        }
    }
}
