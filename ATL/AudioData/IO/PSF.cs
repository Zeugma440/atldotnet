using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using static ATL.AudioData.AudioDataManager;
using Commons;
using System.Text;

namespace ATL.AudioData.IO
{
	/// <summary>
	/// Class for Portable Sound Format files manipulation (extensions : .PSF, .PSF1, .PSF2, 
    /// .MINIPSF, .MINIPSF1, .MINIPSF2, .SSF, .MINISSF, .DSF, .MINIDSF, .GSF, .MINIGSF, .QSF, .MINISQF)
    /// According to Neil Corlett's specifications v. 1.6
	/// </summary>
	class PSF : MetaDataIO, IAudioDataIO
	{
		// Format Type Names
		public const String PSF_FORMAT_UNKNOWN = "Unknown";
		public const String PSF_FORMAT_PSF1 = "Playstation";
		public const String PSF_FORMAT_PSF2 = "Playstation 2";
		public const String PSF_FORMAT_SSF = "Saturn";
		public const String PSF_FORMAT_DSF = "Dreamcast";
		public const String PSF_FORMAT_USF = "Nintendo 64";
		public const String PSF_FORMAT_QSF = "Capcom QSound";

		// Tag predefined fields
		public const String TAG_LENGTH = "length";
		public const String TAG_FADE = "fade";

		private const String PSF_FORMAT_TAG = "PSF";
		private const String TAG_HEADER = "[TAG]";
		private const uint HEADER_LENGTH = 16;

        private const byte LINE_FEED = 0x0A;
        private const byte SPACE = 0x20;

        private const int PSF_DEFAULT_DURATION = 180000; // 3 minutes

        private int sampleRate;
        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private static IDictionary<string, byte> frameMapping; // Mapping between PSF frame codes and ATL frame codes
        private static IList<string> playbackFrames; // Frames that are required for playback


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
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE);
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

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID.ToLower())) supportedMetaId = frameMapping[ID.ToLower()];

            return supportedMetaId;
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private class PSFHeader
		{
			public String FormatTag;					// Format tag (should be PSF_FORMAT_TAG)
			public byte VersionByte;					// Version mark
			public uint ReservedAreaLength;				// Length of reserved area (bytes)
			public uint CompressedProgramLength;		// Length of compressed program (bytes)

			public void Reset()
			{
				FormatTag = "";
				VersionByte = 0;
				ReservedAreaLength = 0;
				CompressedProgramLength = 0;
			}
		}

		private class PSFTag
		{
			public String TagHeader;					// Tag header (should be TAG_HEADER)
            public int size;

			public void Reset()
			{
				TagHeader = "";
                size = 0;
			}
		}


        // ---------- CONSTRUCTORS & INITIALIZERS

        static PSF()
        {
            frameMapping = new Dictionary<string, byte>
            {
                { "title", TagData.TAG_FIELD_TITLE },
                { "game", TagData.TAG_FIELD_ALBUM }, // Small innocent semantic shortcut
                { "artist", TagData.TAG_FIELD_ARTIST },
                { "copyright", TagData.TAG_FIELD_COPYRIGHT },
                { "comment", TagData.TAG_FIELD_COMMENT },
                { "year", TagData.TAG_FIELD_RECORDING_YEAR },
                { "genre", TagData.TAG_FIELD_GENRE },
                { "rating", TagData.TAG_FIELD_RATING } // Does not belong to the predefined standard PSF tags
            };

            playbackFrames = new List<string>
            {
                "volume",
                "length",
                "fade",
                "filedir",
                "filename",
                "fileext"
            };
        }

        private void resetData()
        {
            sampleRate = 44100; // Seems to be de facto value for all PSF files, even though spec doesn't say anything about it
            bitrate = 0;
            duration = 0;

            ResetData();
        }

        public PSF(string filePath)
        {
            this.filePath = filePath;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private bool readHeader(BinaryReader source, ref PSFHeader header)
		{
            header.FormatTag = Utils.Latin1Encoding.GetString(source.ReadBytes(3));
			if (PSF_FORMAT_TAG == header.FormatTag)
			{
				header.VersionByte = source.ReadByte();
				header.ReservedAreaLength = source.ReadUInt32();
				header.CompressedProgramLength = source.ReadUInt32();
				return true;
			}
			else
			{
				return false;
			}
		}

        private string readPSFLine(Stream source, Encoding encoding)
		{
            long lineStart = source.Position;
            long lineEnd;
            bool hasEOL = true;

            if (StreamUtils.FindSequence(source, new byte[1] { LINE_FEED }))
            {
                lineEnd = source.Position;
            }
            else
            {
                lineEnd = source.Length;
                hasEOL = false;
            }

            source.Seek(lineStart, SeekOrigin.Begin);

            byte[] data = new byte[lineEnd - lineStart];
            source.Read(data, 0, data.Length);

            for (int i=0;i<data.Length; i++) if (data[i] < SPACE) data[i] = SPACE; // According to spec : "All characters 0x01-0x20 are considered whitespace"

            return encoding.GetString(data,0,data.Length - (hasEOL?1:0) ).Trim(); // -1 because we don't want to include LINE_FEED in the result
		}

        private bool readTag(BinaryReader source, ref PSFTag tag, ReadTagParams readTagParams)
		{
            long initialPosition = source.BaseStream.Position;
            Encoding encoding = Utils.Latin1Encoding;

            tag.TagHeader = Utils.Latin1Encoding.GetString(source.ReadBytes(5));
			if (TAG_HEADER == tag.TagHeader)
			{
				string s = readPSFLine(source.BaseStream, encoding);
				
				int equalIndex;
                string keyStr, valueStr, lowKeyStr;
                string lastKey = "";
                string lastValue = "";
                bool lengthFieldFound = false;

				while ( s != "" )
				{
					equalIndex = s.IndexOf("=");
					if (equalIndex != -1)
					{
						keyStr = s.Substring(0,equalIndex).Trim();
                        lowKeyStr = keyStr.ToLower();
						valueStr = s.Substring(equalIndex+1, s.Length-(equalIndex+1)).Trim();

                        if (lowKeyStr.Equals("utf8") && valueStr.Equals("1")) encoding = Encoding.UTF8;

                        if (lowKeyStr.Equals(TAG_LENGTH) || lowKeyStr.Equals(TAG_FADE))
                        {
                            if (lowKeyStr.Equals(TAG_LENGTH)) lengthFieldFound = true;
                            duration += parsePSFDuration(valueStr);
                        }

                        // PSF specifics : a field appearing more than once is the same field, with values spanning over multiple lines
                        if (lastKey.Equals(keyStr))
                        {
                            lastValue +=  Environment.NewLine + valueStr;
                        }
                        else
                        {
                            SetMetaField(lastKey, lastValue, readTagParams.ReadAllMetaFrames);
                            lastValue = valueStr;
                        }
                        lastKey = keyStr;
					}

					s = readPSFLine(source.BaseStream, encoding);
				} // Metadata lines 
                SetMetaField(lastKey, lastValue, readTagParams.ReadAllMetaFrames);

                // PSF files without any 'length' tag take default duration, regardless of 'fade' value
                if (!lengthFieldFound) duration = PSF_DEFAULT_DURATION;

                tag.size = (int)(source.BaseStream.Position - initialPosition);
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(initialPosition, tag.size);
                }
				return true;
			}
			else
			{
				return false;
			}
		}

		private double parsePSFDuration(String durationStr)
		{
			String hStr = "";
			String mStr = "";
			String sStr = "";
			String dStr = "";
			double result = 0;

			int sepIndex;

			// decimal
			sepIndex = durationStr.LastIndexOf(".");
			if (-1 == sepIndex) sepIndex = durationStr.LastIndexOf(",");

			if (-1 != sepIndex)
			{
				sepIndex++;
				dStr = durationStr.Substring(sepIndex,durationStr.Length-sepIndex);
				durationStr = durationStr.Substring(0,Math.Max(0,sepIndex-1));
			}

			
			// seconds
			sepIndex = durationStr.LastIndexOf(":");

			sepIndex++;
			sStr = durationStr.Substring(sepIndex,durationStr.Length-sepIndex);
            //if (1 == sStr.Length) sStr = sStr + "0"; // "2:2" means 2:20 and not 2:02

			durationStr = durationStr.Substring(0,Math.Max(0,sepIndex-1));

			// minutes
			if (durationStr.Length > 0)
			{
				sepIndex = durationStr.LastIndexOf(":");
				
				sepIndex++;
				mStr = durationStr.Substring(sepIndex,durationStr.Length-sepIndex);

				durationStr = durationStr.Substring(0,Math.Max(0,sepIndex-1));
			}

			// hours
			if (durationStr.Length > 0)
			{
				sepIndex = durationStr.LastIndexOf(":");
				
				sepIndex++;
				hStr = durationStr.Substring(sepIndex,durationStr.Length-sepIndex);
			}

			if (dStr != "") result = result + (Int32.Parse(dStr) * 100);
			if (sStr != "") result = result + (Int32.Parse(sStr) * 1000);
			if (mStr != "") result = result + (Int32.Parse(mStr) * 60000);
			if (hStr != "") result = result + (Int32.Parse(hStr) * 3600000);
			
			return result;
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
			PSFHeader header = new PSFHeader();
			PSFTag tag = new PSFTag();

            header.Reset();
            tag.Reset();
            resetData();

            isValid = readHeader(source, ref header);
			if ( !isValid ) throw new Exception("Not a PSF file");

			if (source.BaseStream.Length > HEADER_LENGTH+header.CompressedProgramLength+header.ReservedAreaLength)
			{
				source.BaseStream.Seek((long)(4+header.CompressedProgramLength+header.ReservedAreaLength),SeekOrigin.Current);

                if (!readTag(source, ref tag, readTagParams)) throw new Exception("Not a PSF tag");

                tagExists = true;
			} 

            bitrate = (sizeInfo.FileSize-tag.size)* 8 / duration;

			return result;
		}

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;

            w.Write(Utils.Latin1Encoding.GetBytes(TAG_HEADER));

            // Announce UTF-8 support
            w.Write(Utils.Latin1Encoding.GetBytes("utf8=1"));
            w.Write(LINE_FEED);

            IDictionary<byte, string> map = tag.ToMap();

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach (string s in frameMapping.Keys)
                {
                    if (frameType == frameMapping[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            writeTextFrame(w, s, map[frameType]);
                            result++;
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if ((fieldInfo.TagType.Equals(MetaDataIOFactory.TAG_ANY) || fieldInfo.TagType.Equals(getImplementedTagType())) && !fieldInfo.MarkedForDeletion && !fieldInfo.NativeFieldCode.Equals("utf8")) // utf8 already written
                {
                    writeTextFrame(w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                    result++;
                }
            }

            // Remove the last end-of-line character
            w.BaseStream.SetLength(w.BaseStream.Length - 1);

            return result;
        }

        private void writeTextFrame(BinaryWriter writer, string frameCode, string text)
        {
            string[] textLines;
            if (text.Contains(Environment.NewLine))
            {
                // Split a multiple-line value into multiple frames with the same code
                textLines = text.Split(Environment.NewLine.ToCharArray());
            }
            else
            {
                textLines = new string[1] { text };
            }

            foreach (string s in textLines)
            {
                writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
                writer.Write('=');
                writer.Write(Encoding.UTF8.GetBytes(s));
                writer.Write(LINE_FEED);
            }
        }

        // Specific implementation for conservation of fields that are required for playback
        public override bool Remove(BinaryWriter w)
        {
            TagData tag = new TagData();

            foreach (byte b in frameMapping.Values)
            {
                tag.IntegrateValue(b, "");
            }

            string fieldCode;
            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                fieldCode = fieldInfo.NativeFieldCode.ToLower();
                if (!fieldCode.StartsWith("_") && !playbackFrames.Contains(fieldCode) )
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    tag.AdditionalFields.Add(emptyFieldInfo);
                }
            }

            w.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            BinaryReader r = new BinaryReader(w.BaseStream);
            return Write(r, w, tag);
        }

    }
}
