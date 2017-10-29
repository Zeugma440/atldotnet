using System;
using CueSharp;
using System.Collections.Generic;
using Commons;

namespace ATL.CatalogDataReaders.BinaryLogic
{
	/// <summary>
	/// Adapter / Wrapper for CueSharp.CueParser to work with ATL's definition of CatalogDataReader
	/// </summary>
	public class CueAdapter : ICatalogDataReader
	{
		private CueSheet m_cueParser = null;
		private String m_path = "";


		public String Path
		{
			get { return m_path; }
            set { m_path = value; }
		}

		public String Title
		{
			get 
			{
                if (null == m_cueParser) m_cueParser = new CueSheet(m_path);

				if (m_cueParser.Title.Trim().Length > 0)
				{
					return m_cueParser.Title;
				}
				else
				{
					return System.IO.Path.GetFileNameWithoutExtension(m_path);
				}
			}
		}

		public String Artist
		{
			get
            {
                if (null == m_cueParser) m_cueParser = new CueSheet(m_path);
                return m_cueParser.Performer;
            }
		}

        public String Comments
        {
            get
            {
                if (null == m_cueParser) m_cueParser = new CueSheet(m_path);

                string result = "";
                foreach (string aComm in m_cueParser.Comments)
                {
                    if (result.Length > 0) result += Settings.InternalValueSeparator;
                    result += aComm;
                }
                return result;
            }
        }

        public IList<ATL.Track> Tracks
		{
			get
			{
                if (null == m_cueParser) m_cueParser = new CueSheet(m_path);

                IList<ATL.Track> trackList = new List<ATL.Track>();
				
				ATL.Track cueTrack;
                ATL.Track previousCueTrack = null;
                ATL.Track physicalTrack = null;
				int previousIndex = 0;

				foreach (CueSharp.Track aTrack in m_cueParser.Tracks)
				{
					if (CueSharp.DataType.AUDIO == aTrack.TrackDataType)
					{
                        string trackPath = aTrack.DataFile.Filename;
                        if (!System.IO.Path.IsPathRooted(trackPath))
                        {
                            trackPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(m_path), trackPath);
                        }

                        // Avoids scanning N times the same track (e.g. livesets)
                        if (null == physicalTrack || physicalTrack.Path != trackPath)
                        {
                            physicalTrack = new ATL.Track(trackPath);
                        }

                        cueTrack = new ATL.Track();
                        cueTrack.Path = trackPath;
						cueTrack.Album = Title;
						cueTrack.Artist = Utils.ProtectValue(aTrack.Performer);
                        if (0 == cueTrack.Artist.Length) cueTrack.Artist = physicalTrack.Artist;
                        cueTrack.Composer = physicalTrack.Composer;
                        cueTrack.Title = Utils.ProtectValue(aTrack.Title);
                        if (0 == cueTrack.Title.Length) cueTrack.Title = physicalTrack.Title;
                        cueTrack.Comment = "";
						foreach (string aComm in aTrack.Comments)
						{
                            if (cueTrack.Comment.Length > 0) cueTrack.Comment += Settings.InternalValueSeparator;
							cueTrack.Comment += aComm;
						}
                        if (0 == cueTrack.Comment.Length) cueTrack.Comment = physicalTrack.Comment;

						if ((null != previousCueTrack) && (aTrack.Indices.Length > 0))
						{
							previousCueTrack.Duration = getDurationFromIndex(aTrack.Indices[0]) - previousIndex;
						}
						if (aTrack.Indices.Length > 0) previousIndex = getDurationFromIndex(aTrack.Indices[0]);
						
                        cueTrack.TrackNumber = aTrack.TrackNumber;
                        if (0 == cueTrack.TrackNumber) cueTrack.TrackNumber = physicalTrack.TrackNumber;
                        cueTrack.DiscNumber = physicalTrack.DiscNumber;

						cueTrack.Genre = physicalTrack.Genre;
						cueTrack.IsVBR = physicalTrack.IsVBR;
                        cueTrack.Bitrate = physicalTrack.Bitrate;
                        cueTrack.CodecFamily = physicalTrack.CodecFamily;
						cueTrack.Year = physicalTrack.Year;
                        cueTrack.PictureTokens = physicalTrack.PictureTokens;

						trackList.Add(cueTrack);
                        previousCueTrack = cueTrack;
					}
				}
                if (previousCueTrack != null && physicalTrack != null)
                {
                    previousCueTrack.Duration = physicalTrack.Duration - previousIndex;
                }

				return trackList;
			}
		}

		private int getDurationFromIndex(CueSharp.Index index)
		{
			return index.Seconds + (index.Minutes*60);
		}
	}
}
