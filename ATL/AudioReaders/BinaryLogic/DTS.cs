using System;
using System.IO;
using ATL.Logging;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Digital Theatre System files manipulation (extension : .DTS)
    /// </summary>
	class TDTS : AudioDataReader
	{
        // Standard bitrates (KBit/s)
		private static int[] BITRATES = new int[32] { 32, 56, 64, 96, 112, 128, 192, 224, 256,
														320, 384, 448, 512, 576, 640, 768, 960,
														1024, 1152, 1280, 1344, 1408, 1411, 1472,
														1536, 1920, 2048, 3072, 3840, 0, -1, 1 };
		//open, variable, lossless
   
		// Private declarations
		private uint FChannels;
		private uint FBits;
		private uint FSampleRate;

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
        public double CompressionRatio
        {
            get { return FGetCompressionRatio(); }
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
            get { return false; }
        }

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			FChannels = 0;
			FBits = 0;
			FSampleRate = 0;
		}


		// ********************** Public functions & voids **********************

		public TDTS()
		{
			resetData();
		}

		/* -------------------------------------------------------------------------- */

		// No explicit destructors with C#

		/* -------------------------------------------------------------------------- */

        private double FGetCompressionRatio()
        {
            // Get compression ratio
            if (FValid)
                return (double)FFileSize / ((FDuration * FSampleRate) * (FChannels * FBits / 8) + 44) * 100;
            else
                return 0;
        }

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
            Stream fs = source.BaseStream;
            uint signatureChunk;  
			ushort tehWord;  
			byte[] gayDTS = new byte[8];
            bool result = false;
       	
			signatureChunk = source.ReadUInt32();
			if ( /*0x7FFE8001*/ 25230975 == signatureChunk ) 
			{
				Array.Clear(gayDTS,0,8);	
		
				fs.Seek(3, SeekOrigin.Current);
				gayDTS = source.ReadBytes(8);

				FValid = true;

				tehWord = (ushort)(gayDTS[1] | (gayDTS[0] << 8));
		
				switch ((tehWord & 0x0FC0) >> 6)
				{
					case 0: FChannels = 1; break;
					case 1:
					case 2:
					case 3:
					case 4: FChannels = 2; break;
					case 5:
					case 6: FChannels = 3; break;
					case 7:
					case 8: FChannels = 4; break;
					case 9: FChannels = 5; break;
					case 10:
					case 11:
					case 12: FChannels = 6; break;
					case 13: FChannels = 7; break;
					case 14:
					case 15: FChannels = 8; break;
					default: FChannels = 0; break;
				}

				switch ((tehWord & 0x3C) >> 2)
				{
					case 1: FSampleRate = 8000; break;
					case 2: FSampleRate = 16000; break;
					case 3: FSampleRate = 32000; break;
					case 6: FSampleRate = 11025; break;
					case 7: FSampleRate = 22050; break;
					case 8: FSampleRate = 44100; break;
					case 11: FSampleRate = 12000; break;
					case 12: FSampleRate = 24000; break;
					case 13: FSampleRate = 48000; break;
					default: FSampleRate = 0; break;
				}

				tehWord = 0;
				tehWord = (ushort)( gayDTS[2] | (gayDTS[1] << 8) );

				FBitrate = (ushort)BITRATES[(tehWord & 0x03E0) >> 5] * 1000;

				tehWord = 0;
				tehWord = (ushort)( gayDTS[7] | (gayDTS[6] << 8) );

				switch ((tehWord & 0x01C0) >> 6) 
				{
					case 0:
					case 1: FBits = 16; break;
					case 2:
					case 3: FBits = 20; break;
					case 4:
					case 5: FBits = 24; break;
					default: FBits = 16; break;
				}

				FDuration = (double)FFileSize * 8 / FBitrate;

				result = true;
			}    

			return result;
		}

	}
}