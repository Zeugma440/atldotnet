using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for APEtag 1.0 and 2.0 tags manipulation
    /// </summary>
	public class APEtag : MetaDataIO
    {
		// Tag ID
		public const String APE_ID = "APETAGEX";							// APE

		// Size constants
		public const byte APE_TAG_FOOTER_SIZE = 32;							// APE tag footer
		public const byte APE_TAG_HEADER_SIZE = 32;							// APE tag header

		// First version of APE tag
		public const int APE_VERSION_1_0 = 1000;

        // List of standard fields
        private static ICollection<string> standardFrames;

        // Mapping between ID3v2 field IDs and ATL fields
        private static IDictionary<string, byte> frameMapping;


		// APE tag data - for internal use
		private class TagInfo
		{
			// Real structure of APE footer
			public char[] ID = new char[8];                              // Always "APETAGEX"
			public int Version;			                                       // Tag version
			public int Size;				                     // Tag size including footer
			public int Fields;					                          // Number of fields
			public int Flags;						                             // Tag flags
			public char [] Reserved = new char[8];                  // Reserved for later use
			// Extended data
			public byte DataShift;		                           // Used if ID3v1 tag found
			public int FileSize;		                                 // File size (bytes)
//			public String[] Field = new String[APE_FIELD_COUNT];   // Information from fields

            public void Reset()
            {
                Array.Clear(ID,0,ID.Length);
			    Version = 0;
			    Flags = 0;
			    Fields = 0;
			    Size = 0;
			    Array.Clear(Reserved,0,Reserved.Length);
			    DataShift = 0;
			    FileSize = 0;
//			    for (int i=0; i<Field.Length; i++) Field[i] = "";
            }
		}

        static APEtag()
        {
            standardFrames = new List<string>() { "Title", "Artist", "Album", "Track", "Year", "Genre", "Comment", "Copyright", "Composer", "rating", "preference", "Discnumber","Album Artist","Conductor","Disc" };

            // Mapping between standard ATL fields and APE identifiers
            /*
             * Note : APE tag standard being a little loose, field codes vary according to the various implementations that have been made
             * => Some fields can be found in multiple frame code variants
             *      - Rating : "rating", "preference" frames
             *      - Disc number : "disc", "discnumber" frames
             */
            frameMapping = new Dictionary<string, byte>();

            frameMapping.Add("TITLE", TagData.TAG_FIELD_TITLE);
            frameMapping.Add("ARTIST", TagData.TAG_FIELD_ARTIST);
            frameMapping.Add("ALBUM", TagData.TAG_FIELD_ALBUM);
            frameMapping.Add("TRACK", TagData.TAG_FIELD_TRACK_NUMBER);
            frameMapping.Add("YEAR", TagData.TAG_FIELD_RECORDING_YEAR);
            frameMapping.Add("GENRE", TagData.TAG_FIELD_GENRE);
            frameMapping.Add("COMMENT", TagData.TAG_FIELD_COMMENT);
            frameMapping.Add("COPYRIGHT", TagData.TAG_FIELD_COPYRIGHT);
            frameMapping.Add("COMPOSER", TagData.TAG_FIELD_COMPOSER);
            frameMapping.Add("RATING", TagData.TAG_FIELD_RATING);           
            frameMapping.Add("PREFERENCE", TagData.TAG_FIELD_RATING);
            frameMapping.Add("DISC", TagData.TAG_FIELD_DISC_NUMBER);
            frameMapping.Add("DISCNUMBER", TagData.TAG_FIELD_DISC_NUMBER);
            frameMapping.Add("ALBUM ARTIST", TagData.TAG_FIELD_ALBUM_ARTIST);
            frameMapping.Add("CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR);
        }


        // ********************* Auxiliary functions & voids ********************

        private bool ReadFooter(BinaryReader SourceFile, ref TagInfo Tag)
        {	
			char[] tagID = new char[3];
			//int Transferred;
			bool result = true;
            Stream fs = SourceFile.BaseStream;
  
			// Load footer from file to variable
			Tag.FileSize = (int)fs.Length;
		
			// Check for existing ID3v1 tag in order to get the correct offset for APEtag packet
            fs.Seek(Tag.FileSize - ID3v1.ID3V1_TAG_SIZE, SeekOrigin.Begin);
            tagID = StreamUtils.ReadOneByteChars(SourceFile, 3);
            if (StreamUtils.StringEqualsArr(ID3v1.ID3V1_ID, tagID)) Tag.DataShift = ID3v1.ID3V1_TAG_SIZE;

			// Read footer data
            fs.Seek(Tag.FileSize - Tag.DataShift - APE_TAG_FOOTER_SIZE, SeekOrigin.Begin);

            Tag.ID = StreamUtils.ReadOneByteChars(SourceFile, 8);
            if (StreamUtils.StringEqualsArr(APE_ID, Tag.ID))
            {
                Tag.Version = SourceFile.ReadInt32(); //
                Tag.Size = SourceFile.ReadInt32();
                Tag.Fields = SourceFile.ReadInt32();
                Tag.Flags = SourceFile.ReadInt32();
                Tag.Reserved = StreamUtils.ReadOneByteChars(SourceFile, 8);
            }
            else
            {
                result = false;
            }

			return result;
		}

		private void setMetaField(String FieldName, String FieldValue, ref TagInfo Tag, bool readAllMetaFrames)
		{
            byte supportedMetaId = 255;
            FieldName = FieldName.Replace("\0", "").ToUpper();

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(FieldName)) supportedMetaId = frameMapping[FieldName];

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                tagData.IntegrateValue(supportedMetaId, FieldValue);
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                fieldInfo = new TagData.MetaFieldInfo(MetaDataIOFactory.TAG_APE, FieldName, FieldValue);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Replace current value, since there can be no duplicate fields in ID3v2
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

		private void ReadFields(BinaryReader SourceFile, ref TagInfo Tag, ref TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool readAllMetaFrames)
		{		
			String FieldName;
            String strValue;
			int ValueSize;
			long ValuePosition;
			int FieldFlags;
            Stream fs = SourceFile.BaseStream;

			fs.Seek(Tag.FileSize - Tag.DataShift - Tag.Size,SeekOrigin.Begin);
			// Read all stored fields
			for (int iterator=0; iterator < Tag.Fields; iterator++)
			{
				ValueSize = SourceFile.ReadInt32();
				FieldFlags = SourceFile.ReadInt32();
                FieldName = StreamUtils.ReadNullTerminatedString(SourceFile, Encoding.GetEncoding("ISO-8859-1")); // TODO document why forced encoding

                ValuePosition = fs.Position;

                if ((ValueSize > 0) && (ValueSize <= 500))
                {
                    strValue = Encoding.UTF8.GetString(SourceFile.ReadBytes(ValueSize));
                    setMetaField(FieldName.Trim(), strValue.Trim(), ref Tag, readAllMetaFrames);
                }
                else if (ValueSize > 0) // Size > 500 => Probably an embedded picture
                {
                    TagData.PictureInfo picInfo;
                    int picturePosition;
                    TagData.PIC_TYPE picType = decodeAPEPictureType(FieldName);

                    if (picType.Equals(TagData.PIC_TYPE.Unsupported))
                    {
                        picturePosition = takePicturePosition(MetaDataIOFactory.TAG_APE, FieldName);
                        picInfo = new TagData.PictureInfo(null, MetaDataIOFactory.TAG_APE, FieldName, picturePosition);
                    } else
                    {
                        picturePosition = takePicturePosition(picType);
                        picInfo = new TagData.PictureInfo(null, picType, picturePosition);
                    }

                    addPictureToken(picType);
                    if (pictureStreamHandler != null)
                    {
                        String description = StreamUtils.ReadNullTerminatedString(SourceFile, Encoding.GetEncoding("ISO-8859-1")); // Description seems to be a null-terminated ANSI string documenting picture mime-type
                        ImageFormat imgFormat = Utils.GetImageFormatFromMimeType(description);

                        MemoryStream mem = new MemoryStream(ValueSize-description.Length-1);
                        StreamUtils.CopyStream(SourceFile.BaseStream, mem, ValueSize-description.Length-1);
                        // TODO
                        // nativePicCode as byte ?
                        pictureStreamHandler(ref mem, TagData.PIC_TYPE.Front, imgFormat, MetaDataIOFactory.TAG_APE, 0, picturePosition);
                        mem.Close();
                    }
                }
                fs.Seek(ValuePosition + ValueSize, SeekOrigin.Begin);
			}			
		}

        private static TagData.PIC_TYPE decodeAPEPictureType(string picCode)
        {
            picCode = picCode.Trim().ToUpper();
            if ("COVER ART (FRONT)".Equals(picCode)) return TagData.PIC_TYPE.Front;
            else if ("COVER ART (BACK)".Equals(picCode)) return TagData.PIC_TYPE.Back;
            else if ("COVER ART (MEDIA)".Equals(picCode)) return TagData.PIC_TYPE.CD;
            else return TagData.PIC_TYPE.Unsupported;
        }

        private static string encodeAPEPictureType(TagData.PIC_TYPE picCode)
        {
            if (TagData.PIC_TYPE.Front.Equals(picCode)) return "Cover Art (Front)";
            else if (TagData.PIC_TYPE.Back.Equals(picCode)) return "Cover Art (Back)";
            else if (TagData.PIC_TYPE.CD.Equals(picCode)) return "Cover Art (Media)";
            else return "Cover Art (Other)";
        }


        // ********************** Public functions & voids **********************

        public APEtag()
		{
			// Create object		
			ResetData();
		}

		// ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, TagData.PictureStreamHandlerDelegate pictureStreamHandler, bool storeUnsupportedMetaFields = false)
        {
			TagInfo Tag = new TagInfo();

			// Reset data and load footer from file to variable
			ResetData();
            Tag.Reset();
				
			bool result = ReadFooter(source, ref Tag);
		
			// Process data if loaded and footer valid
			if ( result )
			{
				FExists = true;
				// Fill properties with footer data
				FVersion = Tag.Version;
				FSize = Tag.Size;
			
                // Get information from fields
                ReadFields(source, ref Tag, ref pictureStreamHandler, storeUnsupportedMetaFields);
			}

			return result;
		}

        protected override int getDefaultTagOffset()
        {
            return TO_EOF;
        }

        protected override bool write(TagData tag, BinaryWriter w)
        {
            throw new NotImplementedException();
        }
    }
}