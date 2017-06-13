using System;

namespace ATL.AudioReaders.BinaryLogic
{
	/// <summary>
	/// Dummy audio data provider
	/// </summary>
	public class DummyReader : IAudioDataReader
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
        public double SampleRate
        {
            get { return 0.0; }
        }
        public bool IsVBR
		{
			get { return false; }
		}
		public int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public bool AllowsParsableMetadata
        {
            get { return false; }
        }
		public BinaryLogic.TID3v1 ID3v1
		{
			get { return new TID3v1(); }
		}
		public BinaryLogic.TID3v2 ID3v2
		{
			get { return new TID3v2(); }
		}
		public BinaryLogic.TAPEtag APEtag
		{
			get { return new TAPEtag(); }
		}

		public bool ReadFromFile(String fileName, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			return true;
		}
	}
}
