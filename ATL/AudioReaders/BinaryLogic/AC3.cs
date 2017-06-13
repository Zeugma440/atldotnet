using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Dolby Digital files manipulation (extension : .AC3)
    /// </summary>
	class TAC3 : AudioDataReader
	{
        // Standard bitrates (KBit/s)
		private static int[] BITRATES = new int[19] { 32, 40, 48, 56, 64, 80, 96, 112, 128, 160,
														192, 224, 256, 320, 384, 448, 512, 576, 640 };
 
		// Private declarations 
		private uint FChannels;
		private uint FBits;


        // Public declarations 
		public uint Channels
		{
			get { return FChannels; }
		}
		public uint Bits
		{
			get { return FBits; }
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
            get { return true; }
        }

		// ********************** Private functions & voids ********************* 

        protected override void resetSpecificData()
		{
			FChannels = 0;
			FBits = 0;
			FSampleRate = 0;
		}


		// ********************** Public functions & voids ********************** 

		public TAC3()
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
            ushort signatureChunk;
			byte tehByte;

			bool result = false;

            FAPEtag.Read(source, pictureStreamHandler);

			signatureChunk = source.ReadUInt16();
            
			if ( /*0x0B77*/ 30475 == signatureChunk )
			{
				tehByte = 0;
		
				source.BaseStream.Seek(2, SeekOrigin.Current);
				tehByte = source.ReadByte();

				FValid = true;

				switch (tehByte & 0xC0)
				{
					case 0: FSampleRate = 48000; break;
					case 0x40: FSampleRate = 44100; break;
					case 0x80: FSampleRate = 32000; break;
					default : FSampleRate = 0; break;
				}

				FBitrate = BITRATES[(tehByte & 0x3F) >> 1] * 1000;

				tehByte = 0;

                source.BaseStream.Seek(1, SeekOrigin.Current);
				tehByte = source.ReadByte();

				switch (tehByte & 0xE0)
				{
					case 0: FChannels = 2; break;
					case 0x20: FChannels = 1; break;
					case 0x40: FChannels = 2; break;
					case 0x60: FChannels = 3; break;
					case 0x80: FChannels = 3; break;
					case 0xA0: FChannels = 4; break;
					case 0xC0: FChannels = 4; break;
					case 0xE0: FChannels = 5; break;
					default : FChannels = 0; break;
				}

				FBits = 16;
				FDuration = (double)FFileSize * 8 / FBitrate;

				result = true;
			}
  
			return result;
		}
	}
}