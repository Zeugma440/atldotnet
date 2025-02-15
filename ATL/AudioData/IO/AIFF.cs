using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.AudioDataManager;
using static ATL.ChannelsArrangements;
using System.Linq;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Audio Interchange File Format files manipulation (extension : .AIF, .AIFF, .AIFC)
    /// 
    /// Implementation notes
    /// 
    ///  1/ Annotations being somehow deprecated (Cf. specs "Use of this chunk is discouraged within FORM AIFC. The more refined Comments Chunk should be used instead"),
    ///  any data read from an ANNO chunk will be written as a COMT chunk when updating the file (ANNO chunks will be deleted in the process).
    /// 
    ///  2/ Embedded MIDI detection, parsing and writing is not supported
    ///  
    ///  3/ Instrument detection, parsing and writing is not supported
    /// </summary>
	class AIFF : MetaDataIO, IAudioDataIO, IMetaDataEmbedder
    {
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0051 // Remove unused private members
        public static readonly byte[] AIFF_CONTAINER_ID = Utils.Latin1Encoding.GetBytes("FORM");

        private const string FORMTYPE_AIFF = "AIFF";
        private const string FORMTYPE_AIFC = "AIFC";

        private const string COMPRESSION_NONE = "NONE";
        private const string COMPRESSION_NONE_LE = "sowt";

        private const string CHUNKTYPE_COMMON = "COMM";
        private const string CHUNKTYPE_SOUND = "SSND";

        private const string CHUNKTYPE_MARKER = "MARK";
        private const string CHUNKTYPE_INSTRUMENT = "INST";
        private const string CHUNKTYPE_COMMENTS = "COMT";
        private const string CHUNKTYPE_NAME = "NAME";
        private const string CHUNKTYPE_AUTHOR = "AUTH";
        private const string CHUNKTYPE_COPYRIGHT = "(c) ";
        private const string CHUNKTYPE_ANNOTATION = "ANNO"; // Use in discouraged by specs in favour of COMT
        private const string CHUNKTYPE_ID3TAG = "ID3 ";
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore S1144 // Unused private types or members should be removed


        // AIFx timestamp are defined as "the number of seconds since January 1, 1904"
        private static readonly DateTime timestampBase = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public class CommentData
        {
            public uint Timestamp;
            public short MarkerId;
        }

        private struct ChunkHeader
        {
            public string ID;
            public int Size;
        }

        // Private declarations 
        private int bits;

        private string compression;

        private readonly FileStructureHelper id3v2StructureHelper = new FileStructureHelper(false);

        // Mapping between AIFx frame codes and ATL frame codes
        private static readonly IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { CHUNKTYPE_NAME, Field.TITLE },
            { CHUNKTYPE_AUTHOR, Field.ARTIST },
            { CHUNKTYPE_COPYRIGHT, Field.COPYRIGHT }
        };


        // Version code
        public byte VersionID { get; private set; }


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR => false;

        public AudioFormat AudioFormat
        {
            get;
        }
        public int CodecFamily => compression.Equals(COMPRESSION_NONE) || compression.Equals(COMPRESSION_NONE_LE) ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY;

        public string FileName { get; }

        public int SampleRate { get; private set; }

        public double BitRate { get; private set; }

        public int BitDepth => bits > 0 ? bits : -1;

        public double Duration { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.ID3V2, MetaDataIOFactory.TagType.NATIVE };
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
        public override byte FieldCodeFixedLength => 4;

        protected override bool isLittleEndian => false;

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }


        // IMetaDataEmbedder
        public long HasEmbeddedID3v2 { get; private set; }

        public uint ID3v2EmbeddingHeaderSize => 8;

        public FileStructureHelper.Zone Id3v2Zone => id3v2StructureHelper.GetZone(CHUNKTYPE_ID3TAG);


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            Duration = 0;
            BitRate = 0;
            id3v2StructureHelper.Clear();

            bits = 0;
            SampleRate = 0;

            VersionID = 0;

            HasEmbeddedID3v2 = -1;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public AIFF(string filePath, AudioFormat format)
        {
            this.FileName = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        /// <summary>
        /// Reads ID and size of a local chunk and returns them in a dedicated structure _without_ reading nor skipping data
        /// </summary>
        /// <param name="source">Source where to read header information</param>
        /// <param name="limit">Maximum absolute position to search to</param>
        /// <param name="previousChunkId">ID of the previous chunk</param>
        /// <returns>Local chunk header information</returns>
        private ChunkHeader seekNextChunkHeader(BufferedBinaryReader source, long limit, string previousChunkId)
        {
            ChunkHeader header = new ChunkHeader();
            byte[] aByte = new byte[1];
            int previousChunkSizeCorrection = 0;

            if (source.Read(aByte, 0, 1) < 1) return header;

            // In case previous chunk has a padding byte, seek a suitable first character for an ID
            if (aByte[0] != 40 && !char.IsLetter((char)aByte[0]) && source.Position <= limit)
            {
                previousChunkSizeCorrection++;
                if (source.Position < limit && source.Read(aByte, 0, 1) < 1) return header;
            }

            // Update zone size (remove and replace zone with updated size)
            if (previousChunkId.Length > 0 && previousChunkSizeCorrection > 0)
            {
                FileStructureHelper sHelper = previousChunkId == CHUNKTYPE_ID3TAG ? id3v2StructureHelper : structureHelper;
                FileStructureHelper.Zone previousZone = sHelper.GetZone(previousChunkId);
                if (previousZone != null)
                {
                    previousZone.Size += previousChunkSizeCorrection;
                    sHelper.RemoveZone(previousChunkId);
                    sHelper.AddZone(previousZone);
                }
            }

            // Write actual tag size

            if (source.Position < limit)
            {
                source.Seek(-1, SeekOrigin.Current);

                // Chunk ID
                header.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                // Chunk size
                header.Size = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
            }
            else
            {
                header.ID = "";
            }

            return header;
        }

        public static bool IsValidHeader(byte[] data)
        {
            return StreamUtils.ArrBeginsWith(data, AIFF_CONTAINER_ID);
        }

        public bool Read(Stream source, SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            resetData();
            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            reader.Seek(0, SeekOrigin.Begin);

            // Magic number check
            if (!IsValidHeader(reader.ReadBytes(4))) return false;

            // Container chunk size
            long containerChunkPos = reader.Position;
            int containerChunkSize = StreamUtils.DecodeBEInt32(reader.ReadBytes(4));

            if (containerChunkPos + containerChunkSize + 4 != reader.Length)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Header size is incoherent with file size");
            }

            // Form type
            string format = Utils.Latin1Encoding.GetString(reader.ReadBytes(4));

            // AIFF / AIFC format check
            if (!format.Equals(FORMTYPE_AIFF) && !format.Equals(FORMTYPE_AIFC)) return false;

            StringBuilder commentStr = new StringBuilder("");
            long soundChunkPosition = 0;
            long soundChunkSize = 0; // Header size included
            bool nameFound = false;
            bool authorFound = false;
            bool copyrightFound = false;
            bool commentsFound = false;
            long limit = Math.Min(containerChunkPos + containerChunkSize + 4, reader.Length);

            int annotationIndex = 0;
            int commentIndex = 0;
            string chunkId = "";

            while (reader.Position < limit)
            {
                ChunkHeader header = seekNextChunkHeader(reader, limit, chunkId);
                chunkId = header.ID;

                var position = reader.Position;

                switch (header.ID)
                {
                    case CHUNKTYPE_COMMON:
                        {
                            short channels = StreamUtils.DecodeBEInt16(reader.ReadBytes(2));
                            ChannelsArrangement = channels switch
                            {
                                1 => MONO,
                                2 => STEREO,
                                3 => ISO_3_0_0,
                                4 => ISO_2_2_0, // // Specs actually allow both 2/2.0 and LRCS
                                6 => LRLcRcCS,
                                _ => UNKNOWN
                            };

                            uint numSampleFrames = StreamUtils.DecodeBEUInt32(reader.ReadBytes(4));
                            bits = StreamUtils.DecodeBEInt16(reader.ReadBytes(2)); // This sample size is for uncompressed data only
                            byte[] byteArray = reader.ReadBytes(10);
                            Array.Reverse(byteArray);
                            double aSampleRate = StreamUtils.ExtendedToDouble(byteArray);

                            if (format.Equals(FORMTYPE_AIFC))
                            {
                                compression = Utils.Latin1Encoding.GetString(reader.ReadBytes(4));
                            }
                            else // AIFF <=> no compression
                            {
                                compression = COMPRESSION_NONE;
                            }

                            if (aSampleRate > 0)
                            {
                                SampleRate = (int)Math.Round(aSampleRate);
                                Duration = numSampleFrames * 1000.0 / SampleRate;

                                if (!compression.Equals(COMPRESSION_NONE)) // Sample size is specific to selected compression method
                                {
                                    switch (compression.ToLower())
                                    {
                                        case "fl32":
                                            bits = 32;
                                            break;
                                        case "fl64":
                                            bits = 64;
                                            break;
                                        case "alaw":
                                        case "ulaw":
                                            bits = 8;
                                            break;
                                    }
                                }
                                if (Duration > 0) BitRate = bits * numSampleFrames * ChannelsArrangement.NbChannels / Duration;
                            }

                            break;
                        }
                    case CHUNKTYPE_SOUND:
                        soundChunkPosition = reader.Position - 8;
                        soundChunkSize = header.Size + 8;
                        AudioDataOffset = soundChunkPosition;
                        AudioDataSize = soundChunkSize;
                        break;
                    case CHUNKTYPE_NAME:
                    case CHUNKTYPE_AUTHOR:
                    case CHUNKTYPE_COPYRIGHT:
                        {
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID);
                            switch (header.ID)
                            {
                                case CHUNKTYPE_NAME:
                                    nameFound = true;
                                    break;
                                case CHUNKTYPE_AUTHOR:
                                    authorFound = true;
                                    break;
                                case CHUNKTYPE_COPYRIGHT:
                                    copyrightFound = true;
                                    break;
                            }

                            SetMetaField(header.ID, Utils.Latin1Encoding.GetString(reader.ReadBytes(header.Size)), readTagParams.ReadAllMetaFrames);
                            break;
                        }
                    case CHUNKTYPE_ANNOTATION:
                        {
                            annotationIndex++;
                            chunkId = header.ID + annotationIndex;
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID + annotationIndex);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID + annotationIndex);

                            if (commentStr.Length > 0) commentStr.Append(Settings.InternalValueSeparator);
                            commentStr.Append(Utils.Latin1Encoding.GetString(reader.ReadBytes(header.Size)));
                            break;
                        }
                    case CHUNKTYPE_COMMENTS:
                        {
                            commentIndex++;
                            chunkId = header.ID + commentIndex;
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID + commentIndex);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID + commentIndex);

                            commentsFound = true;

                            ushort numComs = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2));

                            for (int i = 0; i < numComs; i++)
                            {
                                CommentData cmtData = new CommentData
                                {
                                    Timestamp = StreamUtils.DecodeBEUInt32(reader.ReadBytes(4)),
                                    MarkerId = StreamUtils.DecodeBEInt16(reader.ReadBytes(2))
                                };

                                // Comments length
                                ushort comLength = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2));
                                MetaFieldInfo comment = new MetaFieldInfo(getImplementedTagType(), header.ID + commentIndex)
                                {
                                    Value = Utils.Latin1Encoding.GetString(reader.ReadBytes(comLength)),
                                    SpecificData = cmtData
                                };
                                tagData.AdditionalFields.Add(comment);

                                // Only read general purpose comments, not those linked to a marker
                                if (0 == cmtData.MarkerId)
                                {
                                    if (commentStr.Length > 0) commentStr.Append(Settings.InternalValueSeparator);
                                    commentStr.Append(comment.Value);
                                }
                            }

                            break;
                        }
                    case CHUNKTYPE_ID3TAG:
                        HasEmbeddedID3v2 = reader.Position;

                        // Zone is already added by Id3v2.Read
                        id3v2StructureHelper.AddZone(HasEmbeddedID3v2 - 8, header.Size + 8, CHUNKTYPE_ID3TAG);
                        id3v2StructureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_ID3TAG);
                        break;
                }

                reader.Position = position + header.Size;
            } // Loop through file

            var commentVal = commentStr.ToString().Replace("\0", " ").Trim();
            if (commentVal.Length > 0) tagData.IntegrateValue(Field.COMMENT, commentVal);

            if (-1 == HasEmbeddedID3v2)
            {
                HasEmbeddedID3v2 = 0; // Switch status to "tried to read, but nothing found"

                if (readTagParams.PrepareForWriting)
                {
                    id3v2StructureHelper.AddZone(soundChunkPosition + soundChunkSize, 0, CHUNKTYPE_ID3TAG);
                    id3v2StructureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_ID3TAG);
                }
            }

            // Add zone placeholders for future tag writing
            if (readTagParams.PrepareForWriting)
            {
                if (!nameFound)
                {
                    structureHelper.AddZone(soundChunkPosition, 0, CHUNKTYPE_NAME);
                    structureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_NAME);
                }
                if (!authorFound)
                {
                    structureHelper.AddZone(soundChunkPosition, 0, CHUNKTYPE_AUTHOR);
                    structureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_AUTHOR);
                }
                if (!copyrightFound)
                {
                    structureHelper.AddZone(soundChunkPosition, 0, CHUNKTYPE_COPYRIGHT);
                    structureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_COPYRIGHT);
                }
                if (!commentsFound)
                {
                    structureHelper.AddZone(soundChunkPosition, 0, CHUNKTYPE_COMMENTS);
                    structureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_COMMENTS);
                }
            }

            return true;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true);
            return write(tag, w, zone);
        }

        private static int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;
            switch (zone)
            {
                case CHUNKTYPE_NAME:
                    {
                        if (tag[Field.TITLE].Length > 0)
                        {
                            w.Write(Utils.Latin1Encoding.GetBytes(zone));
                            long sizePos = w.BaseStream.Position;
                            w.Write(0); // Placeholder for field size that will be rewritten at the end of the method

                            byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag[Field.TITLE]);
                            w.Write(strBytes);

                            // Add the extra padding byte if needed
                            long finalPos = w.BaseStream.Position;
                            long paddingSize = (finalPos - sizePos) % 2;
                            if (paddingSize > 0) w.BaseStream.WriteByte(0);

                            // Write actual tag size
                            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                            w.Write(StreamUtils.EncodeBEInt32(strBytes.Length));

                            result++;
                        }

                        break;
                    }
                case CHUNKTYPE_AUTHOR:
                    {
                        if (tag[Field.ARTIST].Length > 0)
                        {
                            w.Write(Utils.Latin1Encoding.GetBytes(zone));
                            long sizePos = w.BaseStream.Position;
                            w.Write(0); // Placeholder for field size that will be rewritten at the end of the method

                            byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag[Field.ARTIST]);
                            w.Write(strBytes);

                            // Add the extra padding byte if needed
                            long finalPos = w.BaseStream.Position;
                            long paddingSize = (finalPos - sizePos) % 2;
                            if (paddingSize > 0) w.BaseStream.WriteByte(0);

                            // Write actual tag size
                            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                            w.Write(StreamUtils.EncodeBEInt32(strBytes.Length));

                            result++;
                        }

                        break;
                    }
                case CHUNKTYPE_COPYRIGHT:
                    {
                        if (tag[Field.COPYRIGHT].Length > 0)
                        {
                            w.Write(Utils.Latin1Encoding.GetBytes(zone));
                            long sizePos = w.BaseStream.Position;
                            w.Write(0); // Placeholder for field size that will be rewritten at the end of the method

                            byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag[Field.COPYRIGHT]);
                            w.Write(strBytes);

                            // Add the extra padding byte if needed
                            long finalPos = w.BaseStream.Position;
                            long paddingSize = (finalPos - sizePos) % 2;
                            if (paddingSize > 0) w.BaseStream.WriteByte(0);

                            // Write actual tag size
                            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                            w.Write(StreamUtils.EncodeBEInt32(strBytes.Length));

                            result++;
                        }

                        break;
                    }
                default:
                    {
                        if (zone.StartsWith(CHUNKTYPE_ANNOTATION))
                        {
                            // Do not write anything, this field is deprecated (Cf. specs "Use of this chunk is discouraged within FORM AIFC. The more refined Comments Chunk should be used instead")
                        }
                        else if (zone.StartsWith(CHUNKTYPE_COMMENTS))
                        {
                            bool applicable = tag[Field.COMMENT].Length > 0;
                            if (!applicable && tag.AdditionalFields.Count > 0)
                            {
                                foreach (MetaFieldInfo fieldInfo in tag.AdditionalFields)
                                {
                                    applicable = fieldInfo.NativeFieldCode.StartsWith(CHUNKTYPE_COMMENTS);
                                    if (applicable) break;
                                }
                            }

                            if (applicable)
                            {
                                ushort numComments = 0;
                                w.Write(Utils.Latin1Encoding.GetBytes(CHUNKTYPE_COMMENTS));
                                long sizePos = w.BaseStream.Position;
                                w.Write(0); // Placeholder for 'chunk size' field that will be rewritten at the end of the method
                                w.Write((ushort)0); // Placeholder for 'number of comments' field that will be rewritten at the end of the method

                                // First write generic comments (those linked to the Comment field)
                                string[] comments = tag[Field.COMMENT].Split(Settings.InternalValueSeparator);
                                foreach (string s in comments)
                                {
                                    writeCommentChunk(w, null, s);
                                    numComments++;
                                }

                                // Then write comments linked to a Marker ID
                                if (tag.AdditionalFields != null && tag.AdditionalFields.Count > 0)
                                {
                                    foreach (var fieldInfo in tag.AdditionalFields.Where(fieldInfo => fieldInfo.NativeFieldCode.StartsWith(CHUNKTYPE_COMMENTS)).Where(fieldInfo => ((CommentData)fieldInfo.SpecificData).MarkerId != 0))
                                    {
                                        writeCommentChunk(w, fieldInfo);
                                        numComments++;
                                    }
                                }


                                long dataEndPos = w.BaseStream.Position;

                                // Add the extra padding byte if needed
                                long finalPos = w.BaseStream.Position;
                                long paddingSize = (finalPos - sizePos) % 2;
                                if (paddingSize > 0) w.BaseStream.WriteByte(0);

                                // Write actual tag size
                                w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                                w.Write(StreamUtils.EncodeBEInt32((int)(dataEndPos - sizePos - 4)));
                                w.Write(StreamUtils.EncodeBEUInt16(numComments));

                                result++;
                            }
                        }

                        break;
                    }
            }

            return result;
        }

        private static void writeCommentChunk(BinaryWriter w, MetaFieldInfo info, string comment = "")
        {
            byte[] commentData;

            if (null == info) // Plain string
            {
                w.Write(StreamUtils.EncodeBEUInt32(encodeTimestamp(DateTime.Now)));
                w.Write((short)0);
                commentData = Utils.Latin1Encoding.GetBytes(comment);
            }
            else
            {
                w.Write(StreamUtils.EncodeBEUInt32(((CommentData)info.SpecificData).Timestamp));
                w.Write(StreamUtils.EncodeBEInt16(((CommentData)info.SpecificData).MarkerId));
                commentData = Utils.Latin1Encoding.GetBytes(info.Value);
            }

            w.Write(StreamUtils.EncodeBEUInt16((ushort)commentData.Length));
            w.Write(commentData);
        }

        // AIFx timestamps are "the number of seconds since January 1, 1904"
        private static uint encodeTimestamp(DateTime when)
        {
            return (uint)Math.Round((when.Ticks - timestampBase.Ticks) * 1.0 / TimeSpan.TicksPerSecond);
        }

        public void WriteID3v2EmbeddingHeader(Stream s, long tagSize)
        {
            StreamUtils.WriteBytes(s, Utils.Latin1Encoding.GetBytes(CHUNKTYPE_ID3TAG));
            s.Write(StreamUtils.EncodeBEInt32((int)tagSize));
        }

        public void WriteID3v2EmbeddingFooter(Stream s, long tagSize)
        {
            if (tagSize % 2 > 0) s.WriteByte(0);
        }
    }
}