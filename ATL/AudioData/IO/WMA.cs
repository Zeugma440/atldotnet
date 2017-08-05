using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Windows Media Audio 7,8 and 9 files manipulation (extension : .WMA)
    /// </summary>
	class WMA : MetaDataIO, IAudioDataIO
	{
		// Channel modes
		public const byte WMA_CM_UNKNOWN = 0;                                               // Unknown
		public const byte WMA_CM_MONO = 1;                                                     // Mono
		public const byte WMA_CM_STEREO = 2;                                                 // Stereo

		// Channel mode names
		public static String[] WMA_MODE = new String[3] {"Unknown", "Mono", "Stereo"};

		private byte channelModeID;
		private int sampleRate;
		private bool isVBR;
		private bool isLossless;
        private double bitrate;
        private double duration;

        private static Dictionary<string, byte> frameMapping; // Mapping between WMA frame codes and ATL frame codes

        private AudioDataManager.SizeInfo sizeInfo;
        private string fileName;


		public byte ChannelModeID // Channel mode code
		{
			get { return this.channelModeID; }
		}	
		public String ChannelMode // Channel mode name
		{
			get { return this.getChannelMode(); }
		}
		public int SampleRate // Sample rate (hz)
		{
			get { return this.sampleRate; }
		}	

        public bool IsVBR
		{
			get { return this.isVBR; }
		}
		public int CodecFamily
        {
            get
            {
                return isLossless ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY;
            }
        }
		
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }
		public bool IsStreamed
		{
			get { return true; }
		}

        public string FileName { get { return fileName; } }

        public double BitRate { get { return bitrate; } }

        public double Duration { get { return duration; } }


        // Object IDs
        private static byte[] WMA_HEADER_ID = new byte[16] { 48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static byte[] WMA_HEADER_EXTENSION_ID = new byte[16] { 0xB5, 0x03, 0xBF, 0x5F, 0x2E, 0xA9, 0xCF, 0x11, 0x8E, 0xE3, 0x00, 0xc0, 0x0c, 0x20, 0x53, 0x65 };

        private static byte[] WMA_METADATA_OBJECT_ID = new byte[16] { 0xEA, 0xCB, 0xF8, 0xC5, 0xAF, 0x5B, 0x77, 0x48, 0x84, 0x67, 0xAA, 0x8C, 0x44, 0xFA, 0x4C, 0xCA };
        private static byte[] WMA_METADATA_LIBRARY_OBJECT_ID = new byte[16] { 0x94, 0x1C, 0x23, 0x44, 0x98, 0x94, 0xD1, 0x49, 0xA1, 0x41, 0x1D, 0x13, 0x4E, 0x45, 0x70, 0x54 };

        private static byte[] WMA_FILE_PROPERTIES_ID = new byte[16] { 161, 220, 171, 140, 71, 169, 207, 17, 142, 228, 0, 192, 12, 32, 83, 101 };
        private static byte[] WMA_STREAM_PROPERTIES_ID = new byte[16] { 145, 7, 220, 183, 183, 169, 207, 17, 142, 230, 0, 192, 12, 32, 83, 101 };
        private static byte[] WMA_CONTENT_DESCRIPTION_ID = new byte[16] { 51, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108 };
        private static byte[] WMA_EXTENDED_CONTENT_DESCRIPTION_ID = new byte[16] { 64, 164, 208, 210, 7, 227, 210, 17, 151, 240, 0, 160, 201, 94, 168, 80 };

		// Format IDs
		private const int WMA_ID				= 0x161;
		private const int WMA_PRO_ID			= 0x162;
		private const int WMA_LOSSLESS_ID		= 0x163;
		private const int WMA_GSM_CBR_ID		= 0x7A21;
		private const int WMA_GSM_VBR_ID		= 0x7A22;

		// Max. number of characters in tag field
		private const byte WMA_MAX_STRING_SIZE = 250;

		// File data - for internal use
		private class FileData
		{
            public long HeaderSize;
            public int FormatTag;										// Format ID tag
			public ushort Channels;                                // Number of channels
			public int SampleRate;                                   // Sample rate (hz)


            public FileData() { Reset(); }

            public void Reset()
			{
                HeaderSize = 0;
				FormatTag = 0;
				Channels = 0;
				SampleRate = 0;
			}
		}

        static WMA()
        {
            // NB : WM/TITLE, WM/AUTHOR, WM/DESCRIPTION and WM/RATING are not WMA extended fields; therefore
            // their ID will not appear as is in the WMA header. 
            // Their info is contained in the standard Content Description block at the very beginning of the file
            frameMapping = new Dictionary<string, byte>
            {
                { "WM/TITLE", TagData.TAG_FIELD_TITLE },
                { "WM/AlbumTitle", TagData.TAG_FIELD_ALBUM },
                { "WM/AUTHOR", TagData.TAG_FIELD_ARTIST },
                { "WM/DESCRIPTION", TagData.TAG_FIELD_COMMENT },
                { "WM/Comments", TagData.TAG_FIELD_COMMENT },
                { "WM/Year", TagData.TAG_FIELD_RECORDING_YEAR },
                { "WM/Genre", TagData.TAG_FIELD_GENRE },
                { "WM/TrackNumber", TagData.TAG_FIELD_TRACK_NUMBER },
                { "WM/PartOfSet", TagData.TAG_FIELD_DISC_NUMBER },
                { "WM/RATING", TagData.TAG_FIELD_RATING },
                { "WM/SharedUserRating", TagData.TAG_FIELD_RATING },
                { "WM/Composer", TagData.TAG_FIELD_COMPOSER },
                { "Copyright", TagData.TAG_FIELD_COPYRIGHT },
                { "WM/AlbumArtist", TagData.TAG_FIELD_ALBUM_ARTIST },
                { "WM/Conductor", TagData.TAG_FIELD_CONDUCTOR }
            };
        }

        public WMA(string fileName)
        {
            this.fileName = fileName;
            resetData();
        }

       // ---------------------------------------------------------------------------

		private void readTagStandard(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
		{
            ushort[] fieldSize = new ushort[5];
			string fieldValue;

			// Read standard field sizes
			for (int i=0;i<5;i++) fieldSize[i] = source.ReadUInt16();

            // Read standard field values
            for (int i = 0; i < 5; i++)
            {
                if (fieldSize[i] > 0)
                {
                    // Read field value
                    fieldValue = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

                    // Set corresponding tag field if supported
                    switch (i)
                    {
                        case 0: setMetaField("WM/TITLE", fieldValue, readTagParams.ReadAllMetaFrames); break;
                        case 1: setMetaField("WM/AUTHOR", fieldValue, readTagParams.ReadAllMetaFrames); break;
                        case 3: setMetaField("WM/DESCRIPTION", fieldValue, readTagParams.ReadAllMetaFrames); break;
                        case 4: setMetaField("WM/RATING", fieldValue, readTagParams.ReadAllMetaFrames); break;
                    }
                }
            }
		}

        // ---------------------------------------------------------------------------

        private void readHeaderExtended(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            byte[] headerExtensionObjectId;
            long headerExtensionObjectSize, position;
            ulong limit;

            source.BaseStream.Seek(16, SeekOrigin.Current); // Reserved field 1
            source.BaseStream.Seek(2, SeekOrigin.Current); // Reserved field 2

            uint headerExtendedSize = source.ReadUInt32(); // Size of actual data

            // Looping through header extension objects
            position = source.BaseStream.Position;
            limit = (ulong)position + headerExtendedSize;
            while ((ulong)position < limit)
            {
                headerExtensionObjectId = source.ReadBytes(16);
                headerExtensionObjectSize = (long)source.ReadUInt64();

                // Additional metadata
                if (StreamUtils.ArrEqualsArr(WMA_METADATA_OBJECT_ID, headerExtensionObjectId) || StreamUtils.ArrEqualsArr(WMA_METADATA_LIBRARY_OBJECT_ID, headerExtensionObjectId))
                {
                    ushort nbObjects = source.ReadUInt16();
                    ushort nameSize;            // Length (in bytes) of Name field
                    ushort fieldDataType;       // Type of data stored in current field
                    int fieldDataSize;          // Size of data stored in current field
                    string fieldName;           // Name of current field
                    string fieldValue = "";     // Value of current field

                    for (int i = 0; i < nbObjects; i++)
                    {
                        source.BaseStream.Seek(2, SeekOrigin.Current);  // Metadata object : Reserved / Metadata library object : Language list index; unused for now
                        source.BaseStream.Seek(2, SeekOrigin.Current);  // Corresponding stream number; unused for now
                        nameSize = source.ReadUInt16();
                        fieldDataType = source.ReadUInt16();
                        fieldDataSize = source.ReadInt32();
                        fieldName = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);

                        if (0 == fieldDataType) // Unicode string
                        {
                            fieldValue = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);
                        }
                        else if (1 == fieldDataType) // Byte array
                        {
                            if (fieldName.ToUpper().Equals("WM/PICTURE"))
                            {
                                byte picCode = source.ReadByte();
                                // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                                TagData.PIC_TYPE picType = ID3v2.DecodeID3v2PictureType(picCode);

                                int picturePosition;
                                if (picType.Equals(TagData.PIC_TYPE.Unsupported))
                                {
                                    addPictureToken(MetaDataIOFactory.TAG_ID3V2, picCode);
                                    picturePosition = takePicturePosition(MetaDataIOFactory.TAG_ID3V2, picCode);
                                }
                                else
                                {
                                    addPictureToken(picType);
                                    picturePosition = takePicturePosition(picType);
                                }

                                // Next 3 bytes usage is unknown
                                source.BaseStream.Seek(3, SeekOrigin.Current);

                                if (readTagParams.PictureStreamHandler != null)
                                {
                                    string mimeType = StreamUtils.ReadNullTerminatedString(source, Encoding.BigEndianUnicode);

                                    // Next 3 bytes usage is unknown
                                    source.BaseStream.Seek(3, SeekOrigin.Current);

                                    MemoryStream mem = new MemoryStream(fieldDataSize - 3 - (2 * (mimeType.Length + 1)) - 3);
                                    StreamUtils.CopyStream(source.BaseStream, mem, mem.Length);
                                    readTagParams.PictureStreamHandler(ref mem, picType, Utils.GetImageFormatFromMimeType(mimeType), MetaDataIOFactory.TAG_NATIVE, picCode, picturePosition);
                                    mem.Close();
                                }
                            }
                            else
                            {
                                source.BaseStream.Seek(fieldDataSize, SeekOrigin.Current);
                            }
                        }
                        else if (2 == fieldDataType) // 16-bit Boolean
                        {
                            fieldValue = source.ReadUInt16().ToString();
                        }
                        else if (3 == fieldDataType) // 32-bit unsigned integer
                        {
                            fieldValue = source.ReadUInt32().ToString();
                        }
                        else if (4 == fieldDataType) // 64-bit unsigned integer
                        {
                            fieldValue = source.ReadUInt64().ToString();
                        }
                        else if (5 == fieldDataType) // 16-bit unsigned integer
                        {
                            fieldValue = source.ReadUInt16().ToString();
                        }
                        else if (6 == fieldDataType) // 128-bit GUID; unused for now
                        {
                            source.BaseStream.Seek(fieldDataSize, SeekOrigin.Current);
                        }
                        else
                        {
                            fieldValue = "";
                        }

                        setMetaField(fieldName.Trim(), fieldValue, readTagParams.ReadAllMetaFrames);
                    }
                }

                source.BaseStream.Seek(position + headerExtensionObjectSize, SeekOrigin.Begin);
                position = source.BaseStream.Position;
            }

        }

        private void readTagExtended(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
			ushort FieldCount;
			ushort DataSize;
			ushort DataType;
			string FieldName;
			string FieldValue = "";

			// Read extended tag data
			FieldCount = source.ReadUInt16();
			for (int iterator1=0; iterator1 < FieldCount; iterator1++)
			{
				// Read field name
				DataSize = source.ReadUInt16();
				FieldName = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);
                // Read value data type
                DataType = source.ReadUInt16();
				DataSize = source.ReadUInt16();
				
				// Read field value
				if (0 == DataType) // Unicode string
				{
					FieldValue = StreamUtils.ReadNullTerminatedString(source, Encoding.Unicode);
                }
				else if (1 == DataType) // Byte array
				{
                    if (FieldName.ToUpper().Equals("WM/PICTURE"))
                    {
                        byte picCode = source.ReadByte();
                        // TODO factorize : abstract PictureTypeDecoder + unsupported / supported decision in MetaDataIO ? 
                        TagData.PIC_TYPE picType = ID3v2.DecodeID3v2PictureType(picCode);

                        int picturePosition;
                        if (picType.Equals(TagData.PIC_TYPE.Unsupported))
                        {
                            addPictureToken(MetaDataIOFactory.TAG_ID3V2, picCode);
                            picturePosition = takePicturePosition(MetaDataIOFactory.TAG_ID3V2, picCode);
                        }
                        else
                        {
                            addPictureToken(picType);
                            picturePosition = takePicturePosition(picType);
                        }

                        // Next 3 bytes usage is unknown
                        source.BaseStream.Seek(3, SeekOrigin.Current);

                        if (readTagParams.PictureStreamHandler != null)
                        {
                            string mimeType = StreamUtils.ReadNullTerminatedString(source, Encoding.BigEndianUnicode);

                            // Next 3 bytes usage is unknown
                            source.BaseStream.Seek(3, SeekOrigin.Current);

                            MemoryStream mem = new MemoryStream(DataSize - 3 - (2 * (mimeType.Length+1)) - 3);
                            StreamUtils.CopyStream(source.BaseStream, mem, mem.Length);
                            readTagParams.PictureStreamHandler(ref mem, picType, Utils.GetImageFormatFromMimeType(mimeType), MetaDataIOFactory.TAG_NATIVE, picCode, picturePosition);
                            mem.Close();
                        }
                    }
                    else 
                    {
                        source.BaseStream.Seek(DataSize, SeekOrigin.Current);
                    }
				}
				else if (2 == DataType) // 32-bit Boolean
                {
                    FieldValue = source.ReadUInt32().ToString();
                }
				else if (3 == DataType) // 32-bit unsigned integer
				{
					FieldValue = (source.ReadUInt32()+1).ToString(); // TODO - Why the +1 ?? If related to ID3v1 genre index, conversion should be done while getting field name into account
				}
				else if (4 == DataType) // 64-bit unsigned integer
				{
					FieldValue = source.ReadUInt64().ToString();
				}
				else if (5 == DataType) // 16-bit unsigned integer
				{
					FieldValue = source.ReadUInt16().ToString();
				}

                setMetaField(FieldName.Trim(), FieldValue, readTagParams.ReadAllMetaFrames);
			}
		}

        private void setMetaField(string ID, string data, bool readAllMetaFrames)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                tagData.IntegrateValue(supportedMetaId, data);
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                if (ID.StartsWith("WM/")) ID = ID.Substring(3);
                fieldInfo = new TagData.MetaFieldInfo(getImplementedTagType(), ID, data);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Replace current value, since there can be no duplicate fields
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                else
                {
                    tagData.AdditionalFields.Add(fieldInfo);
                }
            }
        }

        // ---------------------------------------------------------------------------

        private void readObject(byte[] ID, BinaryReader Source, ref FileData Data, MetaDataIO.ReadTagParams readTagParams)
        {
			// Read data from header object if supported
			if ( StreamUtils.ArrEqualsArr(WMA_FILE_PROPERTIES_ID,ID) )
			{
                // Read file properties
                Source.BaseStream.Seek(40, SeekOrigin.Current);
                duration = Source.ReadUInt64() / 10000000.0;    // Play duration (100-nanoseconds)
                Source.BaseStream.Seek(8, SeekOrigin.Current);  // Send duration; unused for now
                duration -= Source.ReadUInt64() / 1000.0;       // Preroll duration (ms)
            }
			else if ( StreamUtils.ArrEqualsArr(WMA_STREAM_PROPERTIES_ID,ID) )
			{
				// Read stream properties
				Source.BaseStream.Seek(54, SeekOrigin.Current);
				Data.FormatTag = Source.ReadUInt16();
				Data.Channels = Source.ReadUInt16();
				Data.SampleRate = Source.ReadInt32();
			}
			else if ( StreamUtils.ArrEqualsArr(WMA_CONTENT_DESCRIPTION_ID,ID) && readTagParams.ReadTag )
			{
                // Read standard tag data
                tagExists = true;
				readTagStandard(Source, readTagParams);
			}
			else if ( StreamUtils.ArrEqualsArr(WMA_EXTENDED_CONTENT_DESCRIPTION_ID,ID) && readTagParams.ReadTag )
			{
                // Read extended tag data
                tagExists = true;
				readTagExtended(Source, readTagParams);
			}
            else if (StreamUtils.ArrEqualsArr(WMA_HEADER_EXTENSION_ID, ID) && readTagParams.ReadTag)
            {
                // Read extended header (where additional metadata might be stored)
                readHeaderExtended(Source, readTagParams);
            }
		}

		// ---------------------------------------------------------------------------

		private bool readData(BinaryReader source, ref FileData Data, MetaDataIO.ReadTagParams readTagParams)
        {
            Stream fs = source.BaseStream;

            byte[] ID;
			int objectCount;
			long objectSize, initialPos, position;
            bool result = false;

            fs.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            initialPos = fs.Position;

            // Check for existing header
            ID = source.ReadBytes(16);

			if ( StreamUtils.ArrEqualsArr(WMA_HEADER_ID,ID) )
			{
				fs.Seek(8, SeekOrigin.Current);
                objectCount = source.ReadInt32();		  
				fs.Seek(2, SeekOrigin.Current);

				// Read all objects in header and get needed data
				for (int i=0; i<objectCount; i++)
				{
					position = fs.Position;
                    ID = source.ReadBytes(16);
                    objectSize = source.ReadInt64();
                    readObject(ID, source, ref Data, readTagParams);
					fs.Seek(position + objectSize, SeekOrigin.Begin);				
				}
                result = true;
			}

            Data.HeaderSize = fs.Position - initialPos;

            return result;
		}

		// ---------------------------------------------------------------------------

		private bool isValid(FileData Data)
		{
			// Check for data validity
			return (
				((Data.Channels == WMA_CM_MONO) || (Data.Channels == WMA_CM_STEREO))
				&& (Data.SampleRate >= 8000) && (Data.SampleRate <= 96000)
                );
		}
        
		// ********************** Private functions & voids *********************

		private void resetData()
		{
            ResetData();

            // Reset variables
            channelModeID = WMA_CM_UNKNOWN;
			sampleRate = 0;
			isVBR = false;
			isLossless = false;
            bitrate = 0;
            duration = 0;
		}

		// ---------------------------------------------------------------------------

		private string getChannelMode()
		{
			// Get channel mode name
			return WMA_MODE[channelModeID];
		}

        // ********************** Public functions & voids **********************

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        public override bool Read(BinaryReader Source, MetaDataIO.ReadTagParams readTagParams)
        {
            return read(Source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            FileData Data = new FileData();

            bool result = readData(source, ref Data, readTagParams);

			// Process data if loaded and valid
			if ( result && isValid(Data) )
			{
				channelModeID = (byte)Data.Channels;
				sampleRate = Data.SampleRate;
                bitrate = (sizeInfo.FileSize - sizeInfo.TotalTagSize - Data.HeaderSize) * 8.0 / 1000.0 / duration;
                isVBR = (WMA_GSM_VBR_ID == Data.FormatTag);
                isLossless = (WMA_LOSSLESS_ID == Data.FormatTag);
            }

            return result;
		}

        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_ID3V1) || (metaDataType == MetaDataIOFactory.TAG_ID3V2) || (metaDataType == MetaDataIOFactory.TAG_APE) || (metaDataType == MetaDataIOFactory.TAG_NATIVE);
        }

        public bool HasNativeMeta()
        {
            return true;
        }

        protected override int getDefaultTagOffset()
        {
            return TO_BOF;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        public bool RewriteFileSizeInHeader(BinaryWriter w, int deltaSize)
        {
            return true;
        }

        protected override bool write(TagData tag, BinaryWriter w)
        {
            throw new NotImplementedException();
        }

    }
}