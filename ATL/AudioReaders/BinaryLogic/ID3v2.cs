using System;
using System.IO;
using System.Text;
using ATL.Logging;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Commons;

namespace ATL.AudioReaders.BinaryLogic
{
    /// <summary>
    /// Class for ID3v2.2-2.4 tags manipulation
    /// </summary>
    public class TID3v2 : MetaDataReader
    {
        public const byte TAG_VERSION_2_2 = 2;             // Code for ID3v2.2.x tag
        public const byte TAG_VERSION_2_3 = 3;             // Code for ID3v2.3.x tag
        public const byte TAG_VERSION_2_4 = 4;             // Code for ID3v2.4.x tag

        private String FTrackString;
        private String FDiscString;
        private StreamUtils.StreamHandlerDelegate FPictureStreamHandler;

        private String FEncoder;
        private String FLanguage;
        private String FLink;


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

        // Max. number of supported tag frames
        private const byte ID3V2_FRAME_COUNT = 18;

        // Names of supported tag frames (ID3v2.3.x & ID3v2.4.x)
        // TODO convert that to hashtables
        private static String[] ID3V2_FRAME_NEW = new String[ID3V2_FRAME_COUNT]
	{
		"TIT2", "TPE1", "TALB", "TRCK", "TYER", "TCON", "COMM", "TCOM", "TENC",
		"TCOP", "TLAN", "WXXX", "TDRC", "TOPE", "TIT1", "TOAL", "TPOS", "POPM" };

        // Names of supported tag frames (ID3v2.2.x)
        private static String[] ID3V2_FRAME_OLD = new String[ID3V2_FRAME_COUNT]
		{
			"TT2", "TP1", "TAL", "TRK", "TYE", "TCO", "COM", "TCM", "TEN",
			"TCR", "TLA", "WXX", "TOR", "TOA", "TT1", "TOT", "TPA", "POP" };

        // Max. tag size for saving
        private const int ID3V2_MAX_SIZE = 4096;

        // Unicode ID
        public const char UNICODE_ID = (char)0x1;

        // Frame header (ID3v2.3.x & ID3v2.4.x)
        private class FrameHeaderNew
        {
            public char[] ID = new char[4];                                // Frame ID
            public int Size;                                  // Size excluding header
            public ushort Flags;											  // Flags
        }

        // Frame header (ID3v2.2.x)
        private class FrameHeaderOld
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

            public String[] Frame = new String[ID3V2_FRAME_COUNT];
        }

        // Unicode BOM properties
        private class BOMProperties
        {
            public int Size = 0;                // Size of BOM
            public Encoding Encoding;           // Corresponding encoding
        }

        // ********************* Auxiliary functions & voids ********************

        private bool ReadHeader(String FileName, ref TagInfo Tag)
        {
            FileStream fs = null;
            BinaryReader SourceFile = null;
            bool result = false;

            try
            {
                // Open file, read first block of data and search for a frame		  
                fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                SourceFile = new BinaryReader(fs);

                return ReadHeader(SourceFile, ref Tag, 0);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FileName + ")");
                result = false;
            }

            if (SourceFile != null) SourceFile.Close();
            if (fs != null) fs.Close();

            return result;
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

            //BlockRead(SourceFile, Tag, 10, Transferred);

            Tag.FileSize = SourceFile.BaseStream.Length;

            return result;
        }

        // ---------------------------------------------------------------------------

        private int GetTagSize(TagInfo Tag)
        {
            // Get total tag size
            int result = StreamUtils.ExtractSynchSafeInt32(Tag.Size) + 10;

            if (Tag.HasFooter) result += 10; // Indicates the presence of a footer (ID3v2.4+)
            if (result > Tag.FileSize) result = 0;

            return result;
        }

        // ---------------------------------------------------------------------------

        private void SetTagItem(String ID, String Data, ref TagInfo Tag)
        {
            String FrameID;

            // Set tag item if supported frame found
            for (int iterator = 0; iterator < ID3V2_FRAME_COUNT; iterator++)
            {
                if (Tag.Version > TAG_VERSION_2_2)
                    FrameID = ID3V2_FRAME_NEW[iterator];
                else
                    FrameID = ID3V2_FRAME_OLD[iterator];

                if (ID == FrameID)
                {
                    // Only stores first occurence of a tag
                    if (null == Tag.Frame[iterator]) Tag.Frame[iterator] = Data;
                }
            }
        }

        // Get information from frames (ID3v2.3.x & ID3v2.4.x : frame identifier has 4 characters)
        private void ReadFramesNew(BinaryReader SourceFile, ref TagInfo Tag, long offset)
        {
            Stream fs = SourceFile.BaseStream;
            FrameHeaderNew Frame = new FrameHeaderNew();
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
                    Frame.Size = StreamUtils.ExtractSynchSafeInt32(size);
                }
                Frame.Flags = StreamUtils.ReverseUInt16(SourceFile.ReadUInt16());

                if (!(Char.IsLetter(Frame.ID[0]) && Char.IsUpper(Frame.ID[0]))) break;

                DataSize = Frame.Size - 1; // Minus encoding byte

                // Skips data size indicator if signaled by the flag
                if ((Frame.Flags & 1) > 0)
                {
                    fs.Seek(4, SeekOrigin.Current);
                    DataSize = DataSize - 4;
                }

                // Encoding convention, according to ID3v2.4 specs
                int encoding = fs.ReadByte();
                // Default encoding should be "ISO-8859-1"
                // Warning : due to approximative implementations, some tags seem to be coded
                // with the default encoding of the OS they have been tagged on
                Encoding encodingConvention = Encoding.GetEncoding("ISO-8859-1");
                //Encoding encodingConvention = Encoding.Default;

                if (1 == encoding) encodingConvention = Encoding.Unicode; // UTF-16 with BOM
                else if (2 == encoding) encodingConvention = Encoding.BigEndianUnicode; // UTF-16 Big Endian without BOM (since ID3v2.4)
                else if (3 == encoding) encodingConvention = Encoding.UTF8; // UTF-8 (since ID3v2.4)

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
                    if (1 == encoding)
                    {
                        contentDescriptionBOM = readBOM(ref fs);
                    }

                    if (contentDescriptionBOM.Size <= 3)
                    {
                        // Skip content description
                        StreamUtils.ReadNullTerminatedString(SourceFile, encoding);
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
                if ( 1 == encoding )
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
                        encodingConvention = bom.Encoding;
                        DataSize = DataSize - bom.Size;
                    }
                }
                // If encoding > 3, we might have caught an actual character, which means there is no encoding flag
                else if (encoding > 3) { fs.Seek(-1, SeekOrigin.Current); DataSize++; }

                // Note data position and determine significant data size
                DataPosition = fs.Position;

                if ((DataSize > 0) && (DataSize < 500))
                {
                    // Read frame data and set tag item if frame supported
                    // Specific to Popularitymeter : Rating data has to be extracted from the POPM block
                    if (StreamUtils.StringEqualsArr("POPM", Frame.ID))
                    {
                        strData = extractRatingFromPopularityMeter(SourceFile, 0).ToString();
                    }
                    else
                    {
                        byte[] bData = new byte[DataSize];
                        // Read frame data and set tag item if frame supported
                        bData = SourceFile.ReadBytes((int)DataSize);

                        strData = encodingConvention.GetString(bData);
                    }

                    if (32768 != (Frame.Flags & 32768)) SetTagItem(new String(Frame.ID), strData, ref Tag); // Wipe out \0's to avoid string cuts
                }
                else if (DataSize > 0) // Size > 500 => Probably an embedded picture
                {
                    long Position = fs.Position;
                    if (StreamUtils.StringEqualsArr("APIC",Frame.ID))
                    {
                        // mime-type always coded in ASCII
                        if (1 == encoding) fs.Seek(-1, SeekOrigin.Current);
                        String mimeType = StreamUtils.ReadNullTerminatedString(SourceFile, 0);
                        FPictures.Add(ReadAPICPictureType(SourceFile,8));

                        // However, description can be coded with another convention
                        if (1 == encoding)
                        {
                            readBOM(ref fs);
                        }
                        String description = StreamUtils.ReadNullTerminatedString(SourceFile, encoding);
                        if (FPictureStreamHandler != null)
                        {
                            int picSize = (int)(DataSize - (fs.Position - Position));
                            MemoryStream mem = new MemoryStream(picSize);
                            if (Tag.UsesUnsynchronisation)
                            {
                                CopyUnsynchronizedStream(mem, SourceFile, picSize);
                            }
                            else
                            {
                                StreamUtils.CopyStreamFrom(mem, SourceFile, picSize);
                            }
                            FPictureStreamHandler(ref mem);
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
            FrameHeaderOld Frame = new FrameHeaderOld();
            char[] Data = new char[500];
            long DataPosition;
            int FrameSize;
            int DataSize;

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
                        Data = extractRatingFromPopularityMeter(SourceFile, 0).ToString().ToCharArray();
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
                        byte textEncoding = SourceFile.ReadByte();
                        String imageFormat = new String(StreamUtils.ReadOneByteChars(SourceFile, 3));
                        FPictures.Add(ReadAPICPictureType(SourceFile, 8));
                        String description = StreamUtils.ReadNullTerminatedString(SourceFile, textEncoding);
                        if (FPictureStreamHandler != null)
                        {
                            int picSize = (int)(DataSize - (fs.Position - Position));
                            MemoryStream mem = new MemoryStream(picSize);

                            if (Tag.UsesUnsynchronisation)
                            {
                                CopyUnsynchronizedStream(mem, SourceFile, picSize);
                            }
                            else
                            {
                                StreamUtils.CopyStreamFrom(mem, SourceFile, picSize);
                            }

                            FPictureStreamHandler(ref mem);
                        }
                    }
                    fs.Seek(Position + DataSize, SeekOrigin.Begin);
                }
            }
        }

        // ---------------------------------------------------------------------------

        private String GetContent(String Content1, String Content2)
        {
            String result = Content1;
            if ((null == result) || (0 == result.Trim().Length)) result = Utils.ProtectValue(Content2).Trim();

            return result;
        }

        // ********************** Public functions & voids **********************

        public TID3v2()
        {
            ResetData();
        }

        // ---------------------------------------------------------------------------

        public override bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            return Read(source, pictureStreamHandler, 0);
        }

        public bool Read(BinaryReader source, StreamUtils.StreamHandlerDelegate pictureStreamHandler, long offset)
        {
            TagInfo Tag = new TagInfo();
            this.FPictureStreamHandler = pictureStreamHandler;

            // Reset data and load header from file to variable
            ResetData();
            bool result = ReadHeader(source, ref Tag, offset);

            // Process data if loaded and header valid
            if ((result) && StreamUtils.StringEqualsArr(ID3V2_ID, Tag.ID))
            {
                FExists = true;
                // Fill properties with header data
                FVersion = Tag.Version;
                FSize = GetTagSize(Tag);
                // Get information from frames if version supported
                if ((TAG_VERSION_2_2 <= FVersion) && (FVersion <= TAG_VERSION_2_4) && (FSize > 0))
                {
                    if (FVersion > TAG_VERSION_2_2) ReadFramesNew(source, ref Tag, offset);
                    else ReadFrames_v22(source, ref Tag, offset);

                    FTitle = GetContent(Tag.Frame[0], Tag.Frame[14]);
                    FArtist = GetContent(Tag.Frame[1], Tag.Frame[13]);
                    FAlbum = GetContent(Tag.Frame[2], Tag.Frame[15]);
                    FTrack = TrackUtils.ExtractTrackNumber(Tag.Frame[3]);
                    FTrackString = Utils.ProtectValue(Tag.Frame[3]);
                    FYear = TrackUtils.ExtractStrYear(Tag.Frame[4]);
                    if (0 == FYear.Length) FYear = TrackUtils.ExtractStrYear(Tag.Frame[12]);
                    FGenre = extractGenre(Tag.Frame[5]);
                    FComment = Utils.ProtectValue(Tag.Frame[6]);
                    FComposer = Utils.ProtectValue(Tag.Frame[7]);
                    FEncoder = Utils.ProtectValue(Tag.Frame[8]);
                    FCopyright = Utils.ProtectValue(Tag.Frame[9]);
                    FLanguage = Utils.ProtectValue(Tag.Frame[10]);
                    FLink = Utils.ProtectValue(Tag.Frame[11]);
                    FDisc = TrackUtils.ExtractTrackNumber(Tag.Frame[16]);
                    FDiscString = Utils.ProtectValue(Tag.Frame[16]);
                    if (Utils.ProtectValue(Tag.Frame[17]).Length > 0) FRating = TrackUtils.ExtractIntRating(Byte.Parse(Tag.Frame[17]));
                }
            }

            return result;
        }

        // ---------------------------------------------------------------------------

        // Specific to ID3v2 : extract genre from string
        private String extractGenre(String iGenre)
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

            if (("" == result) && (genreIndex != -1) && (genreIndex < TID3v1.MusicGenre.Length)) result = TID3v1.MusicGenre[genreIndex];

            return result;
        }

        // Specific to ID3v2 : extract numeric rating from POP/POPM block containing other useless/obsolete data
        private byte extractRatingFromPopularityMeter(BinaryReader Source, int coding)
        {
            // Skip the e-mail, which is a null-terminated string
            StreamUtils.ReadNullTerminatedString(Source, coding);

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

        public static MetaReaderFactory.PIC_CODE ReadAPICPictureType(BinaryReader Source, int coding)
        {
            int pictureType = 0;
            if (8 == coding) pictureType = Source.ReadByte();
            else if (16 == coding) pictureType = Source.ReadInt16();
            else if (32 == coding) pictureType = StreamUtils.ReverseInt32(Source.ReadInt32());

            if (3 == pictureType) return MetaReaderFactory.PIC_CODE.Front;
            else if (4 == pictureType) return MetaReaderFactory.PIC_CODE.Back;
            else if (6 == pictureType) return MetaReaderFactory.PIC_CODE.CD;
            else return MetaReaderFactory.PIC_CODE.Generic;
        }

        // Copies the stream while cleaning abnormalities du to unsynchronization
        // => 0xff 0x00 => 0xff
        protected static void CopyUnsynchronizedStream(Stream mTo, BinaryReader r, long length)
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

    }
}