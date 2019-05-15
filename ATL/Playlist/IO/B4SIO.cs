using System.IO;
using System.Text;
using System.Collections.Generic;
using System;
using System.Xml;
using ATL.Logging;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// B4S playlist manager
    /// </summary>
    public class B4SIO : PlaylistIO
    {
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.ReadToFollowing("entry"))
                {
                    parseLocation(source, result);
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
            }
        }

        private void parseLocation(XmlReader source, IList<string> result)
        {
            string href = source.GetAttribute("Playstring").Replace("file:", "");
            if (!System.IO.Path.IsPathRooted(href))
            {
                href = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FFileName), href);
            }
            result.Add(href);
        }

        protected override void setTracks(FileStream fs, IList<Track> values)
        {
            XmlWriterSettings settings = getWriterSettings();
            settings.OmitXmlDeclaration = false;
            settings.ConformanceLevel = ConformanceLevel.Document;

            XmlWriter writer = XmlWriter.Create(fs, settings);
            writer.WriteStartDocument(true);
            writer.WriteStartElement("WinampXML");

            writer.WriteStartElement("playlist");
            writer.WriteAttributeString("num_entries", values.Count.ToString());
            writer.WriteAttributeString("label", "Playlist");

            // Open tracklist
            writer.WriteStartElement("tracklist");
            foreach (Track t in values)
            {
                writer.WriteStartElement("entry");
                writer.WriteAttributeString("Playstring", "file:" + t.Path);

                string label = "";
                if (t.Title != null && t.Title.Length > 0) label = t.Title;
                if (0 == label.Length) label = System.IO.Path.GetFileNameWithoutExtension(t.Path);

                writer.WriteStartElement("Name");
                writer.WriteString(label);
                writer.WriteEndElement();

                if (t.DurationMs > 0)
                {
                    writer.WriteStartElement("Length");
                    writer.WriteValue((long)Math.Round(t.DurationMs));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // entry
            }
            writer.WriteEndDocument();

            writer.Flush();
            writer.Close();
        }
    }
}
