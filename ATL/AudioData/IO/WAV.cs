using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for PCM (uncompressed audio) files manipulation (extension : .WAV)
    /// </summary>
	class WAV : IAudioDataIO
	{
		// Format type names
		public const String WAV_FORMAT_UNKNOWN = "Unknown";
		public const String WAV_FORMAT_PCM = "Windows PCM";
		public const String WAV_FORMAT_ADPCM = "Microsoft ADPCM";
		public const String WAV_FORMAT_ALAW = "A-LAW";
		public const String WAV_FORMAT_MULAW = "MU-LAW";
		public const String WAV_FORMAT_DVI_IMA_ADPCM = "DVI/IMA ADPCM";
		public const String WAV_FORMAT_MP3 = "MPEG Layer III";

		// Used with ChannelModeID property
		public const byte WAV_CM_MONO = 1;                     // Index for mono mode
		public const byte WAV_CM_STEREO = 2;                 // Index for stereo mode

		// Channel mode names
		public String[] WAV_MODE = new String[3] {"Unknown", "Mono", "Stereo"};

		private ushort formatID;
		private byte channelNumber;
		private uint sampleRate;
		private uint bytesPerSecond;
		private ushort blockAlign;
		private byte bitsPerSample;
		private int sampleNumber;
		private ushort headerSize;

        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        /* Unused for now
        public ushort FormatID // Format type code
		{
			get { return this.formatID; }
		}	
		public String Format // Format type name
		{
			get { return this.getFormat(); }
		}	
		public byte ChannelNumber // Number of channels
		{
			get { return this.channelNumber; }
		}		 
		public String ChannelMode // Channel mode name
		{
			get { return this.getChannelMode(); }
		}	
		public byte BitsPerSample // Bits/sample
        {
            get { return this.bitsPerSample; }
        }
		public uint BytesPerSecond // Bytes/second
		{
			get { return this.bytesPerSecond; }
		}
		public ushort BlockAlign // Block alignment
		{
			get { return this.blockAlign; }
		}			
		public ushort HeaderSize // Header size (bytes)
		{
			get { return this.headerSize; }
		}	
        */
  

		private const String DATA_CHUNK = "data";                        // Data chunk ID

		// WAV file header data
		private class WAVRecord
		{
			// RIFF file header
			public char[] RIFFHeader = new char[4];                         // Must be "RIFF"
			public int FileSize;                                // Must be "RealFileSize - 8"
			public char[] WAVEHeader = new char[4];                         // Must be "WAVE"
			// Format information
			public char[] FormatHeader = new char[4];                       // Must be "fmt "
			public int FormatSize;                                             // Format size
			public ushort FormatID;                                       // Format type code
			public ushort ChannelNumber;                                // Number of channels
			public int SampleRate;                                        // Sample rate (hz)
			public int BytesPerSecond;                                        // Bytes/second
			public ushort BlockAlign;                                      // Block alignment
			public ushort BitsPerSample;                                       // Bits/sample
			public char[] DataHeader = new char[4];                          // Can be "data"
			public int SampleNumber;                          // Number of samples (optional)

			public void Reset()
			{
				Array.Clear(RIFFHeader,0,RIFFHeader.Length);
				FileSize = 0;
				Array.Clear(WAVEHeader,0,WAVEHeader.Length);
				Array.Clear(FormatHeader,0,FormatHeader.Length);
				FormatSize = 0;
				FormatID = 0;
				ChannelNumber = 0;
				SampleRate = 0;
				BytesPerSecond = 0;
				BlockAlign = 0;
				BitsPerSample = 0;
				Array.Clear(DataHeader,0,DataHeader.Length);
				SampleNumber = 0;
			}
		}

        
        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate // Sample rate (hz)
        {
            get { return (int)this.sampleRate; }
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
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1);
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;

            formatID = 0;
            channelNumber = 0;
            sampleRate = 0;
            bytesPerSecond = 0;
            blockAlign = 0;
            bitsPerSample = 0;
            sampleNumber = 0;
            headerSize = 0;
        }

        public WAV(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool readWAV(BinaryReader source, ref WAVRecord WAVData)
		{
			bool result = true;

            source.BaseStream.Seek(0, SeekOrigin.Begin);

			// Read header		
			WAVData.RIFFHeader = source.ReadChars(WAVData.RIFFHeader.Length);
			WAVData.FileSize = source.ReadInt32();
			WAVData.WAVEHeader = source.ReadChars(WAVData.WAVEHeader.Length);
			WAVData.FormatHeader = source.ReadChars(WAVData.FormatHeader.Length);
			WAVData.FormatSize = source.ReadInt32();
			WAVData.FormatID = source.ReadUInt16();
			WAVData.ChannelNumber = source.ReadUInt16();
			WAVData.SampleRate = source.ReadInt32();
			WAVData.BytesPerSecond = source.ReadInt32();
			WAVData.BlockAlign = source.ReadUInt16();
			WAVData.BitsPerSample = source.ReadUInt16();
			WAVData.DataHeader = source.ReadChars(WAVData.DataHeader.Length);

			// Read number of samples if exists
			if ( ! StreamUtils.StringEqualsArr(DATA_CHUNK,WAVData.DataHeader))
			{
				source.BaseStream.Seek(WAVData.FormatSize + 28, SeekOrigin.Begin);
				WAVData.SampleNumber = source.ReadInt32();
			}

            return result;
		}

		private static bool headerIsValid(WAVRecord WAVData)
		{
			bool result = true;
			// Header validation
			if (! StreamUtils.StringEqualsArr("RIFF",WAVData.RIFFHeader)) result = false;
			if (! StreamUtils.StringEqualsArr("WAVE",WAVData.WAVEHeader)) result = false;
			if (! StreamUtils.StringEqualsArr("fmt ",WAVData.FormatHeader)) result = false;
			if ( (WAVData.ChannelNumber != WAV_CM_MONO) &&
				(WAVData.ChannelNumber != WAV_CM_STEREO) ) result = false;

			return result;
		}

        /* Unused for now
		private String getFormat()
		{
			// Get format type name
			switch (formatID)
			{
				case 1: return WAV_FORMAT_PCM;
				case 2: return WAV_FORMAT_ADPCM;
				case 6: return WAV_FORMAT_ALAW;
				case 7: return WAV_FORMAT_MULAW;
				case 17: return WAV_FORMAT_DVI_IMA_ADPCM;
				case 85: return WAV_FORMAT_MP3;
				default : return "";  
			}
		}

		private String getChannelMode()
		{
			// Get channel mode name
			return WAV_MODE[channelNumber];
		}
        */

		private double getDuration()
		{
			// Get duration
			double result = 0;
			if (isValid)
			{
				if ((sampleNumber == 0) && (bytesPerSecond > 0))
					result = (double)(sizeInfo.FileSize - headerSize - sizeInfo.ID3v1Size) / bytesPerSecond;
				if ((sampleNumber > 0) && (sampleRate > 0))
					result = (double)sampleNumber / sampleRate;
			}
			return result;
		}

        private double getBitrate()
        {
            return Math.Round((double)this.bitsPerSample * this.sampleRate * this.channelNumber);
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            WAVRecord WAVData = new WAVRecord();

			// Reset and load header data from file to variable
			WAVData.Reset();

            this.sizeInfo = sizeInfo;
            resetData();
  
			bool result = readWAV(source, ref WAVData);
			// Process data if loaded and header valid
			if ( (result) && (headerIsValid(WAVData)) )
			{
				isValid = true;
				// Fill properties with header data
				formatID = WAVData.FormatID;
				channelNumber = (byte)WAVData.ChannelNumber;
				sampleRate = (uint)WAVData.SampleRate;
				bytesPerSecond = (uint)WAVData.BytesPerSecond;
				blockAlign = WAVData.BlockAlign;
				bitsPerSample = (byte)WAVData.BitsPerSample;
				sampleNumber = WAVData.SampleNumber;
				if ( StreamUtils.StringEqualsArr(DATA_CHUNK, WAVData.DataHeader) ) headerSize = 44;
				else headerSize = (ushort)(WAVData.FormatSize + 40);
                bitrate = getBitrate();
                duration = getDuration();
				if (headerSize > sizeInfo.FileSize) headerSize = (ushort)sizeInfo.FileSize; // ??
			}
			return result;
		}

	}
}