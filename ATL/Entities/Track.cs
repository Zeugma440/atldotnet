using ATL.AudioData;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL
{
	/// <summary>
	/// High-level class for audio file manipulation
	/// </summary>
	public class Track
	{
		public Track() {}

        public Track(string iPath)
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
        public IList<TagData.PictureInfo> PictureTokens = null;

        private IList<TagData.PictureInfo> embeddedPictures = null;


        // ========== METHODS

        public IList<TagData.PictureInfo> EmbeddedPictures
        {
            get
            {
                if (null == embeddedPictures)
                {
                    embeddedPictures = new List<TagData.PictureInfo>();

                    Update(new TagData.PictureStreamHandlerDelegate(readBinaryImageData));
                }

                return embeddedPictures;
            }
        }

        protected void readBinaryImageData(ref MemoryStream s, TagData.PIC_TYPE picType, ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            TagData.PictureInfo picInfo = new TagData.PictureInfo(imgFormat, picType, originalTag, picCode, position);
            picInfo.PictureData = s.ToArray();

            embeddedPictures.Add(picInfo);
        }

        protected void Update(TagData.PictureStreamHandlerDelegate pictureStreamHandler = null)
        {
            // TODO when tag is not available, customize by naming options // tracks (...)
            AudioFileIO theReader = new AudioFileIO(Path, pictureStreamHandler);

            Title = theReader.Title;
            if ("" == Title || null == Title)
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(Path); // TODO - this should be an option, as returned value is not really read from the tag
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

            if (null == pictureStreamHandler && embeddedPictures != null)
            {
                embeddedPictures.Clear();
                embeddedPictures = null;
            }
        }

        public void Save()
        {
            

        }
	}
}
