using System;
using System.IO;
using ATL.PlaylistReaders.BinaryLogic;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
	/// <summary>
	/// Description r¨¦sum¨¦e de PlaylistReaderFactory.
	/// </summary>
	public class PlaylistReaderFactory : ReaderFactory
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
            IList<Format> formats = getFormatsFromPath(path);
            IPlaylistReader result;

            if (formats != null && formats.Count > alternate)
            {
                result = GetPlaylistReader(formats[alternate].ID);
            }
            else
            {
                result = GetPlaylistReader(NO_FORMAT);
            }

            result.Path = path;
            return result;
        }

        public IPlaylistReader GetPlaylistReader(int formatId)
        {
            IPlaylistReader theReader = null;

            if (PL_PLS == formatId)
			{
				theReader = new PLSReader();
			}
            else if (PL_M3U == formatId)
			{
				theReader = new M3UReader();
			}
            else if (PL_FPL == formatId)
            {
                theReader = new FPLReader();
            }
            else if (PL_XSPF == formatId)
            {
                theReader = new XSPFReader();
            }
            else if (PL_SMIL == formatId)
            {
                theReader = new SMILReader();
            }
            else if (PL_ASX == formatId)
            {
                theReader = new ASXReader();
            }
            else if (PL_B4S == formatId)
            {
                theReader = new B4SReader();
            }

            if (null == theReader) theReader = new DummyReader();

			return theReader;
		}
	}
}
