using System;
using System.IO;
using System.Collections.Generic;
using Commons;
using static ATL.AudioData.FileStructureHelper;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.AudioData.IO.FileSurgeon;
using static ATL.ChannelsArrangements;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Free Lossless Audio Codec files manipulation (extension : .FLAC)
    /// </summary>
	class FLAC : IMetaDataIO, IAudioDataIO
    {
        private const byte META_STREAMINFO = 0;
        private const byte META_PADDING = 1;
        private const byte META_APPLICATION = 2;
        private const byte META_SEEKTABLE = 3;
        private const byte META_VORBIS_COMMENT = 4;
        private const byte META_CUESHEET = 5;
        private const byte META_PICTURE = 6;

        private const string FLAC_ID = "fLaC";

        private const byte FLAG_LAST_METADATA_BLOCK = 0x80;


        private class FlacHeader
        {
            public string StreamMarker;
            public byte[] MetaDataBlockHeader = new byte[4];
            public byte[] Info = new byte[18];
            // 16-bytes MD5 Sum only applies to audio data

            public void Reset()
            {
                StreamMarker = "";
                Array.Clear(MetaDataBlockHeader, 0, 4);
                Array.Clear(Info, 0, 18);
            }

            public bool IsValid()
            {
                return StreamMarker.Equals(FLAC_ID);
            }

            public FlacHeader()
            {
                Reset();
            }
        }

        private readonly FlacHeader header;

        private readonly string filePath;
        private AudioDataManager.SizeInfo sizeInfo;

        private VorbisTag vorbisTag;

        IList<FileStructureHelper.Zone> zones; // TODO - That's one hint of why interactions with VorbisTag need to be redesigned...

        // Initial offset of the padding block; used to handle padding the smart way when rewriting data
        private long initialPaddingOffset, initialPaddingSize;

        // Offset of audio data
        private long audioOffset;

        // Physical info
        private int sampleRate;
        private byte bitsPerSample;
        private long samples;
        private ChannelsArrangement channelsArrangement;


        /// <summary>
        ///  Write-time vars (TODO - find a better place than the whole class scope)
        /// </summary>

        // Save a snapshot of the initial embedded pictures for processing purposes
        private IList<PictureInfo> initialPictures;

        // Indexes of currently processed existing and target embedded pictures
        private int existingPictureIndex;
        private int targetPictureIndex;

        // Handling of the 'isLast' bit
        private long latestBlockOffset = -1;
        private byte latestBlockType = 0;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate // Sample rate (hz)
        {
            get { return sampleRate; }
        }
        public bool IsVBR
        {
            get { return false; }
        }
        public bool Exists
        {
            get { return vorbisTag.Exists; }
        }
        /// <inheritdoc/>
        public IList<Format> MetadataFormats
        {
            get
            {
                Format nativeFormat = new Format(MetaDataIOFactory.GetInstance().getFormatsFromPath("native")[0]);
                nativeFormat.Name = "Native / Vorbis (FLAC)";
                nativeFormat.ID += AudioFormat.ID;
                return new List<Format>(new Format[1] { nativeFormat });
            }
        }
        public string FileName
        {
            get { return filePath; }
        }
        public double BitRate
        {
            get { return Math.Round(((double)(sizeInfo.FileSize - audioOffset)) * 8 / Duration); }
        }
        public double Duration
        {
            get { return getDuration(); }
        }
        public ChannelsArrangement ChannelsArrangement
        {
            get { return channelsArrangement; }
        }
        public Format AudioFormat
        {
            get;
        }
        public int CodecFamily
        {
            get { return AudioDataIOFactory.CF_LOSSLESS; }
        }

        #region IMetaDataReader
        public string Title
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Title;
            }
        }

        public string Artist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Artist;
            }
        }

        public string Composer
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Composer;
            }
        }

        public string Comment
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Comment;
            }
        }

        public string Genre
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Genre;
            }
        }

        public ushort Track
        {
            get { return ((IMetaDataIO)vorbisTag).Track; }
        }

        public ushort TrackTotal
        {
            get { return ((IMetaDataIO)vorbisTag).TrackTotal; }
        }

        public ushort Disc
        {
            get { return ((IMetaDataIO)vorbisTag).Disc; }
        }

        public ushort DiscTotal
        {
            get { return ((IMetaDataIO)vorbisTag).DiscTotal; }
        }

        public DateTime Date
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Date;
            }
        }

        public string Year
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Year;
            }
        }

        public string Album
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Album;
            }
        }
        public float Popularity
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Popularity;
            }
        }

        public string Copyright
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Copyright;
            }
        }

        public string OriginalArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalArtist;
            }
        }

        public string OriginalAlbum
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).OriginalAlbum;
            }
        }

        public string GeneralDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).GeneralDescription;
            }
        }

        public string Publisher
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Publisher;
            }
        }

        public DateTime PublishingDate
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PublishingDate;
            }
        }

        public string AlbumArtist
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AlbumArtist;
            }
        }

        public string Conductor
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Conductor;
            }
        }

        public long PaddingSize
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PaddingSize;
            }
        }

        public IList<PictureInfo> PictureTokens
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).PictureTokens;
            }
        }

        public long Size
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Size;
            }
        }

        public IDictionary<string, string> AdditionalFields
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).AdditionalFields;
            }
        }

        public string ChaptersTableDescription
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).ChaptersTableDescription;
            }
        }

        public IList<ChapterInfo> Chapters
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Chapters;
            }
        }

        public LyricsInfo Lyrics
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).Lyrics;
            }
        }

        public IList<PictureInfo> EmbeddedPictures
        {
            get
            {
                return ((IMetaDataIO)vorbisTag).EmbeddedPictures;
            }
        }
        #endregion

        public bool IsMetaSupported(int metaDataType)
        {
            return (metaDataType == MetaDataIOFactory.TAG_NATIVE || metaDataType == MetaDataIOFactory.TAG_ID3V2); // Native is for VorbisTag
        }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Audio data
            sampleRate = 0;
            bitsPerSample = 0;
            samples = 0;
            audioOffset = 0;
            initialPaddingOffset = -1;
            initialPaddingSize = 0;
        }

        public FLAC(string path, Format format)
        {
            filePath = path;
            AudioFormat = format;
            header = new FlacHeader();
            resetData();
        }


        // ---------- SUPPORT METHODS

        // Check for right FLAC file data
        private bool isValid()
        {
            return header.IsValid() &&
                    (channelsArrangement.NbChannels > 0) &&
                    (sampleRate > 0) &&
                    (bitsPerSample > 0) &&
                    (samples > 0);
        }

        private void readHeader(BinaryReader source)
        {
            source.BaseStream.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);

            // Read header data    
            header.Reset();

            header.StreamMarker = Utils.Latin1Encoding.GetString(source.ReadBytes(4));
            header.MetaDataBlockHeader = source.ReadBytes(4);
            header.Info = source.ReadBytes(18);
            source.BaseStream.Seek(16, SeekOrigin.Current); // MD5 sum for audio data
        }

        private double getDuration()
        {
            if (isValid() && (sampleRate > 0))
            {
                return samples * 1000.0 / sampleRate;
            }
            else
            {
                return 0;
            }
        }

        /* Unused for now

                //   Get compression ratio
                private double getCompressionRatio()
                {
                    if (isValid()) 
                    {
                        return (double)sizeInfo.FileSize / (samples * channels * bitsPerSample / 8.0) * 100;
                    } 
                    else 
                    {
                        return 0;
                    }
                }
        */

        public bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeInfo;

            return Read(source, readTagParams);
        }

        // TODO : support for CUESHEET block
        public bool Read(BinaryReader source, ReadTagParams readTagParams)
        {
            bool result = false;

            if (readTagParams.ReadTag && null == vorbisTag) vorbisTag = new VorbisTag(false, false, false, false);

            initialPaddingOffset = -1;
            initialPaddingSize = 0;

            byte[] aMetaDataBlockHeader;
            long position;
            uint blockLength;
            byte blockType;
            int blockIndex;
            bool isLast;
            bool paddingFound = false;
            long blockEndOffset = -1;

            readHeader(source);

            // Process data if loaded and header valid    
            if (header.IsValid())
            {
                int channels = (header.Info[12] >> 1) & 0x7;
                switch (channels)
                {
                    case 0b0000: channelsArrangement = MONO; break;
                    case 0b0001: channelsArrangement = STEREO; break;
                    case 0b0010: channelsArrangement = ISO_3_0_0; break;
                    case 0b0011: channelsArrangement = QUAD; break;
                    case 0b0100: channelsArrangement = ISO_3_2_0; break;
                    case 0b0101: channelsArrangement = ISO_3_2_1; break;
                    case 0b0110: channelsArrangement = LRCLFECrLssRss; break;
                    case 0b0111: channelsArrangement = LRCLFELrRrLssRss; break;
                    case 0b1000: channelsArrangement = JOINT_STEREO_LEFT_SIDE; break;
                    case 0b1001: channelsArrangement = JOINT_STEREO_RIGHT_SIDE; break;
                    case 0b1010: channelsArrangement = JOINT_STEREO_MID_SIDE; break;
                    default: channelsArrangement = UNKNOWN; break;
                }

                sampleRate = header.Info[10] << 12 | header.Info[11] << 4 | header.Info[12] >> 4;
                bitsPerSample = (byte)(((header.Info[12] & 1) << 4) | (header.Info[13] >> 4) + 1);
                samples = header.Info[14] << 24 | header.Info[15] << 16 | header.Info[16] << 8 | header.Info[17];

                if (0 == (header.MetaDataBlockHeader[1] & FLAG_LAST_METADATA_BLOCK)) // metadata block exists
                {
                    blockIndex = 0;
                    vorbisTag.Clear();
                    if (readTagParams.PrepareForWriting)
                    {
                        if (null == zones) zones = new List<Zone>(); else zones.Clear();
                        blockEndOffset = source.BaseStream.Position;
                    }

                    do // Read all metadata blocks
                    {
                        aMetaDataBlockHeader = source.ReadBytes(4);
                        isLast = (aMetaDataBlockHeader[0] & FLAG_LAST_METADATA_BLOCK) > 0; // last flag ( first bit == 1 )

                        blockIndex++;
                        blockLength = StreamUtils.DecodeBEUInt24(aMetaDataBlockHeader, 1);

                        blockType = (byte)(aMetaDataBlockHeader[0] & 0x7F); // decode metablock type
                        position = source.BaseStream.Position;

                        if (blockType == META_VORBIS_COMMENT) // Vorbis metadata
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "", position - 4, (int)blockLength + 4, new byte[0], true, blockType));
                            vorbisTag.Read(source, readTagParams);
                        }
                        else if ((blockType == META_PADDING) && (!paddingFound))  // Padding block (skip any other padding block)
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(PADDING_ZONE_NAME, position - 4, (int)blockLength + 4, new byte[0], true, blockType));
                            initialPaddingSize = blockLength;
                            initialPaddingOffset = position;
                            paddingFound = true;
                            source.BaseStream.Seek(blockLength, SeekOrigin.Current);
                        }
                        else if (blockType == META_PICTURE) // Picture (NB: as per FLAC specs, pictures must be embedded at the FLAC level, not in the VorbisComment !)
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "", position - 4, (int)blockLength + 4, new byte[0], true, blockType));
                            vorbisTag.ReadPicture(source.BaseStream, readTagParams);
                        }
                        else // Unhandled block; needs to be zoned anyway to be able to manage the 'isLast' flag at write-time
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "", position - 4, (int)blockLength + 4, new byte[0], true, blockType));
                        }


                        if (blockType < 7)
                        {
                            source.BaseStream.Seek(position + blockLength, SeekOrigin.Begin);
                            blockEndOffset = position + blockLength;
                        }
                        else
                        {
                            // Abnormal header : incorrect size and/or misplaced last-metadata-block flag
                            break;
                        }
                    }
                    while (!isLast);

                    if (readTagParams.PrepareForWriting)
                    {
                        bool vorbisTagFound = false;
                        bool pictureFound = false;

                        foreach (Zone zone in zones)
                        {
                            if (zone.Flag == META_PICTURE) pictureFound = true;
                            else if (zone.Flag == META_VORBIS_COMMENT) vorbisTagFound = true;
                        }

                        if (!vorbisTagFound) zones.Add(new Zone(META_VORBIS_COMMENT + "", blockEndOffset, 0, new byte[0], true, META_VORBIS_COMMENT));
                        if (!pictureFound) zones.Add(new Zone(META_PICTURE + "", blockEndOffset, 0, new byte[0], true, META_PICTURE));
                        // Padding must be the last block for it to correctly absorb size variations of the other blocks
                        if (!paddingFound && Settings.AddNewPadding) zones.Add(new Zone(PADDING_ZONE_NAME, blockEndOffset, 0, new byte[0], true, META_PADDING));
                    }
                }
            }

            if (isValid())
            {
                audioOffset = source.BaseStream.Position;  // we need that to calculate the bitrate
                result = true;
            }

            return result;
        }

        // NB : This only works if writeVorbisTag is called _before_ writePictures, since tagData fusion is done by vorbisTag.Write
        public bool Write(BinaryReader r, BinaryWriter w, TagData tag, IProgress<float> writeProgress = null)
        {
            // Read all the fields in the existing tag (including unsupported fields)
            ReadTagParams readTagParams = new ReadTagParams(true, true);
            readTagParams.PrepareForWriting = true;
            bool tagExists = Read(r, readTagParams);

            // Save a snapshot of the initial embedded pictures for processing purposes
            existingPictureIndex = 0;
            targetPictureIndex = 0;
            initialPictures = vorbisTag.EmbeddedPictures;

            // Prepare picture data with freshly read vorbisTag
            TagData dataToWrite = new TagData();
            dataToWrite.Pictures = vorbisTag.EmbeddedPictures;
            dataToWrite.IntegrateValues(tag, true, false); // Merge existing information + new tag information except additional fields which will be merged by VorbisComment

            adjustPictureZones(dataToWrite.Pictures);

            FileSurgeon surgeon = new FileSurgeon(null, null, MetaDataIOFactory.TAG_NATIVE, TO_BUILTIN, writeProgress);
            surgeon.RewriteZones(w, new WriteDelegate(write), zones, dataToWrite, tagExists);

            // Set the 'isLast' bit on the actual last block
            w.BaseStream.Seek(latestBlockOffset, SeekOrigin.Begin);
            w.Write((byte)(latestBlockType | FLAG_LAST_METADATA_BLOCK));

            return true;
        }

        private WriteResult write(BinaryWriter w, TagData tag, Zone zone)
        {
            WriteResult result;

            if (zone.Name.Equals(META_VORBIS_COMMENT + "")) result = writeVorbisCommentBlock(w, tag);
            else if (zone.Name.Equals(PADDING_ZONE_NAME)) result = writePaddingBlock(w, tag.DataSizeDelta);
            else if (zone.Name.Equals(META_PICTURE + "")) result = processPictureBlock(w, initialPictures, tag.Pictures, ref existingPictureIndex, ref targetPictureIndex);
            else // Unhandled field - write raw header without 'isLast' bit and let the rest as it is
            {
                w.Write(zone.Flag);
                result = new WriteResult(WriteMode.OVERWRITE, 1);
            }

            // Remember the latest block position
            if (result.WrittenFields > 0)
            {
                latestBlockOffset = zone.Offset + tag.DataSizeDelta;
                latestBlockType = zone.Flag;
            }

            return result;
        }

        /// <summary>
        /// Adjust the number of picture zones to match the actual number of pictures to be written
        /// </summary>
        /// <param name="picturesToWrite">List of pictures to be written</param>
        private void adjustPictureZones(IList<PictureInfo> picturesToWrite)
        {
            int nbExistingPictures = 0;
            int lastPictureZoneIndex = -1;
            long lastPictureZoneOffset = -1;

            for (int i = 0; i < zones.Count; i++)
            {
                if (META_PICTURE == zones[i].Flag)
                {
                    nbExistingPictures++;
                    lastPictureZoneIndex = i;
                    lastPictureZoneOffset = zones[i].Offset;
                }
            }

            // Insert additional picture zones after the current ones (not at the end of the zones list to avoid adding data after the padding block)
            if (nbExistingPictures < picturesToWrite.Count)
            {
                for (int i = 0; i < picturesToWrite.Count - nbExistingPictures; i++)
                    zones.Insert(lastPictureZoneIndex + 1, new Zone(META_PICTURE + "", lastPictureZoneOffset, 0, new byte[0], true, META_PICTURE));
            }
        }

        private WriteResult writeVorbisCommentBlock(BinaryWriter w, TagData tag)
        {
            long sizePos, dataPos, finalPos;

            w.Write(META_VORBIS_COMMENT);
            sizePos = w.BaseStream.Position;
            w.Write(new byte[] { 0, 0, 0 }); // Placeholder for 24-bit integer that will be rewritten at the end of the method

            dataPos = w.BaseStream.Position;
            int writtenFields = vorbisTag.Write(w.BaseStream, tag);

            finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt24((uint)(finalPos - dataPos)));
            w.BaseStream.Seek(finalPos, SeekOrigin.Begin);

            return new WriteResult(WriteMode.REPLACE, writtenFields);
        }

        private WriteResult writePaddingBlock(BinaryWriter w, long cumulativeDelta)
        {
            long paddingSizeToWrite = TrackUtils.ComputePaddingSize(initialPaddingOffset, initialPaddingSize, -cumulativeDelta);
            if (paddingSizeToWrite > 0)
            {
                w.Write(META_PADDING);
                w.Write(StreamUtils.EncodeBEUInt24((uint)paddingSizeToWrite));
                for (int i = 0; i < paddingSizeToWrite; i++) w.Write((byte)0);
                return new WriteResult(WriteMode.REPLACE, 1);
            }
            else return new WriteResult(WriteMode.REPLACE, 0);
        }

        /// <summary>
        /// Process picture block at the index 'targetPictureIndex'
        /// Three outcomes :
        ///     1/ Target picture cannot be written => block is marked for deletion
        ///     2/ Target picture can be written and is identical to existing picture at the same position => block is left as it is
        ///     3/ Target picture can be written and is different to existing picture at the same position => target picture is written
        /// </summary>
        /// <param name="w">Writer to be used</param>
        /// <param name="existingPictures">List of existing pictures on the file</param>
        /// <param name="picturesToWrite">List of pictures to write</param>
        /// <param name="existingPictureIndex">Current index of existing pictures in use in the main write loop</param>
        /// <param name="targetPictureIndex">Current index of target pictures in use in the main write loop</param>
        /// <returns></returns>
        private WriteResult processPictureBlock(BinaryWriter w, IList<PictureInfo> existingPictures, IList<PictureInfo> picturesToWrite, ref int existingPictureIndex, ref int targetPictureIndex)
        {
            bool doWritePicture = false;
            PictureInfo pictureToWrite = null;
            while (!doWritePicture && picturesToWrite.Count > targetPictureIndex)
            {
                pictureToWrite = picturesToWrite[targetPictureIndex++];

                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !pictureToWrite.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = (MetaDataIOFactory.TAG_NATIVE == pictureToWrite.TagType);
                // It also has not to be marked for deletion
                doWritePicture = doWritePicture && (!pictureToWrite.MarkedForDeletion);
            }

            if (doWritePicture)
            {
                bool pictureExists = false;
                // Check if the picture to write is already there ('neutral update' use case)
                if (existingPictures.Count > existingPictureIndex)
                {
                    PictureInfo existingPic = existingPictures[existingPictureIndex++];
                    pictureExists = existingPic.ComputePicHash() == pictureToWrite.ComputePicHash(); // No need to rewrite an identical pic
                }
                if (!pictureExists) return new WriteResult(WriteMode.REPLACE, writePictureBlock(w, pictureToWrite));
                else
                {
                    w.Write(META_PICTURE);
                    return new WriteResult(WriteMode.OVERWRITE, 1);
                }
            }
            else return new WriteResult(WriteMode.REPLACE, 0); // Nothing else to write; existing picture blocks are erased
        }

        private int writePictureBlock(BinaryWriter w, PictureInfo picture)
        {
            long sizePos, dataPos, finalPos;

            w.Write(META_PICTURE);

            sizePos = w.BaseStream.Position;
            w.Write(new byte[] { 0, 0, 0 }); // Placeholder for 24-bit integer that will be rewritten at the end of the method

            dataPos = w.BaseStream.Position;
            vorbisTag.WritePicture(w, picture.PictureData, picture.MimeType, picture.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picture.NativePicCode : ID3v2.EncodeID3v2PictureType(picture.PicType), picture.Description);

            finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt24((uint)(finalPos - dataPos)));
            w.BaseStream.Seek(finalPos, SeekOrigin.Begin);

            return 1;
        }

        public bool Remove(BinaryWriter w)
        {
            bool result = true;
            long cumulativeDelta = 0;

            // Handling of the 'isLast' bit
            latestBlockOffset = -1;
            latestBlockType = 0;

            foreach (Zone zone in zones)
            {
                if (zone.Offset > -1 && zone.Size > zone.CoreSignature.Length)
                {
                    if (zone.Flag == META_PADDING || zone.Flag == META_PICTURE || zone.Flag == META_VORBIS_COMMENT)
                    {
                        StreamUtils.ShortenStream(w.BaseStream, zone.Offset + zone.Size - cumulativeDelta, (uint)(zone.Size - zone.CoreSignature.Length));
                        vorbisTag.Clear();

                        cumulativeDelta += zone.Size - zone.CoreSignature.Length;
                    }
                    else
                    {
                        latestBlockOffset = zone.Offset - cumulativeDelta;
                        latestBlockType = zone.Flag;

                        w.BaseStream.Seek(latestBlockOffset, SeekOrigin.Begin);
                        w.Write(latestBlockType);
                    }
                }
            }

            // Set the 'isLast' bit on the actual last block
            if (latestBlockOffset > -1)
            {
                w.BaseStream.Seek(latestBlockOffset, SeekOrigin.Begin);
                w.Write((byte)(latestBlockType | FLAG_LAST_METADATA_BLOCK));
            }

            return result;
        }

        public void SetEmbedder(IMetaDataEmbedder embedder)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            vorbisTag.Clear();
        }
    }
}