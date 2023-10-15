#nullable enable
using System.IO;
using System.Collections.Generic;
using System;
using System.Xml;
using Commons;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// XSPF (spiff) playlist manager
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class XSPFIO : PlaylistIO
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public XSPFIO(string filePath) : base(filePath)
        {
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            using XmlReader source = XmlReader.Create(fs);
            while (source.ReadToFollowing("track"))
            {
                FileLocation? location = null;
                string? title = null;
                string? artist = null;
                string? album = null;
                string? comment = null;
                string? tracknumber = null;

                while (source.Read())
                {
                    // TODO handle image element = fetch track picture from playlists info 
                    if (source.NodeType == XmlNodeType.Element)
                    {
                        if (source.Name.Equals("location", StringComparison.OrdinalIgnoreCase)) location = parseLocation(source);
                        else if (source.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) title = parseString(source);
                        else if (source.Name.Equals("creator", StringComparison.OrdinalIgnoreCase)) artist = parseString(source);
                        else if (source.Name.Equals("album", StringComparison.OrdinalIgnoreCase)) album = parseString(source);
                        else if (source.Name.Equals("annotation", StringComparison.OrdinalIgnoreCase)) comment = parseString(source);
                        else if (source.Name.Equals("trackNum", StringComparison.OrdinalIgnoreCase)) tracknumber = parseString(source);
                    }
                    else if (source.NodeType == XmlNodeType.EndElement && source.Name.Equals("track", StringComparison.OrdinalIgnoreCase)) break;
                }

                if (null == location) continue;
                var track = new Track(location.Path);
                if (title != null) track.Title = title;
                if (artist != null) track.Artist = artist;
                if (album != null) track.Album = album;
                if (comment != null) track.Comment = comment;
                if (tracknumber != null && Utils.IsNumeric(tracknumber)) track.TrackNumber = int.Parse(tracknumber);
                tracks.Add(track);
                locations.Add(location);
            }
        }

        private FileLocation? parseLocation(XmlReader source)
        {
            var str = parseString(source);
            return str != null ? decodeLocation(str) : null;
        }

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            XmlWriter writer = XmlWriter.Create(fs, generateWriterSettings());
            writer.WriteStartElement("playlist", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("title");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            writer.WriteStartElement("trackList");
            foreach (Track t in tracks)
            {
                writer.WriteStartElement("track");

                writer.WriteStartElement("location");
                writer.WriteString(encodeLocation(t.Path));
                writer.WriteEndElement();

                if (!string.IsNullOrEmpty(t.Title))
                {
                    writer.WriteStartElement("title");
                    writer.WriteString(t.Title);
                    writer.WriteEndElement();
                }

                if (!string.IsNullOrEmpty(t.Artist))
                {
                    writer.WriteStartElement("creator");
                    writer.WriteString(t.Artist);
                    writer.WriteEndElement();
                }

                if (!string.IsNullOrEmpty(t.Album))
                {
                    writer.WriteStartElement("album");
                    writer.WriteString(t.Album);
                    writer.WriteEndElement();
                }

                if (!string.IsNullOrEmpty(t.Comment))
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
