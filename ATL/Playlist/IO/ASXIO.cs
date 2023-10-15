#nullable enable
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
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public ASXIO(string filePath) : base(filePath)
        {
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            using XmlReader source = XmlReader.Create(fs);
            while (source.ReadToFollowing("ENTRY"))
            {
                FileLocation? location = null;
                string? title = null;
                string? artist = null;

                while (source.Read())
                {
                    if (source.NodeType == XmlNodeType.Element)
                    {
                        if (source.Name.Equals("REF", StringComparison.OrdinalIgnoreCase))
                            location = decodeLocation(source, "HREF");
                        if (source.Name.Equals("TITLE", StringComparison.OrdinalIgnoreCase))
                            title = parseString(source);
                        if (source.Name.Equals("AUTHOR", StringComparison.OrdinalIgnoreCase))
                            artist = parseString(source);
                    }
                    else if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("ENTRY", StringComparison.OrdinalIgnoreCase)) break;
                }

                if (location != null)
                {
                    var track = new Track(location.Path);
                    if (!string.IsNullOrEmpty(title)) track.Title = title;
                    if (!string.IsNullOrEmpty(artist)) track.Artist = artist;
                    tracks.Add(track);
                    locations.Add(location);
                }
            }
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            XmlWriter writer = XmlWriter.Create(fs, generateWriterSettings());
            writer.WriteStartElement("ASX", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("TITLE");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            foreach (Track t in tracks)
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
