using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.PlaylistReaders.BinaryLogic
{
    /// <summary>
    /// SMIL playlist reader
    /// NB : Very basic implementation that lists every single audio file in the playlist
    /// </summary>
    public class SMILReader : PlaylistReader
    {

        public override void GetFiles(FileStream fs, IList<String> result)
        {
            using (XmlTextReader source = new XmlTextReader(fs))
            {
                while (source.Read())
                {
                    switch (source.NodeType)
                    {
                        case XmlNodeType.Element: // Element start
                            if (source.Name.Equals("audio", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(getResourceLocation(source));
                            }
                            else if (source.Name.Equals("media", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Add(getResourceLocation(source));
                            }
                            break;

                            //                    case XmlNodeType.Text:
                            //                        break;

                            //                    case XmlNodeType.EndElement: // Element end
                            //                        break;
                    }
                }

            }
        }

        // Most SMIL sample playlists store resource location with a relative path
        private String getResourceLocation(XmlTextReader source)
        {
            String result = "";
            while (source.MoveToNextAttribute()) // Read the attributes.
            {
                if (source.Name.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    result = source.Value;

                    // It it an URI ?
                    if (result.Contains("://"))
                    {
                        if (Uri.IsWellFormedUriString(result, UriKind.RelativeOrAbsolute))
                        {
                            try
                            {
                                Uri uri = new Uri(result);
                                if (uri.IsFile)
                                {
                                    result = uri.LocalPath;
                                }
                                else // other protocols (e.g. HTTP, SMB)
                                {
                                    //TODO other protocols
                                }
                            }
                            catch (UriFormatException)
                            {
                                LogDelegator.GetLogDelegate()(Log.LV_WARNING, result + " is not a valid URI [" + FFileName + "]");
                            }
                        } else
                        {
                            result = result.Replace("file:///", "").Replace("file://", "");
                        }
                    }
                    
                    if (!System.IO.Path.IsPathRooted(result))
                    {
                        result = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FFileName), result);
                    }
                    result = System.IO.Path.GetFullPath(result);
                }
            }
            return result;
        }
    }
}
