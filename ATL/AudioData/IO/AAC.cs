using System;
using System.IO;
using ATL.Logging;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Advanced Audio Coding files manipulation (extensions : .AAC, .MP4, .M4A)
    /// 
    /// Implementation notes
    /// 
    ///     1. Tag edition optimization through the use of padding frames
    /// 
    ///     Current implementation doesn't use the extra space allocated by 'free' padding frames, and pulls/pushes the 'mdat' frame regardless of the size of the edited data.
    ///     A faster, more optimized way of doing things would be to use padding space as far as edited data size fits into it, thus preventing the entire file to be rewritten.
    ///     
    ///     2. LATM and LOAS/LATM support is missing
    ///     
    ///     3. MP4 files with their 'mdat' atom located before their 'moov' atom have not been tested
    ///     
    ///     4. MP4 files with multiple 'trak' are not supported yet. Hence, when the audio track is not located first, it is not detected properly.
    /// 
    /// </summary>
	class AAC : MetaDataIO, IAudioDataIO
    {

        // Header type codes

        public const byte AAC_HEADER_TYPE_UNKNOWN = 0;                       // Unknown
        public const byte AAC_HEADER_TYPE_ADIF = 1;                          // ADIF
        public const byte AAC_HEADER_TYPE_ADTS = 2;                          // ADTS
        public const byte AAC_HEADER_TYPE_MP4 = 3;                          // MP4

        // Header type names
        public static string[] AAC_HEADER_TYPE = { "Unknown", "ADIF", "ADTS" };

        // MPEG version codes
        public const byte AAC_MPEG_VERSION_UNKNOWN = 0;                      // Unknown
        public const byte AAC_MPEG_VERSION_2 = 1;                            // MPEG-2
        public const byte AAC_MPEG_VERSION_4 = 2;                            // MPEG-4

        // MPEG version names
        public static string[] AAC_MPEG_VERSION = { "Unknown", "MPEG-2", "MPEG-4" };

        // Profile codes
        public const byte AAC_PROFILE_UNKNOWN = 0;                           // Unknown
        public const byte AAC_PROFILE_MAIN = 1;                              // Main
        public const byte AAC_PROFILE_LC = 2;                                // LC
        public const byte AAC_PROFILE_SSR = 3;                               // SSR
        public const byte AAC_PROFILE_LTP = 4;                               // LTP

        // Profile names
        public static string[] AAC_PROFILE =
        { "Unknown", "AAC Main", "AAC LC", "AAC SSR", "AAC LTP" };

        // Bit rate type codes
        public const byte AAC_BITRATE_TYPE_UNKNOWN = 0;                      // Unknown
        public const byte AAC_BITRATE_TYPE_CBR = 1;                          // CBR
        public const byte AAC_BITRATE_TYPE_VBR = 2;                          // VBR

        // Bit rate type names
        public static string[] AAC_BITRATE_TYPE = { "Unknown", "CBR", "VBR" };

        // Sample rate values
        private static int[] SAMPLE_RATE = {    96000, 88200, 64000, 48000, 44100, 32000,
                                                24000, 22050, 16000, 12000, 11025, 8000,
                                                0, 0, 0, 0 };

        private static Dictionary<string, byte> frameMapping_mp4; // Mapping between MP4 frame codes and ATL frame codes
        private static Dictionary<string, byte> frameClasses_mp4; // Mapping between MP4 frame codes and frame classes that aren't class 1 (UTF-8 text)

        private int FTotalFrames;
        private byte FHeaderTypeID;
        private byte FMPEGVersionID;
        private byte FProfileID;
        private byte FChannels;
        private byte FBitrateTypeID;
        private byte FVersionID;

        private double bitrate;
        private double duration;
        private int sampleRate;

        private AudioDataManager.SizeInfo sizeInfo;
        private string fileName;

        // List of all atoms whose size to rewrite after editing metadata
        private class ValueInfo
        {
            public ulong Position;
            public ulong Value;
            public byte NbBytes;

            public ValueInfo(ulong position, ulong value, byte nbBytes = 4)
            {
                Position = position; Value = value; NbBytes = nbBytes;
            }
        }
        private IList<ValueInfo> upperAtoms;


        public byte VersionID // Version code
        {
            get { return this.FVersionID; }
        }
        public byte HeaderTypeID // Header type code
        {
            get { return this.FHeaderTypeID; }
        }
        public String HeaderType // Header type name
        {
            get { return this.getHeaderType(); }
        }
        public byte MPEGVersionID // MPEG version code
        {
            get { return this.FMPEGVersionID; }
        }
        public String MPEGVersion // MPEG version name
        {
            get { return this.getMPEGVersion(); }
        }
        public byte ProfileID // Profile code
        {
            get { return this.FProfileID; }
        }
        public String Profile // Profile name
        {
            get { return this.getProfile(); }
        }
        public byte Channels // Number of channels
        {
            get { return this.FChannels; }
        }
        public byte BitRateTypeID // Bit rate type code
        {
            get { return this.FBitrateTypeID; }
        }
        public String BitRateType // Bit rate type name
        {
            get { return this.getBitRateType(); }
        }
        public bool Valid // true if data valid
        {
            get { return this.isValid(); }
        }

        public bool IsVBR
        {
            get { return (AAC_BITRATE_TYPE_VBR == FBitrateTypeID); }
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSY; }
        }
        public bool AllowsParsableMetadata
        {
            get { return true; }
        }
        public double BitRate
        {
            get { return bitrate/1000.0; }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }
        public string FileName
        {
            get { return fileName; }
        }

        public override byte[] CoreSignature
        {
            get
            {
                return new byte[] { 0, 0, 0, 8, 105, 108, 115, 116 }; // (int32)8 followed by "ilst" field code
            }
        }

        public override byte FieldCodeFixedLength
        {
            get
            {
                return 4;
            }
        }


        // ===== CONTRUCTORS

        public AAC(string fileName)
        {
            this.fileName = fileName;
            resetData();
        }

        static AAC()
        {
            frameMapping_mp4 = new Dictionary<string, byte>
            {
                { "©nam", TagData.TAG_FIELD_TITLE },
                { "titl", TagData.TAG_FIELD_TITLE },
                { "©alb", TagData.TAG_FIELD_ALBUM },
                { "©art", TagData.TAG_FIELD_ARTIST },
                { "©ART", TagData.TAG_FIELD_ARTIST },
                { "©cmt", TagData.TAG_FIELD_COMMENT },
                { "©day", TagData.TAG_FIELD_RECORDING_YEAR },
                { "©gen", TagData.TAG_FIELD_GENRE },
                { "gnre", TagData.TAG_FIELD_GENRE },
                { "trkn", TagData.TAG_FIELD_TRACK_NUMBER },
                { "disk", TagData.TAG_FIELD_DISC_NUMBER },
                { "rtng", TagData.TAG_FIELD_RATING },
                { "©wrt", TagData.TAG_FIELD_COMPOSER },
                { "desc", TagData.TAG_FIELD_GENERAL_DESCRIPTION },
                { "cprt", TagData.TAG_FIELD_COPYRIGHT },
                { "aART", TagData.TAG_FIELD_ALBUM_ARTIST },
                { "----:com.apple.iTunes:CONDUCTOR", TagData.TAG_FIELD_CONDUCTOR }
            };

            frameClasses_mp4 = new Dictionary<string, byte>
            {
                { "gnre", 0 },
                { "trkn", 0 },
                { "disk", 0 },
                { "rtng", 21 },
                { "tmpo", 21 },
                { "cpil", 21 },
                { "stik", 21 },
                { "pcst", 21 },
                { "purl", 0 },
                { "egid", 0 },
                { "tvsn", 21 },
                { "tves", 21 },
                { "pgap", 21 }
            };
        }


        // ********************** Private functions & procedures *********************

        // Reset all variables
        private void resetData()
        {
            ResetData();
            FHeaderTypeID = AAC_HEADER_TYPE_UNKNOWN;
            FMPEGVersionID = AAC_MPEG_VERSION_UNKNOWN;
            FProfileID = AAC_PROFILE_UNKNOWN;
            FChannels = 0;
            FBitrateTypeID = AAC_BITRATE_TYPE_UNKNOWN;
            FTotalFrames = 0;

            bitrate = 0;
            sampleRate = 0;
            duration = 0;

            FVersionID = 0;
        }

        // ---------------------------------------------------------------------------

        // Get header type name
        private string getHeaderType()
        {
            return AAC_HEADER_TYPE[FHeaderTypeID];
        }

        // ---------------------------------------------------------------------------

        // Get MPEG version name
        private string getMPEGVersion()
        {
            return AAC_MPEG_VERSION[FMPEGVersionID];
        }

        // ---------------------------------------------------------------------------

        // Get profile name
        private string getProfile()
        {
            return AAC_PROFILE[FProfileID];
        }

        // ---------------------------------------------------------------------------

        // Get bit rate type name
        private string getBitRateType()
        {
            return AAC_BITRATE_TYPE[FBitrateTypeID];
        }

        // ---------------------------------------------------------------------------

        // Calculate duration time
        private double getDuration()
        {
            if (FHeaderTypeID == AAC_HEADER_TYPE_MP4)
            {
                return duration;
            }
            else
            {
                if (0 == bitrate)
                    return 0;
                else
                    return 8.0 * (sizeInfo.FileSize - sizeInfo.ID3v2Size) / bitrate;
            }
        }

        // ---------------------------------------------------------------------------

        // Check for file correctness
        private bool isValid()
        {
            return ((FHeaderTypeID != AAC_HEADER_TYPE_UNKNOWN) &&
                (FChannels > 0) && (sampleRate > 0) && (bitrate > 0));
        }

        // ---------------------------------------------------------------------------

        // Get header type of the file
        private byte recognizeHeaderType(BinaryReader Source)
        {
            byte result;
            byte[] header;
            string headerStr;

            result = AAC_HEADER_TYPE_UNKNOWN;
            Source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            header = Source.ReadBytes(4);
            headerStr = Utils.Latin1Encoding.GetString(header);

            if ("ADIF".Equals(headerStr))
            {
                result = AAC_HEADER_TYPE_ADIF;
            }
            else if ((0xFF == header[0]) && (0xF0 == ((header[0]) & 0xF0)))
            {
                result = AAC_HEADER_TYPE_ADTS;
            }
            else
            {
                headerStr = Utils.Latin1Encoding.GetString(Source.ReadBytes(4)); // bytes 4 to 8
                if ("ftyp".Equals(headerStr))
                {
                    result = AAC_HEADER_TYPE_MP4;
                }
            }
            return result;
        }

        // ---------------------------------------------------------------------------

        // Read ADIF header data
        private void readADIF(BinaryReader Source)
        {
            int Position;

            Position = (int)(sizeInfo.ID3v2Size * 8 + 32);
            if (0 == StreamUtils.ReadBits(Source, Position, 1)) Position += 3;
            else Position += 75;
            if (0 == StreamUtils.ReadBits(Source, Position, 1)) FBitrateTypeID = AAC_BITRATE_TYPE_CBR;
            else FBitrateTypeID = AAC_BITRATE_TYPE_VBR;

            Position++;

            bitrate = (int)StreamUtils.ReadBits(Source, Position, 23);

            if (AAC_BITRATE_TYPE_CBR == FBitrateTypeID) Position += 51;
            else Position += 31;

            FMPEGVersionID = AAC_MPEG_VERSION_4;
            FProfileID = (byte)(StreamUtils.ReadBits(Source, Position, 2) + 1);
            Position += 2;

            sampleRate = SAMPLE_RATE[StreamUtils.ReadBits(Source, Position, 4)];
            Position += 4;
            FChannels += (byte)StreamUtils.ReadBits(Source, Position, 4);
            Position += 4;
            FChannels += (byte)StreamUtils.ReadBits(Source, Position, 4);
            Position += 4;
            FChannels += (byte)StreamUtils.ReadBits(Source, Position, 4);
            Position += 4;
            FChannels += (byte)StreamUtils.ReadBits(Source, Position, 2);
        }

        // ---------------------------------------------------------------------------

        // Read ADTS header data
        private void readADTS(BinaryReader Source)
        {
            int Frames = 0;
            int TotalSize = 0;
            int Position;

            do
            {
                Frames++;
                Position = (int)(sizeInfo.ID3v2Size + TotalSize) * 8;

                if (StreamUtils.ReadBits(Source, Position, 12) != 0xFFF) break;

                Position += 12;

                if (0 == StreamUtils.ReadBits(Source, Position, 1))
                    FMPEGVersionID = AAC_MPEG_VERSION_4;
                else
                    FMPEGVersionID = AAC_MPEG_VERSION_2;

                Position += 4;
                FProfileID = (byte)(StreamUtils.ReadBits(Source, Position, 2) + 1);
                Position += 2;

                sampleRate = SAMPLE_RATE[StreamUtils.ReadBits(Source, Position, 4)];
                Position += 5;

                FChannels = (byte)StreamUtils.ReadBits(Source, Position, 3);

//                if (AAC_MPEG_VERSION_4 == FMPEGVersionID)
//                    Position += 9;
//                else
                    Position += 7;

                TotalSize += (int)StreamUtils.ReadBits(Source, Position, 13);
                Position += 13;

                if (0x7FF == StreamUtils.ReadBits(Source, Position, 11))
                    FBitrateTypeID = AAC_BITRATE_TYPE_VBR;
                else
                    FBitrateTypeID = AAC_BITRATE_TYPE_CBR;

                if (AAC_BITRATE_TYPE_CBR == FBitrateTypeID) break;
            }
            while (Source.BaseStream.Length > sizeInfo.ID3v2Size + TotalSize);
            FTotalFrames = Frames;
            bitrate = (int)Math.Round(8 * TotalSize / 1024.0 / Frames * sampleRate);
        }

        // Read MP4 header data
        // http://www.jiscdigitalmedia.ac.uk/guide/aac-audio-and-the-mp4-media-format
        // http://atomicparsley.sourceforge.net/mpeg-4files.html
        // - Metadata is located in the moov/udta/meta/ilst atom
        // - Physical information are located in the moov/trak atom (to be confirmed ?)
        // - Binary physical data are located in the mdat atom
        //
        private void readMP4(BinaryReader Source, MetaDataIO.ReadTagParams readTagParams)
        {
            long iListSize = 0;
            long iListPosition = 0;
            uint metadataSize = 0;
            byte dataClass = 0;

            ushort int16Data = 0;
            uint int32Data = 0;

            string strData = "";
            uint atomSize;
            long atomPosition;
            string atomHeader;

            if (readTagParams.PrepareForWriting) upperAtoms = new List<ValueInfo>();

            Source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // FTYP atom
            atomSize = StreamUtils.ReverseUInt32(Source.ReadUInt32());
            Source.BaseStream.Seek(atomSize - 4, SeekOrigin.Current);

            // MOOV atom
            atomSize = lookForMP4Atom(Source, "moov"); // === Physical data
            long moovPosition = Source.BaseStream.Position;
            if (readTagParams.PrepareForWriting) upperAtoms.Add( new ValueInfo((ulong)(Source.BaseStream.Position - 8), atomSize) );

            lookForMP4Atom(Source, "mvhd"); // === Physical data
            byte version = Source.ReadByte();
            Source.BaseStream.Seek(3, SeekOrigin.Current); // 3-byte flags
            if (1 == version) Source.BaseStream.Seek(16, SeekOrigin.Current); else Source.BaseStream.Seek(8, SeekOrigin.Current);

            int timeScale = StreamUtils.ReverseInt32(Source.ReadInt32());
            ulong timeLengthPerSec;
            if (1 == version) timeLengthPerSec = StreamUtils.ReverseUInt64(Source.ReadUInt64()); else timeLengthPerSec = StreamUtils.ReverseUInt32(Source.ReadUInt32());
            duration = timeLengthPerSec * 1.0 / timeScale;

            Source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            // TODO : handle files with multiple trak Atoms (loop through them)
            lookForMP4Atom(Source, "trak");
            lookForMP4Atom(Source, "mdia");
            lookForMP4Atom(Source, "minf");
            lookForMP4Atom(Source, "stbl");
            long stblPosition = Source.BaseStream.Position;

            // Look for sample rate
            lookForMP4Atom(Source, "stsd");
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            uint nbDescriptions = StreamUtils.ReverseUInt32(Source.ReadUInt32());

            for (int i = 0; i < nbDescriptions; i++)
            {
                int32Data = StreamUtils.ReverseUInt32(Source.ReadUInt32()); // 4-byte description length
                string descFormat = Utils.Latin1Encoding.GetString(Source.ReadBytes(4));

                if (descFormat.Equals("mp4a") || descFormat.Equals("enca") || descFormat.Equals("samr") || descFormat.Equals("sawb"))
                {
                    Source.BaseStream.Seek(4, SeekOrigin.Current); // 6-byte reserved zone set to zero

                    Source.BaseStream.Seek(10, SeekOrigin.Current); // Not useful here

                    FChannels = (byte)StreamUtils.ReverseUInt16(Source.ReadUInt16()); // Audio channels

                    Source.BaseStream.Seek(2, SeekOrigin.Current); // Sample size
                    Source.BaseStream.Seek(4, SeekOrigin.Current); // Quicktime stuff

                    sampleRate = StreamUtils.ReverseInt32(Source.ReadInt32());
                }
                else
                {
                    Source.BaseStream.Seek(int32Data - 4, SeekOrigin.Current);
                }
            }

            // VBR detection : if the gap between the smallest and the largest sample size is no more than 1%, we can consider the file is CBR; if not, VBR
            Source.BaseStream.Seek(stblPosition, SeekOrigin.Begin);
            lookForMP4Atom(Source, "stsz");
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
            int blocByteSizeForAll = StreamUtils.ReverseInt32(Source.ReadInt32());
            if (0 == blocByteSizeForAll) // If value other than 0, same size everywhere => CBR
            {
                uint nbSizes = StreamUtils.ReverseUInt32(Source.ReadUInt32());
                uint max = 0;
                uint min = UInt32.MaxValue;
                for (int i = 0; i < nbSizes; i++)
                {
                    int32Data = StreamUtils.ReverseUInt32(Source.ReadUInt32());
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

            // "Physical" audio chunks are referenced by position (offset) in  moov.trak.mdia.minf.stbl.stco / co64
            // => They have to be rewritten if the position (offset) of the 'mdat' atom changes
            if (readTagParams.PrepareForWriting)
            {
                atomPosition = Source.BaseStream.Position;
                byte nbBytes = 0;
                uint nbChunkOffsets = 0;
                ulong value;
                try
                {
                    lookForMP4Atom(Source, "stco"); // Chunk offsets
                    nbBytes = 4;
                } catch (Exception)
                {
                    Source.BaseStream.Seek(atomPosition, SeekOrigin.Begin);
                    lookForMP4Atom(Source, "co64");
                    nbBytes = 8;
                }
                Source.BaseStream.Seek(4, SeekOrigin.Current); // Flags
                nbChunkOffsets = StreamUtils.ReverseUInt32( Source.ReadUInt32() );
                for (int i = 0; i < nbChunkOffsets; i++)
                {
                    if (4 == nbBytes) value = StreamUtils.ReverseUInt32( Source.ReadUInt32() ); else value = StreamUtils.ReverseUInt64( Source.ReadUInt64() );
                    upperAtoms.Add(new ValueInfo((ulong)(Source.BaseStream.Position-nbBytes),value,nbBytes));
                }
            }

            Source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            atomSize = lookForMP4Atom(Source, "udta");
            if (readTagParams.PrepareForWriting) upperAtoms.Add(new ValueInfo((ulong)(Source.BaseStream.Position - 8), atomSize));
            atomSize = lookForMP4Atom(Source, "meta");
            if (readTagParams.PrepareForWriting) upperAtoms.Add(new ValueInfo((ulong)(Source.BaseStream.Position - 8), atomSize));
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags

            if (readTagParams.ReadTag)
            {
                atomPosition = Source.BaseStream.Position;
                atomSize = lookForMP4Atom(Source, "hdlr"); // Metadata handler
                long hdlrPosition = Source.BaseStream.Position - 8;
                Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                Source.BaseStream.Seek(4, SeekOrigin.Current); // Quicktime type
                strData = Utils.Latin1Encoding.GetString(Source.ReadBytes(4)); // Meta data type

                if (!strData.Equals("mdir"))
                {
                    string errMsg = "ATL does not support ";
                    if (strData.Equals("mp7t")) errMsg += "MPEG-7 XML metadata";
                    else if (strData.Equals("mp7b")) errMsg += "MPEG-7 binary XML metadata";
                    else errMsg = "Unrecognized metadata format";

                    throw new NotSupportedException(errMsg);
                }
                Source.BaseStream.Seek(atomSize+ hdlrPosition, SeekOrigin.Begin); // Reach the end of the hdlr box

                iListSize = lookForMP4Atom(Source, "ilst"); // === Metadata list
                tagOffset = Source.BaseStream.Position - 8;

                tagSize = (int)iListSize;
                if (8 == tagSize) // Core minimal size
                {
                    tagExists = false;
                    return;
                } else
                {
                    tagExists = true;
                }

                // Browse all metadata
                while (iListPosition < iListSize - 8)
                {
                    atomSize = StreamUtils.ReverseUInt32(Source.ReadUInt32());
                    atomHeader = Utils.Latin1Encoding.GetString(Source.ReadBytes(4));

                    if ("----".Equals(atomHeader)) // Custom text metadata
                    {
                        metadataSize = lookForMP4Atom(Source, "mean"); // "issuer" of the field
                        Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                        atomHeader += ":" + Utils.Latin1Encoding.GetString(Source.ReadBytes((int)metadataSize - 8 - 4));

                        metadataSize = lookForMP4Atom(Source, "name"); // field type
                        Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags
                        atomHeader += ":" + Utils.Latin1Encoding.GetString(Source.ReadBytes((int)metadataSize - 8 - 4));
                    }

                    // Having a 'data' header here means we're still on the same field, with a 2nd value
                    // (e.g. multiple embedded pictures)
                    if (!"data".Equals(atomHeader))
                    {
                        metadataSize = lookForMP4Atom(Source, "data");
                        atomPosition = Source.BaseStream.Position - 8;
                    } else
                    {
                        metadataSize = atomSize;
                    }

                    // We're only looking for the last byte of the flag
                    Source.BaseStream.Seek(3, SeekOrigin.Current);
                    dataClass = Source.ReadByte();

                    // 4-byte NULL space
                    Source.BaseStream.Seek(4, SeekOrigin.Current);

                    if (1 == dataClass) // UTF-8 Text
                    {
                        strData = Encoding.UTF8.GetString(Source.ReadBytes((int)metadataSize - 16));
                        setMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                    }
                    else if (21 == dataClass) // uint8
                    {
                        int16Data = Source.ReadByte();
//                        Source.BaseStream.Seek(atomPosition+metadataSize, SeekOrigin.Begin); // The rest are padding bytes
                        setMetaField(atomHeader, int16Data.ToString(), readTagParams.ReadAllMetaFrames);
                    }
                    else if (13 == dataClass || 14 == dataClass) // JPEG/PNG picture -- TODO what if a BMP or GIF picture was embedded ?
                    {
                        TagData.PIC_TYPE picType = TagData.PIC_TYPE.Generic;

                        int picturePosition;
                        addPictureToken(picType);
                        picturePosition = takePicturePosition(picType);

                        if (readTagParams.PictureStreamHandler != null)
                        {
                            ImageFormat imgFormat = ImageFormat.Png; // PNG or JPEG according to specs

                            // Peek the next 3 bytes to know the picture type
                            byte[] data = Source.ReadBytes(3);
                            Source.BaseStream.Seek(-3, SeekOrigin.Current);
                            if (0xFF == data[0] && 0xD8 == data[1] && 0xFF == data[2]) imgFormat = ImageFormat.Jpeg; // JPEG signature

                            MemoryStream mem = new MemoryStream((int)metadataSize - 16);
                            StreamUtils.CopyStream(Source.BaseStream, mem, metadataSize - 16);
                            readTagParams.PictureStreamHandler(ref mem, picType, imgFormat, MetaDataIOFactory.TAG_NATIVE, dataClass, picturePosition);
                            mem.Close();
                        }
                        else
                        {
//                            Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                        }
                    }
                    else if (0 == dataClass) // Special cases : gnre, trkn, disk
                    {
                        if ("trkn".Equals(atomHeader) || "disk".Equals(atomHeader))
                        {
                            Source.BaseStream.Seek(2, SeekOrigin.Current);
                            int16Data = StreamUtils.ReverseUInt16( Source.ReadUInt16() );
                            Source.BaseStream.Seek(2, SeekOrigin.Current); // Total number of tracks/discs is on the following 2 bytes; ignored for now
                            setMetaField(atomHeader, int16Data.ToString(), readTagParams.ReadAllMetaFrames);
                        }
                        else if ("gnre".Equals(atomHeader)) // ©gen is a text field and doesn't belong here
                        {
                            int16Data = StreamUtils.ReverseUInt16(Source.ReadUInt16());

                            strData = "";
                            if (int16Data < ID3v1.MAX_MUSIC_GENRES) strData = ID3v1.MusicGenre[int16Data - 1];

                            setMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                        }
                        else
                        { // Other unhandled cases
  //                          Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                        }
                    }
                    else // Other unhandled cases
                    {
//                        Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                    }

                    Source.BaseStream.Seek(atomPosition + metadataSize, SeekOrigin.Begin);
                    iListPosition += atomSize;
                }
            }

            // Seek audio data segment to calculate mean bitrate 
            // NB : This figure is closer to truth than the "average bitrate" recorded in the esds/m4ds header
            Source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            uint mdatSize = lookForMP4Atom(Source, "mdat"); // === Audio binary data
            bitrate = (int)Math.Round(mdatSize * 8 / duration, 0);
        }

        private void setMetaField(string ID, string Data, bool readAllMetaFrames)
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier
            if (frameMapping_mp4.ContainsKey(ID)) supportedMetaId = frameMapping_mp4[ID];

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                tagData.IntegrateValue(supportedMetaId, Data);
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                fieldInfo = new TagData.MetaFieldInfo(getImplementedTagType(), ID, Data);
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

        // Looks for the atom segment starting with the given key, at the current atom level
        // Returns with Source positioned right after the atom header, on the 1st byte of data
        // Returned value is the raw size of the atom (including the already-read 8-byte header)
        //
        // Warning : stream must be positioned at the end of a previous atom before being called
        private uint lookForMP4Atom(BinaryReader Source, string atomKey)
        {
            uint atomSize = 0;
            string atomHeader;
            bool first = true;
            int iterations = 0;

            do
            {
                if (!first) Source.BaseStream.Seek(atomSize - 8, SeekOrigin.Current);
                atomSize = StreamUtils.ReverseUInt32(Source.ReadUInt32());
                atomHeader = Utils.Latin1Encoding.GetString(Source.ReadBytes(4));
                if (first) first = false;
                if (++iterations > 100) throw new Exception(atomKey + " atom could not be found");
            } while (!atomKey.Equals(atomHeader) && Source.BaseStream.Position + atomSize - 16 < Source.BaseStream.Length);

            if (Source.BaseStream.Position + atomSize - 16 > Source.BaseStream.Length) throw new Exception(atomKey + " atom could not be found");

            return atomSize;
        }


        // Read data from file
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
            bool result = false;

            ResetData();
            FHeaderTypeID = recognizeHeaderType(source);
            // Read header data
            if (AAC_HEADER_TYPE_ADIF == FHeaderTypeID) readADIF(source);
            else if (AAC_HEADER_TYPE_ADTS == FHeaderTypeID) readADTS(source);
            else if (AAC_HEADER_TYPE_MP4 == FHeaderTypeID) readMP4(source,readTagParams);

            result = true;

            return result;
        }

        public bool IsMetaSupported(int metaType)
        {
            return (metaType == MetaDataIOFactory.TAG_ID3V1) || (metaType == MetaDataIOFactory.TAG_ID3V2) || (metaType == MetaDataIOFactory.TAG_APE) || (metaType == MetaDataIOFactory.TAG_NATIVE);
        }

        public bool HasNativeMeta()
        {
            return true;
        }

        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override int getImplementedTagType()
        {
            return MetaDataIOFactory.TAG_NATIVE;
        }

        protected override bool write(TagData tag, BinaryWriter w)
        {
            bool result;
            long tagSizePos;
            uint tagSize;

            // ============
            // == HEADER ==
            // ============
            // Keep position in mind to calculate final size and come back here to write it
            tagSizePos = w.BaseStream.Position;
            w.Write((int)0); // Tag size placeholder to be rewritten in a few lines
            w.Write("ilst".ToCharArray());

            // ============
            // == FRAMES ==
            // ============
            long dataPos = w.BaseStream.Position;
            result = writeFrames(ref tag, w);

            // Record final size of tag into "tag size" field of header
            long finalTagPos = w.BaseStream.Position;
            w.BaseStream.Seek(tagSizePos, SeekOrigin.Begin);
            tagSize = Convert.ToUInt32(finalTagPos - tagSizePos);
            w.Write( StreamUtils.ReverseUInt32(tagSize));
            w.BaseStream.Seek(finalTagPos, SeekOrigin.Begin);

            return result;
        }

        private bool writeFrames(ref TagData tag, BinaryWriter w)
        {
            bool result = true;
            bool doWritePicture;

            IDictionary<byte, String> map = tag.ToMap();

            // Supported textual fields
            foreach (byte frameType in map.Keys)
            {
                foreach (string s in frameMapping_mp4.Keys)
                {
                    if (frameType == frameMapping_mp4[s])
                    {
                        if (map[frameType].Length > 0) // No frame with empty value
                        {
                            writeTextFrame(ref w, s, map[frameType]);
                        }
                        break;
                    }
                }
            }

            // Other textual fields
            foreach (TagData.MetaFieldInfo fieldInfo in tag.AdditionalFields)
            {
                if (fieldInfo.TagType.Equals(getImplementedTagType()) && !fieldInfo.MarkedForDeletion)
                {
                    writeTextFrame(ref w, fieldInfo.NativeFieldCode, fieldInfo.Value);
                }
            }

            // Picture fields
            bool firstPic = true;
            foreach (TagData.PictureInfo picInfo in tag.Pictures)
            {
                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !picInfo.PicType.Equals(TagData.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (getImplementedTagType() == picInfo.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!picInfo.MarkedForDeletion);

                if (doWritePicture)
                {
                    writePictureFrame(ref w, picInfo.PictureData, picInfo.NativeFormat, firstPic);
                    firstPic = false;
                }
            }

            return result;
        }

        private void writeTextFrame(ref BinaryWriter writer, string frameCode, string text)
        {
            long frameSizePos1;
            long frameSizePos2;
            long finalFramePos;

            int frameFlags = 0;

            // == METADATA HEADER ==
            frameSizePos1 = writer.BaseStream.Position;
            writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines
            if (frameCode.StartsWith("----")) // Specific metadata
            { 
                string[] frameCodeComponents = frameCode.Split(':');
                if (3 == frameCodeComponents.Length)
                {
                    writer.Write(Utils.Latin1Encoding.GetBytes("----"));

                    writer.Write(StreamUtils.ReverseInt32(frameCodeComponents[1].Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("mean"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(frameCodeComponents[1]));

                    writer.Write(StreamUtils.ReverseInt32(frameCodeComponents[2].Length + 4 + 4 + 4));
                    writer.Write(Utils.Latin1Encoding.GetBytes("name"));
                    writer.Write(frameFlags);
                    writer.Write(Utils.Latin1Encoding.GetBytes(frameCodeComponents[2]));
                }
            } else
            {
                writer.Write(Utils.Latin1Encoding.GetBytes(frameCode));
            }

            // == METADATA VALUE ==
            frameSizePos2 = writer.BaseStream.Position;
            writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines
            writer.Write("data".ToCharArray());

            int frameClass = 1;
            if (frameClasses_mp4.ContainsKey(frameCode)) frameClass = frameClasses_mp4[frameCode];

            writer.Write(StreamUtils.ReverseInt32(frameClass));
            writer.Write(frameFlags);
    
            if (0 == frameClass) // Special cases : gnre, trkn, disk
            {
                UInt16 int16data;
                if (frameCode.Equals("trkn") || frameCode.Equals("disk"))
                {
                    int16data = 0;
                    writer.Write(int16data);
                    int16data = StreamUtils.ReverseUInt16(Convert.ToUInt16( TrackUtils.ExtractTrackNumber(text) ));
                    writer.Write(int16data);
                    int16data = 0;              // Total number of tracks/discs; unsupported for now
                    writer.Write(int16data);
                    if (frameCode.Equals("trkn")) writer.Write(int16data); // trkn field always has two more bytes than disk field....
                }
                else if (frameCode.Equals("gnre"))
                {
                    int16data = StreamUtils.ReverseUInt16(Convert.ToUInt16(text));
                }
            }
            else if (1 == frameClass) // UTF-8 text
            {
                writer.Write(Encoding.UTF8.GetBytes(text));
            }
            else if (21 == frameClass) // uint8
            {
                writer.Write(Convert.ToByte(text));
            }

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
            writer.BaseStream.Seek(frameSizePos1, SeekOrigin.Begin);
            writer.Write(StreamUtils.ReverseUInt32(Convert.ToUInt32(finalFramePos -frameSizePos1)));
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.ReverseUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        private void writePictureFrame(ref BinaryWriter writer, byte[] pictureData, ImageFormat picFormat, bool firstPicture)
        {
            long frameSizePos1 = 0;
            long frameSizePos2;
            long finalFramePos;

            int frameFlags = 0;

            // == METADATA HEADER ==
            if (firstPicture) // If multiples pictures are embedded, the 'covr' atom is not repeated; the 'data' atom is
            {
                frameSizePos1 = writer.BaseStream.Position;
                writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines
                writer.Write(Utils.Latin1Encoding.GetBytes("covr"));
            }

            // == METADATA VALUE ==
            frameSizePos2 = writer.BaseStream.Position;
            writer.Write((int)0); // Frame size placeholder to be rewritten in a few lines
            writer.Write("data".ToCharArray());

            // TODO what if a BMP or GIF picture was embedded ?
            int frameClass = 13; // JPEG
            if (picFormat.Equals(ImageFormat.Png)) frameClass = 14;

            writer.Write(StreamUtils.ReverseInt32(frameClass));
            writer.Write(frameFlags);

            writer.Write(pictureData);

            // Go back to frame size locations to write their actual size 
            finalFramePos = writer.BaseStream.Position;
            if (firstPicture)
            {
                writer.BaseStream.Seek(frameSizePos1, SeekOrigin.Begin);
                writer.Write(StreamUtils.ReverseUInt32(Convert.ToUInt32(finalFramePos - frameSizePos1)));
            }
            writer.BaseStream.Seek(frameSizePos2, SeekOrigin.Begin);
            writer.Write(StreamUtils.ReverseUInt32(Convert.ToUInt32(finalFramePos - frameSizePos2)));
            writer.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        public bool RewriteSizeMarkers(BinaryWriter w, int deltaSize)
        {
            bool result = true;

            if (upperAtoms != null)
            {
                for (int i = 0; i < upperAtoms.Count; i++)
                {
                    upperAtoms[i] = new ValueInfo(upperAtoms[i].Position, (ulong)((long)upperAtoms[i].Value + deltaSize), upperAtoms[i].NbBytes);
                    w.BaseStream.Seek((long)upperAtoms[i].Position, SeekOrigin.Begin);
                    if (4 == upperAtoms[i].NbBytes)
                    {
                        w.Write(StreamUtils.ReverseUInt32((uint)upperAtoms[i].Value));
                    }
                    else
                    {
                        w.Write(StreamUtils.ReverseUInt64(upperAtoms[i].Value));
                    }
                }
            }
            
            return result;
        }

    }
}