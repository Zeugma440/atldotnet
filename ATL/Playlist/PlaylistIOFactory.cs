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
        public const int PL_PLS = 1;
        public const int PL_FPL = 2;
        public const int PL_XSPF = 3;

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

                tempFmt = new Format("PLS");
                tempFmt.ID = PL_PLS;
                tempFmt.AddExtension(".pls");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("FPL (experimental)");
                tempFmt.ID = PL_FPL;
                tempFmt.AddExtension(".fpl");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("XSPF (spiff)");
                tempFmt.ID = PL_XSPF;
                tempFmt.AddExtension(".xspf");
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
            } else if (PL_PLS == formatId)
            {
                theReader = new PLSIO();
            }
            else if (PL_FPL == formatId)
            {
                theReader = new FPLIO();
            }
            else if (PL_XSPF == formatId)
            {
                theReader = new XSPFIO();
            }

            if (null == theReader) theReader = new DummyIO();

            return theReader;
        }
    }
}
