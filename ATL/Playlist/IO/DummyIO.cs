using ATL.Playlist;
using System.Collections.Generic;

namespace ATL.Playlist.IO
{
    public class DummyIO : IPlaylistIO
    {
        public string Path
        {
            get
            {
                return "";
            }
            set
            {
                // Nothing;
            }
        }

        public IList<string> FilePaths
        {
            get => new List<string>();
            set
            {
                // Nothing;
            }
        }

        public IList<Track> Tracks
        {
            get => new List<Track>();
            set
            {
                // Nothing;
            }
        }

    }
}
