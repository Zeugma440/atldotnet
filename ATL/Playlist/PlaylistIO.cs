using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static readonly byte[] BOM_UTF8 = new byte[] { 0xEF, 0xBB, 0xBF };
        protected static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(false);
        protected static readonly Encoding ANSI = Utils.Latin1Encoding;

        public string Path { get; set; }
        public PlaylistFormat.LocationFormatting LocationFormatting { get; set; }
        public PlaylistFormat.FileEncoding Encoding { get; set; }

        public IList<string> FilePaths
        {
            get => getFiles();
            set => setFiles(value);
        }
        public IList<Track> Tracks
        {
            get => getTracks();
            set => setTracks(value);
        }

        abstract protected void getFiles(FileStream fs, IList<string> result);
        abstract protected void setTracks(FileStream fs, IList<Track> values);

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
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
            }

            return result;
        }

        public IList<Track> getTracks()
        {
            IList<Track> result = new List<Track>();

            try
            {
                IList<string> files = getFiles();
                foreach(string s in files)
                {
                    result.Add(new Track(s));
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
            }

            return result;
        }

        public void setFiles(IList<string> fileList)
        {
            IList<Track> trackList = new List<Track>();

            foreach (string file in fileList)
            {
                Track t = new Track(file, false); // Empty container
                trackList.Add(t);
            }

            setTracks(trackList);
        }

        public void setTracks(IList<Track> trackList)
        {
            LogDelegator.GetLocateDelegate()(Path);
            try
            {
                using (FileStream fs = new FileStream(Path, FileMode.Create, FileAccess.ReadWrite))
                {
                    if (Encoding.Equals(PlaylistFormat.FileEncoding.UTF8_BOM)) fs.Write(BOM_UTF8, 0, 3);
                    setTracks(fs, trackList);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message);
            }
        }

        protected XmlWriterSettings getWriterSettings()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = true;
            switch(Encoding)
            {
                case (PlaylistFormat.FileEncoding.ANSI):
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
            // It it an URI ?
            string hrefUri = href.Replace('\\', '/'); // Try and replace all \'s by /'s to detect URIs even if the location has been badly formatted
            if (hrefUri.Contains("://")) // RFC URI
            {
                try
                {
                    Uri uri = new Uri(hrefUri);
                    if (uri.IsFile)
                    {
                        if (!System.IO.Path.IsPathRooted(uri.LocalPath))
                        {
                            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), uri.LocalPath);
                        }
                        else
                        {
                            return uri.LocalPath;
                        }
                    }
                }
                catch (UriFormatException)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, hrefUri + " is not a valid URI [" + Path + "]");
                }
            }
            
            href = href.Replace("file:///", "").Replace("file://", "").Replace("file:", "");
            if (!System.IO.Path.IsPathRooted(href))
            {
                href = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), href);
            }
            return href;
        }
    }
}
