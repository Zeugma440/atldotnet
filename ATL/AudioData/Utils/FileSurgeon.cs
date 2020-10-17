using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    class FileSurgeon
    {
        // Modes for zone block modification
        public enum WriteMode
        {
            REPLACE = 0,   // Replace : existing block is replaced by written data
            OVERWRITE = 1  // Overwrite : written data overwrites existing block (non-overwritten parts are kept as is)
        }

        // Modes for zone management
        public enum ZoneManagement
        {
            ON_DISK = 0,    // Modifications are performed directly on disk; adapted for small files or single zones
            BUFFERED = 1    // Modifications are performed in a memory buffer, then written on disk in one go
        }


        private readonly FileStructureHelper structureHelper;
        private readonly IMetaDataEmbedder embedder;

        private readonly int implementedTagType;
        private readonly int defaultTagOffset;

        //        private readonly IProgress<float> writeProgress;
        //        private float currentProgress;
        //        private int totalProgressSteps;

        public delegate WriteResult WriteDelegate(BinaryWriter w, TagData tag, Zone zone);


        public class WriteResult
        {
            public readonly WriteMode RequiredMode;
            public readonly int WrittenFields;

            public WriteResult(WriteMode requiredMode, int writtenFields)
            {
                RequiredMode = requiredMode;
                WrittenFields = writtenFields;
            }
        }


        public FileSurgeon(
            FileStructureHelper structureHelper,
            IMetaDataEmbedder embedder,
            int implementedTagType,
            int defaultTagOffset,
            IProgress<float> writeProgress)
        {
            this.structureHelper = structureHelper;
            this.embedder = embedder;
            this.implementedTagType = implementedTagType;
            this.defaultTagOffset = defaultTagOffset;
            //            this.writeProgress = writeProgress;
        }


        public bool RewriteZones(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            ZoneManagement mode;
            if (1 == zones.Count) mode = ZoneManagement.ON_DISK;
            else mode = ZoneManagement.BUFFERED;

            //            currentProgress = 0;
            //            totalProgressSteps = 0;

            if (ZoneManagement.ON_DISK == mode) return RewriteZonesDirect(w, null, write, zones, dataToWrite, tagExists);
            else return RewriteZonesHybrid(w, write, zones, dataToWrite, tagExists);
        }

        BinaryWriter getWriter(BinaryWriter writer, BinaryWriter staticWriter, Zone zone)
        {
            if (null == staticWriter || zone.Resizable) return writer;
            return staticWriter;
        }

        private bool RewriteZonesDirect(
            BinaryWriter writer,
            BinaryWriter staticWriter,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists,
            long globalOffsetCorrection = 0,
            bool buffered = false)
        {
            long oldTagSize;
            long newTagSize;
            long cumulativeDelta = 0;
            bool result = true;

            //            totalProgressSteps += zones.Count;
            foreach (Zone zone in zones)
            {
                oldTagSize = zone.Size;

                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "RewriteZonesDirect " + ((staticWriter != null && !zone.Resizable) ? "(static mode)" : "") + " : Loading " + Utils.GetBytesReadable(zone.Size) + " into memory)");

                // Write new tag to a MemoryStream
                using (MemoryStream s = new MemoryStream(zone.Size))
                using (BinaryWriter msw = new BinaryWriter(s, Settings.DefaultTextEncoding))
                {
                    dataToWrite.DataSizeDelta = cumulativeDelta;
                    WriteResult writeResult = write(msw, dataToWrite, zone);

                    if (WriteMode.REPLACE == writeResult.RequiredMode)
                    {
                        if (writeResult.WrittenFields > 0)
                        {
                            newTagSize = s.Length;

                            if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2 && embedder.ID3v2EmbeddingHeaderSize > 0)
                            {
                                StreamUtils.LengthenStream(s, 0, embedder.ID3v2EmbeddingHeaderSize);
                                s.Position = 0;
                                embedder.WriteID3v2EmbeddingHeader(msw, newTagSize);

                                newTagSize = s.Length;
                            }
                        }
                        else
                        {
                            newTagSize = zone.CoreSignature.Length;
                        }
                    }
                    else // Overwrite mode
                    {
                        newTagSize = zone.Size;
                    }

                    // -- Adjust tag slot to new size in file --
                    long tagBeginOffset, tagEndOffset;

                    if (tagExists && zone.Size > zone.CoreSignature.Length) // An existing tag has been reprocessed
                    {
                        tagBeginOffset = zone.Offset + cumulativeDelta - globalOffsetCorrection;
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }
                    else // A brand new tag has been added to the file
                    {
                        if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
                        {
                            tagBeginOffset = embedder.Id3v2Zone.Offset - globalOffsetCorrection;
                        }
                        else
                        {
                            switch (defaultTagOffset)
                            {
                                case MetaDataIO.TO_EOF: tagBeginOffset = getWriter(writer, staticWriter, zone).BaseStream.Length; break;
                                case MetaDataIO.TO_BOF: tagBeginOffset = 0; break;
                                case MetaDataIO.TO_BUILTIN: tagBeginOffset = zone.Offset + cumulativeDelta; break;
                                default: tagBeginOffset = -1; break;
                            }
                            tagBeginOffset -= globalOffsetCorrection;
                        }
                        tagEndOffset = tagBeginOffset + zone.Size;
                    }

                    if (WriteMode.REPLACE == writeResult.RequiredMode)
                    {
                        // Need to build a larger file
                        if (newTagSize > zone.Size)
                        {
                            if (!buffered) Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Lengthening (delta=" + (newTagSize - zone.Size) + ")");
                            StreamUtils.LengthenStream(getWriter(writer, staticWriter, zone).BaseStream, tagEndOffset, (uint)(newTagSize - zone.Size));
                        }
                        else if (newTagSize < zone.Size) // Need to reduce file size
                        {
                            if (!buffered) Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Shortening (delta=" + (newTagSize - zone.Size) + ")");
                            StreamUtils.ShortenStream(getWriter(writer, staticWriter, zone).BaseStream, tagEndOffset, (uint)(zone.Size - newTagSize));
                        }
                    }

                    // Copy tag contents to the new slot
                    getWriter(writer, staticWriter, zone).BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                    s.Seek(0, SeekOrigin.Begin);

                    if (writeResult.WrittenFields > 0)
                    {
                        StreamUtils.CopyStream(s, getWriter(writer, staticWriter, zone).BaseStream);
                    }
                    else
                    {
                        if (zone.CoreSignature.Length > 0) msw.Write(zone.CoreSignature);
                    }

                    long delta = newTagSize - oldTagSize;
                    cumulativeDelta += delta;

                    // Edit wrapping size markers and frame counters if needed
                    if (structureHelper != null && (MetaDataIOFactory.TAG_NATIVE == implementedTagType || (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)))
                    {
                        ACTION action;
                        bool isTagWritten = (writeResult.WrittenFields > 0);

                        if (0 == delta) action = ACTION.Edit; // Zone content has not changed; headers might need to be rewritten (e.g. offset changed)
                        else
                        {
                            if (oldTagSize == zone.CoreSignature.Length && isTagWritten) action = ACTION.Add;
                            else if (newTagSize == zone.CoreSignature.Length && !isTagWritten) action = ACTION.Delete;
                            else action = ACTION.Edit;
                        }
                        // Use plain writer here on purpose because its zone contains headers for the zones adressed by the static writer
                        result = structureHelper.RewriteHeaders(writer, delta, action, zone.Name, globalOffsetCorrection);
                    }

                    zone.Size = (int)newTagSize;
                }
                //                if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);
            } // Loop through zones

            return result;
        }

        private bool RewriteZonesBuffered(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            bool result = true;
            //            totalProgressSteps += 3;

            // Load the 'interesting' part of the file in memory
            // TODO - detect and fine-tune cases when block at the extreme ends of the file are considered (e.g. SPC, certain MP4s where useful zones are at the very end)
            long chunkBeginOffset = getFirstRecordedOffset(zones);
            long chunkEndOffset = getLastRecordedOffset(zones);

            if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
            {
                chunkBeginOffset = Math.Min(chunkBeginOffset, embedder.Id3v2Zone.Offset);
                chunkEndOffset = Math.Max(chunkEndOffset, embedder.Id3v2Zone.Offset + embedder.Id3v2Zone.Size);
            }

            long initialChunkLength = chunkEndOffset - chunkBeginOffset;


            w.BaseStream.Seek(chunkBeginOffset, SeekOrigin.Begin);

            using (MemoryStream chunk = new MemoryStream((int)initialChunkLength))
            {
                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "RewriteZonesBuffered : Loading " + Utils.GetBytesReadable(initialChunkLength) + " into memory)");

                StreamUtils.CopyStream(w.BaseStream, chunk, (int)initialChunkLength);
                //                if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);

                using (BinaryWriter msw = new BinaryWriter(chunk, Settings.DefaultTextEncoding))
                {
                    result = RewriteZonesDirect(msw, null, write, zones, dataToWrite, tagExists, chunkBeginOffset, true);

                    // -- Adjust file slot to new size of chunk --
                    long tagBeginOffset = chunkBeginOffset;
                    long tagEndOffset = tagBeginOffset + initialChunkLength;

                    // Need to build a larger file
                    if (chunk.Length > initialChunkLength)
                    {
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Lengthening (delta=" + (chunk.Length - initialChunkLength) + ")");
                        StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(chunk.Length - initialChunkLength));
                    }
                    else if (chunk.Length < initialChunkLength) // Need to reduce file size
                    {
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Shortening (delta=" + (chunk.Length - initialChunkLength) + ")");
                        StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(initialChunkLength - chunk.Length));
                    }
                    //                    if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);

                    // Copy tag contents to the new slot
                    w.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                    chunk.Seek(0, SeekOrigin.Begin);

                    StreamUtils.CopyStream(chunk, w.BaseStream);
                    //                    if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);
                }
            }

            return result;
        }

        private bool RewriteZonesHybrid(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            bool result = true;
            //            totalProgressSteps += 3;

            // Load the 'interesting' part of the file in memory
            // TODO - detect and fine-tune cases when block at the extreme ends of the file are considered (e.g. SPC, certain MP4s where useful zones are at the very end)

            ICollection<Zone> resizableZones = zones.Where(zone => zone.Resizable).ToList();
            ICollection<Zone> staticZones = zones.Where(zone => !zone.Resizable).ToList();

            long chunkBeginOffset = getFirstRecordedOffset(resizableZones);
            long chunkEndOffset = getLastRecordedOffset(resizableZones);

            if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
            {
                chunkBeginOffset = Math.Min(chunkBeginOffset, embedder.Id3v2Zone.Offset);
                chunkEndOffset = Math.Max(chunkEndOffset, embedder.Id3v2Zone.Offset + embedder.Id3v2Zone.Size);
            }

            long initialChunkLength = chunkEndOffset - chunkBeginOffset;


            w.BaseStream.Seek(chunkBeginOffset, SeekOrigin.Begin);

            using (MemoryStream chunk = new MemoryStream((int)initialChunkLength))
            {
                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "RewriteZonesBuffered : Loading " + Utils.GetBytesReadable(initialChunkLength) + " into memory)");

                StreamUtils.CopyStream(w.BaseStream, chunk, (int)initialChunkLength);
                //                if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);

                using (BinaryWriter msw = new BinaryWriter(chunk, Settings.DefaultTextEncoding))
                {
                    result = RewriteZonesDirect(msw, w, write, zones, dataToWrite, tagExists, chunkBeginOffset, true);

                    // -- Adjust file slot to new size of chunk --
                    long tagBeginOffset = chunkBeginOffset;
                    long tagEndOffset = tagBeginOffset + initialChunkLength;

                    // Need to build a larger file
                    if (chunk.Length > initialChunkLength)
                    {
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Lengthening (delta=" + (chunk.Length - initialChunkLength) + ")");
                        StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(chunk.Length - initialChunkLength));
                    }
                    else if (chunk.Length < initialChunkLength) // Need to reduce file size
                    {
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation : Shortening (delta=" + (chunk.Length - initialChunkLength) + ")");
                        StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(initialChunkLength - chunk.Length));
                    }
                    //                    if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);

                    // Copy tag contents to the new slot
                    w.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                    chunk.Seek(0, SeekOrigin.Begin);

                    StreamUtils.CopyStream(chunk, w.BaseStream);
                    //                    if (writeProgress != null) writeProgress.Report(++currentProgress / totalProgressSteps);
                }
            }

            return result;
        }

        private static long getFirstRecordedOffset(ICollection<Zone> zones)
        {
            long result = long.MaxValue;
            if (zones != null)
                foreach (Zone zone in zones)
                {
                    result = Math.Min(result, zone.Offset);
                    foreach (FrameHeader header in zone.Headers)
                        result = Math.Min(result, header.Position);
                }
            return result;
        }

        private static long getLastRecordedOffset(ICollection<Zone> zones)
        {
            long result = 0;
            if (zones != null)
                foreach (Zone zone in zones)
                {
                    result = Math.Max(result, zone.Offset + zone.Size);
                    foreach (FrameHeader header in zone.Headers)
                        result = Math.Max(result, header.Position);
                }
            return result;
        }
    }
}
