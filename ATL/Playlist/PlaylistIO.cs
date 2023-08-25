using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

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
        /// File Path of the playlist file
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Formatting of the track locations (file paths) within the playlist
        /// </summary>
        public PlaylistFormat.LocationFormatting LocationFormatting { get; set; }

        /// <summary>
        /// String encoding used within the playlist file
        /// </summary>
        public PlaylistFormat.FileEncoding Encoding { get; set; }

        /// <summary>
        /// Paths of the track files described by the playlist
        /// </summary>
        public IList<string> FilePaths
        {
            get => getFiles();
            set => setFiles(value);
        }

        /// <summary>
        /// Track files described by the playlist
        /// </summary>
        public IList<Track> Tracks
        {
            get => getTracks();
            set => setTracks(value);
        }

        /// <summary>
        /// Read the paths of the track files described by the playlist using the given Stream
        /// and put them into the given list
        /// </summary>
        /// <param name="fs">FileStream to use to read the values</param>
        /// <param name="result">List that will receive the values</param>
        protected abstract void getFiles(FileStream fs, IList<string> result);

        /// <summary>
        /// Read the tracks described by the playlist using the given Stream
        /// and put them into the given list
        /// </summary>
        /// <param name="fs">FileStream to use to read the tracks</param>
        /// <param name="result">List that will receive the tracks</param>
        protected abstract void setTracks(FileStream fs, IList<Track> result);

        /// <summary>
        /// Read the paths of the track files described by the playlist
        /// </summary>
        /// <returns>List of the paths</returns>
        public IList<string> getFiles()
        {
            IList<string> result = new List<string>();
            LogDelegator.GetLocateDelegate()(Path);

            try
            {
                using (FileStream fs = new FileStream(Path, FileMode.Open, FileAccess.Read))
                {
                    getFiles(fs, result);
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }

            return result;
        }

        /// <summary>
        /// Read the tracks described by the playlist
        /// </summary>
        /// <returns>List of the tracks</returns>
        public IList<Track> getTracks()
        {
            IList<Track> result = new List<Track>();

            try
            {
                IList<string> files = getFiles();
                foreach (string s in files)
                {
                    result.Add(new Track(s));
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }

            return result;
        }

        /// <summary>
        /// Modify playlist by replacing all file paths by the given file paths
        /// </summary>
        /// <param name="fileList">List of file paths to write in the playlist, replacing current ones</param>
        public void setFiles(IList<string> fileList)
        {
            IList<Track> trackList = fileList.Select(file => new Track(file, false)).ToList();

            setTracks(trackList);
        }

        /// <summary>
        /// Modify playlist by replacing all tracks by the given tracks
        /// </summary>
        /// <param name="trackList">List of tracks to write in the playlist, replacing current ones</param>
        public void setTracks(IList<Track> trackList)
        {
            LogDelegator.GetLocateDelegate()(Path);
            try
            {
                using (FileStream fs = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    if (Encoding.Equals(PlaylistFormat.FileEncoding.UTF8_BOM)) fs.Write(BOM_UTF8, 0, 3);
                    setTracks(fs, trackList);
                }
            }
            catch (Exception e)
            {
                Utils.TraceException(e);
            }
        }

        /// <summary>
        /// Generate XmlWriterSettings from the current file's and global settings
        /// </summary>
        /// <returns>New instance of XmlWriterSettings</returns>
        protected XmlWriterSettings generateWriterSettings()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = true;
            switch (Encoding)
            {
                case PlaylistFormat.FileEncoding.ANSI:
                    settings.Encoding = ANSI;
                    break;
                default:
                    settings.Encoding = UTF8_NO_BOM;
                    break;
            }

            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            settings.Indent = true;
            settings.DoNotEscapeUriAttributes = false;
            return settings;
        }

        /// <summary>
        /// Encodes the given location usingcurrent LocationFormatting
        /// </summary>
        /// <param name="location">File path to encode</param>
        /// <returns></returns>
        protected string encodeLocation(string location)
        {
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
        protected void decodeLocation(XmlReader source, string attributeName, IList<string> result)
        {
            string location = decodeLocation(source.GetAttribute(attributeName));
            if (location != null) result.Add(location);
        }

        /// <summary>
        /// Decodes the given location to an absolute filepath
        /// </summary>
        /// <param name="href">Location to decode (can be an URI of any form or a relative filepath)</param>
        /// <returns>Absolute filepath corresponding to the given location</returns>
        protected string decodeLocation(string href)
        {
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
                            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), uri.LocalPath);
                        }

                        // Hack to avoid paths being rooted by a double '\', thus making them unreadable by System.IO.Path
                        return uri.LocalPath.Replace(
                            "" + System.IO.Path.DirectorySeparatorChar + System.IO.Path.DirectorySeparatorChar,
                            "" + System.IO.Path.DirectorySeparatorChar);
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
                    href = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), href);
                }
            }
            return href;
        }
    }
}