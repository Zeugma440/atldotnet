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
        private static int[] SAMPLE_RATE = { 96000, 88200, 64000, 48000, 44100, 32000,
                                        24000, 22050, 16000, 12000, 11025, 8000,
                                        0, 0, 0, 0};

        private static Dictionary<string, byte> frameMapping_mp4;

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
        private AudioDataIO.SizeInfo sizeInfo;
        private string fileName;


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
            get { return bitrate; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }
        public string FileName
        {
            get { return fileName; }
        }

        // ********************* Auxiliary functions & procedures ********************

        uint ReadBits(BinaryReader Source, int Position, int Count)
        {
            byte[] buffer = new byte[4];

            // Read a number of bits from file at the given position
            Source.BaseStream.Seek(Position / 8, SeekOrigin.Begin); // integer division =^ div
            buffer = Source.ReadBytes(4);
            uint result = (uint)((buffer[0] << 24) + (buffer[1] << 16) + (buffer[2] << 8) + buffer[3]);
            result = (result << (Position % 8)) >> (32 - Count);

            return result;
        }

        // ********************** Private functions & procedures *********************

        // Reset all variables
        protected void resetData()
        {
            ResetData();
            FHeaderTypeID = AAC_HEADER_TYPE_UNKNOWN;
            FMPEGVersionID = AAC_MPEG_VERSION_UNKNOWN;
            FProfileID = AAC_PROFILE_UNKNOWN;
            FChannels = 0;
            FBitrateTypeID = AAC_BITRATE_TYPE_UNKNOWN;
            FTotalFrames = 0;

            bitrate = 0;
            duration = 0;
            sampleRate = 0;

            FVersionID = 0;
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
            return AAC_PROFILE[FProfileID];
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
                return duration;
            }
            else
            {
                if (0 == bitrate)
                    return 0;
                else
                    return 8 * (sizeInfo.FileSize - sizeInfo.ID3v2Size) / bitrate;
            }
        }

        // ---------------------------------------------------------------------------

        // Check for file correctness
        bool FIsValid()
        {
            return ((FHeaderTypeID != AAC_HEADER_TYPE_UNKNOWN) &&
                (FChannels > 0) && (sampleRate > 0) && (bitrate > 0));
        }

        // ---------------------------------------------------------------------------

        // Get header type of the file
        byte FRecognizeHeaderType(BinaryReader Source)
        {
            byte result;
            char[] Header = new char[4];

            result = AAC_HEADER_TYPE_UNKNOWN;
            Source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            Header = StreamUtils.ReadOneByteChars(Source, 4);

            if (StreamUtils.StringEqualsArr("ADIF", Header))
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
        private void readADIF(BinaryReader Source)
        {
            int Position;
//            isValid = true;

            Position = (int)(sizeInfo.ID3v2Size * 8 + 32);
            if (0 == ReadBits(Source, Position, 1)) Position += 3;
            else Position += 75;
            if (0 == ReadBits(Source, Position, 1)) FBitrateTypeID = AAC_BITRATE_TYPE_CBR;
            else FBitrateTypeID = AAC_BITRATE_TYPE_VBR;

            Position++;

            bitrate = (int)ReadBits(Source, Position, 23);

            if (AAC_BITRATE_TYPE_CBR == FBitrateTypeID) Position += 51;
            else Position += 31;

            FMPEGVersionID = AAC_MPEG_VERSION_4;
            FProfileID = (byte)(ReadBits(Source, Position, 2) + 1);
            Position += 2;

            sampleRate = SAMPLE_RATE[ReadBits(Source, Position, 4)];
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
        private void readADTS(BinaryReader Source)
        {
            int Frames = 0;
            int TotalSize = 0;
            int Position;
//            isValid = true;

            do
            {
                Frames++;
                Position = (int)(sizeInfo.ID3v2Size + TotalSize) * 8;

                if (ReadBits(Source, Position, 12) != 0xFFF) break;

                Position += 12;

                if (0 == ReadBits(Source, Position, 1))
                    FMPEGVersionID = AAC_MPEG_VERSION_4;
                else
                    FMPEGVersionID = AAC_MPEG_VERSION_2;

                Position += 4;
                FProfileID = (byte)(ReadBits(Source, Position, 2) + 1);
                Position += 2;

                sampleRate = SAMPLE_RATE[ReadBits(Source, Position, 4)];
                Position += 5;

                FChannels = (byte)ReadBits(Source, Position, 3);

                if (AAC_MPEG_VERSION_4 == FMPEGVersionID)
                    Position += 9;
                else
                    Position += 7;

                TotalSize += (int)ReadBits(Source, Position, 13);
                Position += 13;

                if (0x7FF == ReadBits(Source, Position, 11))
                    FBitrateTypeID = AAC_BITRATE_TYPE_VBR;
                else
                    FBitrateTypeID = AAC_BITRATE_TYPE_CBR;

                if (AAC_BITRATE_TYPE_CBR == FBitrateTypeID) break;
                // more accurate
                //until (Frames != 1000) && (Source.Size > FID3v2.Size + TotalSize)
            }
            while (Source.BaseStream.Length > sizeInfo.ID3v2Size + TotalSize);
            FTotalFrames = Frames;
            bitrate = (int)Math.Round(8 * (double)TotalSize / 1024 / Frames * sampleRate);
        }

        // Read MP4 header data
        // http://www.jiscdigitalmedia.ac.uk/guide/aac-audio-and-the-mp4-media-format
        // http://atomicparsley.sourceforge.net/mpeg-4files.html
        // - Metadata is located in the moov/udta/meta/ilst atom
        // - Physical information are located in the moov/trak atom (to be confirmed ?)
        private void readMP4(BinaryReader Source, MetaDataIO.ReadTagParams readTagParams)
        {
            long iListSize = 0;
            long iListPosition = 0;
            int metadataSize = 0;
            byte dataClass = 0;

            ushort int16Data = 0;
            uint int32Data = 0;

            string strData = "";
            string atomHeader;

//            isValid = true;
            Source.BaseStream.Seek(0, SeekOrigin.Begin);

            // FTYP atom
            int atomSize = StreamUtils.ReverseInt32(Source.ReadInt32());
            Source.BaseStream.Seek(atomSize - 4, SeekOrigin.Current);

            // MOOV atom
            lookForMP4Atom(Source, "moov"); // === Physical data

            long moovPosition = Source.BaseStream.Position;
            lookForMP4Atom(Source, "mvhd"); // === Physical data
            byte version = Source.ReadByte();
            Source.BaseStream.Seek(3, SeekOrigin.Current); // 3-byte flags
            if (1 == version) Source.BaseStream.Seek(16, SeekOrigin.Current); else Source.BaseStream.Seek(8, SeekOrigin.Current);

            int timeScale = StreamUtils.ReverseInt32(Source.ReadInt32());
            long timeLengthPerSec;
            if (1 == version) timeLengthPerSec = StreamUtils.ReverseInt64(Source.ReadUInt64()); else timeLengthPerSec = StreamUtils.ReverseInt32(Source.ReadInt32());
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
                string descFormat = Utils.GetLatin1Encoding().GetString(Source.ReadBytes(4));

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

            Source.BaseStream.Seek(moovPosition, SeekOrigin.Begin);
            lookForMP4Atom(Source, "udta");
            lookForMP4Atom(Source, "meta");
            Source.BaseStream.Seek(4, SeekOrigin.Current); // 4-byte flags

            if (readTagParams.ReadTag)
            {
                iListSize = lookForMP4Atom(Source, "ilst") - 8; // === Metadata list

                tagExists = true;
                tagSize = (int)iListSize; // tagSize here...

                // Browse all metadata
                while (iListPosition < iListSize)
                {
                    atomSize = StreamUtils.ReverseInt32(Source.ReadInt32());
                    atomHeader = Utils.GetLatin1Encoding().GetString(Source.ReadBytes(4));
                    metadataSize = lookForMP4Atom(Source, "data");

                    // We're only looking for the last byte of the flag
                    Source.BaseStream.Seek(3, SeekOrigin.Current);
                    dataClass = Source.ReadByte();

                    // 4-byte NULL space
                    Source.BaseStream.Seek(4, SeekOrigin.Current);

                    if (1 == dataClass) // UTF-8 Text
                    {
                        strData = Encoding.UTF8.GetString(Source.ReadBytes(metadataSize - 16));
                        setMetaField(atomHeader, strData, readTagParams.ReadAllMetaFrames);
                    }
                    else if (21 == dataClass) // uint8
                    {
                        int16Data = Source.ReadByte();
                        Source.BaseStream.Seek(metadataSize - 17, SeekOrigin.Current); // Potential remaining padding bytes
                        setMetaField(atomHeader, int16Data.ToString(), readTagParams.ReadAllMetaFrames);
                    }
                    else if (13 == dataClass || 14 == dataClass) // JPEG/PNG picture
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

                            MemoryStream mem = new MemoryStream(metadataSize - 16);
                            StreamUtils.CopyStream(Source.BaseStream, mem, metadataSize - 16);
                            readTagParams.PictureStreamHandler(ref mem, picType, imgFormat, MetaDataIOFactory.TAG_NATIVE, dataClass, picturePosition); // TODO !
                            mem.Close();
                        }
                        else
                        {
                            Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                        }
                    }
                    else if (0 == dataClass) // Special cases : gnre, trkn, disk
                    {
                        if ("trkn".Equals(atomHeader) || "disk".Equals(atomHeader))
                        {
                            Source.BaseStream.Seek(3, SeekOrigin.Current);
                            int16Data = Source.ReadByte();
                            Source.BaseStream.Seek(metadataSize - 20, SeekOrigin.Current); // Potential remaining padding bytes
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
                            Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                        }
                    }
                    else // Other unhandled cases
                    {
                        Source.BaseStream.Seek(metadataSize - 16, SeekOrigin.Current);
                    }

                    iListPosition += atomSize;
                }
            }

            // Seek audio data segment to calculate mean bitrate 
            // NB : This figure is closer to truth than the "average bitrate" recorded in the esds/m4ds header
            Source.BaseStream.Seek(0, SeekOrigin.Begin);
            int mdatSize = lookForMP4Atom(Source, "mdat"); // === Audio binary data
            bitrate = (int)Math.Round(mdatSize * 8 / duration, 0);
        }

        private void setMetaField(string ID, string Data, bool readAllMetaFrames)
        {
            byte supportedMetaId = 255;
            ID = ID.ToLower();

            // Finds the ATL field identifier according to the ID3v2 version
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

        // Looks for the atom segment starting with the given key, at the current atom level
        // Returns with Source positioned right after the atom header, on the 1st byte of data
        // Returned value is the raw size of the atom (including the already-read 8-byte header)
        private int lookForMP4Atom(BinaryReader Source, string atomKey)
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
            } while (!StreamUtils.StringEqualsArr(atomKey, atomHeader) && Source.BaseStream.Position + (atomSize - 8) < Source.BaseStream.Length);

            return atomSize;
        }


        // ********************** Public functions & procedures ********************** 

        // constructor
        public AAC(string fileName)
        {
            this.fileName = fileName;
            resetData();
        }

        static AAC()
        {
            frameMapping_mp4 = new Dictionary<string, byte>();

            frameMapping_mp4.Add("©nam", TagData.TAG_FIELD_TITLE);
            frameMapping_mp4.Add("titl", TagData.TAG_FIELD_TITLE);
            frameMapping_mp4.Add("©alb", TagData.TAG_FIELD_ALBUM);
            frameMapping_mp4.Add("©art", TagData.TAG_FIELD_ARTIST);
            frameMapping_mp4.Add("©cmt", TagData.TAG_FIELD_COMMENT);
            frameMapping_mp4.Add("©day", TagData.TAG_FIELD_RECORDING_YEAR);
            frameMapping_mp4.Add("©gen", TagData.TAG_FIELD_GENRE);
            frameMapping_mp4.Add("gnre", TagData.TAG_FIELD_GENRE);
            frameMapping_mp4.Add("trkn", TagData.TAG_FIELD_TRACK_NUMBER);
            frameMapping_mp4.Add("disk", TagData.TAG_FIELD_DISC_NUMBER);
            frameMapping_mp4.Add("rtng", TagData.TAG_FIELD_RATING);
            frameMapping_mp4.Add("©wrt", TagData.TAG_FIELD_COMPOSER);
            frameMapping_mp4.Add("desc", TagData.TAG_FIELD_GENERAL_DESCRIPTION);
            frameMapping_mp4.Add("cprt", TagData.TAG_FIELD_COPYRIGHT);
            frameMapping_mp4.Add("aart", TagData.TAG_FIELD_ALBUM_ARTIST); // aART
        }

        // --------------------------------------------------------------------------- 

        // No explicit destructor in C#

        // --------------------------------------------------------------------------- 

        // Read data from file
        public bool Read(BinaryReader source, AudioDataIO.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
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
            FHeaderTypeID = FRecognizeHeaderType(source);
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

        public bool RewriteFileSizeInHeader(BinaryWriter w, long newFileSize)
        {
            throw new NotImplementedException();
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

        protected override bool write(TagData tag, BinaryWriter w)
        {
            throw new NotImplementedException();
        }
    }
}