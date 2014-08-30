using ATL.AudioReaders.BinaryLogic;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ATL.AudioReaders
{
	/// <summary>
	/// This class is the one which is _really_ called when encountering a file.
	/// It calls AudioReaderFactory and queries AudioDataReader/MetaDataReader to provide physical 
	/// _and_ meta information about the given file.
	/// </summary>
	public class AudioFileReader
	{	
		private AudioReaderFactory theReaderFactory;			// Reader Factory
		private MetaReaderFactory theMetaFactory;				// Meta Factory
		private IAudioDataReader audioData;						// Audio data reader used for this file
		private IMetaDataReader metaData;						// Metadata reader used for this file
		private String thePath;									// Path of this file

		// ------------------------------------------------------------------------------------------

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="path">Path of the file to be parsed</param>
		public AudioFileReader(String path, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			thePath = path;
			theReaderFactory = AudioReaderFactory.GetInstance();
			theMetaFactory = MetaReaderFactory.GetInstance();

			audioData = theReaderFactory.GetDataReader(path);
            audioData.ReadFromFile(path, pictureStreamHandler);
			metaData = theMetaFactory.GetMetaReader(ref audioData);

            if (audioData.AllowsParsableMetadata && metaData is DummyTag) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata for " + thePath);
		}


		/// <summary>
		/// Title of the track
		/// </summary>
		public String Title
		{
			get { return metaData.Title.Replace('\t',',').Replace("\r","").Replace('\n',',').Replace("\0",""); }
		}
		/// <summary>
		/// Artist
		/// </summary>
		public String Artist
		{
			get { return metaData.Artist.Replace('\t',',').Replace("\r","").Replace('\n',',').Replace("\0",""); }
		}
        /// <summary>
        /// Composer
        /// </summary>
        public String Composer
        {
            get { return metaData.Composer.Replace('\t', ',').Replace("\r", "").Replace('\n', ',').Replace("\0", ""); }
        }
		/// <summary>
		/// Comments
		/// </summary>
		public String Comment
		{
			get { return metaData.Comment.Replace('\t',',').Replace("\r","").Replace('\n',' ').Replace("\0",""); }
		}
        /// <summary>
        /// Flag indicating the presence of embedded pictures
        /// </summary>
        public IList<MetaReaderFactory.PIC_CODE> Pictures
        {
            get { return metaData.Pictures; }
        }
		/// <summary>
		/// Genre
		/// </summary>
		public String Genre
		{
            get { return metaData.Genre.Replace('\t', ',').Replace("\r", "").Replace('\n', ' ').Replace("\0", ""); }
		}
		/// <summary>
		/// Track number
		/// </summary>
		public int Track
		{
			get { return metaData.Track; }
		}
        /// <summary>
        /// Disc number
        /// </summary>
        public int Disc
        {
            get { return metaData.Disc; }
        }
		/// <summary>
		/// Year
		/// </summary>
		public int Year
		{
			get { return TrackUtils.ExtractIntYear(metaData.Year); }
		}
		/// <summary>
		/// Album title
		/// </summary>
		public String Album
		{
            get { return metaData.Album.Replace('\t', ',').Replace("\r", "").Replace('\n', ' ').Replace("\0", ""); }
		}
		/// <summary>
		/// Track duration (seconds)
		/// </summary>
		public int Duration
		{
			get { return (int)Math.Round(audioData.Duration); }
		}
		/// <summary>
		/// Bitrate
		/// </summary>
		public int BitRate
		{
			get { return (int)Math.Round(audioData.BitRate); }
		}
        /// <summary>
        /// Track rating
        /// </summary>
        public int Rating
        {
            get { return metaData.Rating; }
        }
		/// <summary>
		/// Codec family
		/// </summary>
		public int CodecFamily
		{
			get { return audioData.CodecFamily; }
		}
        /// <summary>
        /// Indicates whether the audio stream is in VBR
        /// </summary>
        public bool IsVBR
        {
            get { return audioData.IsVBR; }
        }
	}
}
