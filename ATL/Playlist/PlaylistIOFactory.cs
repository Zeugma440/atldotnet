using ATL.Playlist.IO;
using System.Collections.Generic;

namespace ATL.Playlist
{
    /// <summary>
    /// TODO
    /// </summary>
    public class PlaylistIOFactory : Factory
    {
        // Defines the supported formats
        public const int PL_M3U = 0;

        // The instance of this factory
        private static PlaylistIOFactory theFactory = null;


        public static PlaylistIOFactory GetInstance()
        {
            if (null == theFactory)
            {
                theFactory = new PlaylistIOFactory();
                theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                Format tempFmt = new Format("M3U");
                tempFmt.ID = PL_M3U;
                tempFmt.AddExtension(".m3u");
                tempFmt.AddExtension(".m3u8");
                theFactory.addFormat(tempFmt);
            }

            return theFactory;
        }

        public IPlaylistIO GetPlaylistIO(string path, int alternate = 0)
        {
            IList<Format> formats = getFormatsFromPath(path);
            IPlaylistIO result;

            if (formats != null && formats.Count > alternate)
            {
                result = GetPlaylistIO(formats[alternate].ID);
            }
            else
            {
                result = GetPlaylistIO(NO_FORMAT);
            }

            result.Path = path;
            return result;
        }

        public IPlaylistIO GetPlaylistIO(int formatId)
        {
            IPlaylistIO theReader = null;

            if (PL_M3U == formatId)
            {
                theReader = new M3UIO();
            }

            if (null == theReader) theReader = new DummyIO();

            return theReader;
        }
    }
}
