using ATL.AudioData;
using ATL.AudioReaders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ATL
{
	/// <summary>
	/// Track description
	/// </summary>
	public class Track
	{
		public Track() {}

        public Track(String iPath, bool useOldImplementation = false)
        {
            Path = iPath;

            if (useOldImplementation) UpdateOld(); else Update();
        }

        // TODO align on TagData properties

		public String Path;		
		public String Title;
		public String Artist;
        public String Composer;
		public String Comment;
		public String Genre;		
		public String Album;
		public int Year;
		public int Bitrate;
		public bool IsVBR;
		public int CodecFamily;
        public int SampleRate;
		public long Size;
		public long LastModified;
		public int Duration;		
		public int TrackNumber;
        public int DiscNumber;
        public int Rating;
        [Obsolete]
        public IList<MetaReaderFactory.PIC_CODE> Pictures;
        public IList<TagData.PictureInfo> PictureTokens;

        protected Image coverArt = null;


        public Image GetEmbeddedPicture()
        {
            if (null == coverArt)
            {
                Update(new TagData.PictureStreamHandlerDelegate(this.readImageData));
            }
            
            return coverArt;
        }

        // Kept for compatibility issues during parallel development
        protected void readImageData(ref MemoryStream s)
        {
            readImageData(ref s, TagData.PIC_TYPE.Front, ImageFormat.Jpeg, MetaDataIOFactory.TAG_NATIVE, 0, 1);
        }

        protected void readImageData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            coverArt = Image.FromStream(s);
        }

        [Obsolete]
        protected void UpdateOld(StreamUtils.StreamHandlerDelegate pictureStreamHandler = null)
        {
            FileInfo theFileInfo = new FileInfo(Path);

            if (theFileInfo.Exists)
            {
                Size = theFileInfo.Length;
                LastModified = theFileInfo.LastWriteTime.Ticks;
            }

            //TODO when tag is not available, customize by naming options // tracks (...)
            AudioFileReader theReader = new AudioFileReader(Path, pictureStreamHandler);

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
                CodecFamily = theReader.CodecFamily;
                SampleRate = 0;
                Duration = theReader.IntDuration;
                Rating = theReader.Rating;
                IsVBR = theReader.IsVBR;
                Pictures = new List<MetaReaderFactory.PIC_CODE>(theReader.Pictures);
            }
        }

        protected void Update(TagData.PictureStreamHandlerDelegate pictureStreamHandler = null)
        {
            FileInfo theFileInfo = new FileInfo(Path);

            if (theFileInfo.Exists)
            {
                Size = theFileInfo.Length;
                LastModified = theFileInfo.LastWriteTime.Ticks;
            }

            // TODO when tag is not available, customize by naming options // tracks (...)
            AudioFileIO theReader = new AudioFileIO(Path, pictureStreamHandler);

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
                CodecFamily = theReader.CodecFamily;
                Duration = theReader.IntDuration;
                Rating = theReader.Rating;
                IsVBR = theReader.IsVBR;
                SampleRate = theReader.SampleRate;
                PictureTokens = new List<TagData.PictureInfo>(theReader.PictureTokens);
            }
        }
	}
}
