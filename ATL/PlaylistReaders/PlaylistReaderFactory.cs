using System;
using System.Collections.Generic;
using ATL.Playlist;

namespace ATL.PlaylistReaders
{
    /// <summary>
    /// TODO
    /// </summary>
    [Obsolete("Use Playlist.PlaylistIOFactory")]
    public class PlaylistReaderFactory : Factory
	{
		// Defines the supported formats
		public const int PL_M3U     = 0;
		public const int PL_PLS     = 1;
        public const int PL_FPL     = 2;
        public const int PL_XSPF    = 3;
        public const int PL_SMIL    = 4;
        public const int PL_ASX     = 5;
        public const int PL_B4S     = 6;

		// The instance of this factory
		private static PlaylistReaderFactory theFactory = null;

        
        public static PlaylistReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new PlaylistReaderFactory();
                theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                Format tempFmt = new Format("PLS");
                tempFmt.ID = PL_PLS;
                tempFmt.AddExtension(".pls");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("M3U");
                tempFmt.ID = PL_M3U;
                tempFmt.AddExtension(".m3u");
                tempFmt.AddExtension(".m3u8");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("FPL (experimental)");
                tempFmt.ID = PL_FPL;
                tempFmt.AddExtension(".fpl");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("XSPF (spiff)");
                tempFmt.ID = PL_XSPF;
                tempFmt.AddExtension(".xspf");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("SMIL");
                tempFmt.ID = PL_SMIL;
                tempFmt.AddExtension(".smil");
                tempFmt.AddExtension(".smi");
                tempFmt.AddExtension(".zpl");
                tempFmt.AddExtension(".wpl");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("ASX");
                tempFmt.ID = PL_ASX;
                tempFmt.AddExtension(".asx");
                tempFmt.AddExtension(".wax");
                tempFmt.AddExtension(".wvx");
                theFactory.addFormat(tempFmt);

                tempFmt = new Format("B4S");
                tempFmt.ID = PL_B4S;
                tempFmt.AddExtension(".b4s");
                theFactory.addFormat(tempFmt);
            }

			return theFactory;
		}

        public IPlaylistReader GetPlaylistReader(String path, int alternate = 0)
		{
            return new PlaylistIOAdapter(PlaylistIOFactory.GetInstance().GetPlaylistIO(path, alternate));
        }
	}
}
