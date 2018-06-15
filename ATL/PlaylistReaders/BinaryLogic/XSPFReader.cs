using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.PlaylistReaders.BinaryLogic
{
    /// <summary>
    /// XSPF (spiff) playlist reader
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class XSPFReader : PlaylistReader
    {

        public override void GetFiles(FileStream fs, IList<String> result)
        {
            Uri uri;

            // The following flags indicate if the parser is currently reading
            // the content of the corresponding tag
            bool inPlaylist = false;
            bool inTracklist = false;
            bool inTrack = false;
            bool inLocation = false;
            bool inImage = false;

            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.Read())
                {
                    switch (source.NodeType)
                    {
                        case XmlNodeType.Element: // Element start
                            if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = true;
                            }
                            else if (inPlaylist && source.Name.Equals("tracklist", StringComparison.OrdinalIgnoreCase))
                            {
                                inTracklist = true;
                            }
                            else if (inTracklist && source.Name.Equals("track", StringComparison.OrdinalIgnoreCase))
                            {
                                inTrack = true;
                            }
                            else if (inTrack && source.Name.Equals("location", StringComparison.OrdinalIgnoreCase))
                            {
                                inLocation = true;
                            }
                            else if (inTrack && source.Name.Equals("image", StringComparison.OrdinalIgnoreCase))
                            {
                                inImage = true;
                            }
                            break;

                        case XmlNodeType.Text:
                            if (inLocation || inImage)
                            {
                                try
                                {
                                    uri = new Uri(source.Value);
                                    if (uri.IsFile)
                                    {
                                        if (inLocation)
                                        {
                                            if (!System.IO.Path.IsPathRooted(uri.LocalPath))
                                            {
                                                result.Add(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FFileName), uri.LocalPath));
                                            }
                                            else
                                            {
                                                result.Add(uri.LocalPath);
                                            }
                                        }
                                        //else if (inImage) result.Add(System.IO.Path.GetFullPath(uri.LocalPath));
                                        //TODO fetch track picture from playlists info ?
                                    }
                                }
                                catch (UriFormatException)
                                {
                                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, result + " is not a valid URI [" + FFileName + "]");
                                }
                            }
                            break;

                        case XmlNodeType.EndElement: // Element end
                            if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase))
                            {
                                inPlaylist = false;
                            }
                            else if (source.Name.Equals("tracklist", StringComparison.OrdinalIgnoreCase))
                            {
                                inTracklist = false;
                            }
                            else if (source.Name.Equals("track", StringComparison.OrdinalIgnoreCase))
                            {
                                inTrack = false;
                            }
                            else if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase))
                            {
                                inLocation = false;
                            }
                            else if (inTrack && source.Name.Equals("image", StringComparison.OrdinalIgnoreCase))
                            {
                                inImage = false;
                            }
                            break;
                    } // switch
                } // while
            } // using
        }
    }
}
