using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for DSD Stream File files manipulation (extension : .DSF)
    /// </summary>
	class DSF : IAudioDataIO, IMetaDataEmbedder
    {
        // Headers ID
        public const String DSD_ID = "DSD ";
        public const String FMT_ID = "fmt ";
        public const String DATA_ID = "data";

 
		// Private declarations 
        private int formatVersion;
		private uint channels;
		private uint bits;
		private uint sampleRate;

        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private long id3v2Offset;
        private FileStructureHelper id3v2StructureHelper = new FileStructureHelper();


        // Public declarations 
        public uint Channels
		{
			get { return channels; }
		}
		public uint Bits
		{
			get { return bits; }
		}
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }

        
        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate
        {
            get { return (int)sampleRate; }
        }
        public bool IsVBR
		{
			get { return false; }
		}
        public int CodecFamily
		{
			get { return AudioDataIOFactory.CF_LOSSLESS; }
		}
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V2);
        }

        // IMetaDataEmbedder
        public long HasEmbeddedID3v2
        {
            get { return id3v2Offset; }
        }
        public uint ID3v2EmbeddingHeaderSize
        {
            get { return 0; }
        }
        public FileStructureHelper.Zone Id3v2Zone
        {
            get { return id3v2StructureHelper.GetZone(FileStructureHelper.DEFAULT_ZONE_NAME); }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
		{
            formatVersion = -1;
			channels = 0;
			bits = 0;
			sampleRate = 0;
            duration = 0;
            bitrate = 0;
            isValid = false;
            id3v2Offset = -1;
            id3v2StructureHelper.Clear();
        }

        public DSF(string filePath)
		{
            this.filePath = filePath;

            resetData();
		}


        // ---------- SUPPORT METHODS

        // Get compression ratio 
        private double getCompressionRatio()
        {
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration / 1000.0 * sampleRate) * (channels * bits / 8) + 44) * 100;
            else
                return 0;
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;
            bool result = false;

            resetData();

            source.BaseStream.Seek(0, SeekOrigin.Begin);
            if (DSD_ID.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(4))))
			{
				source.BaseStream.Seek(16, SeekOrigin.Current); // Chunk size and file size
                id3v2Offset = source.ReadInt64();

                if (FMT_ID.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(4))))
                {
                    source.BaseStream.Seek(8, SeekOrigin.Current); // Chunk size

                    formatVersion = source.ReadInt32();

                    if (formatVersion > 1)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
                        return result;
                    }

                    isValid = true;

                    source.BaseStream.Seek(8, SeekOrigin.Current); // Format ID (4), Channel type (4)

                    channels = source.ReadUInt32();
                    sampleRate = source.ReadUInt32();
                    bits = source.ReadUInt32();

                    ulong sampleCount = source.ReadUInt64();

                    duration = (double)sampleCount * 1000.0 / sampleRate;
                    bitrate = Math.Round(((double)(sizeInfo.FileSize - source.BaseStream.Position)) * 8 / duration); //time to calculate average bitrate

                    result = true;
                }

                // Load tag if exists
                if (id3v2Offset > 0)
                {
                    if (readTagParams.PrepareForWriting)
                    {
                        id3v2StructureHelper.AddZone(id3v2Offset, (int)(source.BaseStream.Length - id3v2Offset));
                        id3v2StructureHelper.AddSize(12, source.BaseStream.Length);
                        id3v2StructureHelper.AddIndex(20, id3v2Offset);
                    }
                }
                else
                {
                    id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

                    if (readTagParams.PrepareForWriting)
                    {
                        // Add EOF zone for future tag writing
                        id3v2StructureHelper.AddZone(source.BaseStream.Length, 0);
                        id3v2StructureHelper.AddSize(12, source.BaseStream.Length);
                        id3v2StructureHelper.AddIndex(20, source.BaseStream.Length);
                    }
                }
            }

            return result;
		}

        public void WriteID3v2EmbeddingHeader(BinaryWriter w, long tagSize)
        {
            // Nothing to do here; DSF format defines no frame header for its embedded ID3v2 tag
        }
    }
}