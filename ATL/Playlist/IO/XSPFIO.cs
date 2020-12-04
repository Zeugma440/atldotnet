using System.IO;
using System.Collections.Generic;
using System;
using System.Xml;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// XSPF (spiff) playlist manager
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
#pragma warning disable S101 // Types should be named in PascalCase
    public class XSPFIO : PlaylistIO
#pragma warning restore S101 // Types should be named in PascalCase
    {
        /// <inheritdoc/>
        protected override void getFiles(FileStream fs, IList<string> result)
        {
            using (XmlReader source = XmlReader.Create(fs))
            {
                while (source.ReadToFollowing("track"))
                {
                    while (source.Read())
                    {
                        // TODO handle image element = fetch track picture from playlists info 
                        if (source.NodeType == XmlNodeType.Element && source.Name.Equals("location", StringComparison.OrdinalIgnoreCase)) parseLocation(source, result);
                        else if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("track", StringComparison.OrdinalIgnoreCase)) break;
                    }
                }
            }

        }

        private void parseLocation(XmlReader source, IList<string> result)
        {
            source.Read();
            if (source.NodeType == XmlNodeType.Text) result.Add(decodeLocation(source.Value));
        }

        /// <inheritdoc/>
        protected override void setTracks(FileStream fs, IList<Track> result)
        {
            XmlWriter writer = XmlWriter.Create(fs, generateWriterSettings());
            writer.WriteStartElement("playlist", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("title");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            writer.WriteStartElement("trackList");
            foreach (Track t in result)
            {
                writer.WriteStartElement("track");

                writer.WriteStartElement("location");
                writer.WriteString(encodeLocation(t.Path));
                writer.WriteEndElement();

                if (t.Title != null && t.Title.Length > 0)
                {
                    writer.WriteStartElement("title");
                    writer.WriteString(t.Title);
                    writer.WriteEndElement();
                }

                if (t.Artist != null && t.Artist.Length > 0)
                {
                    writer.WriteStartElement("creator");
                    writer.WriteString(t.Artist);
                    writer.WriteEndElement();
                }

                if (t.Album != null && t.Album.Length > 0)
                {
                    writer.WriteStartElement("album");
                    writer.WriteString(t.Album);
                    writer.WriteEndElement();
                }

                if (t.Comment != null && t.Comment.Length > 0)
                {
                    writer.WriteStartElement("annotation");
                    writer.WriteString(t.Comment);
                    writer.WriteEndElement();
                }

                if (t.TrackNumber > 0)
                {
                    writer.WriteStartElement("trackNum");
                    writer.WriteValue(t.TrackNumber);
                    writer.WriteEndElement();
                }

                if (t.DurationMs > 0)
                {
                    writer.WriteStartElement("duration");
                    writer.WriteValue((long)Math.Round(t.DurationMs));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // track
            }

            writer.WriteEndElement(); // tracklist
            writer.WriteEndElement(); // playlist

            writer.Flush();
            writer.Close();
        }
    }
}
