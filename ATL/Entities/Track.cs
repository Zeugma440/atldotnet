using ATL.AudioReaders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ATL
{
	/// <summary>
	/// Track description
	/// </summary>
	public class Track
	{
		public Track() {}

        public Track(String iPath)
        {
            Path = iPath;

            Update();
        }

		public String Path;		
		public String Title;
		public String Artist;
        public String Composer;
		public String Comment;
		public String Genre;		
		public String Album;
		public int Year;
		public int Bitrate;
        public double SampleRate;
        public bool IsVBR;
		public int CodecFamily;
		public long Size;
		public long LastModified;
		public int Duration;		
		public int TrackNumber;
        public int DiscNumber;
        public int Rating;
        public IList<MetaReaderFactory.PIC_CODE> Pictures;

        protected Image coverArt = null;


        public Image GetEmbeddedPicture()
        {
            if (null == coverArt)
            {
                Update(new StreamUtils.StreamHandlerDelegate(this.readImageData));
            }
            
            return coverArt;
        }

        protected void readImageData(ref MemoryStream s)
        {
                coverArt = Image.FromStream(s);
        }

        protected void Update(StreamUtils.StreamHandlerDelegate pictureStreamHandler = null)
        {
            AudioFileReader theReader;

            FileInfo theFileInfo = new FileInfo(Path);

            if (theFileInfo.Exists)
            {
                Size = theFileInfo.Length;
                LastModified = theFileInfo.LastWriteTime.Ticks;
            }

            //TODO when tag is not available, customize by naming options // tracks (...)
            theReader = new AudioFileReader(Path, pictureStreamHandler);

            // Per convention, the presence of a pictureStreamHandler
            // indicates that we only want coverArt updated
            if (null == pictureStreamHandler)
            {
                Title = theReader.Title;
                if ("" == Title || null == Title)
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(Path);
                }
                Artist = theReader.Artist;
                if (null == Artist) { Artist = ""; }
                Composer = theReader.Composer;
                if (null == Composer) { Composer = ""; }
                Comment = theReader.Comment;
                if (null == Comment) { Comment = ""; }
                Genre = theReader.Genre;
                if (null == Genre) { Genre = ""; }
                Year = theReader.IntYear;
                Album = theReader.Album;
                TrackNumber = theReader.Track;
                DiscNumber = theReader.Disc;
                Bitrate = theReader.IntBitRate;
                SampleRate = theReader.SampleRate;
                CodecFamily = theReader.CodecFamily;
                Duration = theReader.IntDuration;
                Rating = theReader.Rating;
                IsVBR = theReader.IsVBR;
                Pictures = new List<MetaReaderFactory.PIC_CODE>(theReader.Pictures);
            }
        }
	}
}
