#nullable enable
using System.IO;
using System.Collections.Generic;
using System;
using System.Xml;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// B4S playlist manager
    /// </summary>
    public class B4SIO : PlaylistIO
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public B4SIO(string filePath) : base(filePath, false)
        {
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            using XmlReader source = XmlReader.Create(fs);
            while (source.ReadToFollowing("entry"))
            {
                string? title = null;

                var location = decodeLocation(source, "Playstring");
                while (source.Read())
                {
                    if (source.NodeType == XmlNodeType.Element && source.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        title = parseString(source);

                    if (source.NodeType == XmlNodeType.EndElement
                        && source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase)) break;
                }

                if (null == location) continue;

                var track = new Track(location.Path);
                if (title != null) track.Title = title;
                tracks.Add(track);
                locations.Add(location);
            }
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            XmlWriterSettings settings = generateWriterSettings();
            settings.OmitXmlDeclaration = false;
            settings.ConformanceLevel = ConformanceLevel.Document;

            XmlWriter writer = XmlWriter.Create(fs, settings);
            writer.WriteStartDocument(true);
            writer.WriteStartElement("WinampXML");

            writer.WriteStartElement("playlist");
            writer.WriteAttributeString("num_entries", tracks.Count.ToString());
            writer.WriteAttributeString("label", "Playlist");

            // Open tracklist
            foreach (Track t in tracks)
            {
                writer.WriteStartElement("entry");
                // Although the unofficial standard is "file:" followed by the filepath, URI seems to work best with Winamp and VLC
                writer.WriteAttributeString("Playstring", encodeLocation(t.Path));

                string label = "";
                if (!string.IsNullOrEmpty(t.Title)) label = t.Title;
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

            writer.Flush();
            writer.Close();
        }
    }
}
