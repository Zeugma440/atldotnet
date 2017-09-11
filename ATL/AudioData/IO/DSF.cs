using ATL.Logging;
using System;
using System.IO;
using static ATL.AudioData.AudioDataManager;
using System.Collections.Generic;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for DSD Stream File files manipulation (extension : .DSF)
    /// </summary>
	class DSF : MetaDataIO, IAudioDataIO
    {
        // Headers ID
        public const String DSD_ID = "DSD ";
        public const String FMT_ID = "fmt ";
        public const String DATA_ID = "data";

 
		// Private declarations 
        private int formatVersion;
		private uint channels;
		private uint bits;
		private uint sampleRate;

        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        // Has to be there as a "native" field because DSF forces ID3v2 to be an end-of-file tag, which is not standard
        private ID3v2 id3v2 = new ID3v2();


        // Public declarations 
        public uint Channels
		{
			get { return channels; }
		}
		public uint Bits
		{
			get { return bits; }
		}
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }

        
        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int SampleRate
        {
            get { return (int)sampleRate; }
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
            return true; // For ID3v2 located at the end of file (!)
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }



        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
		{
            formatVersion = -1;
			channels = 0;
			bits = 0;
			sampleRate = 0;
            duration = 0;
            bitrate = 0;
            isValid = false;
		}

		public DSF(string filePath)
		{
            this.filePath = filePath;
            delegatedMeta = id3v2;
            // TODO : delegate tagData, pictureTokens and structureHelper

            resetData();
		}

        
        // ---------- SUPPORT METHODS

        private double getCompressionRatio()
        {
            // Get compression ratio 
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration * sampleRate) * (channels * bits / 8) + 44) * 100;
            else
                return 0;
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        public override bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            resetData();

            source.BaseStream.Seek(0, SeekOrigin.Begin);
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
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
                        return result;
                    }

                    isValid = true;

                    source.BaseStream.Seek(8, SeekOrigin.Current); // Format ID (4), Channel type (4)

                    channels = source.ReadUInt32();
                    sampleRate = source.ReadUInt32();
                    bits = source.ReadUInt32();

                    ulong sampleCount = source.ReadUInt64();

                    duration = (double)sampleCount / sampleRate;
                    bitrate = Math.Round(((double)(sizeInfo.FileSize - source.BaseStream.Position)) * 8 / duration); //time to calculate average bitrate

                    result = true;
                }

                // Load tag if exists
                if (id3v2Offset > 0)
                {
                    id3v2.Read(source, id3v2Offset, readTagParams);
                    // Zone is already added by Id3v2.Read
                    id3v2.structureHelper.AddIndex(20, id3v2Offset);
                    copyFrom(id3v2);
                } else if (readTagParams.PrepareForWriting)
                {
                    // Add EOF zone for future tag writing
                    id3v2.structureHelper.AddZone(source.BaseStream.Length, 0);
                    id3v2.structureHelper.AddIndex(20, source.BaseStream.Length);
                }
            }

            return result;
		}

        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            return id3v2.writeInternal(tag, w, zone);
        }

        protected override void resetSpecificData()
        {
            // Nothing to do at this level
        }
    }
}