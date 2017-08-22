using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text;
using static ATL.AudioData.MetaDataIO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Free Lossless Audio Codec files manipulation (extension : .FLAC)
    /// </summary>
	class FLAC : IMetaDataIO, IAudioDataIO
	{

		private static readonly int META_STREAMINFO      = 0;
		private static readonly int META_PADDING         = 1;
		private static readonly int META_APPLICATION     = 2;
		private static readonly int META_SEEKTABLE       = 3;
		private static readonly int META_VORBIS_COMMENT  = 4;
		private static readonly int META_CUESHEET        = 5;
        private static readonly int META_PICTURE         = 6;


		private class TFlacHeader
		{
			public char[] StreamMarker  = new char[4]; //should always be "fLaC"
			public byte[] MetaDataBlockHeader = new byte[4];
			public byte[] Info = new byte[18];
			public byte[] MD5Sum = new byte[16];
    
			public void Reset()
			{
				Array.Clear(StreamMarker,0,4);
				Array.Clear(MetaDataBlockHeader,0,4);
				Array.Clear(Info,0,18);
				Array.Clear(MD5Sum,0,16);
			}
		}

        private readonly string filePath;
        private AudioDataManager.SizeInfo sizeInfo;

        private VorbisTag vorbisTag;


        // Private declarations
        private TFlacHeader header;
		private int paddingIndex;
		private bool paddingLast;
		private int vorbisIndex;
		private int padding;
		private long vCOffset;
		private long audioOffset;
		private byte channels;
		private int sampleRate;
		private byte bitsPerSample;
		private long samples;


		public byte Channels // Number of channels
		{
			get { return channels; }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return sampleRate; }
		}
		public byte BitsPerSample // Bits per sample
		{
			get { return bitsPerSample; }
		}
		public long Samples // Number of samples
		{
			get { return samples; }
		}
		public double Ratio // Compression ratio (%)
		{
			get { return getCompressionRatio(); }
		}
        public bool IsVBR
        {
			get { return false; }
		}
        public String ChannelMode 
		{
			get { return getChannelMode(); }
		}
		public bool Exists 
		{
			get { return vorbisTag.Exists; }
		}
		public long AudioOffset //offset of audio data
		{
			get { return audioOffset; }
		}
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return Math.Round( ((double)(sizeInfo.FileSize - audioOffset)) * 8 / Duration / 1000.0 ); }
        }
        public double Duration
        {
            get { return getDuration(); }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Audio data
			padding = 0;
			paddingLast = false;
			channels = 0;
			sampleRate = 0;
			bitsPerSample = 0;
			samples = 0;
			vorbisIndex = 0;
			paddingIndex = 0;
			vCOffset = 0;
			audioOffset = 0;
		}

        public FLAC(string path)
        {
            filePath = path;
            header = new TFlacHeader();
            resetData();
        }

        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSLESS; }
        }
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }

        #region IMetaDataReader
        public string Title
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Title;
            }
        }

        public string Artist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Artist;
            }
        }

        public string Composer
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Composer;
            }
        }

        public string Comment
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Comment;
            }
        }

        public string Genre
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Genre;
            }
        }

        public ushort Track
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Track;
            }
        }

        public ushort Disc
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Disc;
            }
        }

        public string Year
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Year;
            }
        }

        public string Album
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Album;
            }
        }

        public ushort Rating
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Rating;
            }
        }

        public string Copyright
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Copyright;
            }
        }

        public string OriginalArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalArtist;
            }
        }

        public string OriginalAlbum
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalAlbum;
            }
        }

        public string GeneralDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).GeneralDescription;
            }
        }

        public string Publisher
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Publisher;
            }
        }

        public string AlbumArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AlbumArtist;
            }
        }

        public string Conductor
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Conductor;
            }
        }

        public IList<TagData.PictureInfo> PictureTokens
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PictureTokens;
            }
        }

        public int Size
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Size;
            }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AdditionalFields;
            }
        }
        #endregion

        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE || metaDataType == MetaDataIOFactory.TAG_ID3V2);
        }
        public bool HasNativeMeta()
        {
            return true; // Native is for VorbisTag
        }



        /* -------------------------------------------------------------------------- */
        
        // Check for right FLAC file data
        private bool isValid()
		{
			return ( ( StreamUtils.StringEqualsArr("fLaC",header.StreamMarker) ) &&
				(channels > 0) &&
				(sampleRate > 0) &&
				(bitsPerSample > 0) &&
				(samples > 0) );
		}

        private void readHeader(BinaryReader source)
        {
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // Read header data    
            header.Reset();

            header.StreamMarker = StreamUtils.ReadOneByteChars(source, 4);
            header.MetaDataBlockHeader = source.ReadBytes(4);
            header.Info = source.ReadBytes(18);
            header.MD5Sum = source.ReadBytes(16);
        }

		private double getDuration()
		{
			if ( (isValid()) && (sampleRate > 0) )  
			{
				return (double)samples / sampleRate;
			} 
			else 
			{
				return 0;
			}
		}

		//   Get compression ratio
		private double getCompressionRatio()
		{
			if (isValid()) 
			{
				return (double)sizeInfo.FileSize / (samples * channels * bitsPerSample / 8.0) * 100;
			} 
			else 
			{
				return 0;
			}
		}

		//   Get channel mode
		private String getChannelMode()
		{
			String result;
			if (isValid())
			{
				switch(channels)
				{
					case 1 : result = "Mono"; break;
					case 2 : result = "Stereo"; break;
					default: result = "Multi Channel"; break;
				}
			} 
			else 
			{
				result = "";
			}
			return result;
		}

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        public bool Read(BinaryReader source, ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, ReadTagParams readTagParams)
        {
            bool result = false;

            if (readTagParams.ReadTag && null == vorbisTag) vorbisTag = new VorbisTag();

            Stream fs = source.BaseStream;

			byte[] aMetaDataBlockHeader = new byte[4];
            long position;
            int iBlockLength;
			int iMetaType;
			int iIndex;
			bool bPaddingFound;
  
			bPaddingFound = false;
  
            readHeader(source);

			// Process data if loaded and header valid    
			if ( StreamUtils.StringEqualsArr("fLaC",header.StreamMarker) )
			{
//                FValid = true;
				channels      = (byte)( ((header.Info[12] >> 1) & 0x7) + 1 );
				sampleRate    = ( header.Info[10] << 12 | header.Info[11] << 4 | header.Info[12] >> 4 );
				bitsPerSample = (byte)( ((header.Info[12] & 1) << 4) | (header.Info[13] >> 4) + 1 );
				samples       = ( header.Info[14] << 24 | header.Info[15] << 16 | header.Info[16] << 8 | header.Info[17] );

				if ( 0 == (header.MetaDataBlockHeader[1] & 0x80) ) // metadata block exists
				{
					iIndex = 0;
					do // read more metadata blocks if available
					{
						aMetaDataBlockHeader = source.ReadBytes(4);

						iIndex++; // metadatablock index
						iBlockLength = (aMetaDataBlockHeader[1] << 16 | aMetaDataBlockHeader[2] << 8 | aMetaDataBlockHeader[3]); //decode length
						if (iBlockLength <= 0) break; // can it be 0 ?

						iMetaType = (aMetaDataBlockHeader[0] & 0x7F); // decode metablock type
                        position = fs.Position;

						if ( iMetaType == META_VORBIS_COMMENT ) // Vorbis metadata
						{
							vCOffset = fs.Position;
							vorbisIndex = iIndex;
                            vorbisTag.Read(source, readTagParams);
						}
						else if ((iMetaType == META_PADDING) && (! bPaddingFound) )  // Padding block
						{ 
							padding = iBlockLength;                                            // if we find more skip & put them in metablock array
							paddingLast = ((aMetaDataBlockHeader[0] & 0x80) != 0);
							paddingIndex = iIndex;
							bPaddingFound = true;
							fs.Seek(padding, SeekOrigin.Current); // advance into file till next block or audio data start
						}
                        else if (iMetaType == META_PICTURE)
                        {
                            vorbisTag.ReadPicture(source.BaseStream, readTagParams);
                        }
                        // TODO : APPLICATION and CUESHEET blocks

                        if (iMetaType < 7)
                        {
                            fs.Seek(position + iBlockLength, SeekOrigin.Begin);
                        }
                        else
                        {
                            break;
                        }
					}
					while ( 0 == (aMetaDataBlockHeader[0] & 0x80) ); // while is not last flag ( first bit == 1 )
				}
			}

            if (isValid())
            {
                audioOffset = fs.Position;  // we need that to rebuild the file if nedeed
                result = true;
            }

			return result;  
		}

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return ((IMetaDataIO)vorbisTag).Write(r, w, tag);
        }

        public bool Remove(BinaryWriter w)
        {
            TagData tag = vorbisTag.GetDeletionTagData();

            BinaryReader r = new BinaryReader(w.BaseStream);
            return Write(r, w, tag);
        }
    }
}