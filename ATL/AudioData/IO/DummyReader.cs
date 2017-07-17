using System;

namespace ATL.AudioData.IO
{
	/// <summary>
	/// Dummy audio data provider
	/// </summary>
	public class DummyReader : IAudioDataIO
	{
		public DummyReader()
		{
		}

		public double BitRate
		{
			get { return 0; }
		}		
		public double Duration
		{
			get { return 0; }
		}
		public bool IsVBR
		{
			get { return false; }
		}
		public int CodecFamily
		{
			get { return AudioDataIOFactory.CF_LOSSY; }
		}
        public bool AllowsParsableMetadata
        {
            get { return false; }
        }
		public IO.ID3v1 ID3v1
		{
			get { return new ID3v1(); }
		}
		public IO.ID3v2 ID3v2
		{
			get { return new ID3v2(); }
		}
		public IO.APEtag APEtag
		{
			get { return new APEtag(); }
		}
        public IMetaDataIO NativeTag
        {
            get { return new DummyTag(); }
        }
        public bool ReadFromFile(MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames)
		{
			return true;
		}
        public bool HasNativeMeta()
        {
            return false;
        }
        public bool RemoveTagFromFile(int tagType)
        {
            return true;
        }
        public bool AddTagToFile(int tagType)
        {
            return true;
        }
        public bool AddTagToFile(TagData theTag, int tagType)
        {
            return true;
        }
    }
}
