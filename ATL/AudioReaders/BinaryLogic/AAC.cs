using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Advanced Audio Coding files manipulation (extensions : .AAC, .MP4, .M4A)
    /// </summary>
	class TAAC : AudioDataReader, IMetaDataReader
	{

		// Header type codes

		public const byte AAC_HEADER_TYPE_UNKNOWN = 0;                       // Unknown
		public const byte AAC_HEADER_TYPE_ADIF = 1;                          // ADIF
		public const byte AAC_HEADER_TYPE_ADTS = 2;                          // ADTS
        public const byte AAC_HEADER_TYPE_MP4 = 3;                          // MP4

		// Header type names
		public static String[] AAC_HEADER_TYPE = { "Unknown", "ADIF", "ADTS" };

		// MPEG version codes
		public const byte AAC_MPEG_VERSION_UNKNOWN = 0;                      // Unknown
		public const byte AAC_MPEG_VERSION_2 = 1;                            // MPEG-2
		public const byte AAC_MPEG_VERSION_4 = 2;                            // MPEG-4

		// MPEG version names
		public static String[] AAC_MPEG_VERSION = { "Unknown", "MPEG-2", "MPEG-4" };

		// Profile codes
		public const byte AAC_PROFILE_UNKNOWN = 0;                           // Unknown
		public const byte AAC_PROFILE_MAIN = 1;                              // Main
		public const byte AAC_PROFILE_LC = 2;                                // LC
		public const byte AAC_PROFILE_SSR = 3;                               // SSR
		public const byte AAC_PROFILE_LTP = 4;                               // LTP

		// Profile names
		public static String[] AAC_PROFILE =  
		{ "Unknown", "AAC Main", "AAC LC", "AAC SSR", "AAC LTP" };

		// Bit rate type codes
		public const byte AAC_BITRATE_TYPE_UNKNOWN = 0;                      // Unknown
		public const byte AAC_BITRATE_TYPE_CBR = 1;                          // CBR
		public const byte AAC_BITRATE_TYPE_VBR = 2;                          // VBR

		// Bit rate type names
		public static String[] AAC_BITRATE_TYPE = { "Unknown", "CBR", "VBR" };
    
		private int FTotalFrames;
		private byte FHeaderTypeID;
		private byte FMPEGVersionID;
		private byte FProfileID;
		private byte FChannels;
		private int FSampleRate;
        private long FSampleSize;
		private byte FBitrateTypeID;

        private bool FExists;
        private byte FVersionID;
        private long FSize;
        private String FTitle;
        private String FArtist;
        private String FComposer;
        private String FAlbum;
        private ushort FTrack;
        private ushort FDisc;
        private ushort FRating;
        private String FTrackString;
        private String FDiscString;
        private String FYear;
        private String FGenre;
        private String FComment;


        private IList<MetaReaderFactory.PIC_CODE> FPictures;


        public bool Exists // True if tag found
        {
            get { return this.FExists; }
        }
        public byte VersionID // Version code
        {
            get { return this.FVersionID; }
        }
        public long Size // Total tag size
        {
            get { return this.FSize; }
        }
        public String Title // Song title
        {
            get { return this.FTitle; }
        }
        public String Artist // Artist name
        {
            get { return this.FArtist; }
        }
        public String Composer // Composer name
        {
            get { return this.FComposer; }
        }
        public String Album // Album title
        {
            get { return this.FAlbum; }
        }
        public ushort Track // Track number 
        {
            get { return this.FTrack; }
        }
        public String TrackString // Track number (string)
        {
            get { return this.FTrackString; }
        }
        public ushort Disc // Disc number 
        {
            get { return this.FDisc; }
        }
        public String DiscString // Disc number (string)
        {
            get { return this.FDiscString; }
        }
        public ushort Rating // Rating
        {
            get { return this.FRating; }
        }
        public String Year // Release year
        {
            get { return this.FYear; }
        }
        public String Genre // Genre name
        {
            get { return this.FGenre; }
        }
        public String Comment // Comment
        {
            get { return this.FComment; }
        }
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Tags indicating the presence of embedded pictures
        {
            get { return this.FPictures; }
        }

		public byte HeaderTypeID // Header type code
		{
			get { return this.FHeaderTypeID; }
		}
		public String HeaderType // Header type name
		{
			get { return this.FGetHeaderType(); }
		}
		public byte MPEGVersionID // MPEG version code
		{
			get { return this.FMPEGVersionID; }   
		}
		public String MPEGVersion // MPEG version name
		{
			get { return this.FGetMPEGVersion(); }
		}
		public byte ProfileID // Profile code
		{		
			get { return this.FProfileID; }
		}
		public String Profile // Profile name
		{
			get { return this.FGetProfile(); }
		}
		public byte Channels // Number of channels
		{
			get { return this.FChannels; }
		}
		public int SampleRate // Sample rate (Hz)
		{
			get { return this.FSampleRate; }
		}
        public byte BitRateTypeID // Bit rate type code
        {
            get { return this.FBitrateTypeID; }
        }
        public String BitRateType // Bit rate type name
        {
            get { return this.FGetBitRateType(); }
        }
        public bool Valid // true if data valid
        {
            get { return this.FIsValid(); }
        }

        public override bool IsVBR
		{
			get { return (AAC_BITRATE_TYPE_VBR == FBitrateTypeID); }
		}
        public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_LOSSY; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
        }

		// Sample rate values
		private static int[] SAMPLE_RATE = { 96000, 88200, 64000, 48000, 44100, 32000,
										24000, 22050, 16000, 12000, 11025, 8000, 
										0, 0, 0, 0};

		// ********************* Auxiliary functions & procedures ********************

		uint ReadBits(BinaryReader Source, int Position, int Count)
		{
			byte[] buffer = new byte[4];	
	
			// Read a number of bits from file at the given position
			Source.BaseStream.Seek(Position / 8, SeekOrigin.Begin); // integer division =^ div
			buffer = Source.ReadBytes(4);
			uint result = (uint)( (buffer[0] << 24) + (buffer[1] << 16) + (buffer[2] << 8) + buffer[3] );
			result = (result << (Position % 8)) >> (32 - Count);

			return result;
		}

		// ********************** Private functions & procedures *********************

		// Reset all variables
		protected override void resetSpecificData()
		{
			FHeaderTypeID = AAC_HEADER_TYPE_UNKNOWN;
			FMPEGVersionID = AAC_MPEG_VERSION_UNKNOWN;
			FProfileID = AAC_PROFILE_UNKNOWN;
			FChannels = 0;
			FSampleRate = 0;
			FBitrateTypeID = AAC_BITRATE_TYPE_UNKNOWN;
			FTotalFrames = 0;

            FExists = false;
            FVersionID = 0;
            FSize = 0;
            FTitle = "";
            FArtist = "";
            FComposer = "";
            FAlbum = "";
            FTrack = 0;
            FDisc = 0;
            FRating = 0;
            FGenre = "";
            FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		// ---------------------------------------------------------------------------

		// Get header type name
		String FGetHeaderType()
		{  
			return AAC_HEADER_TYPE[FHeaderTypeID];
		}

		// ---------------------------------------------------------------------------

		// Get MPEG version name
		String FGetMPEGVersion()
		{  
			return AAC_MPEG_VERSION[FMPEGVersionID];
		}

		// ---------------------------------------------------------------------------

		// Get profile name
		String FGetProfile()
		{  
			return  AAC_PROFILE[FProfileID];
		}

		// ---------------------------------------------------------------------------

		// Get bit rate type name
		String FGetBitRateType()
		{  
			return AAC_BITRATE_TYPE[FBitrateTypeID];
		}

		// ---------------------------------------------------------------------------

		// Calculate duration time
		double FGetDuration()
		{
            if (FHeaderTypeID == AAC_HEADER_TYPE_MP4)
            {
                return Math.Round((double)FSampleSize / FSampleRate,0);
            }
            else
            {
                if (0 == FBitrate)
                    return 0;
                else
                    return 8 * (FFileSize - ID3v2.Size) / FBitrate;
            }
		}

		// ---------------------------------------------------------------------------

		// Check for file correctness
		bool FIsValid()
		{ 
			return ( (FHeaderTypeID != AAC_HEADER_TYPE_UNKNOWN) &&
				(FChannels > 0) && (FSampleRate > 0) && (FBitrate > 0) );
		}

		// ---------------------------------------------------------------------------

		// Get header type of the file
		byte FRecognizeHeaderType(BinaryReader Source)
		{
			byte result;
			char[] Header = new char[4]; 
		
			result = AAC_HEADER_TYPE_UNKNOWN;
			Source.BaseStream.Seek(FID3v2.Size, SeekOrigin.Begin);
			Header = StreamUtils.ReadOneByteChars(Source,4);
			
			if ( StreamUtils.StringEqualsArr("ADIF",Header) )
			{
				result = AAC_HEADER_TYPE_ADIF;
			}
            else if ((0xFF == (byte)Header[0]) && (0xF0 == (((byte)Header[0]) & 0xF0)))
            {
                result = AAC_HEADER_TYPE_ADTS;
            }
            else
            {
                Header = StreamUtils.ReadOneByteChars(Source, 4); // bytes 4 to 8
                if (StreamUtils.StringEqualsArr("ftyp", Header))
                {
                    result = AAC_HEADER_TYPE_MP4;
                }
            }
			return result;
		}

		// ---------------------------------------------------------------------------

		// Read ADIF header data
		private void FReadADIF(BinaryReader Source)
		{
			int Position;
            FValid = true;

            Position = (int)(FID3v2.Size * 8 + 32);
			if ( 0 == ReadBits(Source, Position, 1) ) Position += 3;
			else Position += 75;
			if ( 0 == ReadBits(Source, Position, 1) ) FBitrateTypeID = AAC_BITRATE_TYPE_CBR;
			else FBitrateTypeID = AAC_BITRATE_TYPE_VBR;
		
			Position++;

			FBitrate = (int)ReadBits(Source, Position, 23);

			if ( AAC_BITRATE_TYPE_CBR == FBitrateTypeID ) Position += 51;
			else Position += 31;

			FMPEGVersionID = AAC_MPEG_VERSION_4;
			FProfileID = (byte)(ReadBits(Source, Position, 2) + 1);
			Position += 2;

			FSampleRate = SAMPLE_RATE[ReadBits(Source, Position, 4)];
			Position += 4;
			FChannels += (byte)ReadBits(Source, Position, 4);
			Position += 4;
			FChannels += (byte)ReadBits(Source, Position, 4);
			Position += 4;
			FChannels += (byte)ReadBits(Source, Position, 4);
			Position += 4;
			FChannels += (byte)ReadBits(Source, Position, 2);
		}

		// ---------------------------------------------------------------------------

		// Read ADTS header data
        private void FReadADTS(BinaryReader Source)
		{
			int Frames = 0;
			int TotalSize = 0;
			int Position;
            FValid = true;
	  
			do
			{
				Frames++;
				Position = (int)(FID3v2.Size + TotalSize) * 8;

				if ( ReadBits(Source, Position, 12) != 0xFFF ) break;
			
				Position += 12;

				if ( 0 == ReadBits(Source, Position, 1) )
					FMPEGVersionID = AAC_MPEG_VERSION_4;			
				else
					FMPEGVersionID = AAC_MPEG_VERSION_2;
			
				Position += 4;
				FProfileID = (byte)(ReadBits(Source, Position, 2) + 1);
				Position += 2;

				FSampleRate = SAMPLE_RATE[ReadBits(Source, Position, 4)];
				Position += 5;

				FChannels = (byte)ReadBits(Source, Position, 3);

				if ( AAC_MPEG_VERSION_4 == FMPEGVersionID )
					Position += 9;
				else 
					Position += 7;

				TotalSize += (int)ReadBits(Source, Position, 13);
				Position += 13;

				if ( 0x7FF == ReadBits(Source, Position, 11) ) 
					FBitrateTypeID = AAC_BITRATE_TYPE_VBR;
				else
					FBitrateTypeID = AAC_BITRATE_TYPE_CBR;

				if ( AAC_BITRATE_TYPE_CBR == FBitrateTypeID ) break;
				// more accurate
				//until (Frames != 1000) && (Source.Size > FID3v2.Size + TotalSize)
			}
			while (Source.BaseStream.Length > FID3v2.Size + TotalSize);
			FTotalFrames = Frames;
			FBitrate = (int)Math.Round(8 * (double)TotalSize / 1024 / Frames * FSampleRate);
		}

        // Read MP4 header data
        // http://www.jiscdigitalmedia.ac.uk/guide/aac-audio-and-the-mp4-media-format
        // http://atomicparsley.sourceforge.net/mpeg-4files.html
        // - Metadata is located in the moov/udta/meta/ilst atom
        // - Physical information are located in the moov/trak atom (to be confirmed ?)
        private void FReadMP4(BinaryReader Source, StreamUtils.StreamHandlerDelegate PictureStreamHandler)
        {
            long iListSize = 0;
            long iListPosition = 0;
            int metadataSize = 0;
            byte dataClass = 0;

            ushort int16Data = 0;
            int int32Data = 0;
            String strData = "";
            char[] atomHeader;

            FValid = true;
            Source.BaseStream.Seek(0, SeekOrigin.Begin);
            
            // FTYP atom
            int atomSize = StreamUtils.ReverseInt32( Source.ReadInt32() );
            Source.BaseStream.Seek(atomSize-4, SeekOrigin.Current);

            // MOOV atom
            lookForMP4Atom(Source, "moov"); // === Physical data

            long moovPosition = Source.BaseStream.Position;
            lookForMP4Atom(Source, "mvhd"); // === Physical data
            byte version = Source.ReadByte();
            Source.BaseStream.Seek(3, SeekOrigin.Current); // 3-byte flags
            if (1 == version) Source.BaseStream.Seek(16, SeekOrigin.Current); else Source.BaseStream.Seek(8, SeekOrigin.Current);

            FSampleRate = StreamUtils.ReverseInt32(Source.ReadInt32());
            if (1 == version) FSampleSize = StreamUtils.ReverseInt64(Source.ReadUInt64()); else FSampleSize = StreamUtils.ReverseInt32(Source.ReadInt32());

            // VBR detection : if the gap between the smallest and the largest sample size is no more than 1%, we can consider the file is CBR; if not, VBR
            Source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            lookForMP4Atom(Source, "trak");
            lookForMP4Atom(Source, "mdia");
            lookForMP4Atom(Source, "minf");
            lookForMP4Atom(Source, "stbl");
            lookForMP4Atom(Source, "stsz");
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            int32Data = StreamUtils.ReverseInt32(Source.ReadInt32());
            if (0 == int32Data) // If value other than 0, same size everywhere => CBR
            {
                int nbSizes = StreamUtils.ReverseInt32(Source.ReadInt32());
                int max = 0;
                int min = Int32.MaxValue;
                for (int i=0; i<nbSizes; i++)
                {
                    int32Data = StreamUtils.ReverseInt32(Source.ReadInt32());
                    min = Math.Min(min, int32Data);
                    max = Math.Max(max, int32Data);
                }
                if ((min * 1.01) < max)
                {
                    FBitrateTypeID = AAC_BITRATE_TYPE_VBR;
                }
                else
                {
                    FBitrateTypeID = AAC_BITRATE_TYPE_CBR;
                }
            }
            else
            {
                FBitrateTypeID = AAC_BITRATE_TYPE_CBR;
            }

            Source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            lookForMP4Atom(Source, "udta");
            lookForMP4Atom(Source, "meta");
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            FDuration = FGetDuration();

            iListSize = lookForMP4Atom(Source, "ilst")-8; // === Metadata list
            
            FExists = true;
            FSize = iListSize;

            // Browse all metadata
            while (iListPosition < iListSize)
            {
                atomSize = StreamUtils.ReverseInt32(Source.ReadInt32());
                atomHeader = StreamUtils.ReadOneByteChars(Source, 4);
                metadataSize = lookForMP4Atom(Source, "data");
                
                // We're only looking for the last byte of the flag
                Source.BaseStream.Seek(3, SeekOrigin.Current);
                dataClass = Source.ReadByte();

                // 4-byte NULL space
                Source.BaseStream.Seek(4, SeekOrigin.Current);

                if (1 == dataClass) // UTF-8 Text
                {
                    //strData = new String( StreamUtils.ReadOneByteChars(Source, metadataSize - 16) );
                    strData = Encoding.UTF8.GetString(Source.ReadBytes(metadataSize - 16));
                }
                else if (21 == dataClass) // uint8
                {
                    int16Data = Source.ReadByte();
                    Source.BaseStream.Seek(metadataSize - 17, SeekOrigin.Current); // Potential remaining padding bytes
                }
                else if (13 == dataClass || 14 == dataClass) // JPEG/PNG picture
                {
                    FPictures.Add(MetaReaderFactory.PIC_CODE.Generic);
                    if (PictureStreamHandler != null)
                    {
                        MemoryStream mem = new MemoryStream(metadataSize - 16);
                        StreamUtils.CopyStreamFrom(mem, Source, metadataSize - 16);
                        PictureStreamHandler(ref mem);
                        mem.Close();
                    }
                    else
                    {
                        Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                    }
                }
                else if (0 == dataClass) // Special cases : gnre, trkn, disk
                {
                    if (StreamUtils.StringEqualsArr("trkn", atomHeader) || StreamUtils.StringEqualsArr("disk", atomHeader))
                    {
                        Source.BaseStream.Seek(3, SeekOrigin.Current);
                        int16Data = Source.ReadByte();
                        Source.BaseStream.Seek(metadataSize - 20, SeekOrigin.Current); // Potential remaining padding bytes
                    }
                    else if (StreamUtils.StringEqualsArr("gnre", atomHeader))
                    {
                        int16Data = StreamUtils.ReverseInt16( Source.ReadUInt16() );
                    } else { // Other unhandled cases
                        Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                    }
                }
                else // Other unhandled cases
                {
                    Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                }

                if (StreamUtils.StringEqualsArr("©alb", atomHeader)) FAlbum = strData;
                if (StreamUtils.StringEqualsArr("©art", atomHeader) || StreamUtils.StringEqualsArr("©ART", atomHeader)) FArtist = strData;
                if (StreamUtils.StringEqualsArr("©cmt", atomHeader)) FComment = strData;
                if (StreamUtils.StringEqualsArr("©day", atomHeader)) FYear = TrackUtils.ExtractStrYear(strData);
                if (StreamUtils.StringEqualsArr("©cmt", atomHeader)) FComment = strData;
                if (StreamUtils.StringEqualsArr("©nam", atomHeader)) FTitle = strData;
                if (StreamUtils.StringEqualsArr("©gen", atomHeader))
                {
                    if (1 == dataClass) FGenre = strData;
                    else if (0 == dataClass && int16Data < TID3v1.MAX_MUSIC_GENRES) FGenre = TID3v1.MusicGenre[int16Data];
                }
                if (StreamUtils.StringEqualsArr("trkn", atomHeader))
                {
                    FTrack = int16Data;
                    FTrackString = FTrack.ToString();
                }
                if (StreamUtils.StringEqualsArr("disk", atomHeader))
                {
                    FDisc = int16Data;
                    FDiscString = FDisc.ToString();
                }
                if (StreamUtils.StringEqualsArr("rtng", atomHeader))
                {
                    FRating = int16Data;
                }
                if (StreamUtils.StringEqualsArr("©wrt", atomHeader)) FComposer = strData;

                iListPosition += atomSize;
            }

            // Seek audio data segment to calculate mean bitrate 
            // NB : This figure is closer to truth than the "average bitrate" recorded in the esds/m4ds header
            Source.BaseStream.Seek(0, SeekOrigin.Begin);
            int mdatSize = lookForMP4Atom(Source, "mdat"); // === Audio binary data
            FBitrate = (int)Math.Round(mdatSize * 8 / FGetDuration(),0);
        }

        // Looks for the atom segment starting with the given key, at the current atom level
        // Returns with Source positioned right after the atom header, on the 1st byte of data
        // Returned value is the raw size of the atom (including the already-read 8-byte header)
        private int lookForMP4Atom(BinaryReader Source, String atomKey)
        {
            int atomSize = 0;
            char[] atomHeader;
            Boolean first = true;
            int iterations = 0;

            do
            {
                if (!first) Source.BaseStream.Seek(atomSize - 8, SeekOrigin.Current);
                atomSize = StreamUtils.ReverseInt32(Source.ReadInt32());
                atomHeader = StreamUtils.ReadOneByteChars(Source, 4);
                if (first) first = false;
                if (++iterations > 100) throw new Exception(atomKey + " atom could not be found");
            } while (!StreamUtils.StringEqualsArr(atomKey, atomHeader) && Source.BaseStream.Position + (atomSize-8) < Source.BaseStream.Length);

            return atomSize;
        }


		// ********************** Public functions & procedures ********************** 

		// constructor
		public TAAC()
		{
			resetData();		
		}

		// --------------------------------------------------------------------------- 

		// No explicit destructor in C#

		// --------------------------------------------------------------------------- 

		// Read data from file
        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{		
			bool result = false;
	  
            // At first search for tags, then try to recognize header type
            FID3v2.Read(source, pictureStreamHandler);
            FID3v1.Read(source);
            FAPEtag.Read(source, pictureStreamHandler);

            FHeaderTypeID = FRecognizeHeaderType(source);
			// Read header data
            if (AAC_HEADER_TYPE_ADIF == FHeaderTypeID) FReadADIF(source);
            else if (AAC_HEADER_TYPE_ADTS == FHeaderTypeID) FReadADTS(source);
            else if (AAC_HEADER_TYPE_MP4 == FHeaderTypeID) FReadMP4(source, pictureStreamHandler);

			result = true;

            return result;
		}

	}
}