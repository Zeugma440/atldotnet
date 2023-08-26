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
#pragma warning restore S1144 // Unused private types or members should be removed


        // AIFx timestamp are defined as "the number of seconds since January 1, 1904"
        private static DateTime timestampBase = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
        private byte versionID;

        private int sampleRate;
        private double bitrate;
        private double duration;
        private ChannelsArrangement channelsArrangement;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private long id3v2Offset;
        private readonly FileStructureHelper id3v2StructureHelper = new FileStructureHelper(false);

        // Mapping between AIFx frame codes and ATL frame codes
        private static IDictionary<string, Field> frameMapping = new Dictionary<string, Field>
        {
            { CHUNKTYPE_NAME, Field.TITLE },
            { CHUNKTYPE_AUTHOR, Field.ARTIST },
            { CHUNKTYPE_COPYRIGHT, Field.COPYRIGHT }
        };


        public byte VersionID // Version code
        {
            get { return this.versionID; }
        }
        public double CompressionRatio
        {
            get { return getCompressionRatio(); }
        }



        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public bool IsVBR
        {
            get { return false; }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return (compression.Equals(COMPRESSION_NONE) || compression.Equals(COMPRESSION_NONE_LE)) ? AudioDataIOFactory.CF_LOSSLESS : AudioDataIOFactory.CF_LOSSY; }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public int SampleRate
        {
            get { return sampleRate; }
        }
        public double BitRate
        {
            get { return bitrate; }
        }

        public int BitDepth => (bits > 0) ? bits : -1;

        public double Duration
        {
            get { return duration; }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public bool IsMetaSupported(MetaDataIOFactory.TagType metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TagType.NATIVE) || (metaDataType == MetaDataIOFactory.TagType.ID3V2);
        }

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
        public override byte FieldCodeFixedLength
        {
            get { return 4; }
        }
        protected override bool isLittleEndian
        {
            get { return false; }
        }
        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            // Finds the ATL field identifier according to the ID3v2 version
            if (frameMapping.ContainsKey(ID)) supportedMetaId = frameMapping[ID];

            return supportedMetaId;
        }


        // IMetaDataEmbedder
        public long HasEmbeddedID3v2
        {
            get { return id3v2Offset; }
        }
        public uint ID3v2EmbeddingHeaderSize
        {
            get { return 8; }
        }
        public FileStructureHelper.Zone Id3v2Zone
        {
            get { return id3v2StructureHelper.GetZone(CHUNKTYPE_ID3TAG); }
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        private void resetData()
        {
            duration = 0;
            bitrate = 0;
            isValid = false;
            id3v2StructureHelper.Clear();

            bits = 0;
            sampleRate = 0;

            versionID = 0;

            id3v2Offset = -1;
            AudioDataOffset = -1;
            AudioDataSize = 0;

            ResetData();
        }

        public AIFF(string filePath, Format format)
        {
            this.filePath = filePath;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        private double getCompressionRatio()
        {
            // Get compression ratio 
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration / 1000.0 * sampleRate) * (channelsArrangement.NbChannels * bits / 8.0) + 44) * 100;
            else
                return 0;
        }

        /// <summary>
        /// Reads ID and size of a local chunk and returns them in a dedicated structure _without_ reading nor skipping the data
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

            source.Read(aByte, 0, 1);
            // In case previous chunk has a padding byte, seek a suitable first character for an ID
            if (!((aByte[0] == 40) || ((64 < aByte[0]) && (aByte[0] < 91))) && source.Position <= limit)
            {
                previousChunkSizeCorrection++;
                if (source.Position < limit) source.Read(aByte, 0, 1);
            }

            // Update zone size (remove and replace zone with updated size)
            if (previousChunkId.Length > 0 && previousChunkSizeCorrection > 0)
            {
                FileStructureHelper sHelper = (previousChunkId == CHUNKTYPE_ID3TAG) ? id3v2StructureHelper : structureHelper;
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

        public bool Read(Stream source, SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            bool result = false;
            long position;

            resetData();
            BufferedBinaryReader reader = new BufferedBinaryReader(source);
            reader.Seek(0, SeekOrigin.Begin);

            if (IsValidHeader(reader.ReadBytes(4)))
            {
                // Container chunk size
                long containerChunkPos = reader.Position;
                int containerChunkSize = StreamUtils.DecodeBEInt32(reader.ReadBytes(4));

                if (containerChunkPos + containerChunkSize + 4 != reader.Length)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Header size is incoherent with file size");
                }

                // Form type
                string format = Utils.Latin1Encoding.GetString(reader.ReadBytes(4));

                if (format.Equals(FORMTYPE_AIFF) || format.Equals(FORMTYPE_AIFC))
                {
                    isValid = true;

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

                        position = reader.Position;

                        if (header.ID.Equals(CHUNKTYPE_COMMON))
                        {
                            short channels = StreamUtils.DecodeBEInt16(reader.ReadBytes(2));
                            switch (channels)
                            {
                                case 1: channelsArrangement = MONO; break;
                                case 2: channelsArrangement = STEREO; break;
                                case 3: channelsArrangement = ISO_3_0_0; break;
                                case 4: channelsArrangement = ISO_2_2_0; break; // Specs actually allow both 2/2.0 and LRCS
                                case 6: channelsArrangement = LRLcRcCS; break;
                                default: channelsArrangement = UNKNOWN; break;
                            }

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
                                sampleRate = (int)Math.Round(aSampleRate);
                                duration = (double)numSampleFrames * 1000.0 / sampleRate;

                                if (!compression.Equals(COMPRESSION_NONE)) // Sample size is specific to selected compression method
                                {
                                    if (compression.ToLower().Equals("fl32")) bits = 32;
                                    else if (compression.ToLower().Equals("fl64")) bits = 64;
                                    else if (compression.ToLower().Equals("alaw")) bits = 8;
                                    else if (compression.ToLower().Equals("ulaw")) bits = 8;
                                }
                                if (duration > 0) bitrate = bits * numSampleFrames * channelsArrangement.NbChannels / duration;
                            }
                        }
                        else if (header.ID.Equals(CHUNKTYPE_SOUND))
                        {
                            soundChunkPosition = reader.Position - 8;
                            soundChunkSize = header.Size + 8;
                            AudioDataOffset = soundChunkPosition;
                            AudioDataSize = soundChunkSize;
                        }
                        else if (header.ID.Equals(CHUNKTYPE_NAME) || header.ID.Equals(CHUNKTYPE_AUTHOR) || header.ID.Equals(CHUNKTYPE_COPYRIGHT))
                        {
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID);

                            tagExists = true;
                            if (header.ID.Equals(CHUNKTYPE_NAME)) nameFound = true;
                            if (header.ID.Equals(CHUNKTYPE_AUTHOR)) authorFound = true;
                            if (header.ID.Equals(CHUNKTYPE_COPYRIGHT)) copyrightFound = true;

                            SetMetaField(header.ID, Utils.Latin1Encoding.GetString(reader.ReadBytes(header.Size)), readTagParams.ReadAllMetaFrames);
                        }
                        else if (header.ID.Equals(CHUNKTYPE_ANNOTATION))
                        {
                            annotationIndex++;
                            chunkId = header.ID + annotationIndex;
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID + annotationIndex);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID + annotationIndex);

                            if (commentStr.Length > 0) commentStr.Append(Settings.InternalValueSeparator);
                            commentStr.Append(Utils.Latin1Encoding.GetString(reader.ReadBytes(header.Size)));
                            tagExists = true;
                        }
                        else if (header.ID.Equals(CHUNKTYPE_COMMENTS))
                        {
                            commentIndex++;
                            chunkId = header.ID + commentIndex;
                            structureHelper.AddZone(reader.Position - 8, header.Size + 8, header.ID + commentIndex);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID + commentIndex);

                            tagExists = true;
                            commentsFound = true;

                            ushort numComs = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2));

                            for (int i = 0; i < numComs; i++)
                            {
                                CommentData cmtData = new CommentData();
                                cmtData.Timestamp = StreamUtils.DecodeBEUInt32(reader.ReadBytes(4));
                                cmtData.MarkerId = StreamUtils.DecodeBEInt16(reader.ReadBytes(2));

                                // Comments length
                                ushort comLength = StreamUtils.DecodeBEUInt16(reader.ReadBytes(2));
                                MetaFieldInfo comment = new MetaFieldInfo(getImplementedTagType(), header.ID + commentIndex);
                                comment.Value = Utils.Latin1Encoding.GetString(reader.ReadBytes(comLength));
                                comment.SpecificData = cmtData;
                                tagData.AdditionalFields.Add(comment);

                                // Only read general purpose comments, not those linked to a marker
                                if (0 == cmtData.MarkerId)
                                {
                                    if (commentStr.Length > 0) commentStr.Append(Settings.InternalValueSeparator);
                                    commentStr.Append(comment.Value);
                                }
                            }
                        }
                        else if (header.ID.Equals(CHUNKTYPE_ID3TAG))
                        {
                            id3v2Offset = reader.Position;

                            // Zone is already added by Id3v2.Read
                            id3v2StructureHelper.AddZone(id3v2Offset - 8, header.Size + 8, CHUNKTYPE_ID3TAG);
                            id3v2StructureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_ID3TAG);
                        }

                        reader.Position = position + header.Size;
                    } // Loop through file

                    tagData.IntegrateValue(Field.COMMENT, commentStr.ToString().Replace("\0", " ").Trim());

                    if (-1 == id3v2Offset)
                    {
                        id3v2Offset = 0; // Switch status to "tried to read, but nothing found"

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

                    result = true;
                } // AIFF / AIFC format check
            } // Magic number check

            return result;
        }

        protected override int write(TagData tag, Stream s, string zone)
        {
            using (BinaryWriter w = new BinaryWriter(s, Encoding.UTF8, true)) return write(tag, w, zone);
        }

        private int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;
            if (zone.Equals(CHUNKTYPE_NAME))
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
            }
            else if (zone.Equals(CHUNKTYPE_AUTHOR))
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
            }
            else if (zone.Equals(CHUNKTYPE_COPYRIGHT))
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
            }
            else if (zone.StartsWith(CHUNKTYPE_ANNOTATION))
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