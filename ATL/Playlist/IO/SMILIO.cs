using System.IO;
using System.Collections.Generic;
using System;
using System.Xml;

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
                            if (source.Name.Equals("audio", StringComparison.OrdinalIgnoreCase)
                                || source.Name.Equals("media", StringComparison.OrdinalIgnoreCase))
                            {
                                string resourceLocation = getResourceLocation(source);
                                if (null != resourceLocation) result.Add(resourceLocation);
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
        private string getResourceLocation(XmlReader source)
        {
            while (source.MoveToNextAttribute()) // Read the attributes.
            {
                if (source.Name.Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    return decodeLocation(source.Value);
                }
            }
            return null;
        }

        protected override void setTracks(FileStream fs, IList<Track> values)
        {
            XmlWriter writer = XmlWriter.Create(fs, getWriterSettings());
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
                writer.WriteAttributeString("src", trackUri.IsAbsoluteUri ? trackUri.AbsoluteUri : trackUri.OriginalString);
                writer.WriteEndElement();
            }

            writer.Flush();
            writer.Close();
        }
    }
}
