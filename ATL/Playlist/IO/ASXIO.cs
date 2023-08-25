using System.IO;
using System.Collections.Generic;
using System;
using System.Xml;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// ASX playlist manager
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class ASXIO : PlaylistIO
    {
        /// <inheritdoc/>
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.ReadToFollowing("ENTRY"))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element && source.Name.Equals("REF", StringComparison.OrdinalIgnoreCase)) decodeLocation(source, "HREF", result);
                        else if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("ENTRY", StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
            }

        }

        /// <inheritdoc/>
        protected override void setTracks(FileStream fs, IList<Track> result)
        {
            XmlWriter writer = XmlWriter.Create(fs, generateWriterSettings());
            writer.WriteStartElement("ASX", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("TITLE");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            foreach (Track t in result)
            {
                writer.WriteStartElement("ENTRY");

                writer.WriteStartElement("REF");
                writer.WriteAttributeString("HREF", encodeLocation(t.Path));
                writer.WriteEndElement();

                if (!string.IsNullOrEmpty(t.Title))
                {
                    writer.WriteStartElement("TITLE");
                    writer.WriteString(t.Title);
                    writer.WriteEndElement();
                }

                if (!string.IsNullOrEmpty(t.Artist))
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
