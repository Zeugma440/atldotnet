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
	class DSF : IAudioDataIO, IMetaDataIO
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

        private ID3v2 id3v2; // Has to be there as a "native" field because DSF forces ID3v2 to be an end-of-file tag, which is not standard


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
            get { return FGetCompressionRatio(); }
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

        #region IMetaData
        public bool Exists
        {
            get
            {
                return ((IMetaDataIO)id3v2).Exists;
            }
        }

        public string Title
        {
            get
            {
                return ((IMetaDataIO)id3v2).Title;
            }
        }

        public string Artist
        {
            get
            {
                return ((IMetaDataIO)id3v2).Artist;
            }
        }

        public string Composer
        {
            get
            {
                return ((IMetaDataIO)id3v2).Composer;
            }
        }

        public string Comment
        {
            get
            {
                return ((IMetaDataIO)id3v2).Comment;
            }
        }

        public string Genre
        {
            get
            {
                return ((IMetaDataIO)id3v2).Genre;
            }
        }

        public ushort Track
        {
            get
            {
                return ((IMetaDataIO)id3v2).Track;
            }
        }

        public ushort Disc
        {
            get
            {
                return ((IMetaDataIO)id3v2).Disc;
            }
        }

        public string Year
        {
            get
            {
                return ((IMetaDataIO)id3v2).Year;
            }
        }

        public string Album
        {
            get
            {
                return ((IMetaDataIO)id3v2).Album;
            }
        }

        public ushort Rating
        {
            get
            {
                return ((IMetaDataIO)id3v2).Rating;
            }
        }

        public string Copyright
        {
            get
            {
                return ((IMetaDataIO)id3v2).Copyright;
            }
        }

        public string OriginalArtist
        {
            get
            {
                return ((IMetaDataIO)id3v2).OriginalArtist;
            }
        }

        public string OriginalAlbum
        {
            get
            {
                return ((IMetaDataIO)id3v2).OriginalAlbum;
            }
        }

        public string GeneralDescription
        {
            get
            {
                return ((IMetaDataIO)id3v2).GeneralDescription;
            }
        }

        public string Publisher
        {
            get
            {
                return ((IMetaDataIO)id3v2).Publisher;
            }
        }

        public string AlbumArtist
        {
            get
            {
                return ((IMetaDataIO)id3v2).AlbumArtist;
            }
        }

        public string Conductor
        {
            get
            {
                return ((IMetaDataIO)id3v2).Conductor;
            }
        }

        public IList<TagData.PictureInfo> PictureTokens
        {
            get
            {
                return ((IMetaDataIO)id3v2).PictureTokens;
            }
        }

        public int Size
        {
            get
            {
                return ((IMetaDataIO)id3v2).Size;
            }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return ((IMetaDataIO)id3v2).AdditionalFields;
            }
        }
#endregion

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
		}

		public DSF(string filePath)
		{
            this.filePath = filePath;
			resetData();
		}

        
        // ---------- SUPPORT METHODS

        private double FGetCompressionRatio()
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

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
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
                        LogDelegator.GetLogDelegate()(Log.LV_ERROR, "DSF format version " + formatVersion + " not supported");
                        return result;
                    }

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
                    id3v2 = new ID3v2();
                    id3v2.Read(source, id3v2Offset, readTagParams);
                }
            }

            return result;
		}

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            TODO write tag at the END of the file and update tag offset within header
            return ((IMetaDataIO)id3v2).Write(r, w, tag);
        }

        public bool Remove(BinaryWriter w)
        {
            return ((IMetaDataIO)id3v2).Remove(w);
        }
    }
}