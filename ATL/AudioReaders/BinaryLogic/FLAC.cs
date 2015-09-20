using System;
using System.Collections;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for Free Lossless Audio Codec files manipulation (extension : .FLAC)
    /// </summary>
	class TFLAC : AudioDataReader, IMetaDataReader
	{

		private const int META_STREAMINFO      = 0;
		private const int META_PADDING         = 1;
		private const int META_APPLICATION     = 2;
		private const int META_SEEKTABLE       = 3;
		private const int META_VORBIS_COMMENT  = 4;
		private const int META_CUESHEET        = 5;
        private const int META_PICTURE         = 6;


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


		private class TMetaData
		{
			public byte[] MetaDataBlockHeader = new byte[4];
			public MemoryStream Data;
    
			public void Reset()
			{
				Array.Clear(MetaDataBlockHeader,0,4);
				Data.Flush();
			}
		}

        // Reference : https://xiph.org/flac/format.html#metadata_block_picture
        public class FlacMetaDataBlockPicture
        {
            //public Int32 pictureAPICType;
            public MetaReaderFactory.PIC_CODE picCode;
            public String mimeType;
            public String description;
            public Int32 width;
            public Int32 height;
            public Int32 colorDepth;
            public Int32 colorNum;
            public Int32 picDataLength;

            public int picDataOffset;
        }


		// Private declarations
		private TFlacHeader FHeader;
		private int FPaddingIndex;
		private bool FPaddingLast;
		private int FVorbisIndex;
		private int FPadding;
		private int FVCOffset;
		private long FAudioOffset;
		private byte FChannels;
		private int FSampleRate;
		private byte FBitsPerSample;
		private long FSamples;

		// ArrayList of TMetaData
		private ArrayList aMetaBlockOther;
		//private TMetaData[] aMetaBlockOther;    

		// tag data
		private String FVendor;
		private int FTagSize;
		private bool FExists;


		// Meta members (turned to private because of properties)
		private String FTrackString;
        private String FDiscString;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private String FYear;
		private String FGenre;
		private String FComment;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;
    
		//extra
		public String xTones;
		public String xStyles;
		public String xMood;
		public String xSituation;
		public ushort xRating;
		public String xQuality;
		public String xTempo;
		public String xType;

		//
		public String Language;
		public String Copyright;
		public String Link;
		public String Encoder;
		public String Lyrics;
		public String Performer;
		public String License;
		public String Organization;
		public String Description;
		public String Location;
		public String Contact;
		public String ISRC;
    
		public ArrayList aExtraFields;
		//public String[][] aExtraFields;


		public byte Channels // Number of channels
		{
			get { return FChannels; }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return FSampleRate; }
		}
		public byte BitsPerSample // Bits per sample
		{
			get { return FBitsPerSample; }
		}
		public long Samples // Number of samples
		{
			get { return FSamples; }
		}
		public double Ratio // Compression ratio (%)
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
		
        public String ChannelMode 
		{
			get { return FGetChannelMode(); }
		}
		public bool Exists 
		{
			get { return FExists; }
		}
		public String Vendor 
		{
			get { return FVendor; }
		}
		public long AudioOffset //offset of audio data
		{
			get { return FAudioOffset; }
		}
		public bool HasLyrics 
		{
			get { return FGetHasLyrics(); }
		}
		
		// PROPERTIES FOR METADATAREADER
		public String Title 
		{
			get { return this.FTitle; }
			set { FTitle = value; }
		}
		public String Artist
		{
			get { return this.FArtist; }
			set { FArtist = value; }
		}
        public String Composer
        {
            get { return this.FComposer; }
            set { FComposer = value; }
        }
		public String Album
		{
			get { return this.FAlbum; }
			set { FAlbum = value; }
		}	
		public String Year
		{
			get { return this.FYear; }
			set { FYear = value; }
		}	
		public String Comment
		{
			get { return this.FComment; }
			set { FComment = value; }
		}	
		public ushort Track
		{
			get { return (ushort)(Int32.Parse(FTrackString)); }
			set { FTrackString = value.ToString(); }
		}
        public ushort Disc
        {
            get { return (ushort)(Int32.Parse(FDiscString)); }
            set { FDiscString = value.ToString(); }
        }
        public ushort Rating
        {
            get { return xRating; }
            set { xRating = value; }
        }
		public String Genre
		{
			get { return FGenre; }
		}
        public IList<MetaReaderFactory.PIC_CODE> Pictures
        {
            get { return FPictures; }
        }

		/* -------------------------------------------------------------------------- */

		protected override void resetSpecificData()
		{
            // Audio data
			FPadding = 0;
			FPaddingLast = false;
			FChannels = 0;
			FSampleRate = 0;
			FBitsPerSample = 0;
			FSamples = 0;
			FVorbisIndex = 0;
			FPaddingIndex = 0;
			FVCOffset = 0;
			FAudioOffset = 0;

			for (int i = 0; i< aMetaBlockOther.Count ;i++) ((TMetaData)aMetaBlockOther[i]).Data.Close();
			aMetaBlockOther.Clear();      

			//tag data
			FVendor = "";
			FTagSize = 0;
			FExists = false;

			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrackString = "00";
            FDiscString = "0";
			FYear = "";
			FGenre = "";
			FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
			//extra
			xTones = "";
			xStyles = "";
			xMood = "";
			xSituation = "";
			xRating = 0;
			xQuality = "";
			xTempo = "";
			xType = "";

			//
			Language = "";
			Copyright = "";
			Link = "";
			Encoder = "";
			Lyrics = "";
			Performer = "";
			License = "";
			Organization = "";
			Description = "";
			Location = "";
			Contact = "";
			ISRC = "";
      
			foreach(ArrayList aList in aExtraFields)
			{
				aList.Clear();		
			}
			aExtraFields.Clear();
		}

		/* -------------------------------------------------------------------------- */
		// Check for right FLAC file data
		private bool FIsValid()
		{
			return ( ( StreamUtils.StringEqualsArr("fLaC",FHeader.StreamMarker) ) &&
				(FChannels > 0) &&
				(FSampleRate > 0) &&
				(FBitsPerSample > 0) &&
				(FSamples > 0) );
		}

        /* -------------------------------------------------------------------------- */

        private void FReadHeader(BinaryReader source)
        {
            source.BaseStream.Seek(FID3v2.Size, SeekOrigin.Begin);

            // Read header data    
            FHeader.Reset();

            FHeader.StreamMarker = StreamUtils.ReadOneByteChars(source, 4);
            FHeader.MetaDataBlockHeader = source.ReadBytes(4);
            FHeader.Info = source.ReadBytes(18);
            FHeader.MD5Sum = source.ReadBytes(16);
        }

		/* -------------------------------------------------------------------------- */

		private double FGetDuration()
		{
			if ( (FIsValid()) && (FSampleRate > 0) )  
			{
				return (double)FSamples / FSampleRate;
			} 
			else 
			{
				return 0;
			}
		}

		/* -------------------------------------------------------------------------- */
		//   Get compression ratio
		private double FGetCompressionRatio()
		{
			if (FIsValid()) 
			{
				return (double)FFileSize / (FSamples * FChannels * FBitsPerSample / 8) * 100;
			} 
			else 
			{
				return 0;
			}
		}

		/* -------------------------------------------------------------------------- */
		//   Get channel mode
		private String FGetChannelMode()
		{
			String result;
			if (FIsValid())
			{
				switch(FChannels)
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

		/* -------------------------------------------------------------------------- */

		private bool FGetHasLyrics()
		{
			return ( Lyrics.Trim() != "" );
		}

		/* -------------------------------------------------------------------------- */

		public TFLAC()
		{  
			FHeader = new TFlacHeader();
			aMetaBlockOther = new ArrayList();
			aExtraFields = new ArrayList();
            resetData();
		}

		// No explicit destructor with C#

		/* -------------------------------------------------------------------------- */

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            Stream fs = source.BaseStream;

			byte[] aMetaDataBlockHeader = new byte[4];
			int iBlockLength;
			int iMetaType;
			int iIndex;
			bool bPaddingFound;

			bool result = true;
  
			bPaddingFound = false;
  

            // Try to read data from ID3 tags
            FID3v2.Read(source, pictureStreamHandler);

            FReadHeader(source);

			// Process data if loaded and header valid    
			if ( StreamUtils.StringEqualsArr("fLaC",FHeader.StreamMarker) )
			{
                FValid = true;
				FChannels      = (byte)( ((FHeader.Info[12] >> 1) & 0x7) + 1 );
				FSampleRate    = ( FHeader.Info[10] << 12 | FHeader.Info[11] << 4 | FHeader.Info[12] >> 4 );
				FBitsPerSample = (byte)( ((FHeader.Info[12] & 1) << 4) | (FHeader.Info[13] >> 4) + 1 );
				FSamples       = ( FHeader.Info[14] << 24 | FHeader.Info[15] << 16 | FHeader.Info[16] << 8 | FHeader.Info[17] );

				if ( 0 == (FHeader.MetaDataBlockHeader[1] & 0x80) ) // metadata block exists
				{
					iIndex = 0;
					do // read more metadata blocks if available
					{		  
						aMetaDataBlockHeader = source.ReadBytes(4);

						iIndex++; // metadatablock index
						iBlockLength = (aMetaDataBlockHeader[1] << 16 | aMetaDataBlockHeader[2] << 8 | aMetaDataBlockHeader[3]); //decode length
						if (iBlockLength <= 0) break; // can it be 0 ?

						iMetaType = (aMetaDataBlockHeader[0] & 0x7F); // decode metablock type

						if ( iMetaType == META_VORBIS_COMMENT )
						{  // read vorbis block
							FVCOffset = (int)fs.Position;
							FTagSize = iBlockLength;
							FVorbisIndex = iIndex;
							readTag(source); // set up fields
						}
						else 
						{
							if ((iMetaType == META_PADDING) && (! bPaddingFound) )  // we have padding block
							{ 
								FPadding = iBlockLength;                                            // if we find more skip & put them in metablock array
								FPaddingLast = ((aMetaDataBlockHeader[0] & 0x80) != 0);
								FPaddingIndex = iIndex;
								bPaddingFound = true;
								fs.Seek(FPadding, SeekOrigin.Current); // advance into file till next block or audio data start
							} 
							else // all other
							{
                                if (iMetaType == META_PICTURE)
                                {
                                    FlacMetaDataBlockPicture picHeader = ReadMetadataBlockPicture(fs);
                                    FPictures.Add(picHeader.picCode);
                                    if (pictureStreamHandler != null)
                                    {
                                        MemoryStream mem = new MemoryStream(picHeader.picDataLength);
                                        for (int i = 0; i < picHeader.picDataLength; i++) mem.WriteByte(source.ReadByte());

                                        pictureStreamHandler(ref mem);
                                            
                                        mem.Close();
                                    }
                                }
                                else if (iMetaType <= 5)   // is it a valid metablock ?
                                {
                                    AddMetaDataOther(aMetaDataBlockHeader, source, iBlockLength, iIndex);
                                }
                                else
                                {
                                    FSamples = 0; //ops...
                                    break;
                                }
							}
						}
					}
					while ( 0 == (aMetaDataBlockHeader[0] & 0x80) ); // while is not last flag ( first bit == 1 )
				}
			}

            if (FIsValid())
            {
                FAudioOffset = fs.Position;  // we need that to rebuild the file if nedeed
                FDuration = FGetDuration();
                FBitrate = Math.Round(((double)(FFileSize - FAudioOffset) ) * 8 / FDuration); //time to calculate average bitrate
            }
            else
            {
                result = false;
            }

			return result;  
		}

		/* -------------------------------------------------------------------------- */

		public void AddMetaDataOther( byte[] aMetaHeader, BinaryReader stream, int iBlocklength, int iIndex )
		{
			TMetaData theMetaData = new TMetaData();

			theMetaData.MetaDataBlockHeader[0] = aMetaHeader[0];
			theMetaData.MetaDataBlockHeader[1] = aMetaHeader[1];
			theMetaData.MetaDataBlockHeader[2] = aMetaHeader[2];
			theMetaData.MetaDataBlockHeader[3] = aMetaHeader[3];
			// save content in a stream
            theMetaData.Data = new MemoryStream(iBlocklength);
			theMetaData.Data.Position = 0;
				
			StreamUtils.CopyMemoryStreamFrom(theMetaData.Data, stream, iBlocklength );

			aMetaBlockOther.Add(theMetaData);
		}

		/* -------------------------------------------------------------------------- */

		private void readTag(BinaryReader Source)
		{  
			int iCount;
			int iSize;
			int iSepPos;
			char[] Data;
			String sFieldID;
			String sFieldData;
			String dataString;

			iSize = Source.ReadInt32(); // vendor
			Data = new char[iSize];
            Data = StreamUtils.ReadOneByteChars(Source, iSize);
			FVendor = new String( Data );

			iCount = Source.ReadInt32();  

			FExists = ( iCount > 0 );

			for (int i=0; i< iCount; i++)
			{
				iSize = Source.ReadInt32();
                dataString = Encoding.UTF8.GetString( Source.ReadBytes(iSize) );

				iSepPos = dataString.IndexOf("=");
				if (iSepPos > 0)
				{
                    // Field ID
					sFieldID = dataString.Substring(0,iSepPos).ToUpper();
                    // Field Data
					sFieldData = dataString.Substring(iSepPos+1,dataString.Length-iSepPos-1);

					if ( (sFieldID == "TRACKNUMBER") && (FTrackString == "00") )  
					{
						FTrackString = sFieldData;
					}
                    else if ((sFieldID == "DISCNUMBER") && (FDiscString == "0"))
                    {
                        FDiscString = sFieldData;
                    } 
					else if ( (sFieldID == "ARTIST") && (FArtist == "") ) 
					{
						FArtist = sFieldData;
					} 
					else if ( (sFieldID == "ALBUM") && (FAlbum == "") ) 
					{
						FAlbum = sFieldData;
					} 
					else if ( (sFieldID == "TITLE") && (FTitle == "") ) 
					{
						FTitle = sFieldData;
					} 
					else if ( (sFieldID == "DATE") && (FYear == "") ) 
					{
						FYear = sFieldData;
					} 
					else if ( (sFieldID == "GENRE") && (FGenre == "") ) 
					{
						FGenre = sFieldData;
					} 
					else if ( (sFieldID == "COMMENT") && (FComment == "") ) 
					{
						FComment = sFieldData;
					}
                    else if ((sFieldID == "COMPOSER") && (FComposer == "")) 
					{
						FComposer = sFieldData;
					} 
					else if ( (sFieldID == "LANGUAGE") && (Language == "") ) 
					{
						Language = sFieldData;
					} 
					else if ( (sFieldID == "COPYRIGHT") && (Copyright == "") ) 
					{
						Copyright = sFieldData;
					} 
					else if ( (sFieldID == "URL") && (Link == "") )  
					{
						Link = sFieldData;
					} 
					else if ( (sFieldID == "ENCODER") && (Encoder == "") ) 
					{
						Encoder = sFieldData;
					} 
					else if ( (sFieldID == "TONES") && (xTones == "") ) 
					{
						xTones = sFieldData;
					} 
					else if ( (sFieldID == "STYLES") && (xStyles == "") ) 
					{
						xStyles = sFieldData;
					} 
					else if ( (sFieldID == "MOOD") && (xMood == "") ) 
					{
						xMood = sFieldData;
					} 
					else if ( (sFieldID == "SITUATION") && (xSituation == "") ) 
					{
						xSituation = sFieldData;
					} 
					else if ( (sFieldID == "RATING") && (0 == xRating) ) 
					{
						xRating = TrackUtils.ExtractIntRating(sFieldData);
					} 
					else if ( (sFieldID == "QUALITY") && (xQuality == "") ) 
					{
						xQuality = sFieldData;
					} 
					else if ( (sFieldID == "TEMPO") && (xTempo == "") ) 
					{
						xTempo = sFieldData;
					} 
					else if ( (sFieldID == "TYPE") && (xType == "") ) 
					{
						xType = sFieldData;
					} 
					else if ( (sFieldID == "LYRICS") && (Lyrics == "") ) 
					{
						Lyrics = sFieldData;
					} 
					else if ( (sFieldID == "PERFORMER") && (Performer == "") ) 
					{
						Performer = sFieldData;
					} 
					else if ( (sFieldID == "LICENSE") && (License == "") ) 
					{
						License = sFieldData;
					} 
					else if ( (sFieldID == "ORGANIZATION") && (Organization == "") ) 
					{
						Organization = sFieldData;
					} 
					else if ( (sFieldID == "DESCRIPTION") && (Description == "") ) 
					{
						Description = sFieldData;
					} 
					else if ( (sFieldID == "LOCATION") && (Location == "") ) 
					{
						Location = sFieldData;
					} 
					else if ( (sFieldID == "CONTACT") && (Contact == "") ) 
					{
						Contact = sFieldData;
					} 
					else if ( (sFieldID == "ISRC") && (ISRC == "") ) 
					{
						ISRC = sFieldData;
					} 
					else 
					{ // more fields
						AddExtraField( sFieldID, sFieldData );
					}

				}

			}

		}

		/* -------------------------------------------------------------------------- */

		private void AddExtraField(String sID, String sValue)
		{  
			ArrayList newItem = new ArrayList();

			newItem.Add(sID);
			newItem.Add(sValue);
  
			aExtraFields.Add(newItem);
		}

		/* -------------------------------------------------------------------------- */

        public static FlacMetaDataBlockPicture ReadMetadataBlockPicture(Stream s)
        {
            FlacMetaDataBlockPicture result = new FlacMetaDataBlockPicture();
            int stringLen;

            BinaryReader r = new BinaryReader(s);
                result.picCode = TID3v2.ReadAPICPictureType(r, 32);
                stringLen = StreamUtils.ReverseInt32( r.ReadInt32() );
                result.mimeType = new String(StreamUtils.ReadOneByteChars(r, stringLen));
                stringLen = StreamUtils.ReverseInt32( r.ReadInt32() );
                result.description = new String(StreamUtils.ReadOneByteChars(r, stringLen));
                result.width = StreamUtils.ReverseInt32(r.ReadInt32());
                result.height = StreamUtils.ReverseInt32(r.ReadInt32());
                result.colorDepth = StreamUtils.ReverseInt32(r.ReadInt32());
                result.colorNum = StreamUtils.ReverseInt32(r.ReadInt32());
                result.picDataLength = StreamUtils.ReverseInt32(r.ReadInt32());

                result.picDataOffset = 4+4+result.mimeType.Length+4+result.description.Length+4+4+4+4+4;

            return result;
        }
	}
}