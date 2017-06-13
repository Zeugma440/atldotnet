
using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for OptimFROG files manipulation (extensions : .OFR, .OFS)
    /// </summary>
	class TOptimFrog : AudioDataReader
	{

		private String[] OFR_COMPRESSION = new String[10] 
		{
			"fast", "normal", "high", "extra",
			"best", "ultra", "insane", "highnew", "extranew", "bestnew"};

		private sbyte[] OFR_BITS = new sbyte[11] 
	{
		8, 8, 16, 16, 24, 24, 32, 32,
		-32, -32, -32 }; //negative value corresponds to floating point type.

		private String[] OFR_CHANNELMODE = new String[2] {"Mono", "Stereo"};

					
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
      
		public TOfrHeader Header // OptimFROG header
		{
			get { return this.FHeader; }
		}	

        public String Version // Encoder version
		{
			get { return this.FGetVersion(); }
		}									   
		public String Compression // Compression level
		{
			get { return this.FGetCompression(); }
		}	
		public String ChannelMode // Channel mode
		{
			get { return this.FGetChannelMode(); }
		}
		public sbyte Bits // Bits per sample
		{
			get { return this.FGetBits(); }
		}		  		
		public long Samples // Number of samples
		{
			get { return this.FGetSamples(); }
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

		public double CompressionRatio // Compression ratio (%)
		{
			get { return this.FGetCompressionRatio(); }
		}	

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset data
			FHeader.Reset();
		}

		// ---------------------------------------------------------------------------

		private bool FGetValid()
		{
			return (
				StreamUtils.StringEqualsArr("OFR ", FHeader.ID) &&
				(FHeader.SampleRate > 0) &&
				((0 <= FHeader.SampleType) && (FHeader.SampleType <= 10)) &&
				((0 <= FHeader.ChannelMode) &&(FHeader.ChannelMode <= 1)) &&
				((0 <= FHeader.CompressionID >> 3) && (FHeader.CompressionID >> 3 <= 9)) );
		}

		// ---------------------------------------------------------------------------

		private String FGetVersion()
		{
			// Get encoder version
			return  ( ((FHeader.EncoderID >> 4) + 4500) / 1000 ).ToString().Substring(0,5); // Pas exactement...
		}

		// ---------------------------------------------------------------------------

		private String FGetCompression()
		{
			// Get compression level
			return OFR_COMPRESSION[FHeader.CompressionID >> 3];
		}

		// ---------------------------------------------------------------------------

		private sbyte FGetBits()
		{
			// Get number of bits per sample
			return OFR_BITS[FHeader.SampleType];
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			// Get channel mode
			return OFR_CHANNELMODE[FHeader.ChannelMode];
		}

		// ---------------------------------------------------------------------------

		private long FGetSamples()
		{
			// Get number of samples
			return ( ((Header.Length >> Header.ChannelMode) * 0x00000001) +
				((Header.HiLength >> Header.ChannelMode) * 0x00010000) );
		}

		// ---------------------------------------------------------------------------

		private double getDuration()
		{
			double nbSamples = (double)FGetSamples();
			// Get song duration
			if (FHeader.SampleRate > 0)
				return nbSamples / FHeader.SampleRate;
			else
				return 0;
		}

		// ---------------------------------------------------------------------------

		private int getSampleRate()
		{
			return Header.SampleRate;
		}

		// ---------------------------------------------------------------------------

		private double FGetCompressionRatio()
		{
			// Get compression ratio
			if (FGetValid())
				return FFileSize /
					(FGetSamples() * (FHeader.ChannelMode+1) * Math.Abs(FGetBits()) / 8 + 44) * 100;
			else
				return 0;
		}

        private double getBitrate()
        {
            return ((this.FFileSize - FHeader.Size - FID3v1.Size -FID3v2.Size - FAPEtag.Size) * 8 / (Duration));
        }

		// ********************** Public functions & voids **********************

		public TOptimFrog()
		{
			// Create object  
			resetData();
		}

		// ---------------------------------------------------------------------------

		// No explicit destructors with C#

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			bool result = false;

			// Search for file tag
            FID3v1.Read(source);
            FID3v2.Read(source, pictureStreamHandler);
            FAPEtag.Read(source, pictureStreamHandler);

			// Read header data
			source.BaseStream.Seek(ID3v2.Size, SeekOrigin.Begin);

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
                FValid = true;
                result = true;
                FDuration = getDuration();
                FBitrate = getBitrate();
                FSampleRate = getSampleRate();
            }

			return result;
		}

	}
}