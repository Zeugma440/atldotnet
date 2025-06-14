using System;
using System.IO;
using System.Collections.Generic;
using static ATL.AudioData.FileStructureHelper;
using static ATL.AudioData.IO.MetaDataIO;
using static ATL.AudioData.IO.FileSurgeon;
using static ATL.ChannelsArrangements;
using static ATL.AudioData.FlacHelper;
using System.Threading.Tasks;
using Commons;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Free Lossless Audio Codec files manipulation (extension : .FLAC)
    /// </summary>
	partial class FLAC : VorbisTagHolder, IMetaDataIO, IAudioDataIO
    {
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0051 // Remove unused private members
        private const byte META_STREAMINFO = 0;
        private const byte META_PADDING = 1;
        private const byte META_APPLICATION = 2;
        private const byte META_SEEKTABLE = 3;
        private const byte META_VORBIS_COMMENT = 4;
        private const byte META_CUESHEET = 5;
        private const byte META_PICTURE = 6;
        private const byte META_INVALID = 127;
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore S1144 // Unused private types or members should be removed

        public static readonly byte[] FLAC_ID = Utils.Latin1Encoding.GetBytes("fLaC");

        private const byte FLAG_LAST_METADATA_BLOCK = 0x80;


        private FlacHeader header;

        private AudioDataManager.SizeInfo sizeInfo;

        IList<Zone> zones; // TODO - That's one hint of why interactions with VorbisTag need to be redesigned...

        // Initial offset of the padding block; used to handle padding the smart way when rewriting data
        private long initialPaddingOffset, initialPaddingSize;

        // Physical info
        private byte bitsPerSample;
        private long samples;


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
        private byte latestBlockType;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        // IAudioDataIO
        public int SampleRate { get; private set; }

        public bool IsVBR => false;

        /// <inheritdoc/>
        public override IList<Format> MetadataFormats
        {
            get
            {
                IList<Format> result = base.MetadataFormats;
                result[0].Name += " (FLAC)";
                result[0].ID += AudioFormat.ID;
                return result;
            }
        }
        /// <inheritdoc/>
        public string FileName { get; }

        /// <inheritdoc/>
        public double BitRate => Math.Round((double)(sizeInfo.FileSize - AudioDataOffset) * 8 / Duration);

        /// <inheritdoc/>
        public int BitDepth => bitsPerSample;
        /// <inheritdoc/>
        public double Duration => getDuration();

        /// <inheritdoc/>
        public ChannelsArrangement ChannelsArrangement { get; private set; }

        /// <inheritdoc/>
        public AudioFormat AudioFormat { get; }

        /// <inheritdoc/>
        public int CodecFamily => AudioDataIOFactory.CF_LOSSLESS;

        /// <inheritdoc/>
        public long AudioDataOffset { get; set; }
        /// <inheritdoc/>
        public long AudioDataSize { get; set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            // Native is for VorbisTag
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE, MetaDataIOFactory.TagType.ID3V2 };
        }
        /// <inheritdoc/>
        public bool IsNativeMetadataRich => true;


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            // Audio data
            SampleRate = 0;
            bitsPerSample = 0;
            samples = 0;
            initialPaddingOffset = -1;
            initialPaddingSize = 0;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        public FLAC(string path, AudioFormat format) : base(false, false, false, false)
        {
            FileName = path;
            AudioFormat = format;
            resetData();
        }


        // ---------- SUPPORT METHODS

        // Check for right FLAC file data
        private bool isValid()
        {
            if (header == null) return false;
            return header.IsValid() &&
                    ChannelsArrangement.NbChannels > 0 &&
                    SampleRate > 0 &&
                    bitsPerSample > 0 &&
                    samples > 0;
        }

        private double getDuration()
        {
            if (isValid() && SampleRate > 0) return samples * 1000.0 / SampleRate;
            return 0;
        }

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeNfo, ReadTagParams readTagParams)
        {
            this.sizeInfo = sizeNfo;

            return Read(source, readTagParams);
        }

        // TODO : support for CUESHEET block
        /// <inheritdoc/>
        public bool Read(Stream source, ReadTagParams readTagParams)
        {
            bool result = false;

            initialPaddingOffset = -1;
            initialPaddingSize = 0;

            int blockIndex;
            bool paddingFound = false;
            long blockEndOffset = -1;

            source.Seek(sizeInfo.ID3v2Size, SeekOrigin.Begin);
            header = ReadHeader(source);

            // Process data if loaded and header valid    
            if (header.IsValid())
            {
                ChannelsArrangement = header.getChannelsArrangement();
                SampleRate = header.SampleRate;
                bitsPerSample = header.BitsPerSample;
                samples = header.NbSamples;

                if (header.MetadataExists)
                {
                    blockIndex = 0;
                    vorbisTag.Clear();
                    if (readTagParams.PrepareForWriting)
                    {
                        if (null == zones) zones = new List<Zone>(); else zones.Clear();
                        blockEndOffset = source.Position;
                    }

                    byte[] metaDataBlockHeader = new byte[4];
                    bool isLast;
                    do // Read all metadata blocks
                    {
                        if (source.Read(metaDataBlockHeader, 0, 4) < 4) return false;
                        isLast = (metaDataBlockHeader[0] & FLAG_LAST_METADATA_BLOCK) > 0; // last flag ( first bit == 1 )

                        blockIndex++;
                        var blockLength = StreamUtils.DecodeBEUInt24(metaDataBlockHeader, 1);

                        var blockType = (byte)(metaDataBlockHeader[0] & 0x7F);
                        var position = source.Position;

                        if (blockType == META_VORBIS_COMMENT) // Vorbis metadata
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "." + zones.Count, position - 4, (int)blockLength + 4, Array.Empty<byte>(), true, blockType));
                            vorbisTag.Read(source, readTagParams);
                        }
                        else if (blockType == META_PADDING && !paddingFound)  // Padding block (skip any other padding block)
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(PADDING_ZONE_NAME, position - 4, (int)blockLength + 4, Array.Empty<byte>(), true, blockType));
                            initialPaddingSize = blockLength;
                            initialPaddingOffset = position;
                            paddingFound = true;
                            source.Seek(blockLength, SeekOrigin.Current);
                        }
                        else if (blockType == META_PICTURE) // Picture (NB: as per FLAC specs, pictures must be embedded at the FLAC level, not in the VorbisComment !)
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "." + zones.Count, position - 4, (int)blockLength + 4, Array.Empty<byte>(), true, blockType));
                            vorbisTag.ReadPicture(source, readTagParams);
                        }
                        else if (blockType != META_INVALID) // Unhandled block; needs to be zoned anyway to be able to manage the 'isLast' flag at write-time
                        {
                            if (readTagParams.PrepareForWriting) zones.Add(new Zone(blockType + "." + zones.Count, position - 4, (int)blockLength + 4, Array.Empty<byte>(), true, blockType));
                        }

                        if (blockType < 7)
                        {
                            source.Seek(position + blockLength, SeekOrigin.Begin);
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

                        if (!vorbisTagFound) zones.Add(new Zone(META_VORBIS_COMMENT + "." + zones.Count, blockEndOffset, 0, Array.Empty<byte>(), true, META_VORBIS_COMMENT));
                        if (!pictureFound) zones.Add(new Zone(META_PICTURE + "." + zones.Count, blockEndOffset, 0, Array.Empty<byte>(), true, META_PICTURE));
                        // Padding must be the last block for it to correctly absorb size variations of the other blocks
                        if (!paddingFound && Settings.AddNewPadding) zones.Add(new Zone(PADDING_ZONE_NAME, blockEndOffset, 0, Array.Empty<byte>(), true, META_PADDING));
                    }
                }
            }

            if (isValid())
            {
                AudioDataOffset = source.Position;
                AudioDataSize = sizeInfo.FileSize - sizeInfo.APESize - sizeInfo.ID3v1Size - AudioDataOffset;
                result = true;
            }

            return result;
        }

        // NB : This only works if writeVorbisTag is called _before_ writePictures, since tagData fusion is done by vorbisTag.Write
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> WriteAsync(Stream s, TagData tag, WriteTagParams args, ProgressToken<float> writeProgress = null)
        {
            Tuple<bool, TagData> results = prepareWrite(s, tag);

            FileSurgeon surgeon = new FileSurgeon(null, null, MetaDataIOFactory.TagType.NATIVE, MetaDataIO.TO_BUILTIN, writeProgress);
            await surgeon.RewriteZonesAsync(s, new FileSurgeon.WriteDelegate(write), zones, results.Item2, results.Item1);

            postWrite(s);

            return true;
        }

        private Tuple<bool, TagData> prepareWrite(Stream r, TagData tag)
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
            return Tuple.Create(tagExists, dataToWrite);
        }

        private void postWrite(Stream w)
        {
            // Remove the isLast flag from the header
            if (latestBlockOffset > header.Offset + 4)
            {
                w.Seek(header.Offset + 4, SeekOrigin.Begin);
                byte b = (byte)w.ReadByte();
                w.Seek(-1, SeekOrigin.Current);
                w.WriteByte((byte)(b & ~FLAG_LAST_METADATA_BLOCK));
            }
            setIsLast(w);
        }

        private WriteResult write(Stream s, TagData tag, Zone zone)
        {
            WriteResult result;

            if (zone.Name.StartsWith(META_VORBIS_COMMENT + ".")) result = writeVorbisCommentBlock(s, tag, vorbisTag);
            else if (zone.Name.Equals(PADDING_ZONE_NAME)) result = writePaddingBlock(s, tag.DataSizeDelta);
            else if (zone.Name.StartsWith(META_PICTURE + ".")) result = processPictureBlock(s, initialPictures, tag.Pictures, ref existingPictureIndex, ref targetPictureIndex);
            else // Unhandled field - write raw header without 'isLast' bit and let the rest as it is
            {
                s.WriteByte(zone.Flag);
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
            long lastPictureZoneEnd = -1;

            for (int i = 0; i < zones.Count; i++)
            {
                if (META_PICTURE == zones[i].Flag)
                {
                    nbExistingPictures++;
                    lastPictureZoneIndex = i;
                    lastPictureZoneEnd = zones[i].Offset + zones[i].Size;
                }
            }

            // Insert additional picture zones after the current ones (not at the end of the zones list to avoid adding data after the padding block)
            if (nbExistingPictures < picturesToWrite.Count)
            {
                for (int i = 0; i < picturesToWrite.Count - nbExistingPictures; i++)
                    zones.Insert(lastPictureZoneIndex + 1, new Zone(META_PICTURE + "." + zones.Count, lastPictureZoneEnd, 0, Array.Empty<byte>(), true, META_PICTURE));
            }
        }

        public static WriteResult writeVorbisCommentBlock(Stream w, TagData tag, VorbisTag vorbisTag, bool isLastMetaBlock = false)
        {
            byte toWrite = META_VORBIS_COMMENT;
            if (isLastMetaBlock) toWrite |= FLAG_LAST_METADATA_BLOCK;
            w.Write(new byte[] { toWrite }, 0, 1);
            var sizePos = w.Position;
            w.Write(new byte[] { 0, 0, 0 }, 0, 3); // Placeholder for 24-bit integer that will be rewritten at the end of the method

            var dataPos = w.Position;
            int writtenFields = vorbisTag.Write(w, tag, new WriteTagParams());

            var finalPos = w.Position;
            w.Seek(sizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt24((uint)(finalPos - dataPos)), 0, 3);
            w.Seek(finalPos, SeekOrigin.Begin);

            return new WriteResult(WriteMode.REPLACE, writtenFields);
        }

        private WriteResult writePaddingBlock(Stream w, long cumulativeDelta, bool isLastMetaBlock = false)
        {
            long paddingSizeToWrite = TrackUtils.ComputePaddingSize(initialPaddingOffset, initialPaddingSize, -cumulativeDelta);
            if (paddingSizeToWrite > 0)
            {
                byte toWrite = META_PADDING;
                if (isLastMetaBlock) toWrite |= FLAG_LAST_METADATA_BLOCK;
                w.WriteByte(toWrite);
                w.Write(StreamUtils.EncodeBEUInt24((uint)paddingSizeToWrite));
                for (int i = 0; i < paddingSizeToWrite; i++) w.WriteByte(0);
                return new WriteResult(WriteMode.REPLACE, 1);
            }
            return new WriteResult(WriteMode.REPLACE, 0);
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
        private static WriteResult processPictureBlock(Stream w, IList<PictureInfo> existingPictures, IList<PictureInfo> picturesToWrite, ref int existingPictureIndex, ref int targetPictureIndex)
        {
            bool doWritePicture = false;
            PictureInfo pictureToWrite = null;
            while (!doWritePicture && picturesToWrite.Count > targetPictureIndex)
            {
                pictureToWrite = picturesToWrite[targetPictureIndex++];

                // Picture has either to be supported, or to come from the right tag standard
                doWritePicture = !pictureToWrite.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported);
                if (!doWritePicture) doWritePicture = MetaDataIOFactory.TagType.NATIVE.Equals(pictureToWrite.TagType);
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
                    // Keep existing picture block as is
                    w.WriteByte(META_PICTURE);
                    return new WriteResult(WriteMode.OVERWRITE, 1);
                }
            }
            else return new WriteResult(WriteMode.REPLACE, 0); // Nothing else to write; existing picture blocks are erased
        }

        private static int writePictureBlock(Stream w, PictureInfo picture, bool isLastMetaBlock = false)
        {
            byte toWrite = META_PICTURE;
            if (isLastMetaBlock) toWrite |= FLAG_LAST_METADATA_BLOCK;
            w.WriteByte(toWrite);

            var sizePos = w.Position;
            w.Write(new byte[] { 0, 0, 0 }); // Placeholder for 24-bit integer that will be rewritten at the end of the method

            var dataPos = w.Position;
            VorbisTag.WritePicture(w, picture.PictureData, picture.MimeType, picture.PicType.Equals(PictureInfo.PIC_TYPE.Unsupported) ? picture.NativePicCode : ID3v2.EncodeID3v2PictureType(picture.PicType), picture.Description);

            var finalPos = w.Position;
            w.Seek(sizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEUInt24((uint)(finalPos - dataPos)));
            w.Seek(finalPos, SeekOrigin.Begin);

            return 1;
        }

        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public async Task<bool> RemoveAsync(Stream s, WriteTagParams args)
        {
            long cumulativeDelta = 0;

            // Handling of the 'isLast' flag for METADATA_BLOCK_HEADER
            latestBlockOffset = header.Offset + 4;
            latestBlockType = 0;

            foreach (var zone in zones)
            {
                if (zone.Offset > -1 && zone.Size > zone.CoreSignature.Length)
                {
                    if (zone.Flag == META_PADDING || zone.Flag == META_PICTURE || zone.Flag == META_VORBIS_COMMENT)
                    {
                        await StreamUtils.ShortenStreamAsync(s, zone.Offset + zone.Size - cumulativeDelta, (uint)(zone.Size - zone.CoreSignature.Length));
                        vorbisTag.Clear();

                        cumulativeDelta += zone.Size - zone.CoreSignature.Length;
                    }
                    else
                    {
                        latestBlockOffset = zone.Offset - cumulativeDelta;
                        latestBlockType = zone.Flag;

                        s.Seek(latestBlockOffset, SeekOrigin.Begin);
                        s.WriteByte(latestBlockType);
                    }
                }
            }
            setIsLast(s);

            return true;
        }

        // Set the 'isLast' flag on the header of the actual last metadata block
        private void setIsLast(Stream s)
        {
            s.Seek(latestBlockOffset, SeekOrigin.Begin);
            s.WriteByte((byte)(latestBlockType | FLAG_LAST_METADATA_BLOCK));
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