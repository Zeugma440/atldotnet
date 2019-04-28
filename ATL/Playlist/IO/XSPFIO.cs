using System.IO;
using System.Text;
using System.Collections.Generic;
using System;
using System.Xml;
using ATL.Logging;

namespace ATL.Playlist.IO
{
    /// <summary>
    /// XSPF (spiff) playlist manager
    /// 
    /// Implementation notes : Playlist items other than local files (e.g. file accessible via HTTP) are not supported
    /// </summary>
    public class XSPFIO : PlaylistIO
    {
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
            if (source.NodeType == XmlNodeType.Text)
            {
                try
                {
                    Uri uri = new Uri(source.Value);
                    if (uri.IsFile)
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
                }
                catch (UriFormatException)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, result + " is not a valid URI [" + FFileName + "]");
                }
            }
        }

        protected override void setTracks(FileStream fs, IList<Track> values)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            settings.Encoding = Encoding.UTF8;

            XmlWriter writer = XmlWriter.Create(fs, settings);

            writer.WriteStartDocument();
            writer.WriteStartElement("playlist", "http://xspf.org/ns/0/");

            // Write the title.
            writer.WriteStartElement("title");
            writer.WriteString("Playlist");
            writer.WriteEndElement();

            // Open tracklist
            writer.WriteStartElement("tracklist");
            foreach (Track t in values)
            {
                Uri trackUri = new Uri(t.Path, UriKind.RelativeOrAbsolute);

                writer.WriteStartElement("track");

                writer.WriteStartElement("location");
                writer.WriteString(trackUri.IsAbsoluteUri?trackUri.AbsolutePath:trackUri.OriginalString);
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
            writer.WriteEndDocument();

            writer.Close();
        }
    }
}
