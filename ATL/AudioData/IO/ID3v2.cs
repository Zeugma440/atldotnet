using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Commons;
using System.Drawing;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for ID3v2.2-2.4 tags manipulation
    /// </summary>
    public class ID3v2 : MetaDataIO
    {
        public const byte TAG_VERSION_2_2 = 2;             // Code for ID3v2.2.x tag
        public const byte TAG_VERSION_2_3 = 3;             // Code for ID3v2.3.x tag
        public const byte TAG_VERSION_2_4 = 4;             // Code for ID3v2.4.x tag

        private MetaDataIOFactory.PictureStreamHandlerDelegate FPictureStreamHandler;

        private String FEncoder;
        private String FLanguage;
        private String FLink;

        private TagInfo FTag;

        public String Encoder // Encoder
        {
            get { return this.FEncoder; }
            set { FEncoder = value; }
        }
        public String Language // Language
        {
            get { return this.FLanguage; }
            set { FLanguage = value; }
        }
        public String Link // URL link
        {
            get { return this.FLink; }
            set { FLink = value; }
        }

        // ID3v2 tag ID
        private const String ID3V2_ID = "ID3";

        // Mapping between ID3v2 field IDs and ATL fields
        private static IDictionary<string, byte> Frames_v22;
        private static IDictionary<string, byte> Frames_v23_24;

        // Max. tag size for saving
        private const int ID3V2_MAX_SIZE = 4096;

        // Unicode ID
        public const char UNICODE_ID = (char)0x1;

        // Frame header (ID3v2.3.x & ID3v2.4.x)
        private class FrameHeader_23_24
        {
            public char[] ID = new char[4];                                // Frame ID
            public int Size;                                  // Size excluding header
            public ushort Flags;											  // Flags
        }

        // Frame header (ID3v2.2.x)
        private class FrameHeader_22
        {
            public char[] ID = new char[3];                                // Frame ID
            public byte[] Size = new byte[3];                 // Size excluding header
        }

        // ID3v2 header data - for internal use
        private class TagInfo
        {
            // Real structure of ID3v2 header
            public char[] ID = new char[3];                            // Always "ID3"
            public byte Version;                                     // Version number
            public byte Revision;                                   // Revision number
            public byte Flags;                                         // Flags of tag
            public byte[] Size = new byte[4];             // Tag size excluding header
            // Extended data
            public long FileSize;		                          // File size (bytes)

            public bool UsesUnsynchronisation;          // Determinated from flags; indicates if tag uses unsynchronisation (ID3v2.2+)
            public bool HasExtendedHeader;              // Determinated from flags; indicates if tag has an extended header (ID3v2.3+)
            public bool IsExperimental;                 // Determinated from flags; indicates if tag is experimental (ID3v2.4+)
            public bool HasFooter;                      // Determinated from flags; indicates if tag has a footer (ID3v2.4+)

            // Mapping between ATL fields and actual values contained in this file's metadata
            public IDictionary<byte, String> Frames = new Dictionary<byte, String>();
        }

        // Unicode BOM properties
        private class BOMProperties
        {
            public int Size = 0;                // Size of BOM
            public Encoding Encoding;           // Corresponding encoding
        }

        // ********************* Auxiliary functions & voids ********************

        static ID3v2()
        {
            // Mapping between standard fields and ID3v2.2 identifiers
            Frames_v22 = new Dictionary<string, byte>();

            Frames_v22.Add("TT1", TagData.TAG_FIELD_GENERAL_DESCRIPTION);
            Frames_v22.Add("TT2", TagData.TAG_FIELD_TITLE);
            Frames_v22.Add("TP1", TagData.TAG_FIELD_ARTIST);
            Frames_v22.Add("TOA", TagData.TAG_FIELD_ORIGINAL_ARTIST);
            Frames_v22.Add("TAL", TagData.TAG_FIELD_ALBUM);
            Frames_v22.Add("TOT", TagData.TAG_FIELD_ORIGINAL_ALBUM);
            Frames_v22.Add("TRK", TagData.TAG_FIELD_TRACK_NUMBER);
            Frames_v22.Add("TYE", TagData.TAG_FIELD_RELEASE_YEAR);
            Frames_v22.Add("TDA", TagData.TAG_FIELD_RELEASE_DATE);
            Frames_v22.Add("COM", TagData.TAG_FIELD_COMMENT);
            Frames_v22.Add("TCM", TagData.TAG_FIELD_COMPOSER);
            Frames_v22.Add("POP", TagData.TAG_FIELD_RATING);

            // Mapping between standard fields and ID3v2.3+ identifiers
            Frames_v23_24 = new Dictionary<string, byte>();

            Frames_v23_24.Add("TIT1", TagData.TAG_FIELD_GENERAL_DESCRIPTION);
            Frames_v23_24.Add("TIT2", TagData.TAG_FIELD_TITLE);
            Frames_v23_24.Add("TPE1", TagData.TAG_FIELD_ARTIST);
            Frames_v23_24.Add("TOPE", TagData.TAG_FIELD_ORIGINAL_ARTIST);
            Frames_v23_24.Add("TALB", TagData.TAG_FIELD_ALBUM);
            Frames_v23_24.Add("TOAL", TagData.TAG_FIELD_ORIGINAL_ALBUM);
            Frames_v23_24.Add("TRCK", TagData.TAG_FIELD_TRACK_NUMBER);
            Frames_v23_24.Add("TYER", TagData.TAG_FIELD_RELEASE_YEAR);
            Frames_v23_24.Add("TDAT", TagData.TAG_FIELD_RELEASE_DATE);
            Frames_v23_24.Add("COMM", TagData.TAG_FIELD_COMMENT);
            Frames_v23_24.Add("TCOM", TagData.TAG_FIELD_COMPOSER);
            Frames_v23_24.Add("POPM", TagData.TAG_FIELD_RATING);
        }

        public ID3v2()
        {
            ResetData();
        }

        private bool ReadHeader(BinaryReader SourceFile, ref TagInfo Tag, long offset)
        {
            bool result = true;

            // Read header and get file size
            SourceFile.BaseStream.Seek(offset, SeekOrigin.Begin);
            Tag.ID = StreamUtils.ReadOneByteChars(SourceFile, 3);
            Tag.Version = SourceFile.ReadByte();
            Tag.Revision = SourceFile.ReadByte();
            
            Tag.Flags = SourceFile.ReadByte();
            Tag.UsesUnsynchronisation = ((Tag.Flags & 128) > 0);
            Tag.HasExtendedHeader = (((Tag.Flags & 64) > 0) && (Tag.Version > TAG_VERSION_2_2));
            Tag.IsExperimental = ((Tag.Flags & 32) > 0);
            Tag.HasFooter = ((Tag.Flags & 0x10) > 0);
            
            Tag.Size = SourceFile.ReadBytes(4);

            Tag.FileSize = SourceFile.BaseStream.Length;

            return result;
        }

        // ---------------------------------------------------------------------------

        private int GetTagSize(TagInfo Tag)
        {
            // Get total tag size
            int result = StreamUtils.DecodeSynchSafeInt32(Tag.Size) + 10;

            if (Tag.HasFooter) result += 10; // Indicates the presence of a footer (ID3v2.4+)
            if (result > Tag.FileSize) result = 0;

            return result;
        }

        // ---------------------------------------------------------------------------

        private void SetTagItem(String ID, String Data, ref TagInfo Tag)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier according to the ID3v2 version
            if (Tag.Version > TAG_VERSION_2_2)
            {
                if (Frames_v23_24.ContainsKey(ID)) supportedMetaId = Frames_v23_24[ID];
            } else
            {
                if (Frames_v22.ContainsKey(ID)) supportedMetaId = Frames_v22[ID];
            }

            // If ID has been mapped with an ATL field, store it in a dedicated Dictionary...
            if (supportedMetaId < 255)
            {
                // Only stores first occurence of a tag
                if (!Tag.Frames.ContainsKey(supportedMetaId))
                {
                    Tag.Frames[supportedMetaId] = Data;
                }
            } else // ...else store it in the unsupported fields Dictionary
            {
                if (unsupportedTagFields != null) unsupportedTagFields[ID] = Data;
            }
        }

        // Get information from frames (ID3v2.3.x & ID3v2.4.x : frame identifier has 4 characters)
        private void ReadFrames_v23_24(BinaryReader SourceFile, ref TagInfo Tag, long offset)
        {
            Stream fs = SourceFile.BaseStream;
            FrameHeader_23_24 Frame = new FrameHeader_23_24();
            long DataPosition;
            long DataSize;
            String strData;

            fs.Seek(offset+10, SeekOrigin.Begin);
            while ((fs.Position-offset < GetTagSize(Tag)) && (fs.Position < fs.Length))
            {
                // Read frame header and check frame ID
                // ID3v2.3+ : 4 characters
                Frame.ID = StreamUtils.ReadOneByteChars(SourceFile, 4);
                
                // Frame size measures number of bytes between end of flag and end of payload
                // ID3v2.3 : 4 byte size descriptor 
                // ID3v2.4 : Size descriptor is coded as a synch-safe Int32
                if (TAG_VERSION_2_3 == FVersion) Frame.Size = StreamUtils.ReverseInt32(SourceFile.ReadInt32());
                else if (TAG_VERSION_2_4 == FVersion)
                {
                    byte[] size = SourceFile.ReadBytes(4);
                    Frame.Size = StreamUtils.DecodeSynchSafeInt32(size);
                }
                Frame.Flags = StreamUtils.ReverseInt16(SourceFile.ReadUInt16());

                if (!(Char.IsLetter(Frame.ID[0]) && Char.IsUpper(Frame.ID[0]))) break;

                DataSize = Frame.Size - 1; // Minus encoding byte

                // Skips data size indicator if signaled by the flag
                if ((Frame.Flags & 1) > 0)
                {
                    fs.Seek(4, SeekOrigin.Current);
                    DataSize = DataSize - 4;
                }

                int encodingCode = fs.ReadByte();
                FEncoding = decodeID3v2CharEncoding((byte)encodingCode);

                // COMM fields contain :
                //   a 3-byte langage ID
                //   a "short content description", as an encoded null-terminated string
                //   the actual comment, as an encoded, null-terminated string
                // => lg lg lg (BOM) (encoded description) 00 (00) (BOM) encoded text 00 (00)
                if (StreamUtils.StringEqualsArr("COM", Frame.ID) || StreamUtils.StringEqualsArr("COMM", Frame.ID))
                {
                    long initialPos = fs.Position;

                    // Skip langage ID
                    fs.Seek(3, SeekOrigin.Current);

                    // Skip BOM
                    BOMProperties contentDescriptionBOM = new BOMProperties();
                    if (1 == encodingCode)
                    {
                        contentDescriptionBOM = readBOM(ref fs);
                    }

                    if (contentDescriptionBOM.Size <= 3)
                    {
                        // Skip content description
                        StreamUtils.ReadNullTerminatedString(SourceFile, FEncoding);
                    }
                    else
                    {
                        // If content description BOM > 3 bytes, there might not be any BOM
                        // for content description, and the algorithm might have bumped into
                        // the comment BOM => backtrack just after langage tag
                        fs.Seek(initialPos + 3, SeekOrigin.Begin);
                    }

                    DataSize = DataSize - (fs.Position - initialPos);
                }

                // A $01 "Unicode" encoding flag means the presence of a BOM (Byte Order Mark)
                // http://en.wikipedia.org/wiki/Byte_order_mark
                //    3-byte BOM : FF 00 FE
                //    2-byte BOM : FE FF (UTF-16 Big Endian)
                //    2-byte BOM : FF FE (UTF-16 Little Endian)
                //    Other variants...
                if ( 1 == encodingCode)
                {
                    long initialPos = fs.Position;
                    BOMProperties bom = readBOM(ref fs);

                    // A BOM has been read, but it lies outside the current frame
                    // => Backtrack and directly read data without BOM
                    if (bom.Size > DataSize)
                    {
                        fs.Seek(initialPos, SeekOrigin.Begin);
                    }
                    else
                    {
                        FEncoding = bom.Encoding;
                        DataSize = DataSize - bom.Size;
                    }
                }
                // If encoding > 3, we might have caught an actual character, which means there is no encoding flag
                else if (encodingCode > 3) { fs.Seek(-1, SeekOrigin.Current); DataSize++; }

                // Note data position and determine significant data size
                DataPosition = fs.Position;

                if ((DataSize > 0) && (DataSize < 500))
                {
                    // Read frame data and set tag item if frame supported
                    // Specific to Popularitymeter : Rating data has to be extracted from the POPM block
                    if (StreamUtils.StringEqualsArr("POPM", Frame.ID))
                    {
                        strData = readRatingInPopularityMeter(SourceFile, Encoding.GetEncoding("ISO-8859-1")).ToString();
                    }
                    else
                    {
                        byte[] bData = new byte[DataSize];
                        // Read frame data and set tag item if frame supported
                        bData = SourceFile.ReadBytes((int)DataSize);

                        strData = FEncoding.GetString(bData);
                    }

                    if (32768 != (Frame.Flags & 32768)) SetTagItem(new String(Frame.ID), strData, ref Tag); // Wipe out \0's to avoid string cuts
                }
                else if (DataSize > 0) // Size > 500 => Probably an embedded picture
                {
                    long Position = fs.Position;
                    if (StreamUtils.StringEqualsArr("APIC",Frame.ID))
                    {
                        // mime-type always coded in ASCII
                        if (1 == encodingCode) fs.Seek(-1, SeekOrigin.Current);
                        String mimeType = StreamUtils.ReadNullTerminatedString(SourceFile, Encoding.GetEncoding("ISO-8859-1"));
                        MetaDataIOFactory.PIC_TYPE picCode = DecodeID3v2PictureType(SourceFile.ReadByte());
                        FPictures.Add(picCode);

                        // However, description can be coded with another convention
                        if (1 == encodingCode)
                        {
                            readBOM(ref fs);
                        }
                        String description = StreamUtils.ReadNullTerminatedString(SourceFile, FEncoding);
                        if (FPictureStreamHandler != null)
                        {
                            int picSize = (int)(DataSize - (fs.Position - Position));
                            MemoryStream mem = new MemoryStream(picSize);
                            if (Tag.UsesUnsynchronisation)
                            {
                                copyUnsynchronizedStream(mem, SourceFile, picSize);
                            }
                            else
                            {
                                StreamUtils.CopyStream(SourceFile.BaseStream, mem, picSize);
                            }
                            FPictureStreamHandler(ref mem, picCode);
                            mem.Close();
                        }
                    }
                    fs.Seek(Position + DataSize, SeekOrigin.Begin);
                }
            }
        }

        // ---------------------------------------------------------------------------

        // Get information from frames (ID3v2.2.x : frame identifier has 3 characters)
        private void ReadFrames_v22(BinaryReader SourceFile, ref TagInfo Tag, long offset)
        {
            Stream fs = SourceFile.BaseStream;
            FrameHeader_22 Frame = new FrameHeader_22();
            char[] Data = new char[500];
            long DataPosition;
            int FrameSize;
            int DataSize;

            // The vast majority of ID3v2.2 tags use default encoding
            FEncoding = Encoding.GetEncoding("ISO-8859-1");

            fs.Seek(offset+10, SeekOrigin.Begin);
            while ((fs.Position - offset < GetTagSize(Tag)) && (fs.Position < fs.Length))
            {
                Array.Clear(Data, 0, Data.Length);

                // Read frame header and check frame ID
                // ID3v2.2 : 3 characters
                Frame.ID = SourceFile.ReadChars(3);
                Frame.Size = SourceFile.ReadBytes(3);

                if (!(Char.IsLetter(Frame.ID[0]) && Char.IsUpper(Frame.ID[0]))) break;

                // Note data position and determine significant data size
                DataPosition = fs.Position;
                FrameSize = (Frame.Size[0] << 16) + (Frame.Size[1] << 8) + Frame.Size[2];
                DataSize = FrameSize;

                if ((DataSize > 0) && (DataSize < 500))
                {
                    // Read frame data and set tag item if frame supported
                    // Specific to Popularitymeter : Rating data has to be extracted from the POP block
                    if (StreamUtils.StringEqualsArr("POP", Frame.ID))
                    {
                        Data = readRatingInPopularityMeter(SourceFile, FEncoding).ToString().ToCharArray();
                    }
                    else
                    {
                        Data = SourceFile.ReadChars(DataSize);
                    }
                    SetTagItem(new String(Frame.ID), new String(Data), ref Tag);
                    fs.Seek(DataPosition + FrameSize, SeekOrigin.Begin);
                }
                else if (DataSize > 0) // Size > 500 => Probably an embedded picture
                {
                    long Position = fs.Position;
                    if (StreamUtils.StringEqualsArr("PIC", Frame.ID))
                    {
                        // ID3v2.2 specific layout
                        Encoding encoding = decodeID3v2CharEncoding(SourceFile.ReadByte());
                        String imageFormat = new String(StreamUtils.ReadOneByteChars(SourceFile, 3));
                        byte picCode = SourceFile.ReadByte();
                        MetaDataIOFactory.PIC_TYPE picType = DecodeID3v2PictureType(picCode);
                        String description = StreamUtils.ReadNullTerminatedString(SourceFile, encoding);

                        if (MetaDataIOFactory.PIC_TYPE.Unsupported.Equals(picCode))
                        {
                            // If enabled, save it to unsupported pictures
                            if (unsupportedTagFields != null)
                            {
                                if (null == unsupportedPictures) unsupportedPictures = new Dictionary<int, Image>();
                                int picSize = (int)(DataSize - (fs.Position - Position));

                                using (MemoryStream mem = new MemoryStream(picSize))
                                {
                                    if (Tag.UsesUnsynchronisation)
                                    {
                                        copyUnsynchronizedStream(mem, SourceFile, picSize);
                                    }
                                    else
                                    {
                                        StreamUtils.CopyStream(SourceFile.BaseStream, mem, picSize);
                                    }

                                    unsupportedPictures.Add(picCode, Image.FromStream(mem));
                                }
                            }
                        }
                        else
                        {
                            FPictures.Add(picType);
                        }

                        if (FPictureStreamHandler != null)
                        {
                            int picSize = (int)(DataSize - (fs.Position - Position));
                            MemoryStream mem = new MemoryStream(picSize);

                            if (Tag.UsesUnsynchronisation)
                            {
                                copyUnsynchronizedStream(mem, SourceFile, picSize);
                            }
                            else
                            {
                                StreamUtils.CopyStream(SourceFile.BaseStream, mem, picSize);
                            }

                            FPictureStreamHandler(ref mem, picType);
                        }
                    }
                    fs.Seek(Position + DataSize, SeekOrigin.Begin);
                }
            }
        }

        // ********************** Public functions & voids **********************

        public override bool Read(BinaryReader source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, bool storeUnsupportedMetaFields = false)
        {
            return Read(source, pictureStreamHandler, 0, storeUnsupportedMetaFields);
        }

        public bool Read(BinaryReader source, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler, long offset, bool storeUnsupportedMetaFields = false)
        {
            FTag = new TagInfo();
            this.FPictureStreamHandler = pictureStreamHandler;

            // Reset data and load header from file to variable
            ResetData();
            bool result = ReadHeader(source, ref FTag, offset);

            // Process data if loaded and header valid
            if ((result) && StreamUtils.StringEqualsArr(ID3V2_ID, FTag.ID))
            {
                FExists = true;
                // Fill properties with header data
                FVersion = FTag.Version;
                FSize = GetTagSize(FTag);

                if (storeUnsupportedMetaFields)
                {
                    unsupportedTagFields = new Dictionary<String, String>();
                    // They will be read by ReadFrames_vXX below
                }

                // Get information from frames if version supported
                if ((TAG_VERSION_2_2 <= FVersion) && (FVersion <= TAG_VERSION_2_4) && (FSize > 0))
                {
                    if (FVersion > TAG_VERSION_2_2) ReadFrames_v23_24(source, ref FTag, offset);
                    else ReadFrames_v22(source, ref FTag, offset);

                    TagData tagData = new TagData();
                    foreach (byte b in FTag.Frames.Keys)
                    {
                        tagData.IntegrateValue(b, FTag.Frames[b]);
                        if (TagData.TAG_FIELD_GENRE == b)
                        {
                            tagData.IntegrateValue(b, extractGenreFromID3v2Code(FTag.Frames[b]));
                        }
                    }
                    this.fromTagData(tagData);
                }
            }

            return result;
        }

        // ---------------------------------------------------------------------------

        // Specific to ID3v2 : extract genre from string
        private static String extractGenreFromID3v2Code(String iGenre)
        {
            if (null == iGenre) return "";

            String result = Utils.StripZeroChars(iGenre.Trim());
            int genreIndex = -1;

            // Any number between parenthesis
            Regex regex = new Regex(@"(?<=\()\d+?(?=\))");

            Match match = regex.Match(result);
            // First match is directly returned
            if (match.Success)
            {
                genreIndex = Int32.Parse(match.Value);
                // Delete genre index string from the tag value
                result = result.Remove(0, result.LastIndexOf(')') + 1);
            }

            if (("" == result) && (genreIndex != -1) && (genreIndex < ID3v1.MusicGenre.Length)) result = ID3v1.MusicGenre[genreIndex];

            return result;
        }

        // Specific to ID3v2 : extract numeric rating from POP/POPM block containing other useless/obsolete data
        private static byte readRatingInPopularityMeter(BinaryReader Source, Encoding encoding)
        {
            // Skip the e-mail, which is a null-terminated string
            StreamUtils.ReadNullTerminatedString(Source, encoding);

            // Returns the rating, contained in the following byte
            return Source.ReadByte();
        }

        // Specific to ID3v2 : read Unicode BOM and return the corresponding encoding
        // NB : This implementation only works with UTF-16 BOMs (i.e. UTF-8 and UTF-32 BOMs will not be detected)
        private BOMProperties readBOM(ref Stream fs)
        {
            BOMProperties result = new BOMProperties();
            result.Size = 1;
            result.Encoding = Encoding.Unicode;

            int b = fs.ReadByte();
            bool first = true;
            bool foundFE = false;
            bool foundFF = false;

            while (0 == b || 0xFF == b || 0xFE == b)
            {
                // All UTF-16 BOMs either start or end with 0xFE or 0xFF
                // => Having them both read means that the entirety of the UTF-16 BOM has been read
                foundFE = foundFE || (0xFE == b);
                foundFF = foundFF || (0xFF == b);
                if (foundFE & foundFF) break;

                if (first && b != 0)
                {
                    // 0xFE first means data is coded in Big Endian
                    if (0xFE == b) result.Encoding = Encoding.BigEndianUnicode;
                    first = false;
                }

                b = fs.ReadByte();
                result.Size++;
            }

            return result;
        }

        // TODO - should be private ?
        public static MetaDataIOFactory.PIC_TYPE DecodeID3v2PictureType(int picCode)
        {
            if (0 == picCode) return MetaDataIOFactory.PIC_TYPE.Generic;
            else if (3 == picCode) return MetaDataIOFactory.PIC_TYPE.Front;
            else if (4 == picCode) return MetaDataIOFactory.PIC_TYPE.Back;
            else if (6 == picCode) return MetaDataIOFactory.PIC_TYPE.CD;
            else return MetaDataIOFactory.PIC_TYPE.Unsupported;
        }

        public static byte EncodeID3v2PictureType(MetaDataIOFactory.PIC_TYPE picCode)
        {
            if (MetaDataIOFactory.PIC_TYPE.Front.Equals(picCode)) return 3;
            else if (MetaDataIOFactory.PIC_TYPE.Back.Equals(picCode)) return 4;
            else if (MetaDataIOFactory.PIC_TYPE.CD.Equals(picCode)) return 6;
            else return 0;
        }


        // Copies the stream while cleaning abnormalities du to unsynchronization
        // => 0xff 0x00 => 0xff
        protected static void copyUnsynchronizedStream(Stream mTo, BinaryReader r, long length)
        {
            BinaryWriter w = new BinaryWriter(mTo);

            long effectiveLength;
            long initialPosition;
            byte prevB_2 = 0;
            byte prevB_1 = 0;
            byte b;

            initialPosition = r.BaseStream.Position;
            if (0 == length) effectiveLength = r.BaseStream.Length; else effectiveLength = length;

            while (r.BaseStream.Position < initialPosition + effectiveLength && r.BaseStream.Position < r.BaseStream.Length)
            {
                b = r.ReadByte();
                //if ((0xFF == prevB_2) && (0x00 == prevB_1) && (0x00 == b)) b = r.ReadByte();
                if ((0xFF == prevB_1) && (0x00 == b)) b = r.ReadByte();

                w.Write(b);
                prevB_2 = prevB_1;
                prevB_1 = b;
            }
        }

        /// Returns the .NET Encoding corresponding to the ID3v2 convention (see below)
        ///
        /// Default encoding should be "ISO-8859-1"
        /// 
        /// Warning : due to approximative implementations, some tags seem to be coded
        /// with the default encoding of the OS they have been tagged on
        /// 
        ///  $00   ISO-8859-1 [ISO-8859-1]. Terminated with $00.
        ///  $01   UTF-16 [UTF-16] encoded Unicode [UNICODE] with BOM. All
        ///   strings in the same frame SHALL have the same byteorder.
        ///  Terminated with $00 00.
        ///  $02   UTF-16BE [UTF-16] encoded Unicode [UNICODE] without BOM.
        ///  Terminated with $00 00.
        ///  $03   UTF-8 [UTF-8] encoded Unicode [UNICODE]. Terminated with $00.
        private static Encoding decodeID3v2CharEncoding(byte encoding)
        {
            if (0 == encoding) return Encoding.GetEncoding("ISO-8859-1");   // aka ISO Latin-1
            else if (1 == encoding) return Encoding.Unicode;                // UTF-16 with BOM
            else if (2 == encoding) return Encoding.BigEndianUnicode;       // UTF-16 Big Endian without BOM (since ID3v2.4)
            else if (3 == encoding) return Encoding.UTF8;                   // UTF-8 (since ID3v2.4)
            else return Encoding.Default;
        }

        private static byte encodeID3v2CharEncoding(Encoding encoding)
        {
            if (encoding.Equals(Encoding.Unicode)) return 1;
            else if (encoding.Equals(Encoding.BigEndianUnicode)) return 2;
            else if (encoding.Equals(Encoding.UTF8)) return 3;
            else return 0; // Default = ISO-8859-1 / ISO Latin-1
        }

        protected override int getDefaultTagOffset()
        {
            return TO_BOF;
        }


        // Writes tag info using ID3v2.4 conventions
        // TODO : use extended header restrictions when writing fields and storing embedded pictures
        // TODO : factorize the code below
        public override bool Write(TagData tag, BinaryWriter w)
        {
            bool result = true;
            long tagSizePos;

            w.Write(ID3V2_ID.ToCharArray());

            // Version 2.4.0
            w.Write(TAG_VERSION_2_4);
            w.Write((byte)0);

            // Flags : keep initial flags
            w.Write(FTag.Flags);
            // Keep position in mind to calculate final size and come back here to write it
            tagSizePos = w.BaseStream.Position;
            w.Write((int)0);

            if (FTag.HasExtendedHeader)
            {
                // TODO : handle extended header
            }

            // TODO : Handle unsynchronization on all written data

            // === ID3v2 FRAMES ===
            IDictionary<byte, String> map = tag.ToMap();
            long frameSizePos;
            long finalFramePos;
            bool writeFieldValue;

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach(string s in Frames_v23_24.Keys)
                {
                    if (frameType == Frames_v23_24[s])
                    {
                        writeFieldValue = true;
                        w.Write(s.ToCharArray());
                        frameSizePos = w.BaseStream.Position;
                        w.Write((int)0);

                        // TODO : handle frame flags
                        w.Write('\0');
                        w.Write('\0');

                        // COMM frame specifics
                        if (TagData.TAG_FIELD_COMMENT == frameType)
                        {
                            // Encoding
                            w.Write(encodeID3v2CharEncoding(FEncoding));
                            // Language ID (ISO-639-2)
                            w.Write("eng".ToCharArray()); // TODO : handle this field dynamically
                            // Short content description
                            w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically
                        }

                        // POPM frame specifics
                        if (TagData.TAG_FIELD_RATING == frameType)
                        {
                            // User e-mail
                            w.Write('\0'); // Empty string, null-terminated; TODO : handle this field dynamically
                            // ID3v2 rating : scale 0 to 255
                            w.Write((byte)Math.Max(255, Int32.Parse(map[frameType]) * 51));
                            // Play count
                            w.Write((int)0); // TODO : handle this field dynamically. Warning : may be longer than 32 bits (see specs)

                            writeFieldValue = false;
                        }


                        if (writeFieldValue) w.Write(map[frameType].ToCharArray());

                        finalFramePos = w.BaseStream.Position;
                        w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
                        w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - 6))); // 6  = frame size (4) + frame flags (2)
                        w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);

                        break;
                    }
                }
            }

            // Unsupported textual fields
            foreach (String key in unsupportedTagFields.Keys)
            {
                w.Write(key.ToCharArray());
                frameSizePos = w.BaseStream.Position;
                w.Write((int)0);

                // TODO : handle frame flags
                w.Write('\0');
                w.Write('\0');

                w.Write(unsupportedTagFields[key].ToCharArray());

                finalFramePos = w.BaseStream.Position;
                w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - 6)));
                w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            }

            // Supported pictures
            foreach(MetaDataIOFactory.PIC_TYPE picCode in tag.Pictures.Keys)
            {
                w.Write("APIC".ToCharArray());

                frameSizePos = w.BaseStream.Position;
                w.Write((int)0);

                // TODO : handle frame flags
                w.Write('\0');
                w.Write('\0');

                // Null-terminated string = mime-type encoded in ISO-8859-1
                String mimeType = Utils.GetMimeTypeFromImageFormat(tag.Pictures[picCode].RawFormat);
                w.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(mimeType.ToCharArray())); // Force ISO-8859-1 format for mime-type
                w.Write('\0'); // String should be null-terminated

                w.Write(EncodeID3v2PictureType(picCode));

                w.Write(""); // Picture description
                w.Write('\0'); // Description should be null-terminated

                tag.Pictures[picCode].Save(w.BaseStream, tag.Pictures[picCode].RawFormat);

                finalFramePos = w.BaseStream.Position;
                w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
                w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - 4)));
                w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
            }


            // Unsupported pictures, if any detected
            if (unsupportedPictures != null)
            {
                foreach (byte unsuppPicCode in unsupportedPictures.Keys)
                {
                    w.Write("APIC".ToCharArray());

                    frameSizePos = w.BaseStream.Position;
                    w.Write((int)0);

                    // TODO : handle frame flags
                    w.Write('\0');
                    w.Write('\0');

                    // Null-terminated string = mime-type encoded in ISO-8859-1
                    String mimeType = Utils.GetMimeTypeFromImageFormat(unsupportedPictures[unsuppPicCode].RawFormat);
                    w.Write(Encoding.GetEncoding("ISO-8859-1").GetBytes(mimeType.ToCharArray())); // Force ISO-8859-1 format for mime-type
                    w.Write('\0'); // String should be null-terminated

                    w.Write(unsuppPicCode);

                    w.Write(""); // Picture description
                    w.Write('\0'); // Description should be null-terminated

                    unsupportedPictures[unsuppPicCode].Save(w.BaseStream, unsupportedPictures[unsuppPicCode].RawFormat);

                    finalFramePos = w.BaseStream.Position;
                    w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
                    w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalFramePos - frameSizePos - 4)));
                    w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
                }
            }

            // Note : ATL does not write any ID3v2 footer

            // TODO : Handle unsynchronization here (on all the written data)

            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeSynchSafeInt32((int)(finalTagPos - tagSizePos - 4)));

            return result;
        }
    }
}