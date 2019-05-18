using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace ATL.Playlist
{
    /// <summary>
    /// TODO
    /// </summary>
    public abstract class PlaylistIO : IPlaylistIO
    {
        protected string FFileName; // Path of the playlist file
        protected PlaylistFormat.LocationFormatting locationFormatting;

        public string Path
        {
            get { return FFileName; }
            set { FFileName = value; }
        }
        public PlaylistFormat.LocationFormatting LocationFormatting
        {
            get { return locationFormatting; }
            set { locationFormatting = value; }
        }

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
            LogDelegator.GetLocateDelegate()(FFileName);

            try
            {
                using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.Read))
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
            LogDelegator.GetLocateDelegate()(FFileName);
            try
            {
                using (FileStream fs = new FileStream(FFileName, FileMode.Create, FileAccess.ReadWrite))
                {
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
            settings.Encoding = Encoding.UTF8;
            settings.OmitXmlDeclaration = true;
            settings.ConformanceLevel = ConformanceLevel.Fragment;
            settings.Indent = true;
            settings.DoNotEscapeUriAttributes = false;
            return settings;
        }

        protected void formatLocation(string location)
        {

        }

        protected void parseLocation(XmlReader source, string attributeName, IList<string> result)
        {
            string href = source.GetAttribute(attributeName);

            // Filepath
            if (!href.StartsWith("file://"))
            {
                href = href.Replace("file:", ""); // Older B4S format : "file:" + filepath
                if (!System.IO.Path.IsPathRooted(href))
                {
                    href = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FFileName), href);
                }
                result.Add(href);
            }
            else // URI
            {
                try
                {
                    Uri uri = new Uri(href);
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
    }
}
