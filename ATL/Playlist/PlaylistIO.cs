#nullable enable
using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ATL.AudioData;
using ATL.AudioData.IO;

namespace ATL.Playlist
{
    /// <summary>
    /// Asbtract parent for all playlist manipulation (I/O) classes
    /// Contrains all common methods and attributes
    /// </summary>
    public abstract class PlaylistIO : IPlaylistIO
    {
        /// <summary>
        /// Byte Order Mark of UTF-8 files
        /// </summary>
        public static readonly byte[] BOM_UTF8 = { 0xEF, 0xBB, 0xBF };

        /// <summary>
        /// .NET Encoding for UTF-8 with no Byte Order Mark
        /// </summary>
        protected static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(false);

        /// <summary>
        /// Latin-1 encoding used as ANSI
        /// </summary>
        protected static readonly Encoding ANSI = Utils.Latin1Encoding;

        /// <summary>
        /// Represent the location of a file
        /// </summary>
        protected sealed class FileLocation
        {
            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="path">Path of the file</param>
            /// <param name="isAbsolute">Indicate if the given path is absolute or relative</param>
            public FileLocation(string path, bool isAbsolute)
            {
                Path = path;
                IsAbsolute = isAbsolute;
            }

            /// <summary>
            /// Path of the file
            /// </summary>
            public string Path { get; set; }

            /// <summary>
            /// Indicate if Path is absolute or relative
            /// </summary>
            public bool IsAbsolute { get; set; }
        }



        /// <inheritdoc/>
        public string Path { get; }

        /// <inheritdoc/>
        public PlaylistFormat.LocationFormatting LocationFormatting { get; set; }

        /// <inheritdoc/>
        public PlaylistFormat.FileEncoding Encoding { get; set; }

        /// <inheritdoc/>
        public IList<string> FilePaths { get; set; }

        /// <summary>
        /// Initial fields, used to identify removed/changed ones
        /// </summary>
        private readonly ICollection<FileLocation> initialFilePaths = new List<FileLocation>();

        /// <inheritdoc/>
        public IList<Track> Tracks { get; set; }

        /// <summary>
        /// Initial metadata, used to identify removed/changed ones
        /// </summary>
        private readonly ICollection<IMetaData> initialMetadata = new List<IMetaData>();

        private readonly bool supportsRelativePaths;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="filePath">Path of the playlist file to load</param>
        /// <param name="supportsRelativePaths">Set to false to indicate the format doesn't support relative paths</param>
        protected PlaylistIO(string filePath, bool supportsRelativePaths = true)
        {
            Path = filePath;
            this.supportsRelativePaths = supportsRelativePaths;
            LogDelegator.GetLocateDelegate()(filePath);
            FilePaths = new List<string>();
            Tracks = new List<Track>();
            load();
        }


        /// <summary>
        /// Read the file locations and Tracks described by the playlist using the given Stream
        /// and put them into the given list.
        /// 
        /// Any metyadata (title, artist, description...) written in the playlist
        /// will override the track's metadata
        /// </summary>
        /// <param name="fs">FileStream to use to read the values</param>
        /// <param name="locations">List of Locations that will receive read values</param>
        /// <param name="tracks">List of Tracks that will receive read values</param>
        protected abstract void load(FileStream fs, IList<FileLocation> locations, IList<Track> tracks);

        /// <summary>
        /// Write the given Tracks using the given Stream
        /// </summary>
        /// <param name="fs">FileStream to use to read the tracks</param>
        /// <param name="tracks">Track to write to the playlist file</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        protected abstract void save(FileStream fs, IList<Track> tracks);


        /// <inheritdoc/>
        public bool Save()
        {
            bool haveMetaChanged = !initialMetadata.SequenceEqual(Tracks.Select(t => new TagHolder(t.toTagData())));

            LogDelegator.GetLocateDelegate()(Path);
            try
            {
                using FileStream fs = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                if (Encoding.Equals(PlaylistFormat.FileEncoding.UTF8_BOM)) fs.Write(BOM_UTF8, 0, 3);
                if (haveMetaChanged) save(fs, Tracks);
                else // Use file paths
                {
                    IList<Track> trackList = new List<Track>();
                    // Compute tracks to save, using :
                    // - Initial tracks, for file paths found in initial file paths
                    // - Freshly loaded Tracks, for new file paths
                    foreach (var path in FilePaths)
                    {
                        if (initialFilePaths.Any(location => location.Path.Equals(path)))
                        {
                            var existingTrack = Tracks.FirstOrDefault(t => t.Path.Equals(path));
                            if (existingTrack != null) trackList.Add(existingTrack);
                        }
                        else
                        {
                            trackList.Add(new Track(path));
                        }
                    }
                    save(fs, trackList);
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
                return false;
            }
            load();
            return true;
        }

        private void load()
        {
            Tracks.Clear();
            FilePaths.Clear();

            var locations = new List<FileLocation>();
            try
            {
                using FileStream fs = new FileStream(Path, FileMode.Open, FileAccess.Read);
                load(fs, locations, Tracks);
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }

            initialFilePaths.Clear();
            initialMetadata.Clear();
            foreach (var location in locations)
            {
                FilePaths.Add(location.Path);
                initialFilePaths.Add(location);
            }

            foreach (var t in Tracks) initialMetadata.Add(new TagHolder(t.toTagData()));
        }

        /// <summary>
        /// Generate XmlWriterSettings from the current file's and global settings
        /// </summary>
        /// <returns>New instance of XmlWriterSettings</returns>
        protected XmlWriterSettings generateWriterSettings()
        {
            XmlWriterSettings settings = new XmlWriterSettings
            {
                CloseOutput = true,
                Encoding = Encoding switch
                {
                    PlaylistFormat.FileEncoding.ANSI => ANSI,
                    _ => UTF8_NO_BOM
                },
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment,
                Indent = true,
                DoNotEscapeUriAttributes = false
            };

            return settings;
        }

        /// <summary>
        /// Encodes the given location usingcurrent LocationFormatting
        /// </summary>
        /// <param name="location">File path to encode</param>
        /// <returns></returns>
        protected string encodeLocation(string location)
        {
            var initialLocation = initialFilePaths.FirstOrDefault(fl => fl.Path.Equals(location));
            // Don't change path format if we're rewriting an existing path; use current settings if we're writing a new path
            bool isAbsolute = (initialLocation != null && initialLocation.IsAbsolute) || (null == initialLocation && Settings.PlaylistWriteAbsolutePath);
            if (!location.StartsWith("http") && (System.IO.Path.IsPathRooted(location) ^ isAbsolute))
            {
                if (isAbsolute || !supportsRelativePaths)
                {
                    location = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? "", location);
                }
                else
                {
                    location = location.Replace(System.IO.Path.GetDirectoryName(Path) ?? "", "");
                    if (location.StartsWith(System.IO.Path.DirectorySeparatorChar)) location = location[1..];
                    if (location.StartsWith(System.IO.Path.AltDirectorySeparatorChar)) location = location[1..];
                }
            }

            switch (LocationFormatting)
            {
                case PlaylistFormat.LocationFormatting.RFC_URI:
                    Uri trackUri = new Uri(location, UriKind.RelativeOrAbsolute);
                    return trackUri.IsAbsoluteUri ? trackUri.AbsoluteUri : trackUri.OriginalString;
                case PlaylistFormat.LocationFormatting.MS_URI:
                    return "file://" + location;
                case PlaylistFormat.LocationFormatting.Winamp_URI:
                    return "file:" + location;
                case PlaylistFormat.LocationFormatting.FilePath:
                case PlaylistFormat.LocationFormatting.Undefined:
                default:
                    return location;
            }
        }

        /// <summary>
        /// Return the given location decoded to an absolute filepath
        /// </summary>
        /// <param name="source">Xml source to get the location from</param>
        /// <param name="attributeName">Attribute name in current Xml source to get the location from</param>
        protected FileLocation? decodeLocation(XmlReader source, string attributeName)
        {
            var val = source.GetAttribute(attributeName);
            return val != null ? decodeLocation(val) : null;
        }

        /// <summary>
        /// Decodes the given location to an absolute filepath
        /// </summary>
        /// <param name="href">Location to decode (can be an URI of any form or a relative filepath)</param>
        /// <returns>Absolute filepath corresponding to the given location</returns>
        protected FileLocation decodeLocation(string href)
        {
            bool isAbsolute = true;

            // Is it an URI ?
            // Try and replace all \'s by /'s to detect URIs even if the location has been badly formatted
            var hrefUri = href.Replace('\\', '/');
            if (hrefUri.Contains("://")) // RFC URI
            {
                try
                {
                    var uri = new Uri(hrefUri);
                    if (uri.IsFile)
                    {
                        if (!System.IO.Path.IsPathRooted(uri.LocalPath))
                        {
                            return new FileLocation(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? "", uri.LocalPath), false);
                        }

                        // Hack to avoid paths being rooted by a double '\', thus making them unreadable by System.IO.Path
                        return new FileLocation(uri.LocalPath.Replace(
                            "" + System.IO.Path.DirectorySeparatorChar + System.IO.Path.DirectorySeparatorChar,
                            "" + System.IO.Path.DirectorySeparatorChar), true);
                    }
                }
                catch (UriFormatException)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, hrefUri + " is not a valid URI [" + Path + "]");
                }
            }

            href = href.Trim();
            if (!href.StartsWith("http", StringComparison.InvariantCultureIgnoreCase))
            {
                href = href.Replace("file:///", "").Replace("file://", "").Replace("file:", "").Replace('\\', System.IO.Path.DirectorySeparatorChar);
                if (!System.IO.Path.IsPathRooted(href))
                {
                    href = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? "", href);
                    isAbsolute = false;
                }
            }
            return new FileLocation(href, isAbsolute);
        }

        /// <summary>
        /// Parse a string from the given XmlReader if positioned on a Text node
        /// </summary>
        /// <param name="source">XmlReader to read from</param>
        /// <returns>Value, if the given XmlReader is positioned on a Text node; null instead</returns>
        protected static string? parseString(XmlReader source)
        {
            source.Read();
            return source.NodeType == XmlNodeType.Text ? source.Value : null;
        }

        /// <summary>
        /// Add the given elements to the given Location list and Track list
        /// </summary>
        /// <param name="location">File location to add</param>
        /// <param name="title">Track title to add</param>
        /// <param name="locations">Location list to populate</param>
        /// <param name="tracks">Track list to populate</param>
        protected static void addTrack(FileLocation location, string title, IList<FileLocation> locations, IList<Track> tracks)
        {
            var track = new Track(location.Path);
            if (title.Length > 0 && title != System.IO.Path.GetFileNameWithoutExtension(location.Path)) track.Title = title;
            tracks.Add(track);
            locations.Add(location);
        }

    }
}