using ATL.AudioData.IO;
using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
    /// <summary>
	/// This class is the one which is _really_ called when encountering a file.
	/// It calls AudioReaderFactory and queries AudioDataReader/MetaDataReader to provide physical 
	/// _and_ meta information about the given file.
	/// </summary>
	public class AudioFileIO : IMetaDataIO, IAudioDataIO
    {
        private IAudioDataIO audioData;                     // Audio data reader used for this file
        private IMetaDataIO metaData;                       // Metadata reader used for this file
        private string thePath;                             // Path of this file

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Path of the file to be parsed</param>
        public AudioFileIO(string path, TagData.PictureStreamHandlerDelegate pictureStreamHandler)
        {
            byte alternate = 0;
            thePath = path;

            audioData = AudioDataIOFactory.GetInstance().GetDataReader(path);

            while (!audioData.ReadFromFile(pictureStreamHandler))
            {
                alternate++;
                audioData = AudioDataIOFactory.GetInstance().GetDataReader(path, alternate);
            }

            metaData = MetaDataIOFactory.GetInstance().GetMetaReader(ref audioData);

            if (audioData.AllowsParsableMetadata && metaData is DummyTag) LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Could not find any metadata for " + thePath);
        }




        /// <summary>
        /// Title of the track
        /// </summary>
        public string Title
        {
            get { return metaData.Title.Replace('\t', ',').Replace("\r", "").Replace('\n', ',').Replace("\0", ""); }
        }
        /// <summary>
        /// Artist
        /// </summary>
        public string Artist
        {
            get { return metaData.Artist.Replace('\t', ',').Replace("\r", "").Replace('\n', ',').Replace("\0", ""); }
        }
        /// <summary>
        /// Composer
        /// </summary>
        public string Composer
        {
            get { return metaData.Composer.Replace('\t', ',').Replace("\r", "").Replace('\n', ',').Replace("\0", ""); }
        }
        /// <summary>
        /// Comments
        /// </summary>
        public string Comment
        {
            get { return metaData.Comment.Replace('\t', ',').Replace("\r", "").Replace('\n', ' ').Replace("\0", ""); }
        }
        /// <summary>
        /// Flag indicating the presence of embedded pictures
        /// </summary>
        public IList<TagData.PictureInfo> PictureTokens
        {
            get { return metaData.PictureTokens; }
        }
        /// <summary>
        /// Genre
        /// </summary>
        public string Genre
        {
            get { return metaData.Genre.Replace('\t', ',').Replace("\r", "").Replace('\n', ' ').Replace("\0", ""); }
        }
        /// <summary>
        /// Track number
        /// </summary>
        public ushort Track
        {
            get { return metaData.Track; }
        }
        /// <summary>
        /// Disc number
        /// </summary>
        public ushort Disc
        {
            get { return metaData.Disc; }
        }
        /// <summary>
        /// Year, converted to int
        /// </summary>
        public int IntYear
        {
            get { return TrackUtils.ExtractIntYear(metaData.Year); }
        }
        /// <summary>
        /// Album title
        /// </summary>
        public string Album
        {
            get { return metaData.Album.Replace('\t', ',').Replace("\r", "").Replace('\n', ' ').Replace("\0", ""); }
        }
        /// <summary>
        /// Track duration (seconds), rounded
        /// </summary>
        public int IntDuration
        {
            get { return (int)Math.Round(audioData.Duration); }
        }
        /// <summary>
        /// Track bitrate (KBit/s), rounded
        /// </summary>
        public int IntBitRate
        {
            get { return (int)Math.Round(audioData.BitRate); }
        }
        /// <summary>
        /// Track rating
        /// </summary>
        public ushort Rating
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
        /// <summary>
        /// Indicates whether the audio format allows parsable metadata to exist
        /// </summary>
        public bool AllowsParsableMetadata
        {
            get { return audioData.AllowsParsableMetadata; }
        }
        /// <summary>
        /// Does the tag exist ?
        /// </summary>
        public bool Exists
        {
            get { return metaData.Exists; }
        }
        /// <summary>
        /// Year, in its original form
        /// </summary>
        public string Year
        {
            get { return metaData.Year; }
        }
        /// <summary>
        /// Track bitrate (Kbit/s)
        /// </summary>
        public double BitRate
        {
            get { return audioData.BitRate; }
        }
        /// <summary>
        /// Track duration (seconds)
        /// </summary>
        public double Duration
        {
            get { return audioData.Duration; }
        }

        // AudioFileReader aims at simplifying standard interfaces
        // => the below methods are not implemented
        public ID3v1 ID3v1
        {
            get { throw new NotImplementedException(); }
        }
        public ID3v2 ID3v2
        {
            get { throw new NotImplementedException(); }
        }
        public APEtag APEtag
        {
            get { throw new NotImplementedException(); }
        }

        public int Size
        {
            get
            {
                return metaData.Size;
            }
        }

        public long Offset
        {
            get
            {
                return metaData.Offset;
            }
        }

        public IMetaDataIO NativeTag
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool Read(BinaryReader source, TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames)
        {
            return metaData.Read(source, pictureStreamHandler, readAllMetaFrames);
        }

        public long Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return metaData.Write(r, w, tag);
        }

        public bool ReadFromFile(TagData.PictureStreamHandlerDelegate pictureStreamHandler = null, bool readAllMetaFrames = false)
        {
            throw new NotImplementedException();
        }

        public bool RemoveTagFromFile(int tagType)
        {
            throw new NotImplementedException();
        }

        public bool AddTagToFile(int tagType)
        {
            throw new NotImplementedException();
        }

        public bool AddTagToFile(TagData theTag, int tagType)
        {
            throw new NotImplementedException();
        }

        public bool HasNativeMeta()
        {
            throw new NotImplementedException();
        }
    }

}
