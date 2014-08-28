using System;
using System.IO;
using System.Collections;
using ATL.Logging;
using System.Collections.Generic;

namespace ATL.AudioReaders.BinaryLogic
{
	/// <summary>
    /// Class for SPC700 files manipulation (extensions : .SPC)
    /// According to file format v0.30; inspired by the SNESamp source (ID666.cpp)
	/// </summary>
	class TSPCFile : AudioDataReader, IMetaDataReader
	{
		//private const String SPC_FORMAT_TAG = "SNES-SPC700 Sound File Data v0.30";
        private const String SPC_FORMAT_TAG = "SNES-SPC700 Sound File Data"; // v0.10 can be parsed as well
		private const String XTENDED_TAG = "xid6";

		private const int REGISTERS_LENGTH = 9;
		private const int AUDIODATA_LENGTH = 65792;
		private const int SPC_RAW_LENGTH = 66048;

		private const int HEADER_TEXT = 0;
		private const int HEADER_BINARY = 1;

		private const bool PREFER_BIN = false;

		private const int SPC_DEFAULT_DURATION = 180; // 3 minutes

		//Sub-chunk ID's
		private const byte XID6_SONG =	0x01;						//see ReadMe.Txt for format information
		private const byte XID6_GAME =	0x02;
		private const byte XID6_ARTIST =0x03;
		private const byte XID6_DUMPER =0x04;
		private const byte XID6_DATE =	0x05;
		private const byte XID6_EMU =	0x06;
		private const byte XID6_CMNTS =	0x07;
		private const byte XID6_INTRO =	0x30;
		private const byte XID6_LOOP =	0x31;
		private const byte XID6_END =	0x32;
		private const byte XID6_FADE =	0x33;
		private const byte XID6_MUTE =	0x34;
		private const byte XID6_LOOPX =	0x35;
		private const byte XID6_AMP =	0x36;
		private const byte XID6_OST =	0x10;
		private const byte XID6_DISC =	0x11;
		private const byte XID6_TRACK =	0x12;
		private const byte XID6_PUB =	0x13;
		private const byte XID6_COPY =	0x14;

		//Data types
		private const byte XID6_TVAL =	0x00;
		private const byte XID6_TSTR =	0x01;
		private const byte XID6_TINT =	0x04;

		//Timer stuff
		private const int XID6_MAXTICKS	 = 383999999;			//Max ticks possible for any field (99:59.99 * 64k)
		private const int XID6_TICKSMIN	 = 3840000;			  	//Number of ticks in a minute (60 * 64k)
		private const int XID6_TICKSSEC	 = 64000;			  	//Number of ticks in a second
		private const int XID6_TICKSMS	 = 64;			  		//Number of ticks in a millisecond
		private const int XID6_MAXLOOP	 = 9;				  	//Max loop times

		
		// Standard fields
		private int FSampleRate;
		private bool FTagExists;
		private String FTitle;
		private String FArtist;
        private String FComposer;
		private String FAlbum;
		private int FTrack;
        private int FDisc;
		private String FYear;
		private String FGenre;
		private String FComment;
        private IList<MetaReaderFactory.PIC_CODE> FPictures;


		public bool Exists // for compatibility with other tag readers
		{
			get { return FTagExists; }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return this.FSampleRate; }
		}	

        public override bool IsVBR
		{
			get { return false; }
		}
		public override int CodecFamily
		{
			get { return AudioReaderFactory.CF_SEQ_WAV; }
		}
        public override bool AllowsParsableMetadata
        {
            get { return true; }
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
		public String Album // Album name
		{
			get { return this.FAlbum; }
		}	
		public ushort Track // Track number
		{
			get { return (ushort)this.FTrack; }
		}
        public ushort Disc // Disc number
        {
            get { return (ushort)this.FDisc; }
        }
        public ushort Rating // Rating; not in SPC tag standard
        {
            get { return 0; }
        }
		public String Year // Year
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
        public IList<MetaReaderFactory.PIC_CODE> Pictures // Flags indicating presence of pictures
        {
            get { return this.FPictures; }
        }


		// === PRIVATE STRUCTURES/SUBCLASSES ===

		private class SPCHeader
		{
			public const int TAG_IN_HEADER = 26;

			public String FormatTag;					// Format tag (should be SPC_FORMAT_TAG)
            public long size;
			public byte TagInHeader;					// Set to TAG_IN_HEADER if header contains ID666 info
			public byte VersionByte;					// Version mark

			public void Reset()
			{
				FormatTag = "";
				VersionByte = 0;
                size = 0;
			}
		}

		private class ExtendedItem
		{
			public byte ID;
			public byte Type;
			public int Length;
			public object Data; // String or int32

			public void Reset()
			{
				ID = 0;
				Type = 0;
				Length = 0;
				Data = null;
			}
		}

		private class SPCExTags
		{
			public String FooterTag;					// Extended info tag (should be XTENDED_TAG)
			public uint FooterSize;						// Chunk size
			public Hashtable Items;						// List of ExtendedItems

			public void Reset()
			{
				FooterTag = "";
				FooterSize = 0;
				Items = new Hashtable();
			}
		}

		// === CONSTRUCTOR ===

		public TSPCFile()
		{
			// Create object
			resetData();
		}

		// === PRIVATE METHODS ===

		protected override void resetSpecificData()
		{
			// Reset variables
			FSampleRate = 0;
			FDuration = SPC_DEFAULT_DURATION;
			FTagExists = false;
			FTitle = "";
			FArtist = "";
            FComposer = "";
			FAlbum = "";
			FTrack = 0;
            FDisc = 0;
			FYear = "";
			FGenre = "";
			FComment = "";
            FPictures = new List<MetaReaderFactory.PIC_CODE>();
		}

		private bool readHeader(ref BinaryReader source, ref SPCHeader header)
		{
            source.BaseStream.Seek(0, SeekOrigin.Begin);

            long initialPosition = source.BaseStream.Position;
			header.FormatTag = new String( StreamUtils.ReadOneByteChars(source, SPC_FORMAT_TAG.Length) );
			if (SPC_FORMAT_TAG == header.FormatTag)
			{
				source.BaseStream.Seek(8,SeekOrigin.Current); // Remainder of header tag (version marker vX.XX + 2 bytes)
				header.TagInHeader = source.ReadByte();
				header.VersionByte = source.ReadByte();
                header.size = source.BaseStream.Position - initialPosition;
				return true;
			}
			else
			{
				return false;
			}
		}

        private void readHeaderTags(ref BinaryReader source, ref SPCHeader header)
		{
            long initialPosition = source.BaseStream.Position;

			FTitle = new String(StreamUtils.ReadOneByteChars(source,32));
			FAlbum = new String(StreamUtils.ReadOneByteChars(source,32));
			source.BaseStream.Seek(16,SeekOrigin.Current); // Dumper name
			FComment = new String(StreamUtils.ReadOneByteChars(source,32));

			char[] date;
			char[] song;
			char[] fade;
			
			// NB : Dump date is used to determine if the tag is binary or text-based.
			// It won't be recorded as a property of TSPC
			date = StreamUtils.ReadOneByteChars(source,11);
			song = StreamUtils.ReadOneByteChars(source,3);
			fade = StreamUtils.ReadOneByteChars(source,5);
			
			bool bin;
			int dateRes = isText(date);
			int songRes = isText(song);
			int fadeRes = isText(fade);

			//if ( 0 == (dateRes | songRes | fadeRes) ) // No time nor date -> use default
			//{
				bin = true;
			//}
			//else
			if ((songRes != -1) && (fadeRes != -1)) // No time, or time is text
			{
				if (dateRes > 0)					//If date is text, then tag is text
				{
					bin = false;
				}
				else
					if (0 == dateRes)					//No date
				{
					bin = PREFER_BIN;				//Times could still be binary (ex. 56 bin = '8' txt)
				}
				else
					if (-1 == dateRes)					//Date contains invalid characters
				{
					bin = true;
					for (int i=4; i<8; i++)
					{
						bin = bin & (0 == (byte)date[i]);
					}
				}
			}
			else
			{
				bin = true;
			}

			int fadeVal;
			int songVal;

			if (bin)
			{
                fadeVal = 
                    (byte)fade[0]*0x000001 + 
					(byte)fade[1]*0x0000FF + 
					(byte)fade[2]*0x00FF00 + 
					(byte)fade[3]*0xFF0000;
				if (fadeVal > 59999) fadeVal = 59999;

				songVal = (byte)song[0]*0x01 +
					(byte)song[1]*0x10;
				if (songVal > 959) songVal = 959;

				source.BaseStream.Seek(-1,SeekOrigin.Current); // We're one byte ahead
			}
			else
			{
                fadeVal = TrackUtils.ExtractTrackNumber(new String(fade));
                songVal = TrackUtils.ExtractTrackNumber(new String(song));
			}

            // if fadeval > 0 alone, the fade is applied on the default 3:00 duration without extending it
            if (songVal > 0) FDuration = Math.Round((double)fadeVal / 1000) + songVal;
						
			FArtist = new String(StreamUtils.ReadOneByteChars(source,32));
            header.size += source.BaseStream.Position - initialPosition;
		}

		private int isText(char[] str)
		{
			int c = 0;

			while (c<str.Length && (((byte)str[c]>=0x30 && str[c]<=0x39) || '/'==str[c])) c++;
			if (c==str.Length || str[c]==0)
				return c;
			else
				return -1;
		}

		private void readExtendedData(ref BinaryReader source, ref SPCExTags footer)
		{
			footer.FooterTag = new String(StreamUtils.ReadOneByteChars(source,4));
			if (XTENDED_TAG == footer.FooterTag)
			{
				footer.FooterSize = source.ReadUInt32();
				
				ExtendedItem anItem;

				while(source.BaseStream.Position < source.BaseStream.Length)
				{
					anItem = new ExtendedItem();
					anItem.ID = source.ReadByte();
					anItem.Type = source.ReadByte();
					anItem.Length = source.ReadUInt16();

					switch(anItem.Type)
					{
						case XID6_TVAL :
							// nothing; value is stored into the Length field
							break;
						case XID6_TSTR :
							anItem.Data = new String(StreamUtils.ReadOneByteChars(source,anItem.Length));
							while(0 == source.ReadByte()); // Ending zeroes
							source.BaseStream.Seek(-1,SeekOrigin.Current);
							break;
						case XID6_TINT :
							anItem.Data = source.ReadInt32();
							break;
					}
					footer.Items.Add( anItem.ID, anItem );
				}
			}
		}

		// === PUBLIC METHODS ===

        public override bool ReadFromFile(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
		{
			bool result = true;
			SPCHeader header = new SPCHeader();
			SPCExTags footer = new SPCExTags();

			header.Reset();
			footer.Reset();

            Stream fs = source.BaseStream;

            FValid = readHeader(ref source, ref header);
			if ( !FValid ) throw new Exception("Not a SPC file");

			// Reads the header tag
			if (SPCHeader.TAG_IN_HEADER == header.TagInHeader)
			{
				source.BaseStream.Seek(REGISTERS_LENGTH,SeekOrigin.Current);
				readHeaderTags(ref source, ref header);
			}

			// Reads extended tag
			if (fs.Length > SPC_RAW_LENGTH)
			{
				source.BaseStream.Seek(SPC_RAW_LENGTH,SeekOrigin.Begin);
				readExtendedData(ref source, ref footer);
					
				if (footer.Items.ContainsKey(XID6_ARTIST)) FArtist = (String)((ExtendedItem)footer.Items[XID6_ARTIST]).Data;
				if (footer.Items.ContainsKey(XID6_CMNTS)) FComment = (String)((ExtendedItem)footer.Items[XID6_CMNTS]).Data;
				if (footer.Items.ContainsKey(XID6_SONG)) FTitle = (String)((ExtendedItem)footer.Items[XID6_SONG]).Data;
				if (footer.Items.ContainsKey(XID6_COPY)) FYear = ((ExtendedItem)footer.Items[XID6_COPY]).Length.ToString();
				if (footer.Items.ContainsKey(XID6_TRACK)) FTrack = ((ExtendedItem)footer.Items[XID6_TRACK]).Length >> 8;
                if (footer.Items.ContainsKey(XID6_DISC)) FDisc = ((ExtendedItem)footer.Items[XID6_DISC]).Length;
				if (footer.Items.ContainsKey(XID6_OST)) FAlbum = (String)((ExtendedItem)footer.Items[XID6_OST]).Data;
				if (("" == FAlbum) && (footer.Items.ContainsKey(XID6_GAME))) FAlbum = (String)((ExtendedItem)footer.Items[XID6_GAME]).Data;
					
				long ticks = 0;
				if (footer.Items.ContainsKey(XID6_LOOP)) ticks += Math.Min(XID6_MAXTICKS, (int)((ExtendedItem)footer.Items[XID6_LOOP]).Data);
				if (footer.Items.ContainsKey(XID6_LOOPX)) ticks = ticks * Math.Min(XID6_MAXLOOP, (int)((ExtendedItem)footer.Items[XID6_LOOPX]).Length );
				if (footer.Items.ContainsKey(XID6_INTRO)) ticks += Math.Min(XID6_MAXTICKS, (int)((ExtendedItem)footer.Items[XID6_INTRO]).Data);
				if (footer.Items.ContainsKey(XID6_END)) ticks += Math.Min(XID6_MAXTICKS, (int)((ExtendedItem)footer.Items[XID6_END]).Data);
                if (footer.Items.ContainsKey(XID6_FADE)) ticks += Math.Min(XID6_MAXTICKS, (int)((ExtendedItem)footer.Items[XID6_FADE]).Data);
					
				if (ticks > 0)
					FDuration = Math.Round( (double)ticks / XID6_TICKSSEC );
			}

            FBitrate = (FFileSize - header.size) * 8 / FDuration;

            return result;
		}
	}

}