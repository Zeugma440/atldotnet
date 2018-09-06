using System;
using System.Collections.Generic;

namespace ATL.PlaylistReaders.BinaryLogic
{
    public class DummyReader : IPlaylistReader
    {
        String path = "";

        public string Path
        {
            get
            {
                return path;
            }
            set
            {
                path = value;
            }
        }

        public IList<string> GetFiles()
        {
            return new List<string>();
        }
    }
}
