using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for True Audio files manipulation (extensions : .TTA)
    /// </summary>
	class TTTA : AudioDataReader
	{
		private class tta_header
		{
			public ushort AudioFormat;
			public ushort NumChannels;
			public ushort BitsPerSample;
			public uint SampleRate;
			public uint DataLength;
			public uint CRC32;
    
			public void Reset()    
			{
				AudioFormat = 0;
				NumChannels = 0;
				BitsPerSample = 0;
				SampleRate = 0;
				DataLength = 0;
				CRC32 = 0;
			}
		}

		// Private declarations
		private uint FAudioFormat;
		private uint FChannels;
		private uint FBits;
		private uint FSampleRate;
		private uint FSamples;
		private uint FCRC32;

		// Public declarations    
		public uint Channels
		{
			get { return FChannels; }
		}
		public uint Bits
		{
			get { return FBits; }
		}
		public uint SampleRate
		{
			get { return FSampleRate; }
		}
        
		public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		public double CompressionRatio
		{
			get { return FGetCompressionRatio(); }
		}
		public uint Samples // Number of samples
		{
			get { return FSamples; }	
		}
		public uint CRC32
		{
			get { return FCRC32; }	
		}
		public uint AudioFormat
		{
			get { return FAudioFormat; }	
		}

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset all data
			FAudioFormat = 0;
			FChannels = 0;
			FBits = 0;
			FSampleRate = 0;
			FSamples = 0;
			FCRC32 = 0;
		}


		// ********************** Public functions & voids **********************

		public TTTA()
		{
			// Create object  
			resetData();
		}

		/* -------------------------------------------------------------------------- */

		// No explicit destructors with C#

		/* -------------------------------------------------------------------------- */

        private double FGetCompressionRatio()
        {
            // Get compression ratio
            if (FValid)
                return (double)FFileSize / (FSamples * (FChannels * FBits / 8) + 44) * 100;
            else
                return 0;
        }

        /* -------------------------------------------------------------------------- */

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
            Stream fs = source.BaseStream;

			char[] signatureChunk = new char[4];
			tta_header ttaheader = new tta_header();
			long TagSize;

			bool result = false;
    
            // load tags first
            FID3v2.Read(source, pictureStreamHandler);
            FID3v1.Read(source);
            FAPEtag.Read(source, pictureStreamHandler);

            // calulate total tag size
            TagSize = 0;
            if (FID3v1.Exists) TagSize += FID3v1.Size;
            if (FID3v2.Exists) TagSize += FID3v2.Size;
            if (FAPEtag.Exists) TagSize += FAPEtag.Size;
  	
			// seek past id3v2-tag
			fs.Seek(FID3v2.Size, SeekOrigin.Begin);

			signatureChunk = source.ReadChars(4);
			if ( StreamUtils.StringEqualsArr("TTA1",signatureChunk) ) 
			{
                FValid = true;
				// start looking for chunks
				ttaheader.Reset();
      		
				ttaheader.AudioFormat = source.ReadUInt16();
				ttaheader.NumChannels = source.ReadUInt16();
				ttaheader.BitsPerSample = source.ReadUInt16();
				ttaheader.SampleRate = source.ReadUInt32();
				ttaheader.DataLength = source.ReadUInt32();
				ttaheader.CRC32 = source.ReadUInt32();

				FFileSize = fs.Length;

				FAudioFormat = ttaheader.AudioFormat;
				FChannels = ttaheader.NumChannels;
				FBits = ttaheader.BitsPerSample;
				FSampleRate = ttaheader.SampleRate;
				FSamples = ttaheader.DataLength;
				FCRC32 = ttaheader.CRC32;

				FBitrate = (double)FFileSize * 8 / (FSamples / FSampleRate);
				FDuration = (double)ttaheader.DataLength / ttaheader.SampleRate;

				result = true;
			}
  
			return result;
		}


	}
}
