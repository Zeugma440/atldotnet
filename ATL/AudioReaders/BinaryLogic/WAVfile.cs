using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for PCM (uncompressed audio) files manipulation (extension : .WAV)
    /// </summary>
	class TWAVfile : AudioDataReader
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

		private ushort FFormatID;
		private byte FChannelNumber;
		private uint FSampleRate;
		private uint FBytesPerSecond;
		private ushort FBlockAlign;
		private byte FBitsPerSample;
		private int FSampleNumber;
		private ushort FHeaderSize;
      
		public ushort FormatID // Format type code
		{
			get { return this.FFormatID; }
		}	
		public String Format // Format type name
		{
			get { return this.FGetFormat(); }
		}	
		public byte ChannelNumber // Number of channels
		{
			get { return this.FChannelNumber; }
		}		 
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}	
		public uint SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
		}
		public byte BitsPerSample // Bits/sample
		{
			get { return this.FBitsPerSample; }
		}

		public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSLESS; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		public uint BytesPerSecond // Bytes/second
		{
			get { return this.FBytesPerSecond; }
		}
		public ushort BlockAlign // Block alignment
		{
			get { return this.FBlockAlign; }
		}			
		public ushort HeaderSize // Header size (bytes)
		{
			get { return this.FHeaderSize; }
		}	
  

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

		// ********************* Auxiliary functions & voids ********************

		private bool ReadWAV(BinaryReader source, ref WAVRecord WAVData)
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

		// ---------------------------------------------------------------------------

		private bool HeaderIsValid(WAVRecord WAVData)
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

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset all data
			FFormatID = 0;
			FChannelNumber = 0;
			FSampleRate = 0;
			FBytesPerSecond = 0;
			FBlockAlign = 0;
			FBitsPerSample = 0;
			FSampleNumber = 0;
			FHeaderSize = 0;
		}

		// ---------------------------------------------------------------------------

		private String FGetFormat()
		{
			// Get format type name
			switch (FFormatID)
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

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			// Get channel mode name
			return WAV_MODE[FChannelNumber];
		}

		// ---------------------------------------------------------------------------

		private double FGetDuration()
		{
			// Get duration
			double result = 0;
			if (FValid)
			{
				if ((FSampleNumber == 0) && (FBytesPerSecond > 0))
					result = (double)(FFileSize - FHeaderSize - FID3v1.Size) / FBytesPerSecond;
				if ((FSampleNumber > 0) && (FSampleRate > 0))
					result = (double)FSampleNumber / FSampleRate;
			}
			return result;
		}

        // ---------------------------------------------------------------------------

        private double getBitrate()
        {
            return Math.Round((double)this.FBitsPerSample * this.FSampleRate * this.ChannelNumber);
        }

		// ********************** Public functions & voids **********************

		public TWAVfile()
		{
			// Create object  
			resetData();
		}

		// ---------------------------------------------------------------------------

        public override bool ReadFromFile(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			WAVRecord WAVData = new WAVRecord();

			// Reset and load header data from file to variable
			WAVData.Reset();

            FID3v1.ReadFromFile(source);
  
			bool result = ReadWAV(source, ref WAVData);
			// Process data if loaded and header valid
			if ( (result) && (HeaderIsValid(WAVData)) )
			{
				FValid = true;
				// Fill properties with header data
				FFormatID = WAVData.FormatID;
				FChannelNumber = (byte)WAVData.ChannelNumber;
				FSampleRate = (uint)WAVData.SampleRate;
				FBytesPerSecond = (uint)WAVData.BytesPerSecond;
				FBlockAlign = WAVData.BlockAlign;
				FBitsPerSample = (byte)WAVData.BitsPerSample;
				FSampleNumber = WAVData.SampleNumber;
				if ( StreamUtils.StringEqualsArr(DATA_CHUNK, WAVData.DataHeader) ) FHeaderSize = 44;
				else FHeaderSize = (ushort)(WAVData.FormatSize + 40);
				FFileSize = (uint)WAVData.FileSize + 8;
                FBitrate = getBitrate();
                FDuration = FGetDuration();
				if (FHeaderSize > FFileSize) FHeaderSize = (ushort)FFileSize;
			}
			return result;
		}

	}
}