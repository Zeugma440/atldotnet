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


        abstract public void GetFiles(FileStream fs, IList<String> result);

        public IList<String> GetFiles()
		{
			IList<String> result = new List<String>();

			try
			{
                using (FileStream fs = new FileStream(FFileName, FileMode.Open, FileAccess.Read))
                {
                    GetFiles(fs, result);
                }
			}
			catch (Exception e) 
			{
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FFileName + ")");
			}

			return result;
		}
	}
}
