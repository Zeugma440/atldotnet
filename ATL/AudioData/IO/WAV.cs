using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for PCM (uncompressed audio) files manipulation (extension : .WAV)
    /// </summary>
	class WAV : MetaDataIO, IAudioDataIO
	{
		// Format type names
		public const String WAV_FORMAT_UNKNOWN = "Unknown";
		public const String WAV_FORMAT_PCM = "Windows PCM";
		public const String WAV_FORMAT_ADPCM = "Microsoft ADPCM";
		public const String WAV_FORMAT_ALAW = "A-LAW";
		public const String WAV_FORMAT_MULAW = "MU-LAW";
		public const String WAV_FORMAT_DVI_IMA_ADPCM = "DVI/IMA ADPCM";
		public const String WAV_FORMAT_MP3 = "MPEG Layer III";

        private const string HEADER_RIFF = "RIFF";
        private const string HEADER_RIFX = "RIFX";

        private const string FORMAT_WAVE = "WAVE";

        private const String FORMAT_CHUNK = "fmt ";
        private const String DATA_CHUNK = "data";


        // Used with ChannelModeID property
        public const byte WAV_CM_MONO = 1;                     // Index for mono mode
		public const byte WAV_CM_STEREO = 2;                 // Index for stereo mode

		// Channel mode names
		public String[] WAV_MODE = new String[3] {"Unknown", "Mono", "Stereo"};

//		private ushort formatID;
		private ushort channelNumber;
		private uint sampleRate;
		private uint bytesPerSecond;
//		private ushort blockAlign;
		private ushort bitsPerSample;
		private int sampleNumber;
		private uint headerSize;

        private double bitrate;
        private double duration;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private bool _isLittleEndian;


        private static IDictionary<string, byte> frameMapping; // Mapping between WAV frame codes and ATL frame codes


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

        /*
		// WAV file header data
		private class WAVRecord
		{
			// RIFF file header
			public char[] RIFFHeader = new char[4];               // Must be "RIFF" or "RIFX", though I have yet to find a sample file of the latter
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
        */

        
        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate
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


        // MetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }

        protected override bool isLittleEndian
        {
            get { return _isLittleEndian; }
        }

        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            duration = 0;
            bitrate = 0;

//            formatID = 0;
            channelNumber = 0;
            sampleRate = 0;
            bytesPerSecond = 0;
//            blockAlign = 0;
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

        private bool readWAV(Stream source)
		{
			bool result = true;
            uint riffChunkSize, formatChunkSize;
            byte[] data = new byte[4];

            source.Seek(0, SeekOrigin.Begin);

            // Read header
            source.Read(data, 0, 4);
            string str = Utils.Latin1Encoding.GetString(data);
            if (str.Equals(HEADER_RIFF))
            {
                _isLittleEndian = true;
            }
            else if (str.Equals(HEADER_RIFX))
            {
                _isLittleEndian = false;
            } else
            {
                return false;
            }

            source.Read(data, 0, 4);
            if (isLittleEndian) riffChunkSize = StreamUtils.DecodeUInt32(data); else riffChunkSize = StreamUtils.DecodeBEUInt32(data);

            // Format code
            source.Read(data, 0, 4);
            str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_WAVE)) return false;


            // Format chunk
            source.Read(data, 0, 4);
            str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(FORMAT_CHUNK)) return false;

            source.Read(data, 0, 4);
            if (isLittleEndian) formatChunkSize = StreamUtils.DecodeUInt32(data); else formatChunkSize = StreamUtils.DecodeBEUInt32(data);

            source.Seek(2, SeekOrigin.Current); // FormatId

            source.Read(data, 0, 2);
            if (isLittleEndian) channelNumber = StreamUtils.DecodeUInt16(data); else channelNumber = StreamUtils.DecodeBEUInt16(data);
            if (channelNumber != WAV_CM_MONO && channelNumber != WAV_CM_STEREO) return false;

            source.Read(data, 0, 4);
            if (isLittleEndian) sampleRate = StreamUtils.DecodeUInt32(data); else sampleRate = StreamUtils.DecodeBEUInt32(data);

            source.Read(data, 0, 4);
            if (isLittleEndian) bytesPerSecond = StreamUtils.DecodeUInt32(data); else bytesPerSecond = StreamUtils.DecodeBEUInt32(data);

            source.Seek(2, SeekOrigin.Current); // BlockAlign

            source.Read(data, 0, 2);
            if (isLittleEndian) bitsPerSample = StreamUtils.DecodeUInt16(data); else bitsPerSample = StreamUtils.DecodeBEUInt16(data);


            // Data chunk -- TODO : write a proper parser that can browse through sub-chunks (e.g. fact)
            source.Read(data, 0, 4);
            str = Utils.Latin1Encoding.GetString(data);
            if (!str.Equals(DATA_CHUNK))
            {
                source.Seek(formatChunkSize + 28, SeekOrigin.Begin);

                source.Read(data, 0, 4);
                if (isLittleEndian) sampleNumber = StreamUtils.DecodeInt32(data); else sampleNumber = StreamUtils.DecodeBEInt32(data);

                headerSize = formatChunkSize + 40; 
            } else
            {
                headerSize = 44;
            }

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

            if ((sampleNumber == 0) && (bytesPerSecond > 0))
				result = (double)(sizeInfo.FileSize - headerSize - sizeInfo.ID3v1Size) / bytesPerSecond;
			if ((sampleNumber > 0) && (sampleRate > 0))
				result = (double)(sampleNumber / sampleRate);

			return result;
		}

        private double getBitrate()
        {
            return Math.Round((double)this.bitsPerSample * this.sampleRate * this.channelNumber);
        }

        public bool Read(BinaryReader source, SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            resetData();
  
			bool result = readWAV(source.BaseStream);

			// Process data if loaded and header valid
			if (result)
			{
                bitrate = getBitrate();
                duration = getDuration();
			}
			return result;
		}

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            throw new NotImplementedException();
        }
    }
}