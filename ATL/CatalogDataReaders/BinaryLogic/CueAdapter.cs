using System;
using System.Collections;
using System.IO;
using CueSharp;
using System.Collections.Generic;
using ATL.AudioReaders;
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
				if (m_cueParser.Title.Trim() != "")
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
			get { return m_cueParser.Performer; }
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
                        String trackPath = System.IO.Path.GetDirectoryName(m_path) + System.IO.Path.DirectorySeparatorChar + aTrack.DataFile.Filename;
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
						foreach (String aComm in aTrack.Comments)
						{
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
                        cueTrack.Pictures = physicalTrack.Pictures;

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
