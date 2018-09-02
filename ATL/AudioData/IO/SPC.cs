using System;
using System.IO;
using System.Collections.Generic;
using static ATL.AudioData.AudioDataManager;
using Commons;

namespace ATL.AudioData.IO
{
	/// <summary>
    /// Class for SPC700 files manipulation (extensions : .SPC)
    /// According to file format v0.30; inspired by the SNESamp source (ID666.cpp)
	/// </summary>
	class SPC : MetaDataIO, IAudioDataIO
	{
        private const string ZONE_EXTENDED = "extended";
        private const string ZONE_HEADER = "header";

        //private const String SPC_FORMAT_TAG = "SNES-SPC700 Sound File Data v0.30";
        private const String SPC_FORMAT_TAG = "SNES-SPC700 Sound File Data"; // v0.10 can be parsed as well
		private const String XTENDED_TAG = "xid6";

		private const int REGISTERS_LENGTH = 9;
		private const int AUDIODATA_LENGTH = 65792;
		private const int SPC_RAW_LENGTH = 66048;

		private const int HEADER_TEXT = 0;
		private const int HEADER_BINARY = 1;

		private const bool PREFER_BIN = false;

		private const int SPC_DEFAULT_DURATION = 180000; // 3 minutes

		// Sub-chunk ID's / Metadata
		private const byte XID6_SONG =	0x01;						//see ReadMe.Txt for format information
		private const byte XID6_GAME =	0x02;
		private const byte XID6_ARTIST =0x03;
		private const byte XID6_DUMPER =0x04;
		private const byte XID6_DATE =	0x05;
		private const byte XID6_EMU =	0x06;
		private const byte XID6_CMNTS =	0x07;
        private const byte XID6_OST =   0x10;
        private const byte XID6_DISC =  0x11;
        private const byte XID6_TRACK = 0x12;
        private const byte XID6_PUB =   0x13;
        private const byte XID6_COPY =  0x14;
        // Sub-chunk ID's / Playback data
        private const byte XID6_INTRO =	0x30;
		private const byte XID6_LOOP =	0x31;
		private const byte XID6_END =	0x32;
		private const byte XID6_FADE =	0x33;
		private const byte XID6_MUTE =	0x34;
		private const byte XID6_LOOPX =	0x35;
		private const byte XID6_AMP =	0x36;
		

        // Artificial IDs for fields stored in header
        private const byte HEADER_TITLE     = 0xA0;
        private const byte HEADER_ALBUM     = 0xA1;
        private const byte HEADER_DUMPERNAME= 0xA2;
        private const byte HEADER_COMMENT   = 0xA3;
        private const byte HEADER_DUMPDATE  = 0xA4;
        private const byte HEADER_SONGLENGTH= 0xA5;
        private const byte HEADER_FADE      = 0xA6;
        private const byte HEADER_ARTIST    = 0xA7;

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
		private int sampleRate;
        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private static IDictionary<byte, byte> extendedFrameMapping; // Mapping between SPC extended frame codes and ATL frame codes
        private static IDictionary<byte, byte> headerFrameMapping; // Mapping between SPC header frame codes and ATL frame codes
        private static IList<byte> playbackFrames; // Frames that are required for playback
        private static IDictionary<byte, byte> frameTypes; // Mapping between SPC frame codes and frame types that aren't type 1 (ANSI string)


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // AudioDataIO
        public int SampleRate // Sample rate (hz)
		{
			get { return this.sampleRate; }
		}	
        public bool IsVBR
		{
			get { return false; }
		}
		public int CodecFamily
		{
			get { return AudioDataIOFactory.CF_SEQ_WAV; }
		}
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE) || (metaDataType == MetaDataIOFactory.TAG_APE);
        }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }
        protected override byte getFrameMapping(string zone, string ID, byte tagVersion)
        {
            byte supportedMetaId = 255;
            byte ID_b = Byte.Parse(ID);

            // Finds the ATL field identifier
            if (ZONE_EXTENDED.Equals(zone) && extendedFrameMapping.ContainsKey(ID_b)) supportedMetaId = extendedFrameMapping[ID_b];
            else if (ZONE_HEADER.Equals(zone) && headerFrameMapping.ContainsKey(ID_b)) supportedMetaId = headerFrameMapping[ID_b];

            return supportedMetaId;
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private class SPCHeader
		{
			public const int TAG_IN_HEADER = 26;

			public String FormatTag;					// Format tag (should be SPC_FORMAT_TAG)
            public long Size;
			public byte TagInHeader;					// Set to TAG_IN_HEADER if header contains ID666 info
			public byte VersionByte;					// Version mark

			public void Reset()
			{
				FormatTag = "";
				VersionByte = 0;
                Size = 0;
			}
		}

		private class ExtendedItem
		{
			public byte ID;
			public byte Type;
			public int Size;
			public object Data; // String or int32

			public void Reset()
			{
				ID = 0;
				Type = 0;
				Size = 0;
				Data = null;
			}
		}

		private class SPCExTags
		{
			public String FooterTag;					// Extended info tag (should be XTENDED_TAG)
			public uint FooterSize;						// Chunk size

			public void Reset()
			{
				FooterTag = "";
				FooterSize = 0;
			}
		}


        // ---------- CONSTRUCTORS & INITIALIZERS

        static SPC()
        {
            extendedFrameMapping = new Dictionary<byte, byte>
            {
                { XID6_SONG, TagData.TAG_FIELD_TITLE },
                { XID6_GAME, TagData.TAG_FIELD_ALBUM }, // Small innocent semantic shortcut
                { XID6_ARTIST, TagData.TAG_FIELD_ARTIST },
                { XID6_CMNTS, TagData.TAG_FIELD_COMMENT },
                { XID6_COPY, TagData.TAG_FIELD_RECORDING_YEAR }, // Actual field name is "Copyright year", which makes that legit
                { XID6_TRACK, TagData.TAG_FIELD_TRACK_NUMBER },
                { XID6_DISC, TagData.TAG_FIELD_DISC_NUMBER },
                { XID6_PUB, TagData.TAG_FIELD_PUBLISHER }
            };

            headerFrameMapping = new Dictionary<byte, byte>
            {
                { HEADER_TITLE, TagData.TAG_FIELD_TITLE },
                { HEADER_ALBUM, TagData.TAG_FIELD_ALBUM },
                { HEADER_ARTIST, TagData.TAG_FIELD_ARTIST },
                { HEADER_COMMENT, TagData.TAG_FIELD_COMMENT }
            };

            playbackFrames = new List<byte>
            {
                XID6_INTRO,
                XID6_LOOP,
                XID6_END,
                XID6_FADE,
                XID6_MUTE,
                XID6_LOOPX,
                XID6_AMP,
                HEADER_SONGLENGTH,
                HEADER_FADE
            };

            frameTypes = new Dictionary<byte, byte>(); // To be populated while reading
        }

        private static void addFrameType(byte frameCode, byte frameType)
        {
            if (!frameTypes.ContainsKey(frameCode)) frameTypes.Add(frameCode, frameType);
        }

        private void resetData()
        {
            // Reset variables
            sampleRate = 32000; // Seems to be de facto value for all SPC files, even though spec doesn't say anything about it
            bitrate = 0;
            duration = SPC_DEFAULT_DURATION;

            ResetData();
        }

        public SPC(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


		// === PRIVATE METHODS ===

		private bool readHeader(BinaryReader source, ref SPCHeader header)
		{
            source.BaseStream.Seek(0, SeekOrigin.Begin);

            long initialPosition = source.BaseStream.Position;
            if (SPC_FORMAT_TAG.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(SPC_FORMAT_TAG.Length))))
			{
				source.BaseStream.Seek(8,SeekOrigin.Current); // Remainder of header tag (version marker vX.XX + 2 bytes)
				header.TagInHeader = source.ReadByte();
				header.VersionByte = source.ReadByte();
                header.Size = source.BaseStream.Position - initialPosition;
				return true;
			}
			else
			{
				return false;
			}
		}

        private void readHeaderTags(BinaryReader source, ref SPCHeader header, ReadTagParams readTagParams)
		{
            long initialPosition = source.BaseStream.Position;

            SetMetaField(HEADER_TITLE.ToString(), Utils.Latin1Encoding.GetString(source.ReadBytes(32)).Replace("\0","").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            SetMetaField(HEADER_ALBUM.ToString(), Utils.Latin1Encoding.GetString(source.ReadBytes(32)).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            SetMetaField(HEADER_DUMPERNAME.ToString(), Utils.Latin1Encoding.GetString(source.ReadBytes(16)).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            SetMetaField(HEADER_COMMENT.ToString(), Utils.Latin1Encoding.GetString(source.ReadBytes(32)).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);

            byte[] date, song, fade;

            // NB : Dump date is used to determine if the tag is binary or text-based.
            // It won't be recorded as a property of TSPC
            date = source.ReadBytes(11);
            song = source.ReadBytes(3);
            fade = source.ReadBytes(5);
			
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
						bin = bin & (0 == date[i]);
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
                    fade[0]*0x000001 + 
					fade[1]*0x0000FF + 
					fade[2]*0x00FF00 + 
					fade[3]*0xFF0000;
				if (fadeVal > 59999) fadeVal = 59999;

				songVal =   song[0]*0x01 + song[1]*0x10;
				if (songVal > 959) songVal = 959;

				source.BaseStream.Seek(-1,SeekOrigin.Current); // We're one byte ahead
                SetMetaField(HEADER_FADE.ToString(), Utils.Latin1Encoding.GetString(fade), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            }
			else
			{
                fadeVal = TrackUtils.ExtractTrackNumber(Utils.Latin1Encoding.GetString(fade));
                songVal = TrackUtils.ExtractTrackNumber(Utils.Latin1Encoding.GetString(song));

                SetMetaField(HEADER_FADE.ToString(), Utils.Latin1Encoding.GetString(fade), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            }

            SetMetaField(HEADER_DUMPDATE.ToString(), Utils.Latin1Encoding.GetString(date), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            SetMetaField(HEADER_SONGLENGTH.ToString(), Utils.Latin1Encoding.GetString(song), readTagParams.ReadAllMetaFrames, ZONE_HEADER);

            // if fadeval > 0 alone, the fade is applied on the default 3:00 duration without extending it
            if (songVal > 0) duration = Math.Round((double)fadeVal) + songVal;

            SetMetaField(HEADER_ARTIST.ToString(), Utils.Latin1Encoding.GetString(source.ReadBytes(32)).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            header.Size += source.BaseStream.Position - initialPosition;

            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(initialPosition, (int)(source.BaseStream.Position - initialPosition), ZONE_HEADER);
            }
        }

        private int isText(byte[] data)
		{
			int c = 0;

			while (c < data.Length && ( (data[c]>=0x30 && data[c]<=0x39) || 0x2F == data[c] ) ) c++; // 0x2F = '/' (date separator)

            if (c==data.Length || data[c]==0)
				return c;
			else
				return -1;
		}

		private void readExtendedData(BinaryReader source, ref SPCExTags footer, ReadTagParams readTagParams)
		{
            long initialPosition = source.BaseStream.Position;
            footer.FooterTag = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
			if (XTENDED_TAG == footer.FooterTag)
			{
                tagExists = true;
				footer.FooterSize = source.ReadUInt32();

                byte ID, type;
                ushort size;
                string strData = "";
                int intData = 0;
                long ticks = 0;

                long dataPosition = source.BaseStream.Position;
                while (source.BaseStream.Position < dataPosition + footer.FooterSize - 4)
				{
					ID = source.ReadByte();
					type = source.ReadByte();
					size = source.ReadUInt16();

                    addFrameType(ID, type);

					switch(type)
					{
						case XID6_TVAL :
                            // Value is stored into the Size field
                            if (ID == XID6_TRACK) // Specific case : upper byte is the number 0-99, lower byte is an optional ASCII character
                            {
                                intData = size >> 8;
                                strData = intData.ToString();
                                byte optionalChar = (byte)(size & 0x00FF);
                                if (optionalChar > 0x20) // Character is displayable
                                {
                                    strData += (char)optionalChar;
                                }
                            }
                            else
                            {
                                intData = size;
                                strData = intData.ToString();
                            }
                            break;
						case XID6_TSTR :
                            intData = 0;
                            strData = Utils.Latin1Encoding.GetString(source.ReadBytes(size)).Replace("\0","").Trim();

                            while (source.BaseStream.Position < source.BaseStream.Length && 0 == source.ReadByte()) ; // Skip parasite ending zeroes
                            if (source.BaseStream.Position < source.BaseStream.Length) source.BaseStream.Seek(-1, SeekOrigin.Current);
                            break;
						case XID6_TINT :
							intData = source.ReadInt32();
                            strData = intData.ToString();
							break;
					}

                    if (XID6_LOOP == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_LOOPX == ID) ticks = ticks * Math.Min(XID6_MAXLOOP, (int)size);
                    else if (XID6_INTRO == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_END == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_FADE == ID) ticks += Math.Min(XID6_MAXTICKS, intData);

                    SetMetaField(ID.ToString(), strData, readTagParams.ReadAllMetaFrames, ZONE_EXTENDED);
                }

                if (ticks > 0) duration = Math.Round((double)ticks / XID6_TICKSSEC);

                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(initialPosition, (int)(source.BaseStream.Position - initialPosition), ZONE_EXTENDED);
                }
            }
        }

        // === PUBLIC METHODS ===

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = true;
			SPCHeader header = new SPCHeader();
			SPCExTags footer = new SPCExTags();

			header.Reset();
			footer.Reset();
            resetData();

            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            isValid = readHeader(source, ref header);
			if ( !isValid ) throw new Exception("Not a SPC file");

			// Reads the header tag
			if (SPCHeader.TAG_IN_HEADER == header.TagInHeader)
			{
                tagExists = true;
				source.BaseStream.Seek(REGISTERS_LENGTH,SeekOrigin.Current);
				readHeaderTags(source, ref header, readTagParams);
			}

			// Reads extended tag
			if (source.BaseStream.Length > SPC_RAW_LENGTH)
			{
				source.BaseStream.Seek(SPC_RAW_LENGTH,SeekOrigin.Begin);
                readExtendedData(source, ref footer, readTagParams);
			}
            else
            {
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(SPC_RAW_LENGTH, 0, ZONE_EXTENDED);
                }
            }

            bitrate = (sizeInfo.FileSize - header.Size) * 8 / duration;

            return result;
		}

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;

            if (zone.Equals(ZONE_HEADER))
            {
                w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag.Title,32,'\0') ));
                w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag.Album, 32, '\0')));
                w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(AdditionalFields[HEADER_DUMPERNAME.ToString()], 16, '\0')));
                w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag.Comment, 32, '\0')));
                w.Write(Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_DUMPDATE.ToString()]));
                w.Write(Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_SONGLENGTH.ToString()]));
                w.Write(Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_FADE.ToString()]));
                w.Write(Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag.Artist, 32, '\0')));
                result = 8;
            }
            else if (zone.Equals(ZONE_EXTENDED))
            {
                // SPC specific : are only allowed to appear in extended metadata fields that
                //   - either do not exist in header
                //   - or have been truncated when written in header
                long sizePos;

                w.Write(Utils.Latin1Encoding.GetBytes(XTENDED_TAG));
                sizePos = w.BaseStream.Position;
                w.Write((int)0); // Size placeholder; to be rewritten with actual value at the end of the method

                IDictionary<byte, string> map = tag.ToMap();

                // Supported textual fields
                foreach (byte frameType in map.Keys)
                {
                    foreach (byte b in extendedFrameMapping.Keys)
                    {
                        if (frameType == extendedFrameMapping[b])
                        {
                            if (map[frameType].Length > 0 && canBeWrittenInExtendedMetadata(frameType, map[frameType])) // No frame with empty value
                            {
                                writeSubChunk(w, b, map[frameType]);
                                result++;
                            }
                            break;
                        }
                    }
                }

                // Other textual fields
                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
                {
                    if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion && !fieldInfo.Zone.Equals(ZONE_HEADER)  && fieldInfo.Value.Length > 0)
                    {
                        writeSubChunk(w, Byte.Parse(fieldInfo.NativeFieldCode), fieldInfo.Value);
                        result++;
                    }
                }

                int size = (int)(w.BaseStream.Position - sizePos);
                w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                w.Write(size);
            }

            return result;
        }

        private bool canBeWrittenInExtendedMetadata(byte frameType, string value)
        {
            if (frameType == TagData.TAG_FIELD_TITLE || frameType == TagData.TAG_FIELD_ALBUM || frameType == TagData.TAG_FIELD_COMMENT || frameType == TagData.TAG_FIELD_ARTIST)
            {
                return (value.Length > 32);
            }
            else return true;
        }

        private void writeSubChunk(BinaryWriter writer, byte frameCode, string text)
        {
            writer.Write(frameCode);

            byte type = 1;
            if (frameTypes.ContainsKey(frameCode)) type = frameTypes[frameCode];
            writer.Write(type);

            switch(type)
            {
                case XID6_TVAL:
                    if (frameCode == XID6_TRACK) // Specific case : upper byte is the number 0-99, lower byte is an optional ASCII character
                    {
                        byte trackValue = (byte)Math.Min((ushort)0xFF, TrackUtils.ExtractTrackNumber(text) );
                        writer.Write('\0'); // Optional char support is not implemented
                        writer.Write(trackValue);
                    }
                    else
                    {
                        writer.Write(ushort.Parse(text)); // Value is directly written as an ushort into the length field
                    }
                    break; 
                case XID6_TSTR:
                    if (text.Length > 255) text = text.Substring(0, 255);
                    else if (text.Length < 3) text = Utils.BuildStrictLengthString(text, 3, ' ');

                    byte[] textBinary = Utils.Latin1Encoding.GetBytes(text);
                    writer.Write((ushort)(textBinary.Length + 1));
                    writer.Write(textBinary);
                    writer.Write('\0');
                    break;
                case XID6_TINT:
                    writer.Write((ushort)4);
                    writer.Write(Int32.Parse(text));
                    break;
            }
        }

        // Specific implementation for conservation of fields that are required for playback
        public override bool Remove(BinaryWriter w)
        {
            // Empty metadata
            TagData tag = new TagData();

            foreach (byte b in extendedFrameMapping.Values)
            {
                tag.IntegrateValue(b, "");
            }

            byte fieldCode;
            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                fieldCode = Byte.Parse(fieldInfo.NativeFieldCode);
                if (!playbackFrames.Contains(fieldCode))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    tag.AdditionalFields.Add(emptyFieldInfo);
                }
            }
            
            BinaryReader r = new BinaryReader(w.BaseStream);
            return Write(r, w, tag);
        }

    }

}