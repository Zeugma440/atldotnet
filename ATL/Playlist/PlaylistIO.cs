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
        protected PlaylistIO(string filePath, bool supportsRelativePaths = true)
        {
            Path = filePath;
            this.supportsRelativePaths = supportsRelativePaths;
            LogDelegator.GetLocateDelegate()(filePath);
            load();
        }


        /// <summary>
        /// Read the Locations of the track files described by the playlist using the given Stream
        /// and put them into the given list
        /// </summary>
        /// <param name="fs">FileStream to use to read the values</param>
        /// <param name="result">List that will receive the values</param>
        protected abstract void getFiles(FileStream fs, IList<FileLocation> result);

        /// <summary>
        /// Read the tracks described by the playlist using the given Stream
        /// and put them into the given list
        /// </summary>
        /// <param name="fs">FileStream to use to read the tracks</param>
        /// <param name="result">List that will receive the tracks</param>
        /// <returns>True if save succeeds; false if it fails
        /// NB : Failure reason is saved to the ATL log</returns>
        protected abstract void setTracks(FileStream fs, IList<Track> result);


        /// <inheritdoc/>
        public bool Save()
        {
            bool havePathsChanged = initialFilePaths.Select(ifp => ifp.Path).SequenceEqual(FilePaths);
            bool haveMetaChanged = !initialMetadata.SequenceEqual(Tracks.Select(t => new TagHolder(t.toTagData())));

            LogDelegator.GetLocateDelegate()(Path);
            try
            {
                using FileStream fs = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                if (Encoding.Equals(PlaylistFormat.FileEncoding.UTF8_BOM)) fs.Write(BOM_UTF8, 0, 3);
                if (haveMetaChanged) setTracks(fs, Tracks);
                else
                {
                    IList<Track> trackList = FilePaths.Select(file => new Track(file, false)).ToList();
                    setTracks(fs, trackList);
                }
                load();

                return true;
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
                return false;
            }

        }

        private void load()
        {
            var locations = new List<FileLocation>();
            Tracks = new List<Track>();
            FilePaths = new List<string>();
            try
            {
                using FileStream fs = new FileStream(Path, FileMode.Open, FileAccess.Read);
                getFiles(fs, locations);
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

                var t = new Track(location.Path);
                Tracks.Add(t);
                initialMetadata.Add(new TagHolder(t.toTagData()));
            }
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
            bool isAbsolute = Settings.PlaylistWriteAbsolutePath || (initialLocation != null && initialLocation.IsAbsolute);
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
        /// Decodes the given location to an absolute filepath and adds it to the given list
        /// </summary>
        /// <param name="source">Xml source to get the location from</param>
        /// <param name="attributeName">Attribute name in current Xml source to get the location from</param>
        /// <param name="result">List of locations to add the found location to</param>
        protected void decodeLocation(XmlReader source, string attributeName, IList<FileLocation> result)
        {
            FileLocation location = decodeLocation(source.GetAttribute(attributeName));
            if (location != null) result.Add(location);
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
    }
}