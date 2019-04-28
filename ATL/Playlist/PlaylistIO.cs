using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.Playlist
{
    /// <summary>
    /// TODO
    /// </summary>
    public abstract class PlaylistIO : IPlaylistIO
    {
        protected string FFileName; // Path of the playlist file

        public string Path
        {
            get { return FFileName; }
            set { FFileName = value; }
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
    }
}
