using System;
using System.IO;
using ATL.PlaylistReaders.BinaryLogic;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
	/// <summary>
	/// Description r¨¦sum¨¦e de PlaylistReaderFactory.
	/// </summary>
	public class PlaylistReaderFactory
	{
		// Defines the supported formats
        public const int PL_NONE = -1;
		public const int PL_M3U = 0;
		public const int PL_PLS = 1;
        public const int PL_FPL = 2;

		// The instance of this factory
		private static PlaylistReaderFactory theFactory = null;

        private IList<ATL.Format> formatList;


		public static PlaylistReaderFactory GetInstance()
		{
			if (null == theFactory)
			{
				theFactory = new PlaylistReaderFactory();
                theFactory.formatList = new List<ATL.Format>();

                Format tempFmt = new Format("PLS");
                tempFmt.ID = PL_PLS;
                tempFmt.AddExtension(".pls");
                theFactory.formatList.Add(tempFmt);

                tempFmt = new Format("M3U");
                tempFmt.ID = PL_M3U;
                tempFmt.AddExtension(".m3u");
                tempFmt.AddExtension(".m3u8");
                theFactory.formatList.Add(tempFmt);

                tempFmt = new Format("FPL (experimental)");
                tempFmt.ID = PL_FPL;
                tempFmt.AddExtension(".fpl");
                theFactory.formatList.Add(tempFmt);
            }

			return theFactory;
		}

        public IList<ATL.Format> getFormats()
        {
            return new List<Format>(formatList);
        }

        private int getFormatIDFromPath(String path)
        {
            int result = PL_NONE;

            if (File.Exists(path))
            {
                String ext = Path.GetExtension(path);

                foreach (Format f in formatList)
                {
                    if (f.IsValidExtension(ext)) result = f.ID;
                }
            }

            return result;
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

			return theReader;
		}
	}
}
