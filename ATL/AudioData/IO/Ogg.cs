using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for OGG files manipulation. Current implementation covers :
    ///   - Vorbis data (extensions : .OGG)
    ///   - Opus data (extensions : .OPUS)
    /// </summary>
	class Ogg : IAudioDataIO, IMetaDataIO
	{
        // Contents of the file
        private const int CONTENTS_UNSUPPORTED = -1;	    // Unsupported
        private const int CONTENTS_VORBIS = 0;				// Vorbis
        private const int CONTENTS_OPUS = 1;				// Opus

		// Used with ChannelModeID property
		private const byte VORBIS_CM_MONO = 1;				// Code for mono mode		
		private const byte VORBIS_CM_STEREO = 2;			// Code for stereo mode
		private const byte VORBIS_CM_MULTICHANNEL = 6;		// Code for Multichannel Mode


		// Channel mode names
		private static readonly String[] VORBIS_MODE = new String[4] {"Unknown", "Mono", "Stereo", "Multichannel"};

        private readonly string filePath;
        private VorbisTag vorbisTag;


        private FileInfo info = new FileInfo();

        private int contents;
        
        private byte channelModeID;
		private int sampleRate;
		private ushort bitRateNominal;
		private int samples;

        private AudioDataManager.SizeInfo sizeInfo;
        


        public byte ChannelModeID // Channel mode code
		{ 
			get { return this.channelModeID; }
		}	
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}	
		public int SampleRate // Sample rate (hz)
		{
			get { return this.sampleRate; }
		}
		public ushort BitRateNominal // Nominal bit rate
		{
			get { return this.bitRateNominal; }
		}
		public bool Valid // True if file valid
		{
			get { return this.FIsValid(); }
		}
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return getBitRate(); }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public bool IsVBR
        {
            get { return true; }
        }


        // Ogg page header ID
        private const String OGG_PAGE_ID = "OggS";

		// Vorbis parameter frame ID
		private readonly String VORBIS_HEADER_ID = (char)1 + "vorbis";

		// Vorbis tag frame ID
		private readonly String VORBIS_TAG_ID = (char)3 + "vorbis";

        // Vorbis parameter frame ID
        private const String OPUS_HEADER_ID = "OpusHead";

        // Opus tag frame ID
        private const String OPUS_TAG_ID = "OpusTags";


		// Ogg page header
		private class OggHeader 
		{
			public char[] ID = new char[4];                           // Always "OggS"
			public byte StreamVersion;                     // Stream structure version
			public byte TypeFlag;                                  // Header type flag
			public long AbsolutePosition;                 // Absolute granule position
			public int Serial;                                 // Stream serial number
			public int PageNumber;                             // Page sequence number
			public int Checksum;                                      // Page checksum
			public byte Segments;                           // Number of page segments
			public byte[] LacingValues = new byte[0xFF]; // Lacing values - segment sizes

			public void Reset()
			{
				Array.Clear(ID,0,ID.Length);
				StreamVersion = 0;
				TypeFlag = 0;
				AbsolutePosition = 0;
				Serial = 0;
				PageNumber = 0;
				Checksum = 0;
				Segments = 0;
				Array.Clear(LacingValues,0,LacingValues.Length);
			}

            public int GetPageLength()
            {
                int length = 0;
                for (int i = 0; i < Segments; i++)
                {
                    length += LacingValues[i];
                }
                return length;
            }
		}

		// Vorbis parameter header
		private class VorbisHeader
		{
            public String ID;
			public byte[] BitstreamVersion = new byte[4];  // Bitstream version number
			public byte ChannelMode;                             // Number of channels
			public int SampleRate;                                 // Sample rate (hz)
			public int BitRateMaximal;                         // Bit rate upper limit
			public int BitRateNominal;                             // Nominal bit rate
			public int BitRateMinimal;                         // Bit rate lower limit
			public byte BlockSize;             // Coded size for small and long blocks
			public byte StopFlag;                                          // Always 1

			public void Reset()
			{
                ID = "";
				Array.Clear(BitstreamVersion,0,BitstreamVersion.Length);
				ChannelMode = 0;
				SampleRate = 0;
				BitRateMaximal = 0;
				BitRateNominal = 0;
				BitRateMinimal = 0;
				BlockSize = 0;
				StopFlag = 0;
			}
		}

        // Opus parameter header
        private class OpusHeader
        {
            public String ID;
            public byte Version;
            public byte OutputChannelCount;
            public UInt16 PreSkip;
            public UInt32 InputSampleRate;
            public Int16 OutputGain;
            public byte ChannelMappingFamily;
            
            public byte StreamCount;
            public byte CoupledStreamCount;
            public byte[] ChannelMapping;

            public void Reset()
            {
                ID = "";
                Version = 0;
                OutputChannelCount = 0;
                PreSkip = 0;
                InputSampleRate = 0;
                OutputGain = 0;
                ChannelMappingFamily = 0;
                StreamCount = 0;
                CoupledStreamCount = 0;
            }
        }

		// File data
		private class FileInfo
		{
			public OggHeader FPage = new OggHeader();
			public OggHeader SPage = new OggHeader();
			public OggHeader LPage = new OggHeader();               // First, second and last page
			public VorbisHeader VorbisParameters = new VorbisHeader();  // Vorbis parameter header
            public OpusHeader OpusParameters = new OpusHeader();          // Opus parameter header
			public int Samples;                                         // Total number of samples
			public int SPagePos;                                    // Position of second Ogg page

			public void Reset()
			{
				FPage.Reset();
				SPage.Reset();
				LPage.Reset();
				VorbisParameters.Reset();
                OpusParameters.Reset();
				Samples = 0;
				SPagePos = 0;
			}
		}

        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Reset variables
            channelModeID = 0;
            sampleRate = 0;
            bitRateNominal = 0;
            samples = 0;
            contents = -1;

            info.Reset();
        }

        public Ogg(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }

        public bool Exists
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Exists;
            }
        }

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

        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE); // TODO - check why not ID3v2 since its presence is expected by the parser
        }
        public bool HasNativeMeta()
        {
            return true; // Native is for VorbisTag
        }


        // ---------------------------------------------------------------------------

        private long GetSamples(ref BinaryReader Source)
		{  
			int DataIndex;	
			// Using byte instead of char here to avoid mistaking range of bytes for unicode chars
			byte[] Data = new byte[251];
			OggHeader Header = new OggHeader();

			// Get total number of samples
			int result = 0;

			for (int index=1; index<=50; index++)
			{
				DataIndex = (int)(Source.BaseStream.Length - (/*Data.Length*/251 - 10) * index - 10);
				Source.BaseStream.Seek(DataIndex, SeekOrigin.Begin);
				Data = Source.ReadBytes(251);

				// Get number of PCM samples from last Ogg packet header
				for (int iterator=251 - 10; iterator>=0; iterator--)
				{
					char[] tempArray = new char[4] { (char)Data[iterator],
													   (char)Data[iterator + 1],
													   (char)Data[iterator + 2],
													   (char)Data[iterator + 3] };
					if ( StreamUtils.StringEqualsArr(OGG_PAGE_ID,tempArray) ) 
					{
						Source.BaseStream.Seek(DataIndex + iterator, SeekOrigin.Begin);
        
						Header.ID = Source.ReadChars(4);
						Header.StreamVersion = Source.ReadByte();
						Header.TypeFlag = Source.ReadByte();
						Header.AbsolutePosition = Source.ReadInt64();
						Header.Serial = Source.ReadInt32();
						Header.PageNumber = Source.ReadInt32();
						Header.Checksum = Source.ReadInt32();
						Header.Segments = Source.ReadByte();
						Header.LacingValues = Source.ReadBytes(0xFF);
						return Header.AbsolutePosition;
					}
				}
			}
			return result;
		}

		// ---------------------------------------------------------------------------

		private bool GetInfo(ref BinaryReader source, ref FileInfo info, MetaDataIO.ReadTagParams readTagParams)
		{
            Stream fs = source.BaseStream;

			// Get info from file
			bool result = false;
            bool isValidHeader = false;

            // Check for ID3v2
			source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);    

            // Read global file header
			info.FPage.ID = source.ReadChars(4);
			info.FPage.StreamVersion = source.ReadByte();
			info.FPage.TypeFlag = source.ReadByte();
			info.FPage.AbsolutePosition = source.ReadInt64();
			info.FPage.Serial = source.ReadInt32();
			info.FPage.PageNumber = source.ReadInt32();
			info.FPage.Checksum = source.ReadInt32();
			info.FPage.Segments = source.ReadByte();
			info.FPage.LacingValues = source.ReadBytes(0xFF);

			if ( StreamUtils.StringEqualsArr(OGG_PAGE_ID,info.FPage.ID) )
			{
				source.BaseStream.Seek(sizeInfo.ID3v2Size + info.FPage.Segments + 27, SeekOrigin.Begin);

				// Read Vorbis or Opus stream info
                long position = source.BaseStream.Position;

                String headerStart = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
                source.BaseStream.Seek(position, SeekOrigin.Begin);
                if (VORBIS_HEADER_ID.StartsWith(headerStart))
                {
                    contents = CONTENTS_VORBIS;
                    info.VorbisParameters.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(7));
                    isValidHeader = VORBIS_HEADER_ID.Equals(info.VorbisParameters.ID);

                    info.VorbisParameters.BitstreamVersion = source.ReadBytes(4);
                    info.VorbisParameters.ChannelMode = source.ReadByte();
                    info.VorbisParameters.SampleRate = source.ReadInt32();
                    info.VorbisParameters.BitRateMaximal = source.ReadInt32();
                    info.VorbisParameters.BitRateNominal = source.ReadInt32();
                    info.VorbisParameters.BitRateMinimal = source.ReadInt32();
                    info.VorbisParameters.BlockSize = source.ReadByte();
                    info.VorbisParameters.StopFlag = source.ReadByte();
                }
                else if (OPUS_HEADER_ID.StartsWith(headerStart))
                {
                    contents = CONTENTS_OPUS;
                    info.OpusParameters.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(8));
                    isValidHeader = OPUS_HEADER_ID.Equals(info.OpusParameters.ID);

                    info.OpusParameters.Version = source.ReadByte();
                    info.OpusParameters.OutputChannelCount = source.ReadByte();
                    info.OpusParameters.PreSkip = source.ReadUInt16();
                    //info.OpusParameters.InputSampleRate = source.ReadUInt32();
                    info.OpusParameters.InputSampleRate = 48000; // Actual sample rate is hardware-dependent. Let's assume for now that the hardware ATL runs on supports 48KHz
                    source.BaseStream.Seek(4, SeekOrigin.Current);
                    info.OpusParameters.OutputGain = source.ReadInt16();

                    info.OpusParameters.ChannelMappingFamily = source.ReadByte();

                    if (info.OpusParameters.ChannelMappingFamily > 0)
                    {
                        info.OpusParameters.StreamCount = source.ReadByte();
                        info.OpusParameters.CoupledStreamCount = source.ReadByte();

                        info.OpusParameters.ChannelMapping = new byte[info.OpusParameters.OutputChannelCount];
                        for (int i = 0; i < info.OpusParameters.OutputChannelCount; i++)
                        {
                            info.OpusParameters.ChannelMapping[i] = source.ReadByte();
                        }
                    }
                }

				if ( isValidHeader ) 
				{
                    // Reads all related Vorbis pages that describe file header
                    // and concatenate their content into a single, continuous data stream
                    bool loop = true;
                    bool first = true;
                    using (MemoryStream s = new MemoryStream())
                    using (BinaryReader msr = new BinaryReader(s))
                    {
                        while (loop)
                        {
                            info.SPagePos = (int)fs.Position;

                            info.SPage.ID = source.ReadChars(4);
                            info.SPage.StreamVersion = source.ReadByte();
                            info.SPage.TypeFlag = source.ReadByte();
                            // 0 marks a new page
                            if (0 == info.SPage.TypeFlag)
                            {
                                loop = first;
                            }
                            if (loop)
                            {
                                info.SPage.AbsolutePosition = source.ReadInt64();
                                info.SPage.Serial = source.ReadInt32();
                                info.SPage.PageNumber = source.ReadInt32();
                                info.SPage.Checksum = source.ReadInt32();
                                info.SPage.Segments = source.ReadByte();
                                info.SPage.LacingValues = source.ReadBytes(info.SPage.Segments);
                                s.Write(source.ReadBytes(info.SPage.GetPageLength()), 0, info.SPage.GetPageLength());
                            }
                            first = false;
                        }

                        // Get total number of samples -- unused so far
                        info.Samples = (int)GetSamples(ref source);

                        if (readTagParams.ReadTag)
                        {
                            s.Seek(0, SeekOrigin.Begin);

                            string tagId;
                            bool isValidTagHeader = false;
                            if (contents.Equals(CONTENTS_VORBIS))
                            {
                                tagId = Utils.Latin1Encoding.GetString(msr.ReadBytes(7));
                                isValidTagHeader = (VORBIS_TAG_ID.Equals(tagId));
                            }
                            else if (contents.Equals(CONTENTS_OPUS))
                            {
                                tagId = Utils.Latin1Encoding.GetString(msr.ReadBytes(8));
                                isValidTagHeader = (OPUS_TAG_ID.Equals(tagId));
                            }

                            if (isValidTagHeader) vorbisTag.Read(msr, readTagParams);
                        }
                    }
		    
					result = true;
				}
			}

			return result;
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			String result;
			// Get channel mode name
			if (channelModeID > 2) result = VORBIS_MODE[3]; 
			else
				result = VORBIS_MODE[channelModeID];

			return VORBIS_MODE[channelModeID];
		}

		// ---------------------------------------------------------------------------

        // Calculate duration time
		private double getDuration()
		{
            double result;

                if (samples > 0)
                    if (sampleRate > 0)
                        result = ((double)samples / sampleRate);
                    else
                        result = 0;
                else
                    if ((bitRateNominal > 0) && (channelModeID > 0))
                        result = ((double)sizeInfo.FileSize - sizeInfo.ID3v2Size) /
                            (double)bitRateNominal / channelModeID / 125.0 * 2;
                    else
                        result = 0;
		
			return result;
		}

		// ---------------------------------------------------------------------------

		private double getBitRate()
		{
			// Calculate average bit rate
			double result = 0;

			if (getDuration() > 0) result = (sizeInfo.FileSize - sizeInfo.TotalTagSize)*8.0 / getDuration() / 1000.0;
	
			return result;
		}

		// ---------------------------------------------------------------------------

		private bool FIsValid()
		{
			// Check for file correctness
			return ( ( ((VORBIS_CM_MONO <= channelModeID) && (channelModeID <= VORBIS_CM_STEREO)) || (VORBIS_CM_MULTICHANNEL == channelModeID) ) &&
				(sampleRate > 0) && (getDuration() > 0.1) && (getBitRate() > 0) );
		}

        // ---------------------------------------------------------------------------

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            this.sizeInfo = sizeInfo;

            if (readTagParams.ReadTag && null == vorbisTag) vorbisTag = new VorbisTag();
            info.Reset();
            vorbisTag.ResetData();

            if ( GetInfo(ref source, ref info, readTagParams) )
			{
                //                FValid = true;
				
                // Fill variables
                if (contents.Equals(CONTENTS_VORBIS))
                {
                    channelModeID = info.VorbisParameters.ChannelMode;
                    sampleRate = info.VorbisParameters.SampleRate;
                    bitRateNominal = (ushort)(info.VorbisParameters.BitRateNominal / 1000); // Integer division
                }
                else if (contents.Equals(CONTENTS_OPUS))
                {
                    channelModeID = info.OpusParameters.OutputChannelCount;
                    sampleRate = (int)info.OpusParameters.InputSampleRate;
                }
				
                samples = info.Samples;

				result = true;
			}
			return result;
		}

        public bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return (vorbisTag).Read(source, readTagParams);
        }

        public bool Write(BinaryReader r, BinaryWriter w, TagData tag)
        {
            return (vorbisTag).Write(r, w, tag);
        }

        public bool Remove(BinaryWriter w)
        {
            return (vorbisTag).Remove(w);
        }
    }
}