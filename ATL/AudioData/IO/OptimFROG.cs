using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OptimFROG files manipulation (extensions : .OFR, .OFS)
    /// </summary>
	class OptimFrog : IAudioDataIO
	{

		private static readonly string[] OFR_COMPRESSION = new string[10] 
		{
			"fast", "normal", "high", "extra",
			"best", "ultra", "insane", "highnew", "extranew", "bestnew"
        };

		private static readonly sbyte[] OFR_BITS = new sbyte[11] 
	    {
		    8, 8, 16, 16, 24, 24, 32, 32,
		    -32, -32, -32 }; //negative value corresponds to floating point type.

		private static readonly string[] OFR_CHANNELMODE = new string[2] {"Mono", "Stereo"};

					
		// Real structure of OptimFROG header
		public class TOfrHeader
		{
			public char[] ID = new char[4];                      // Always 'OFR '
			public uint Size;
			public uint Length;
			public ushort HiLength;
			public byte SampleType;
			public byte ChannelMode;
			public int SampleRate;
			public ushort EncoderID;
			public byte CompressionID;
			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				Size = 0;
				Length = 0;
				HiLength = 0;
				SampleType = 0;
				ChannelMode = 0;
				SampleRate = 0;
				EncoderID = 0;
				CompressionID = 0;
			}
		}

    
		private TOfrHeader FHeader = new TOfrHeader();

        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;


        public TOfrHeader Header // OptimFROG header
		{
			get { return this.FHeader; }
		}	

        public String Version // Encoder version
		{
			get { return this.getVersion(); }
		}									   
		public String Compression // Compression level
		{
			get { return this.getCompression(); }
		}	
		public String ChannelMode // Channel mode
		{
			get { return this.getChannelMode(); }
		}
		public sbyte Bits // Bits per sample
		{
			get { return this.getBits(); }
		}		  		
		public long Samples // Number of samples
		{
			get { return this.getSamples(); }
		}
        public double CompressionRatio // Compression ratio (%)
        {
            get { return this.getCompressionRatio(); }
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate // Sample rate (Hz)
		{
			get { return this.getSampleRate(); }
		}
        public bool IsVBR
		{
			get { return false; }
		}
		public int CodecFamily
		{
			get { return AudioDataIOFactory.CF_LOSSLESS; }
		}
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool HasNativeMeta()
        {
            return false;
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_APE) || (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2);
        }

        
        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;

            FHeader.Reset();
		}

        public OptimFrog(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool getValid()
		{
			return (
				StreamUtils.StringEqualsArr("OFR ", FHeader.ID) &&
				(FHeader.SampleRate > 0) &&
				((0 <= FHeader.SampleType) && (FHeader.SampleType <= 10)) &&
				((0 <= FHeader.ChannelMode) &&(FHeader.ChannelMode <= 1)) &&
				((0 <= FHeader.CompressionID >> 3) && (FHeader.CompressionID >> 3 <= 9)) );
		}

		private String getVersion()
		{
			// Get encoder version
			return  ( ((FHeader.EncoderID >> 4) + 4500) / 1000 ).ToString().Substring(0,5); // Pas exactement...
		}

		private String getCompression()
		{
			// Get compression level
			return OFR_COMPRESSION[FHeader.CompressionID >> 3];
		}

		private sbyte getBits()
		{
			// Get number of bits per sample
			return OFR_BITS[FHeader.SampleType];
		}

		private String getChannelMode()
		{
			// Get channel mode
			return OFR_CHANNELMODE[FHeader.ChannelMode];
		}

		private long getSamples()
		{
			// Get number of samples
			return ( ((Header.Length >> Header.ChannelMode) * 0x00000001) +
				((Header.HiLength >> Header.ChannelMode) * 0x00010000) );
		}

		private double getDuration()
		{
			double nbSamples = (double)getSamples();
			// Get song duration
			if (FHeader.SampleRate > 0)
				return nbSamples / FHeader.SampleRate;
			else
				return 0;
		}

		private int getSampleRate()
		{
			return Header.SampleRate;
		}

		private double getCompressionRatio()
		{
			// Get compression ratio
			if (getValid())
				return sizeInfo.FileSize /
					(getSamples() * (FHeader.ChannelMode+1) * Math.Abs(getBits()) / 8 + 44) * 100;
			else
				return 0;
		}

        private double getBitrate()
        {
            return ((sizeInfo.FileSize - FHeader.Size - sizeInfo.TotalTagSize) * 8 / (Duration));
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            this.sizeInfo = sizeInfo;
            resetData();

			// Read header data
			source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

			FHeader.ID = source.ReadChars(4);
			FHeader.Size = source.ReadUInt32();
			FHeader.Length = source.ReadUInt32();
			FHeader.HiLength = source.ReadUInt16();
			FHeader.SampleType = source.ReadByte();
			FHeader.ChannelMode = source.ReadByte();
			FHeader.SampleRate = source.ReadInt32();
			FHeader.EncoderID = source.ReadUInt16();
			FHeader.CompressionID = source.ReadByte();

            if (StreamUtils.StringEqualsArr("OFR ", FHeader.ID))
            {
                isValid = true;
                result = true;
                duration = getDuration();
                bitrate = getBitrate();
            }

			return result;
		}

	}
}