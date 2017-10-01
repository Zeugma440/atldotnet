using ATL.AudioData;
using Commons;
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

        // TODO create a constructor that directly loads pictures instead of opening the file two times

        public Track(String iPath)
        {
            Path = iPath;
            Update();
        }

		public string Path;		
		public string Title;
		public string Artist;
        public string Composer;
		public string Comment;
		public string Genre;
		public string Album;
        public string OriginalAlbum;
        public string OriginalArtist;
        public string Copyright;
        public string Description;
        public string Publisher;
        public string AlbumArtist;
        public string Conductor;
        public int Year;
		public int Bitrate;
        public double SampleRate;
        public bool IsVBR;
		public int CodecFamily;
		public int Duration;		
		public int TrackNumber;
        public int DiscNumber;
        public int Rating;
        public IDictionary<string, string> AdditionalFields;
        public IList<TagData.PictureInfo> PictureTokens;

        protected Image coverArt = null;
        protected byte[] coverArtBinary = null;

        
        // TODO make all embedded pictures available (not only the first one found)

        public Image GetEmbeddedPicture(bool loadIntoDotNetImage = true)
        {
            if (null == coverArt)
            {
                if (loadIntoDotNetImage) Update(new TagData.PictureStreamHandlerDelegate(this.readImageData));
                else Update(new TagData.PictureStreamHandlerDelegate(this.readBinaryImageData));
            }
            
            return coverArt;
        }

        protected void readImageDataOld(ref MemoryStream s)
        {
            coverArt = Image.FromStream(s);
        }

        protected void readImageData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            coverArt = Image.FromStream(s);
        }

        protected void readBinaryImageData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            coverArtBinary = s.GetBuffer();
        }

        protected void Update(TagData.PictureStreamHandlerDelegate pictureStreamHandler = null)
        {
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
                Artist = Utils.ProtectValue(theReader.Artist);
                Composer = Utils.ProtectValue(theReader.Composer);
                Comment = Utils.ProtectValue(theReader.Comment);
                Genre = Utils.ProtectValue(theReader.Genre);
                OriginalArtist = Utils.ProtectValue(theReader.OriginalArtist);
                OriginalAlbum = Utils.ProtectValue(theReader.OriginalAlbum);
                Description = Utils.ProtectValue(theReader.GeneralDescription);
                Copyright = Utils.ProtectValue(theReader.Copyright);
                Publisher = Utils.ProtectValue(theReader.Publisher);
                AlbumArtist = Utils.ProtectValue(theReader.AlbumArtist);
                Conductor = Utils.ProtectValue(theReader.Conductor);
                AdditionalFields = theReader.AdditionalFields;
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
