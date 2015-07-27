using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for OGG files manipulation. Present implementation covers :
    ///   - Vorbis data (extensions : .OGG)
    ///   - Opus data (extensions : .OPUS)
    /// </summary>
	class TOgg : AudioDataReader, IMetaDataReader
	{
        // Contents of the file
        private const int CONTENTS_UNSUPPORTED = -1;	    // Unsupported
        private const int CONTENTS_VORBIS = 0;				// Vorbis
        private const int CONTENTS_OPUS = 1;				// Opus

		// Used with ChannelModeID property
		private const byte VORBIS_CM_MONO = 1;				// Code for mono mode		
		private const byte VORBIS_CM_STEREO = 2;			// Code for stereo mode
		private const byte VORBIS_CM_MULTICHANNEL = 6;		// Code for Multichannel Mode

        private const String PICTURE_METADATA_ID_NEW = "METADATA_BLOCK_PICTURE";
        private const String PICTURE_METADATA_ID_OLD = "COVERART";

		// Channel mode names
		private static String[] VORBIS_MODE = new String[4]
		{"Unknown", "Mono", "Stereo", "Multichannel"};


        private FileInfo Info = new FileInfo();

        private int contents = -1;
        
        private byte FChannelModeID;
		private int FSampleRate;
		private ushort FBitRateNominal;
		private int FSamples;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private ushort FTrack;
        private ushort FDisc;
        private ushort FRating;
		private String FDate;
		private String FGenre;
		private String FComment;
		private String FVendor;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
        private StreamUtils.StreamHandlerDelegate FPictureStreamHandler;
      
		public byte ChannelModeID // Channel mode code
		{ 
			get { return this.FChannelModeID; }
		}	
		public String ChannelMode // Channel mode name
		{
			get { return this.FGetChannelMode(); }
		}	
		public int SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
		}
		public ushort BitRateNominal // Nominal bit rate
		{
			get { return this.FBitRateNominal; }
		}

        public override bool IsVBR
		{
			get { return true; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		public String Title // Song title
		{
			get { return this.FTitle; }
			set { this.FTitle = value; }
		}
        public String Artist // Artist name
        {
            get { return this.FArtist; }
            set { this.FArtist = value; }
        }
        public String Composer // Composer name
        {
            get { return this.FComposer; }
            set { this.FComposer = value; }
        }
		public String Album // Album name
		{
			get { return this.FAlbum; }
			set { this.FAlbum = value; }
		}			  
		public ushort Track // Track number
		{
			get { return this.FTrack; }
			set { this.FTrack = value; }
		}
        public ushort Disc // Disc number
        {
            get { return this.FDisc; }
            set { this.FDisc = value; }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
            set { this.FRating = value; }
        }	
		public String Year // Year
		{
			get { return this.FDate; }
			set { this.FDate = value; }
		}			  
		public String Genre // Genre name
		{
			get { return this.FGenre; }
			set { this.FGenre = value; }
		}			  
		public String Comment // Comment
		{
			get { return this.FComment; }
			set { this.FComment = value; }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }


		public String Vendor // Vendor string
		{
			get { return this.FVendor; }
		}	
		public bool Exists // True if ID3v2 tag exists
		{
            get { return FID3v2.Exists; }
		}	
		public bool Valid // True if file valid
		{
			get { return this.FIsValid(); }
		}	
  
		// Ogg page header ID
		private const String OGG_PAGE_ID = "OggS";

		// Vorbis parameter frame ID
		private String VORBIS_HEADER_ID = (char)1 + "vorbis";

		// Vorbis tag frame ID
		private String VORBIS_TAG_ID = (char)3 + "vorbis";

        // Vorbis parameter frame ID
        private String OPUS_HEADER_ID = "OpusHead";

        // Opus tag frame ID
        private String OPUS_TAG_ID = "OpusTags";

		// Max. number of supported comment fields
		private const sbyte VORBIS_FIELD_COUNT = 12;

		// Names of supported comment fields
		private String[] VORBIS_FIELD = new String[VORBIS_FIELD_COUNT] 
			{
				"TITLE", "ARTIST", "ALBUM", "TRACKNUMBER", "DATE", "GENRE", "COMMENT",
				"PERFORMER", "DESCRIPTION", "DISCNUMBER", "COMPOSER", "RATING" };

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
                //Array.Clear(ChannelMapping, 0, ChannelMapping.Length);
            }
        }

		// Vorbis tag data (NB : structure is identical between Vorbis and Opus)
		private class VorbisTag
		{
            public String ID;
			public int Fields;                                 // Number of tag fields
            public long size;                                  // Size of Vorbis Tag
			public String[] FieldData = new String [VORBIS_FIELD_COUNT+1]; // Tag field data

			public void Reset()
			{
                ID = "";
				Fields = 0;
				// Don't fill this with null, rather with "" :)
				for (int i=0; i<VORBIS_FIELD_COUNT+1; i++)
				{
					FieldData[i] = "";
				}
			}
		}

		// File data
		private class FileInfo
		{
			public OggHeader FPage = new OggHeader();
			public OggHeader SPage = new OggHeader();
			public OggHeader LPage = new OggHeader();   // First, second and last page
			public VorbisHeader VorbisParameters = new VorbisHeader(); // Vorbis parameter header
            public OpusHeader OpusParameters = new OpusHeader(); // Opus parameter header
			public VorbisTag Tag = new VorbisTag();                 // Vorbis tag data
			public int Samples;                             // Total number of samples
			public int SPagePos;                        // Position of second Ogg page
			public int TagEndPos;                                  // Tag end position

			public void Reset()
			{
				FPage.Reset();
				SPage.Reset();
				LPage.Reset();
				VorbisParameters.Reset();
                OpusParameters.Reset();
				Tag.Reset();
				Samples = 0;
				SPagePos = 0;
				TagEndPos = 0;
			}
		}

		// ---------------------------------------------------------------------------

        // Reads large data chunks by streaming
        private void SetExtendedTagItem(Stream Source, int size)
        {
            String tagId = "";
            int IdSize = 1;
            char c = StreamUtils.ReadOneByteChar(Source);
            
            while (c != '=')
            {
                tagId += c;
                IdSize++;
                c = StreamUtils.ReadOneByteChar(Source);
            }

            if (tagId.Equals(PICTURE_METADATA_ID_NEW))
            {
                if (FPictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_NEW.Length;
                    // Make sure total size is a multiple of 4
                    size = size - (size % 4);

                    // Read the whole base64-encoded picture header _and_ binary data
                    MemoryStream mem = new MemoryStream(size);
                        StreamUtils.CopyMemoryStreamFrom(mem, Source, size);
                        byte[] encodedData = mem.GetBuffer();
                    mem.Close();

                    // Gets rid of unwanted zeroes
                    for (int i=0;i<encodedData.Length;i++) if (0 == encodedData[i]) encodedData[i] = 61;

                    mem = new MemoryStream();
                        StreamUtils.DecodeFrom64(encodedData, mem);
                        mem.Seek(0, SeekOrigin.Begin);
                        TFLACFile.FlacMetaDataBlockPicture block = TFLACFile.ReadMetadataBlockPicture(mem);
                        FPictures.Add(block.picCode);

                        MemoryStream picMem = new MemoryStream(block.picDataLength);
                            StreamUtils.CopyMemoryStreamFrom(picMem, mem, block.picDataLength);
                            //for (int i = 0; i < block.picDataLength; i++) picMem.WriteByte((byte)mem.ReadByte());
                            FPictureStreamHandler(ref picMem);
                        picMem.Close();
                    mem.Close();
                }
                else
                {
                    Pictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                }
            }
            else if (tagId.Equals(PICTURE_METADATA_ID_OLD)) // Deprecated picture info
            {
                Pictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                if (FPictureStreamHandler != null)
                {
                    size = size - 1 - PICTURE_METADATA_ID_OLD.Length;
                    // Make sure total size is a multiple of 4
                    size = size - (size % 4);

                    // Read the whole base64-encoded picture binary data
                    MemoryStream mem = new MemoryStream(size);
                        StreamUtils.CopyMemoryStreamFrom(mem, Source, size);
                        byte[] encodedData = mem.GetBuffer();
                    mem.Close();

                    mem = new MemoryStream();
                        StreamUtils.DecodeFrom64(encodedData, mem);
                        FPictureStreamHandler(ref mem);
                    mem.Close();
                }
            }
        }

		private void SetTagItem(String Data, ref FileInfo Info)
		{
			int Separator;	
			String FieldID;
			String FieldData;

			// Set Vorbis tag item if supported comment field found
			Separator = Data.IndexOf("=");
			if (Separator > 0)
			{
				FieldID = Data.Substring(0,Separator).ToUpper();		
				FieldData = Data.Substring(Separator + 1, Data.Length - FieldID.Length - 1);
				for (int index=0; index < VORBIS_FIELD_COUNT; index++)
					if (VORBIS_FIELD[index] == FieldID)
						Info.Tag.FieldData[index+1] = FieldData.Trim();
			}
			else
				if ("" == Info.Tag.FieldData[0]) Info.Tag.FieldData[0] = Data;		
		}

		// ---------------------------------------------------------------------------

		private void ReadTag(BinaryReader Source, ref FileInfo info)
		{
			int Index;
			int Size;
            long initialPos;
			long Position;
            String strData;
            bool isValidTagHeader = false;

			// Read Vorbis tag
			Index = 0;
            initialPos = Source.BaseStream.Position;

            if (contents.Equals(CONTENTS_VORBIS))
            {
                info.Tag.ID = new String(Source.ReadChars(7));
                isValidTagHeader = (VORBIS_TAG_ID.Equals(info.Tag.ID));
            } else if (contents.Equals(CONTENTS_OPUS))
            {
                info.Tag.ID = new String(Source.ReadChars(8));
                isValidTagHeader = (OPUS_TAG_ID.Equals(info.Tag.ID));
            }

            if (isValidTagHeader)
            {
			    do
			    {
                    Size = Source.ReadInt32();
                    info.Tag.size += Size;

                    Position = Source.BaseStream.Position;
                    if (Size < 250)
                    {
                        strData = Encoding.UTF8.GetString(Source.ReadBytes(Size));
                        // Set Vorbis tag item
                        SetTagItem(strData.Trim(), ref info);
                    }
                    else
                    {
                        SetExtendedTagItem(Source.BaseStream, Size);
                    }
                    Source.BaseStream.Seek(Position + Size, SeekOrigin.Begin);
                    if (0 == Index) info.Tag.Fields = Source.ReadInt32();
                    
                    Index++;
                } while (Index <= info.Tag.Fields);
			}
            info.TagEndPos = (int)Source.BaseStream.Position;
            info.Tag.size = info.TagEndPos - initialPos;
		}

		// ---------------------------------------------------------------------------

		private long GetSamples(BinaryReader Source)
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

		private bool GetInfo(BinaryReader source, ref FileInfo info)
		{
            Stream fs = source.BaseStream;

			// Get info from file
			bool result = false;
            bool isValidHeader = false;
            FID3v2.Read(source, FPictureStreamHandler);

            // Check for ID3v2
			source.BaseStream.Seek(FID3v2.Size, SeekOrigin.Begin);    

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
				source.BaseStream.Seek(FID3v2.Size + info.FPage.Segments + 27, SeekOrigin.Begin);

				// Read Vorbis or Opus stream info
                long position = source.BaseStream.Position;

                String headerStart = new String(source.ReadChars(3));
                source.BaseStream.Seek(position, SeekOrigin.Begin);
                if (VORBIS_HEADER_ID.StartsWith(headerStart))
                {
                    contents = CONTENTS_VORBIS;
                    info.VorbisParameters.ID = new String(source.ReadChars(7));
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
                    info.OpusParameters.ID = new String(source.ReadChars(8));
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
                    bool loop = true;
                    bool first = true;
                    MemoryStream s = new MemoryStream();
                    BinaryReader memSource = new BinaryReader(s);
                        while(loop) {
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
                        s.Seek(0, SeekOrigin.Begin);

						// Read Vorbis tag
                        ReadTag(memSource, ref info);
                    memSource.Close();
		    
					// Get total number of samples
					info.Samples = (int)GetSamples(source);

					result = true;
				}
			}

			return result;
		}

		// ********************** Private functions & voids *********************

		protected override void resetSpecificData()
		{
			// Reset variables
			FChannelModeID = 0;
			FSampleRate = 0;
			FBitRateNominal = 0;
			FSamples = 0;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrack = 0;
            FDisc = 0;
            FRating = 0;
			FDate = "";
			FGenre = "";
			FComment = "";
			FVendor = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

		private String FGetChannelMode()
		{
			String result;
			// Get channel mode name
			if (FChannelModeID > 2) result = VORBIS_MODE[3]; 
			else
				result = VORBIS_MODE[FChannelModeID];

			return VORBIS_MODE[FChannelModeID];
		}

		// ---------------------------------------------------------------------------

        // Calculate duration time
		private double FGetDuration()
		{
            double result;

                if (FSamples > 0)
                    if (FSampleRate > 0)
                        result = ((double)FSamples / FSampleRate);
                    else
                        result = 0;
                else
                    if ((FBitRateNominal > 0) && (FChannelModeID > 0))
                        result = ((double)FFileSize - FID3v2.Size) /
                            (double)FBitRateNominal / FChannelModeID / 125 * 2;
                    else
                        result = 0;
		
			return result;
		}

		// ---------------------------------------------------------------------------

		private double FGetBitRate()
		{
			// Calculate average bit rate
			double result = 0;

			if (FGetDuration() > 0)
                result = (FFileSize - FID3v2.Size - Info.Tag.size )*8 / FGetDuration();
	
			return result;
		}

		// ---------------------------------------------------------------------------

		private bool FIsValid()
		{
			// Check for file correctness
			return ( ( ((VORBIS_CM_MONO <= FChannelModeID) && (FChannelModeID <= VORBIS_CM_STEREO)) || (VORBIS_CM_MULTICHANNEL == FChannelModeID) ) &&
				(FSampleRate > 0) && (FGetDuration() > 0.1) && (FGetBitRate() > 0) );
		}

		// ********************** Public functions & voids **********************

		public TOgg()
		{
			// Object constructor
			resetData();  
		}

		// ---------------------------------------------------------------------------

		// No explicit destructors with C#

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
            FPictureStreamHandler = pictureStreamHandler;
			bool result = false;

			Info.Reset();

			if ( GetInfo(source, ref Info) )
			{
                FValid = true;
				
                // Fill variables
                if (contents.Equals(CONTENTS_VORBIS))
                {
                    FChannelModeID = Info.VorbisParameters.ChannelMode;
                    FSampleRate = Info.VorbisParameters.SampleRate;
                    FBitRateNominal = (ushort)(Info.VorbisParameters.BitRateNominal / 1000); // Integer division
                }
                else if (contents.Equals(CONTENTS_OPUS))
                {
                    FChannelModeID = Info.OpusParameters.OutputChannelCount;
                    FSampleRate = (int)Info.OpusParameters.InputSampleRate;
                }
				
                FSamples = Info.Samples;
                FDuration = FGetDuration();
                FBitrate = FGetBitRate();

				FTitle = Info.Tag.FieldData[1];
				if (Info.Tag.FieldData[2] != "") FArtist = Info.Tag.FieldData[2];
				else FArtist = Info.Tag.FieldData[8];
				FAlbum = Info.Tag.FieldData[3];
				FTrack = TrackUtils.ExtractTrackNumber(Info.Tag.FieldData[4]);
				FDate = Info.Tag.FieldData[5];
				FGenre = Info.Tag.FieldData[6];
				if (Info.Tag.FieldData[7] != "") FComment = Info.Tag.FieldData[7];
				else FComment = Info.Tag.FieldData[9];
                FDisc = TrackUtils.ExtractTrackNumber(Info.Tag.FieldData[10]);
                FComposer = Info.Tag.FieldData[11];
                FRating = TrackUtils.ExtractIntRating( Info.Tag.FieldData[12] );
				FVendor = Info.Tag.FieldData[0];
				result = true;
			}
			return result;
		}

	}
}