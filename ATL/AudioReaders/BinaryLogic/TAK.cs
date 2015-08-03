using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for TAK Stream File files manipulation (extension : .TAK)
    /// </summary>
	class TTAK : AudioDataReader
	{
        // Headers ID
        public const int TAK_VERSION_100 = 0;
        public const int TAK_VERSION_210 = 210;
        public const int TAK_VERSION_220 = 220;
        public const String TAK_ID = "tBaK";

 
		// Private declarations 
        private uint formatVersion;
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
			get { return AudioReaderFactory.CF_LOSSLESS; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		// ********************** Private functions & voids ********************* 

        protected override void resetSpecificData()
		{
            formatVersion = 0;
            FChannels = 0;
			FBits = 0;
			FSampleRate = 0;
		}


		// ********************** Public functions & voids ********************** 

		public TTAK()
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
			bool result = false;
            bool doLoop = true;
            long position;

            UInt16 readData16;
            UInt32 readData32;

            UInt32 metaType;
            UInt32 metaSize;
            long sampleCount = 0;
            int frameSizeType = -1;

            if (StreamUtils.StringEqualsArr(TAK_ID,StreamUtils.ReadOneByteChars(source, 4)))
			{
                result = true;
                position = source.BaseStream.Position;
                FAPEtag.Read(source, pictureStreamHandler);
                
                source.BaseStream.Seek(position, SeekOrigin.Begin);

                do // Loop metadata
                {
                    readData32 = source.ReadUInt32();

                    metaType = readData32 & 0x7F;
                    metaSize = readData32 >> 8;

                    position = source.BaseStream.Position;

                    if (0 == metaType) doLoop = false; // End of metadata
                    else if (0x01 == metaType) // Stream info
                    {
                        readData16 = source.ReadUInt16();
                        frameSizeType = readData16 & 0x003C; // bits 11 to 14
                        readData32 = source.ReadUInt32();
                        UInt32 restOfData = source.ReadUInt32();

                        sampleCount = (readData16 >> 14) + (readData32 << 2) + ((restOfData & 0x00000080) << 34);

                        FSampleRate = ((restOfData >> 4) & 0x03ffff) + 6000; // bits 4 to 21
                        FChannels = ((restOfData >> 27) & 0x0F) + 1; // bits 28 to 31

                        if (sampleCount > 0)
                        {
                            FDuration = (double)sampleCount / FSampleRate;
                            FBitrate = Math.Round(((double)(FFileSize - source.BaseStream.Position)) * 8 / FDuration); //time to calculate average bitrate
                        }
                    }
                    else if (0x04 == metaType) // Encoder info
                    {
                        readData32 = source.ReadUInt32();
                        formatVersion = 100 * ((readData32 & 0x00ff0000) >> 16);
                        formatVersion += 10 * ((readData32 & 0x0000ff00) >> 8);
                        formatVersion += (readData32 & 0x000000ff);
                    }

                    source.BaseStream.Seek(position + metaSize, SeekOrigin.Begin);
                } while (doLoop); // End of metadata loop
			}
  
			return result;
		}
	}
}