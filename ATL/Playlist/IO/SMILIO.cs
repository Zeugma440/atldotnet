using System.IO;
using System.Text;
using System.Collections.Generic;
using System;
using System.Xml;
using ATL.Logging;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// SMIL playlist manager
    /// 
    /// This is a very basic implementation that lists every single audio file in the playlist
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class SMILIO : PlaylistIO
    {
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            using (XmlReader source = XmlReader.Create(fs))
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
        private String getResourceLocation(XmlReader source)
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
                            }
                            catch (UriFormatException)
                            {
                                LogDelegator.GetLogDelegate()(Log.LV_WARNING, result + " is not a valid URI");
                            }
                        }
                        else
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

        protected override void setTracks(FileStream fs, IList<Track> values)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            settings.Encoding = Encoding.UTF8;

            XmlWriter writer = XmlWriter.Create(fs, settings);

            writer.WriteStartDocument();
            writer.WriteStartElement("smil");

            writer.WriteStartElement("head");
            writer.WriteEndElement();

            writer.WriteStartElement("body");
            writer.WriteStartElement("seq");
            writer.WriteAttributeString("repeatCount", "indefinite");

            foreach (Track t in values)
            {
                Uri trackUri = new Uri(t.Path, UriKind.RelativeOrAbsolute);

                writer.WriteStartElement("media");
                writer.WriteAttributeString("src", trackUri.IsAbsoluteUri ? trackUri.AbsolutePath : trackUri.OriginalString);
                writer.WriteEndElement();
            }

            writer.WriteEndDocument();

            writer.Close();
        }
    }
}
