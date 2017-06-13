using ATL.Logging;
using System;
using System.IO;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for DSD Stream File files manipulation (extension : .DSF)
    /// </summary>
	class TDSF : AudioDataReader
	{
        // Headers ID
        public const String DSD_ID = "DSD ";
        public const String FMT_ID = "fmt ";
        public const String DATA_ID = "data";

 
		// Private declarations 
        private int formatVersion;
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
			get { return AudioReaderFactory.CF_LOSSLESS; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		// ********************** Private functions & voids ********************* 

        protected override void resetSpecificData()
		{
            formatVersion = -1;
			FChannels = 0;
			FBits = 0;
			FSampleRate = 0;
		}


		// ********************** Public functions & voids ********************** 

		public TDSF()
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

            if (StreamUtils.StringEqualsArr(DSD_ID,StreamUtils.ReadOneByteChars(source, 4)))
			{
				source.BaseStream.Seek(16, SeekOrigin.Current); // Boring stuff
                long id3v2Offset = source.ReadInt64();

                if (StreamUtils.StringEqualsArr(FMT_ID, StreamUtils.ReadOneByteChars(source, 4)))
                {
                    source.BaseStream.Seek(8, SeekOrigin.Current); // Chunk size

                    formatVersion = source.ReadInt32();

                    if (formatVersion > 1)
                    {
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported (" + FFileName + ")");
                        return result;
                    }

                    source.BaseStream.Seek(4, SeekOrigin.Current); // Format ID
                    source.BaseStream.Seek(4, SeekOrigin.Current); // Channel type

                    FChannels = source.ReadUInt32();
                    FSampleRate = source.ReadUInt32();
                    FBits = source.ReadUInt32();

                    UInt64 sampleCount = source.ReadUInt64();

                    FDuration = (double)sampleCount / FSampleRate;
                    FBitrate = Math.Round(((double)(FFileSize - source.BaseStream.Position)) * 8 / FDuration); // Average bitrate

                    result = true;
                }

                // load tag if exists
                if (id3v2Offset > 0) FID3v2.Read(source, pictureStreamHandler, id3v2Offset);
			}
  
			return result;
		}
	}
}