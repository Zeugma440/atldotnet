using System;
using System.IO;

namespace ATL.AudioData.IO
{
	/// <summary>
	/// Dummy audio data provider
	/// </summary>
	public class DummyReader : IAudioDataIO
	{
        private string filePath = "";

        public DummyReader(string filePath)
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Instancing a Dummy Audio Data Reader for " + filePath);
            this.filePath = filePath;
        }

        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
		{
			get { return 0; }
		}		
		public double Duration
		{
			get { return 0; }
		}
        public int SampleRate
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
        public bool RemoveTagFromFile(int tagType)
        {
            return true;
        }
        public bool AddTagToFile(int tagType)
        {
            return true;
        }
        public bool UpdateTagInFile(TagData theTag, int tagType)
        {
            return true;
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return true;
        }
        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            return true;
        }
    }
}
