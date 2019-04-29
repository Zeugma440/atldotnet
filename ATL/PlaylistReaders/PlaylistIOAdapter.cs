using ATL.Playlist;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.PlaylistReaders
{
    class PlaylistIOAdapter : IPlaylistReader
    {
        private readonly IPlaylistIO pls;

        public PlaylistIOAdapter(IPlaylistIO pls)
        {
            this.pls = pls;
        }

        public string Path { get => pls.Path; set => pls.Path = value; }

        public IList<string> GetFiles()
        {
            return pls.FilePaths;
        }
    }
}
