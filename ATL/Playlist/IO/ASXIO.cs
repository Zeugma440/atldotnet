using System.IO;
using System.Text;
using System.Collections.Generic;
using System;
using System.Xml;
using ATL.Logging;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// ASX playlist manager
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class ASXIO : PlaylistIO
    {
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.ReadToFollowing("ENTRY"))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element && source.Name.Equals("REF", StringComparison.OrdinalIgnoreCase)) parseLocation(source, "HREF", result);
                        else if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("ENTRY", StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
            }

        }

        protected override void setTracks(FileStream fs, IList<Track> values)
        {
            XmlWriter writer = XmlWriter.Create(fs, getWriterSettings());
            writer.WriteStartElement("ASX", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("TITLE");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            foreach (Track t in values)
            {
                Uri trackUri = new Uri(t.Path, UriKind.RelativeOrAbsolute);

                writer.WriteStartElement("ENTRY");

                writer.WriteStartElement("REF");
                writer.WriteAttributeString("HREF", trackUri.IsAbsoluteUri ? trackUri.AbsoluteUri : trackUri.OriginalString);
                writer.WriteEndElement();

                if (t.Title != null && t.Title.Length > 0)
                {
                    writer.WriteStartElement("TITLE");
                    writer.WriteString(t.Title);
                    writer.WriteEndElement();
                }

                if (t.Artist != null && t.Artist.Length > 0)
                {
                    writer.WriteStartElement("AUTHOR");
                    writer.WriteString(t.Artist);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement(); // entry
            }

            writer.Flush();
            writer.Close();
        }
    }
}
