using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.PlaylistReaders.BinaryLogic
{
    /// <summary>
    /// ASX playlist reader
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class ASXReader : PlaylistReader
    {

        public override void GetFiles(FileStream fs, IList<String> result)
        {
            // The following flags indicate if the parser is currently reading
            // the content of the corresponding tag
            bool inPlaylist = false;
            bool inTrack = false;

            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.Read())
                {
                    switch (source.NodeType)
                    {
                        case XmlNodeType.Element: // Element start
                            if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = true;
                            }
                            else if (inPlaylist && source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase))
                            {
                                inTrack = true;
                            }
                            else if (inTrack && source.Name.Equals("ref", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(getResourceLocation(source));
                            }
                            break;

                        //                    case XmlNodeType.Text:
                        //                        break;

                        case XmlNodeType.EndElement: // Element end
                            if (source.Name.Equals("asx", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = false;
                            }
                            else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase))
                            {
                                inTrack = false;
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
                if (source.Name.Equals("href", StringComparison.OrdinalIgnoreCase))
                {
                    result = source.Value;

                    if (result.Contains("://") && Uri.IsWellFormedUriString(result, UriKind.RelativeOrAbsolute))
                    {
                        try
                        {
                            Uri uri = new Uri(result);
                            if (uri.IsFile)
                            {
                                result = uri.LocalPath;
                            }
                        }
                        catch (UriFormatException)
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, result + " is not a valid URI [" + FFileName + "]");
                        }
                    }

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
