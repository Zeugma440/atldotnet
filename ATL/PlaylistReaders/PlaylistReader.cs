using System;
using System.IO;
using System.Text;
using System.Collections;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
	public abstract class PlaylistReader : IPlaylistReader
	{
		protected String FFileName; // Path of the playlist file

		public String Path
		{
            get { return FFileName; }
            set { FFileName = value; }
		}


        abstract public void GetFiles(FileStream fs, ref IList<String> result);

        public IList<String> GetFiles()
		{
			IList<String> result = new List<String>();
			FileStream fs = null;

			try
			{
				fs = new FileStream(FFileName,FileMode.Open, FileAccess.Read);

                GetFiles(fs, ref result);
			}
			catch (Exception e) 
			{
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
			}

            if (fs != null) fs.Close();

			return result;
		}
	}
}
