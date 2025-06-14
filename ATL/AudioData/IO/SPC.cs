using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static ATL.AudioData.AudioDataManager;
using Commons;
using static ATL.ChannelsArrangements;
using static ATL.TagData;
using System.Threading.Tasks;
using static System.Int32;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for SPC700 files manipulation (extensions : .SPC)
    /// According to file format v0.30; inspired by the SNESamp source (ID666.cpp)
    /// </summary>
    partial class SPC : MetaDataIO, IAudioDataIO
    {
        private const string ZONE_EXTENDED = "extended";
        private const string ZONE_HEADER = "header";

        private static readonly byte[] SPC_FORMAT_TAG = Utils.Latin1Encoding.GetBytes("SNES-SPC700 Sound File Data");
        private const string XTENDED_TAG = "xid6";

#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0051 // Remove unused private members
        private const int REGISTERS_LENGTH = 9;
        private const int AUDIODATA_LENGTH = 65792;
        private const int SPC_RAW_LENGTH = 66048;

        private const int HEADER_TEXT = 0;
        private const int HEADER_BINARY = 1;

        private const bool PREFER_BIN = false;

        private const int SPC_DEFAULT_DURATION = 180000; // 3 minutes

        // Sub-chunk ID's / Metadata
        private const byte XID6_SONG = 0x01;                        //see ReadMe.Txt for format information
        private const byte XID6_GAME = 0x02;
        private const byte XID6_ARTIST = 0x03;
        private const byte XID6_DUMPER = 0x04;
        private const byte XID6_DATE = 0x05;
        private const byte XID6_EMU = 0x06;
        private const byte XID6_CMNTS = 0x07;
        private const byte XID6_OST = 0x10;
        private const byte XID6_DISC = 0x11;
        private const byte XID6_TRACK = 0x12;
        private const byte XID6_PUB = 0x13;
        private const byte XID6_COPY = 0x14;
        // Sub-chunk ID's / Playback data
        private const byte XID6_INTRO = 0x30;
        private const byte XID6_LOOP = 0x31;
        private const byte XID6_END = 0x32;
        private const byte XID6_FADE = 0x33;
        private const byte XID6_MUTE = 0x34;
        private const byte XID6_LOOPX = 0x35;
        private const byte XID6_AMP = 0x36;


        // Artificial IDs for fields stored in header
        private const byte HEADER_TITLE = 0xA0;
        private const byte HEADER_ALBUM = 0xA1;
        private const byte HEADER_DUMPERNAME = 0xA2;
        private const byte HEADER_COMMENT = 0xA3;
        private const byte HEADER_DUMPDATE = 0xA4;
        private const byte HEADER_SONGLENGTH = 0xA5;
        private const byte HEADER_FADE = 0xA6;
        private const byte HEADER_ARTIST = 0xA7;

        //Data types
        private const byte XID6_TVAL = 0x00; // int16
        private const byte XID6_TSTR = 0x01; // ANSI string
        private const byte XID6_TINT = 0x04; // int32

        //Timer stuff
        private const int XID6_MAXTICKS = 383999999;            //Max ticks possible for any field (99:59.99 * 64k)
        private const int XID6_TICKSMIN = 3840000;              //Number of ticks in a minute (60 * 64k)
        private const int XID6_TICKSSEC = 64000;                //Number of ticks in a second
        private const int XID6_TICKSMS = 64;                    //Number of ticks in a millisecond
        private const int XID6_MAXLOOP = 9;                 //Max loop times
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore S1144 // Unused private types or members should be removed


        // Standard fields

        private SizeInfo sizeInfo;

        // Mapping between SPC extended frame codes and ATL frame codes
        private static readonly IDictionary<byte, Field> extendedFrameMapping = new Dictionary<byte, Field>
        {
            { XID6_SONG, Field.TITLE },
            { XID6_GAME, Field.ALBUM }, // Small innocent semantic shortcut
            { XID6_ARTIST, Field.ARTIST },
            { XID6_CMNTS, Field.COMMENT },
            { XID6_COPY, Field.RECORDING_YEAR }, // Actual field name is "Copyright year", which makes that legit
            { XID6_TRACK, Field.TRACK_NUMBER },
            { XID6_DISC, Field.DISC_NUMBER },
            { XID6_PUB, Field.PUBLISHER }
        };
        // Mapping between SPC header frame codes and ATL frame codes
        private static readonly IDictionary<byte, Field> headerFrameMapping = new Dictionary<byte, Field>
        {
            { HEADER_TITLE, Field.TITLE },
            { HEADER_ALBUM, Field.ALBUM },
            { HEADER_ARTIST, Field.ARTIST },
            { HEADER_COMMENT, Field.COMMENT }
        };
        // Frames that are required for playback
        private static readonly IList<byte> playbackFrames = new List<byte>
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
        // Mapping between SPC frame codes and frame data types that aren't type 1
        private static readonly IDictionary<byte, byte> extendedFrameTypes = new Dictionary<byte, byte>()
        {
            { XID6_DATE, 4 },
            { XID6_EMU, 0 },
            { XID6_DISC, 0 },
            { XID6_TRACK, 0 },
            { XID6_COPY, 0 },
            { XID6_INTRO, 4 },
            { XID6_LOOP, 4 },
            { XID6_END, 4 },
            { XID6_FADE, 4 },
            { XID6_MUTE, 0 },
            { XID6_LOOPX, 0 },
            { XID6_AMP, 4 }
        };


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // AudioDataIO
        public int SampleRate { get; private set; }

        public bool IsVBR => false;

        public AudioFormat AudioFormat { get; }

        public int CodecFamily => AudioDataIOFactory.CF_SEQ_WAV;

        public string FileName { get; }

        public double BitRate { get; private set; }

        public int BitDepth => -1; // Irrelevant for that format
        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement => STEREO;

        /// <inheritdoc/>
        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.APE };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => false;
        /// <inheritdoc/>
        protected override bool supportsAdditionalFields => true;

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }

        // IMetaDataIO
        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }
        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;
            byte ID_b = byte.Parse(ID);

            // Finds the ATL field identifier
            if (ZONE_EXTENDED.Equals(zone) && extendedFrameMapping.TryGetValue(ID_b, out var value)) supportedMetaId = value;
            else if (ZONE_HEADER.Equals(zone) && headerFrameMapping.TryGetValue(ID_b, out var value1)) supportedMetaId = value1;

            return supportedMetaId;
        }


        // === PRIVATE STRUCTURES/SUBCLASSES ===

        private sealed class SpcHeader
        {
            public const int TAG_IN_HEADER = 26;

            public long Size;
            public byte TagInHeader;                    // Set to TAG_IN_HEADER if header contains ID666 info

            public void Reset()
            {
                Size = 0;
            }
        }

        private sealed class SpcExTags
        {
            public string FormatTag;              // Extended info tag (should be XTENDED_TAG)
            public uint Size;                     // Chunk size

            public void Reset()
            {
                FormatTag = "";
                Size = 0;
            }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            // Reset variables
            SampleRate = 32000; // Seems to be de facto value for all SPC files, even though spec doesn't say anything about it
            BitRate = 0;
            Duration = SPC_DEFAULT_DURATION;

            ResetData();
        }

        public SPC(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // === PRIVATE METHODS ===

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, SPC_FORMAT_TAG);
        }

        private static bool readHeader(Stream source, ref SpcHeader header)
        {
            source.Seek(0, SeekOrigin.Begin);

            long initialPosition = source.Position;
            byte[] buffer = new byte[SPC_FORMAT_TAG.Length];
            if (source.Read(buffer, 0, buffer.Length) < buffer.Length) return false;
            if (IsValidHeader(buffer))
            {
                source.Seek(8, SeekOrigin.Current); // Remainder of header tag (version marker vX.XX + 2 bytes)
                if (source.Read(buffer, 0, 2) < 2) return false;
                header.TagInHeader = buffer[0];
                header.Size = source.Position - initialPosition;
                return true;
            }
            else
            {
                return false;
            }
        }

        private void readHeaderTags(Stream source, ref SpcHeader header, ReadTagParams readTagParams)
        {
            byte[] buffer = new byte[32];
            long initialPosition = source.Position;

            if (source.Read(buffer, 0, 32) < 32) return;
            SetMetaField(HEADER_TITLE.ToString(), Utils.Latin1Encoding.GetString(buffer).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            if (source.Read(buffer, 0, 32) < 32) return;
            SetMetaField(HEADER_ALBUM.ToString(), Utils.Latin1Encoding.GetString(buffer).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            if (source.Read(buffer, 0, 16) < 16) return;
            SetMetaField(HEADER_DUMPERNAME.ToString(), Utils.Latin1Encoding.GetString(buffer).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            if (source.Read(buffer, 0, 32) < 32) return;
            SetMetaField(HEADER_COMMENT.ToString(), Utils.Latin1Encoding.GetString(buffer).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);

            byte[] date = new byte[11];
            byte[] song = new byte[3];
            byte[] fade = new byte[5];

            // NB : Dump date is used to determine if the tag is binary or text-based.
            // It won't be recorded as a property of TSPC
            if (source.Read(date, 0, date.Length) < date.Length) return;
            int dateRes = isText(date);
            if (source.Read(song, 0, song.Length) < song.Length) return;
            int songRes = isText(song);
            if (source.Read(fade, 0, fade.Length) < fade.Length) return;
            int fadeRes = isText(fade);

            bool bin = true;

            if (songRes != -1 && fadeRes != -1) // No time, or time is text
            {
                if (dateRes > 0)                    //If date is text, then tag is text
                {
                    bin = false;
                }
                else if (0 == dateRes)                   //No date
                {
                    bin = PREFER_BIN;               //Times could still be binary (ex. 56 bin = '8' txt)
                }
                else if (-1 == dateRes)                  //Date contains invalid characters
                {
                    bin = true;
                    for (int i = 4; i < 8; i++)
                    {
                        bin = bin && 0 == date[i];
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
                    fade[0] * 0x000001 +
                    fade[1] * 0x0000FF +
                    fade[2] * 0x00FF00 +
                    fade[3] * 0xFF0000;
                if (fadeVal > 59999) fadeVal = 59999;

                songVal = song[0] * 0x01 + song[1] * 0x10;
                if (songVal > 959) songVal = 959;

                source.Seek(-1, SeekOrigin.Current); // We're one byte ahead
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
            if (songVal > 0) Duration = fadeVal + songVal;

            if (source.Read(buffer, 0, 32) < 32) return;
            SetMetaField(HEADER_ARTIST.ToString(), Utils.Latin1Encoding.GetString(buffer).Replace("\0", "").Trim(), readTagParams.ReadAllMetaFrames, ZONE_HEADER);
            header.Size += source.Position - initialPosition;

            if (readTagParams.PrepareForWriting)
            {
                structureHelper.AddZone(initialPosition, (int)(source.Position - initialPosition), ZONE_HEADER);
            }
        }

        private static int isText(byte[] data)
        {
            int c = 0;

            while (c < data.Length && ((data[c] >= 0x30 && data[c] <= 0x39) || 0x2F == data[c])) c++; // 0x2F = '/' (date separator)

            if (c == data.Length || data[c] == 0)
                return c;
            else
                return -1;
        }

        private void readExtendedData(Stream source, ref SpcExTags footer, ReadTagParams readTagParams)
        {
            long initialPosition = source.Position;
            byte[] buffer = new byte[4];
            if (source.Read(buffer, 0, buffer.Length) < buffer.Length) return;
            footer.FormatTag = Utils.Latin1Encoding.GetString(buffer);
            if (XTENDED_TAG == footer.FormatTag)
            {
                if (source.Read(buffer, 0, buffer.Length) < buffer.Length) return;
                footer.Size = StreamUtils.DecodeUInt32(buffer);

                string strData = "";
                int intData = 0;
                long ticks = 0;

                long dataPosition = source.Position;
                while (source.Position < dataPosition + footer.Size - 4)
                {
                    if (source.Read(buffer, 0, 2) < 2) break;
                    var ID = buffer[0];
                    var type = buffer[1];
                    if (source.Read(buffer, 0, 2) < 2) break;
                    var size = StreamUtils.DecodeUInt16(buffer);

                    switch (type)
                    {
                        case XID6_TVAL:
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
                        case XID6_TSTR:
                            intData = 0;
                            byte[] strDatab = new byte[size];
                            if (source.Read(strDatab, 0, size) < size) break;
                            strData = Utils.Latin1Encoding.GetString(strDatab).Replace("\0", "").Trim();

                            while (source.Position < source.Length && 0 == source.ReadByte()) ; // Skip parasite ending zeroes
                            if (source.Position < source.Length) source.Seek(-1, SeekOrigin.Current);
                            break;
                        case XID6_TINT:
                            if (source.Read(buffer, 0, 4) < 4) break;
                            intData = StreamUtils.DecodeInt32(buffer);
                            strData = intData.ToString();
                            break;
                    }

                    if (XID6_LOOP == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_LOOPX == ID) ticks *= Math.Min(XID6_MAXLOOP, (int)size);
                    else if (XID6_INTRO == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_END == ID) ticks += Math.Min(XID6_MAXTICKS, intData);
                    else if (XID6_FADE == ID) ticks += Math.Min(XID6_MAXTICKS, intData);

                    SetMetaField(ID.ToString(), strData, readTagParams.ReadAllMetaFrames, ZONE_EXTENDED);
                }

                if (ticks > 0) Duration = Math.Round((double)ticks / XID6_TICKSSEC);

                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(initialPosition, (int)(source.Position - initialPosition), ZONE_EXTENDED);
                }
            }
        }

        // === PUBLIC METHODS ===

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = true;
            SpcHeader header = new SpcHeader();
            SpcExTags footer = new SpcExTags();

            header.Reset();
            footer.Reset();
            resetData();

            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            if (!readHeader(source, ref header)) throw new InvalidDataException("Not a SPC file");

            // Reads the header tag
            if (SpcHeader.TAG_IN_HEADER == header.TagInHeader)
            {
                source.Seek(REGISTERS_LENGTH, SeekOrigin.Current);
                readHeaderTags(source, ref header, readTagParams);
            }

            AudioDataOffset = source.Position;

            // Reads extended tag
            if (source.Length > SPC_RAW_LENGTH)
            {
                source.Seek(SPC_RAW_LENGTH, SeekOrigin.Begin);
                readExtendedData(source, ref footer, readTagParams);
            }
            else
            {
                if (readTagParams.PrepareForWriting)
                {
                    structureHelper.AddZone(SPC_RAW_LENGTH, 0, ZONE_EXTENDED);
                }
            }

            AudioDataSize = sizeInfo.FileSize - header.Size - footer.Size;
            BitRate = AudioDataSize * 8 / Duration;

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            int result = 0;

            var buffer = new Span<byte>(new byte[4]);
            if (zone.Equals(ZONE_HEADER))
            {
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.TITLE], 32, '\0')));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.ALBUM], 32, '\0')));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(AdditionalFields[HEADER_DUMPERNAME.ToString()], 16, '\0')));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.COMMENT], 32, '\0')));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_DUMPDATE.ToString()]));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_SONGLENGTH.ToString()]));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(AdditionalFields[HEADER_FADE.ToString()]));
                StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(Utils.BuildStrictLengthString(tag[Field.ARTIST], 32, '\0')));
                result = 8;
            }
            else if (zone.Equals(ZONE_EXTENDED))
            {
                // SPC specific : are only allowed to appear in extended metadata fields that
                //   - either do not exist in header
                //   - or have been truncated when written in header
                Utils.Latin1Encoding.GetBytes(XTENDED_TAG.AsSpan(), buffer);
                s.Write(buffer);
                long sizePos = s.Position;
                StreamUtils.WriteInt32(s, 0, buffer); // Size placeholder; to be rewritten with actual value at the end of the method

                IDictionary<Field, string> map = tag.ToMap();

                // Supported textual fields
                foreach (Field frameType in map.Keys)
                {
                    foreach (byte b in extendedFrameMapping.Keys)
                    {
                        if (frameType == extendedFrameMapping[b])
                        {
                            if (map[frameType].Length > 0 && canBeWrittenInExtendedMetadata(frameType, map[frameType])) // No frame with empty value
                            {
                                writeSubChunk(s, b, map[frameType], buffer);
                                result++;
                            }
                            break;
                        }
                    }
                }

                // Other textual fields
                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields.Where(isMetaFieldWritable))
                {
                    if (!fieldInfo.Zone.Equals(ZONE_HEADER) && fieldInfo.Value.Length > 0)
                    {
                        writeSubChunk(s, byte.Parse(fieldInfo.NativeFieldCode), FormatBeforeWriting(fieldInfo.Value), buffer);
                        result++;
                    }
                }

                int size = (int)(s.Position - sizePos);
                s.Seek(sizePos, SeekOrigin.Begin);
                StreamUtils.WriteInt32(s, size, buffer);
            }

            return result;
        }

        private static bool canBeWrittenInExtendedMetadata(Field frameType, string value)
        {
            if (frameType == Field.TITLE || frameType == Field.ALBUM || frameType == Field.COMMENT || frameType == Field.ARTIST)
            {
                return value.Length > 32;
            }
            return true;
        }

        private static void writeSubChunk(Stream stream, byte frameCode, string text, Span<byte> buffer)
        {
            stream.WriteByte(frameCode);

            byte type = 1;
            if (extendedFrameTypes.TryGetValue(frameCode, out var frameType)) type = frameType;
            stream.WriteByte(type);

            switch (type)
            {
                case XID6_TVAL:
                    if (frameCode == XID6_TRACK) // Specific case : upper byte is the number 0-99, lower byte is an optional ASCII character
                    {
                        byte trackValue = (byte)Math.Min((ushort)0xFF, TrackUtils.ExtractTrackNumber(text));
                        stream.WriteByte(0); // Optional char support is not implemented
                        stream.WriteByte(trackValue);
                    }
                    else
                    {
                        StreamUtils.WriteUInt16(stream, ushort.Parse(text), buffer); // Value is directly written as an ushort into the length field
                    }
                    break;
                case XID6_TSTR:
                    if (text.Length > 255) text = text[..255];
                    else if (text.Length < 3) text = Utils.BuildStrictLengthString(text, 3, ' ');

                    byte[] textBinary = Utils.Latin1Encoding.GetBytes(text);
                    StreamUtils.WriteUInt16(stream, (ushort)(textBinary.Length + 1), buffer);
                    StreamUtils.WriteBytes(stream, textBinary);
                    stream.WriteByte(0);
                    break;
                case XID6_TINT:
                    StreamUtils.WriteUInt16(stream, 4, buffer);
                    StreamUtils.WriteInt32(stream, Parse(text), buffer);
                    break;
            }
        }

        // Specific implementation for conservation of fields that are required for playback
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public override async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            TagData tag = prepareRemove();
            return await WriteAsync(s, tag, args);
        }

        private TagData prepareRemove()
        {
            TagData result = new TagData();
            foreach (Field b in extendedFrameMapping.Values)
            {
                result.IntegrateValue(b, "");
            }

            byte fieldCode;
            foreach (MetaFieldInfo fieldInfo in GetAdditionalFields())
            {
                fieldCode = byte.Parse(fieldInfo.NativeFieldCode);
                if (!playbackFrames.Contains(fieldCode))
                {
                    MetaFieldInfo emptyFieldInfo = new MetaFieldInfo(fieldInfo);
                    emptyFieldInfo.MarkedForDeletion = true;
                    result.AdditionalFields.Add(emptyFieldInfo);
                }
            }
            return result;
        }
    }
}