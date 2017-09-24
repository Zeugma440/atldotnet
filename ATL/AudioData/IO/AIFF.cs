using ATL.AudioReaders.BinaryLogic;
using ATL.Logging;
using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.AudioData.AudioDataManager;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Audio Interchange File Format files manipulation (extension : .AIF, .AIFF, .AIFC)
    /// 
    /// NB : This class does not implement embedded MIDI data detection nor parsing
    /// </summary>
	class AIFF : MetaDataIO, IAudioDataIO, IMetaDataEmbedder
    {
        public const string AIFF_CONTAINER_ID = "FORM";

        private const string FORMTYPE_AIFF = "AIFF";
        private const string FORMTYPE_AIFC = "AIFC";

        private const string COMPRESSION_NONE       = "NONE";
        private const string COMPRESSION_NONE_LE    = "sowt";

        private const string CHUNKTYPE_COMMON       = "COMM";
        private const string CHUNKTYPE_SOUND        = "SSND";

        private const string CHUNKTYPE_MARKER       = "MARK";
        private const string CHUNKTYPE_INSTRUMENT   = "INST";
        private const string CHUNKTYPE_COMMENTS     = "COMT";
        private const string CHUNKTYPE_NAME         = "NAME";
        private const string CHUNKTYPE_AUTHOR       = "AUTH";
        private const string CHUNKTYPE_COPYRIGHT    = "(c) ";
        private const string CHUNKTYPE_ANNOTATION   = "ANNO"; // Use in discouraged by specs in favour of COMT
        private const string CHUNKTYPE_ID3TAG       = "ID3 ";

        private struct ChunkHeader
        {
            public String ID;
            public int Size;
        }

        // Private declarations 
        private uint channels;
		private uint bits;
        private uint sampleSize;
        private uint numSampleFrames;

        private String format;
        private String compression;
        private byte versionID;

        private int sampleRate;
        private double bitrate;
        private double duration;
        private bool isValid;

        private SizeInfo sizeInfo;
        private readonly string filePath;

        private long id3v2Offset;
        private FileStructureHelper id3v2StructureHelper = new FileStructureHelper();

        private static IDictionary<string, byte> frameMapping; // Mapping between AIFx frame codes and ATL frame codes


        public byte VersionID // Version code
        {
            get { return this.versionID; }
        }
        public uint Channels
		{
			get { return channels; }
		}
		public uint Bits
		{
			get { return bits; }
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
        public int CodecFamily
		{
			get { return (compression.Equals(COMPRESSION_NONE)|| compression.Equals(COMPRESSION_NONE_LE)) ?AudioDataIOFactory.CF_LOSSLESS: AudioDataIOFactory.CF_LOSSY; }
		}
        public bool AllowsParsableMetadata
        {
            get { return true; }
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
            get { return bitrate / 1000.0; }
        }
        public double Duration
        {
            get { return duration; }
        }
        public bool HasNativeMeta()
        {
            return true;
        }
        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE) || (metaDataType == MetaDataIOFactory.TAG_ID3V2);
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
        public override byte FieldCodeFixedLength
        {
            get { return 4; }
        }
        protected override bool IsLittleEndian
        {
            get { return false; }
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

        static AIFF()
        {
            frameMapping = new Dictionary<string, byte>
            {
                { CHUNKTYPE_NAME, TagData.TAG_FIELD_TITLE },
                { CHUNKTYPE_AUTHOR, TagData.TAG_FIELD_ARTIST },
                { CHUNKTYPE_COPYRIGHT, TagData.TAG_FIELD_COPYRIGHT }
            };
        }

        private void resetData()
		{
            duration = 0;
            bitrate = 0;
            isValid = false;

            channels = 0;
			bits = 0;
			sampleRate = 0;

            versionID = 0;

            id3v2Offset = -1;

            ResetData();
        }

        public AIFF(string filePath)
        {
            this.filePath = filePath;

            resetData();
        }

        
        // ---------- SUPPORT METHODS

        private double getCompressionRatio()
        {
            // Get compression ratio 
            if (isValid)
                return (double)sizeInfo.FileSize / ((duration * sampleRate) * (channels * bits / 8.0) + 44) * 100;
            else
                return 0;
        }

        /// <summary>
        /// Reads ID and size of a local chunk and returns them in a dedicated structure _without_ reading nor skipping the data
        /// </summary>
        /// <param name="source">Source where to read header information</param>
        /// <returns>Local chunk header information</returns>
        private ChunkHeader seekNextChunkHeader(BinaryReader source, long limit)
        {
            ChunkHeader header = new ChunkHeader();
            byte[] aByte = new byte[1];

            source.BaseStream.Read(aByte, 0, 1);
            // In case previous field size is not correctly documented, tries to advance to find a suitable first character for an ID
            while ( !((aByte[0] == 40) || ((64 < aByte[0]) && (aByte[0] < 91)) ) && source.BaseStream.Position < limit) 
            {
                source.BaseStream.Read(aByte, 0, 1);
            }

            if (source.BaseStream.Position < limit)
            {
                source.BaseStream.Seek(-1, SeekOrigin.Current);
                
                // Chunk ID
                header.ID = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                // Chunk size
                header.Size = StreamUtils.ReverseInt32(source.ReadInt32());
            }
            else
            {
                header.ID = "";
            }

            return header;
        }

        private void setMetaField(string frameCode, string data, bool readAllMetaFrames, ushort streamNumber = 0, string language = "")
        {
            byte supportedMetaId = 255;

            // Finds the ATL field identifier
            if (frameMapping.ContainsKey(frameCode)) supportedMetaId = frameMapping[frameCode];

            TagData.MetaFieldInfo fieldInfo;
            // If ID has been mapped with an ATL field, store it in the dedicated place...
            if (supportedMetaId < 255)
            {
                tagData.IntegrateValue(supportedMetaId, data);
            }
            else if (readAllMetaFrames) // ...else store it in the additional fields Dictionary
            {
                fieldInfo = new TagData.MetaFieldInfo(getImplementedTagType(), frameCode, data, streamNumber, language);
                if (tagData.AdditionalFields.Contains(fieldInfo)) // Replace current value, since there can be no duplicate fields
                {
                    tagData.AdditionalFields.Remove(fieldInfo);
                }
                tagData.AdditionalFields.Add(fieldInfo);
            }
        }

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return read(source, readTagParams);
        }

        public override bool Read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        private bool read(BinaryReader source, MetaDataIO.ReadTagParams readTagParams)
        {
            bool result = false;
            long position;

            resetData();
            source.BaseStream.Seek(0, SeekOrigin.Begin);

            if (AIFF_CONTAINER_ID.Equals(Utils.Latin1Encoding.GetString(source.ReadBytes(4))))
            {
                // Container chunk size
                long containerChunkPos = source.BaseStream.Position;
                int containerChunkSize = StreamUtils.ReverseInt32(source.ReadInt32());

                // Form type
                format = Utils.Latin1Encoding.GetString(source.ReadBytes(4));

                if (format.Equals(FORMTYPE_AIFF) || format.Equals(FORMTYPE_AIFC))
                {
                    isValid = true;

                    StringBuilder comment = new StringBuilder("");
                    long soundChunkPosition = 0;
                    bool nameFound = false;
                    bool authorFound = false;
                    bool copyrightFound = false;

                    while (source.BaseStream.Position < containerChunkPos + containerChunkSize + 4)
                    {
                        ChunkHeader header = seekNextChunkHeader(source, containerChunkPos + containerChunkSize + 4);

                        position = source.BaseStream.Position;

                        if (header.ID.Equals(CHUNKTYPE_COMMON))
                        {
                            channels = (uint)StreamUtils.ReverseInt16(source.ReadInt16());
                            numSampleFrames = StreamUtils.ReverseUInt32(source.ReadUInt32());
                            sampleSize = (uint)StreamUtils.ReverseInt16(source.ReadInt16());   // This sample size is for uncompressed data only
                            byte[] byteArray = source.ReadBytes(10);
                            Array.Reverse(byteArray);
                            double aSampleRate = StreamUtils.ExtendedToDouble(byteArray);

                            if (format.Equals(FORMTYPE_AIFC))
                            {
                                compression = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
                            }
                            else // AIFF <=> no compression
                            {
                                compression = COMPRESSION_NONE;
                            }

                            if (aSampleRate > 0)
                            {
                                sampleRate = (int)Math.Round(aSampleRate);
                                duration = (double)numSampleFrames / sampleRate;

                                if (!compression.Equals(COMPRESSION_NONE)) // Sample size is specific to selected compression method
                                {
                                    if (compression.ToLower().Equals("fl32")) sampleSize = 32;
                                    else if (compression.ToLower().Equals("fl64")) sampleSize = 64;
                                    else if (compression.ToLower().Equals("alaw")) sampleSize = 8;
                                    else if (compression.ToLower().Equals("ulaw")) sampleSize = 8;
                                }
                                if (duration > 0) bitrate = sampleSize * numSampleFrames * channels / duration;
                            }
                        }
                        else if (header.ID.Equals(CHUNKTYPE_SOUND))
                        {
                            soundChunkPosition = source.BaseStream.Position - 8;
                        }
                        else if (header.ID.Equals(CHUNKTYPE_NAME) || header.ID.Equals(CHUNKTYPE_AUTHOR) || header.ID.Equals(CHUNKTYPE_COPYRIGHT))
                        {
                            structureHelper.AddZone(source.BaseStream.Position - 8, header.Size + 8, header.ID);
                            structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID);

                            tagExists = true;
                            if (header.ID.Equals(CHUNKTYPE_NAME)) nameFound = true;
                            if (header.ID.Equals(CHUNKTYPE_AUTHOR)) authorFound = true;
                            if (header.ID.Equals(CHUNKTYPE_COPYRIGHT)) copyrightFound = true;

                            setMetaField(header.ID, Utils.Latin1Encoding.GetString(source.ReadBytes(header.Size)), readTagParams.ReadAllMetaFrames);
                        }
                        else if (header.ID.Equals(CHUNKTYPE_ANNOTATION))
                        {
                            if (comment.Length > 0) comment.Append(internalLineSeparator);
                            comment.Append(Utils.Latin1Encoding.GetString(source.ReadBytes(header.Size)));
                            tagExists = true;
                        }
                        else if (header.ID.Equals(CHUNKTYPE_COMMENTS))
                        {
                            /*
                             * TODO - Support writing AIFx comments, including timestamp
                             * 
                            structureHelper.AddZone(source.BaseStream.Position - 8, header.Size + 8, header.ID);
                            id3v2.structureHelper.AddSize(containerChunkPos, containerChunkSize, header.ID);
                            */
                            tagExists = true;

                            ushort numComs = StreamUtils.ReverseUInt16(source.ReadUInt16());

                            for (int i = 0; i < numComs; i++)
                            {
                                // Timestamp
                                source.BaseStream.Seek(4, SeekOrigin.Current);
                                // Marker ID
                                short markerId = StreamUtils.ReverseInt16(source.ReadInt16());
                                // Comments length
                                ushort comLength = StreamUtils.ReverseUInt16(source.ReadUInt16());

                                // Only read general purpose comments, not those linked to a marker
                                if (0 == markerId)
                                {
                                    if (comment.Length > 0) comment.Append(internalLineSeparator);
                                    comment.Append(Utils.Latin1Encoding.GetString(source.ReadBytes(comLength)));
                                }
                                else
                                {
                                    source.BaseStream.Seek(comLength, SeekOrigin.Current);
                                }
                            }
                        }
                        else if (header.ID.Equals(CHUNKTYPE_ID3TAG))
                        {
                            id3v2Offset = source.BaseStream.Position;

                            // Zone is already added by Id3v2.Read
                            if (id3v2StructureHelper != null)
                            {
                                id3v2StructureHelper.AddZone(id3v2Offset - 8, header.Size + 8, CHUNKTYPE_ID3TAG);
                                id3v2StructureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_ID3TAG);
                            }
                        }

                        source.BaseStream.Position = position + header.Size;

                        if (header.ID.Equals(CHUNKTYPE_SOUND) && header.Size % 2 > 0) source.BaseStream.Position += 1; // Sound chunk size must be even
                    }

                    tagData.IntegrateValue(TagData.TAG_FIELD_COMMENT, comment.ToString().Replace("\0"," ").Trim());

                    if (-1 == id3v2Offset)
                    {
                        id3v2Offset = 0; // Switch status to "tried to read, but nothing found"
                    }

                    if (readTagParams.PrepareForWriting)
                    {
                        // Add zone placeholders for future tag writing
                        if (-1 == id3v2Offset && id3v2StructureHelper != null)
                        {
                            id3v2StructureHelper.AddZone(soundChunkPosition, 0, CHUNKTYPE_ID3TAG);
                            id3v2StructureHelper.AddSize(containerChunkPos, containerChunkSize, CHUNKTYPE_ID3TAG);
                        }
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
                    }

                    result = true;
                }
			}
  
			return result;
		}

        protected override int write(TagData tag, BinaryWriter w, string zone)
        {
            int result = 0;

            if (zone.Equals(CHUNKTYPE_NAME))
            {
                if (tag.Title.Length > 0)
                {
                    w.Write(Utils.Latin1Encoding.GetBytes(zone));
                    long sizePos = w.BaseStream.Position;
                    w.Write((int)0); // Placeholder for field size that will be rewritten at the end of the method

                    byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag.Title);
                    w.Write(strBytes);

                    w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                    w.Write(StreamUtils.ReverseInt32((int)strBytes.Length));

                    result++;
                }
            }
            else if (zone.Equals(CHUNKTYPE_AUTHOR))
            {
                if (tag.Artist.Length > 0)
                {
                    w.Write(Utils.Latin1Encoding.GetBytes(zone));
                    long sizePos = w.BaseStream.Position;
                    w.Write((int)0); // Placeholder for field size that will be rewritten at the end of the method

                    byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag.Artist);
                    w.Write(strBytes);

                    w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                    w.Write(StreamUtils.ReverseInt32((int)strBytes.Length));

                    result++;
                }
            }
            else if (zone.Equals(CHUNKTYPE_COPYRIGHT))
            {
                if (tag.Copyright.Length > 0)
                {
                    w.Write(Utils.Latin1Encoding.GetBytes(zone));
                    long sizePos = w.BaseStream.Position;
                    w.Write((int)0); // Placeholder for field size that will be rewritten at the end of the method

                    byte[] strBytes = Utils.Latin1Encoding.GetBytes(tag.Copyright);
                    w.Write(strBytes);

                    w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                    w.Write(StreamUtils.ReverseInt32((int)strBytes.Length));

                    result++;
                }
            }
            /*
            * TODO - Support writing AIFx comments, including timestamp
            */

            return result;
        }

        public void WriteID3v2EmbeddingHeader(BinaryWriter w, long tagSize)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNKTYPE_ID3TAG));
            w.Write(StreamUtils.ReverseInt32((int)tagSize));
        }
    }
}