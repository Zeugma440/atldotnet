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
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of playlist file to load</param>
        public SMILIO(string filePath) : base(filePath)
        {
        }

        /// <inheritdoc/>
        protected override void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks)
        {
            using XmlReader source = XmlReader.Create(fs);
            while (source.Read())
            {
                switch (source.NodeType)
                {
                    case XmlNodeType.Element: // Element start
                        if (source.Name.Equals("audio", StringComparison.OrdinalIgnoreCase)
                            || source.Name.Equals("media", StringComparison.OrdinalIgnoreCase))
                        {
                            FileLocation resourceLocation = getResourceLocation(source);
                            if (null != resourceLocation)
                            {
                                tracks.Add(new Track(resourceLocation.Path));
                                locations.Add(resourceLocation);
                            }
                        }
                        break;

                        //                    case XmlNodeType.Text:
                        //                        break;

                        //                    case XmlNodeType.EndElement: // Element end
                        //                        break;
                }
            }
        }

        // Most SMIL sample playlists store resource location with a relative path
        private FileLocation getResourceLocation(XmlReader source)
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

        /// <inheritdoc/>
        protected override void save(FileStream fs, IList<Track> tracks)
        {
            XmlWriter writer = XmlWriter.Create(fs, generateWriterSettings());
            writer.WriteStartElement("smil");

            writer.WriteStartElement("head");
            writer.WriteEndElement();

            writer.WriteStartElement("body");
            writer.WriteStartElement("seq");
            writer.WriteAttributeString("repeatCount", "indefinite");

            foreach (Track t in tracks)
            {
                writer.WriteStartElement("media");
                writer.WriteAttributeString("src", encodeLocation(t.Path));
                writer.WriteEndElement();
            }

            writer.Flush();
            writer.Close();
        }
    }
}
