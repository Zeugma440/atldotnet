using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
    public abstract class PlaylistReader : IPlaylistReader
	{
		protected string FFileName; // Path of the playlist file

		public string Path
		{
            get { return FFileName; }
            set { FFileName = value; }
		}


        abstract public void GetFiles(FileStream fs, IList<string> result);

        public IList<string> GetFiles()
		{
			IList<string> result = new List<string>();

			try
			{
                using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.Read))
                {
                    GetFiles(fs, result);
                }
			}
			catch (Exception e) 
			{
                Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
			}

			return result;
		}
	}
}
