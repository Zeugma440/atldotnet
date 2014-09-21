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

		// The instance of this factory
		private static PlaylistReaderFactory theFactory = null;


		public static PlaylistReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new PlaylistReaderFactory();
                theFactory.formatList = new Dictionary<string, Format>();

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

                tempFmt = new Format("XSPF");
                tempFmt.ID = PL_XSPF;
                tempFmt.AddExtension(".xspf");
                theFactory.addFormat(tempFmt);
            }

			return theFactory;
		}

		public IPlaylistReader GetPlaylistReader(String path)
		{
            IPlaylistReader reader = GetPlaylistReader(getFormatIDFromPath(path));
            reader.Path = path;
            return reader;
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

            if (null == theReader) theReader = new DummyReader();

			return theReader;
		}
	}
}
