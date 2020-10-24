using Commons;
using System;
using System.Collections.Generic;
using System.IO;
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

        private class ZoneRegion
        {
            public ZoneRegion(int id)
            {
                Id = id;
            }

            public readonly int Id;
            public bool IsBufferable = true;
            public IList<Zone> Zones = new List<Zone>();

            public long StartOffset => FileSurgeon.getFirstRecordedOffset(Zones);

            public long EndOffset => FileSurgeon.getLastRecordedOffset(Zones);

            public int Size => (int)(EndOffset - StartOffset);

            public override string ToString()
            {
                return "#" + Id + " : " + StartOffset + "->" + EndOffset + "(" + Utils.GetBytesReadable(Size) + ") IsBufferable = " + IsBufferable;
            }
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
        }


        public bool RewriteZones(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            ZoneManagement mode;
            if (1 == zones.Count || Settings.ForceDiskIO) mode = ZoneManagement.ON_DISK;
            else mode = ZoneManagement.BUFFERED;


            //            mode = ZoneManagement.ON_DISK;

            /*
            if (ZoneManagement.ON_DISK == mode) return RewriteZonesDirect(w, write, zones, dataToWrite, tagExists);
            else return RewriteZonesHybrid(w, write, zones, dataToWrite, tagExists);
            */
            return RewriteZones(w, write, zones, dataToWrite, tagExists, mode == ZoneManagement.BUFFERED);
        }

        private bool RewriteZones(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists,
            bool useBuffer)
        {
            long oldTagSize;
            long newTagSize;
            long globalOffsetCorrection;
            long globalCumulativeDelta = 0;
            bool result = true;
            bool isBuffered = false;

            IList<ZoneRegion> zoneRegions = computeZoneRegions(zones, w.BaseStream.Length);
            BinaryWriter writer = w;

            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "========================================");
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Found " + zoneRegions.Count + " regions");
            foreach (ZoneRegion region in zoneRegions) Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, region.ToString());
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "========================================");

            int regionIndex = 0;
            foreach (ZoneRegion region in zoneRegions)
            {
                long regionCumulativeDelta = 0;
                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "------------ REGION " + regionIndex++);

                int initialBufferSize = region.Size;
                MemoryStream buffer = null;
                try
                {
                    if (useBuffer && region.IsBufferable)
                    {
                        isBuffered = true;
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Buffering " + Utils.GetBytesReadable(initialBufferSize));
                        buffer = new MemoryStream(initialBufferSize);

                        // Copy file data to buffer
                        if (initialBufferSize > 0)
                        {
                            w.BaseStream.Seek(region.StartOffset + globalCumulativeDelta, SeekOrigin.Begin);
                            //w.BaseStream.Seek(structureHelper.getCorrectedOffset(region.StartOffset), SeekOrigin.Begin); <-- won't work for classes that don't use FileStructureHelper (FLAC)
                            StreamUtils.CopyStream(w.BaseStream, buffer, initialBufferSize);
                        }

                        writer = new BinaryWriter(buffer, Settings.DefaultTextEncoding);
                        globalOffsetCorrection = region.StartOffset;
                    }
                    else
                    {
                        isBuffered = false;
                        writer = w;
                        globalOffsetCorrection = 0;
                    }

                    foreach (Zone zone in region.Zones)
                    {
                        oldTagSize = zone.Size;

                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "------ ZONE " + zone.Name + "@" + zone.Offset);
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Allocating " + Utils.GetBytesReadable(zone.Size));

                        // Write new tag to a MemoryStream
                        using (MemoryStream s = new MemoryStream(zone.Size))
                        using (BinaryWriter msw = new BinaryWriter(s, Settings.DefaultTextEncoding))
                        {
                            // DataSizeDelta needs to be incremented to be used by classes that don't use FileStructureHelper (e.g. FLAC)
                            dataToWrite.DataSizeDelta = globalCumulativeDelta;
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

                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "newTagSize : " + Utils.GetBytesReadable(newTagSize));

                            // -- Adjust tag slot to new size in file --
                            long tagBeginOffset, tagEndOffset;

                            if (tagExists && zone.Size > zone.CoreSignature.Length) // An existing tag has been reprocessed
                            {
                                tagBeginOffset = zone.Offset + (isBuffered ? regionCumulativeDelta : globalCumulativeDelta) - globalOffsetCorrection;
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
                                        case MetaDataIO.TO_EOF: tagBeginOffset = writer.BaseStream.Length; break;
                                        case MetaDataIO.TO_BOF: tagBeginOffset = 0; break;
                                        case MetaDataIO.TO_BUILTIN: tagBeginOffset = zone.Offset + (isBuffered ? regionCumulativeDelta : globalCumulativeDelta); break;
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
                                    uint deltaBytes = (uint)(newTagSize - zone.Size);
                                    if (!useBuffer) Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (direct) : Lengthening (delta=" + Utils.GetBytesReadable(deltaBytes) + ")");
                                    else Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Buffer stream operation : Lengthening (delta=" + Utils.GetBytesReadable(deltaBytes) + ")");

                                    StreamUtils.LengthenStream(writer.BaseStream, tagEndOffset, deltaBytes);
                                }
                                else if (newTagSize < zone.Size) // Need to reduce file size
                                {
                                    uint deltaBytes = (uint)(zone.Size - newTagSize);
                                    if (!useBuffer) Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (direct) : Shortening (delta=-" + Utils.GetBytesReadable(deltaBytes) + ")");
                                    else Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Buffer stream operation : Shortening (delta=-" + Utils.GetBytesReadable(deltaBytes) + ")");

                                    StreamUtils.ShortenStream(writer.BaseStream, tagEndOffset, deltaBytes);
                                }
                            }

                            // Copy tag contents to the new slot
                            writer.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                            s.Seek(0, SeekOrigin.Begin);

                            if (writeResult.WrittenFields > 0)
                            {
                                StreamUtils.CopyStream(s, writer.BaseStream);
                            }
                            else
                            {
                                if (zone.CoreSignature.Length > 0) msw.Write(zone.CoreSignature);
                            }

                            long delta = newTagSize - oldTagSize;
                            regionCumulativeDelta += delta;
                            globalCumulativeDelta += delta;

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
                                result = structureHelper.RewriteHeaders(writer, delta, action, zone.Name, globalOffsetCorrection, isBuffered ? region.Id : -1);
                            }

                            zone.Size = (int)newTagSize;
                        } // MemoryStream used to process current zone
                    } // Loop through zones

                    if (buffer != null)
                    {
                        // -- Adjust file slot to new size of buffer --
                        long tagEndOffset = region.StartOffset + initialBufferSize;

                        // Need to build a larger file
                        if (buffer.Length > initialBufferSize)
                        {
                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (buffer) : Lengthening (delta=" + Utils.GetBytesReadable(buffer.Length - initialBufferSize) + ")");
                            StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(buffer.Length - initialBufferSize));
                        }
                        else if (buffer.Length < initialBufferSize) // Need to reduce file size
                        {
                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (buffer) : Shortening (delta=" + Utils.GetBytesReadable(buffer.Length - initialBufferSize) + ")");
                            StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(initialBufferSize - buffer.Length));
                        }

                        // Copy tag contents to the new slot
                        w.BaseStream.Seek(region.StartOffset, SeekOrigin.Begin);
                        buffer.Seek(0, SeekOrigin.Begin);

                        StreamUtils.CopyStream(buffer, w.BaseStream);
                    }
                }
                finally // Make sure buffers are properly disallocated
                {
                    if (buffer != null)
                    {
                        buffer.Close();
                        buffer = null;
                    }
                }

                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "");
            } // Loop through zone regions

            return result;
        }

        /*
        private bool RewriteZonesHybrid(
            BinaryWriter w,
            WriteDelegate write,
            ICollection<Zone> zones,
            TagData dataToWrite,
            bool tagExists)
        {
            bool result = true;

            // Load the 'interesting' part of the file in memory
            // TODO - detect and fine-tune cases when block at the extreme ends of the file are considered (e.g. SPC, certain MP4s where useful zones are at the very end)

            /*
            ICollection<Zone> resizableZones = zones.Where(zone => zone.IsResizable).ToList();
            IDictionary<int, Tuple<long, long>> bufferMap = new Dictionary<int, Tuple<long, long>>();

            //            long chunkBeginOffset = getFirstRecordedOffset(resizableZones);
            //            long chunkEndOffset = getLastRecordedOffset(resizableZones);

            int bufferIndex = 0;
            long bufferBeginOffset = 0;
            long bufferEndOffset = 0;

            long previousZoneEndOffset = -1;

            foreach (Zone zone in resizableZones)
            {
                long zoneBeginOffset = getFirstRecordedOffset(zone);
                long zoneEndOffset = getLastRecordedOffset(zone);

                if (embedder != null && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
                {
                    zoneBeginOffset = Math.Min(zoneBeginOffset, embedder.Id3v2Zone.Offset);
                    zoneEndOffset = Math.Max(zoneEndOffset, embedder.Id3v2Zone.Offset + embedder.Id3v2Zone.Size);
                }

                // If current zone is distant to the previous by more than 20% of total file size, create another buffer
                if (previousZoneEndOffset > -1 && zoneBeginOffset - previousZoneEndOffset > w.BaseStream.Length * 0.2)
                {
                    bufferEndOffset = previousZoneEndOffset;
                    bufferMap.Add(bufferIndex, new Tuple<long, long>(bufferBeginOffset, bufferEndOffset));

                    bufferIndex++;
                    bufferBeginOffset = zoneBeginOffset;
                }
                previousZoneEndOffset = zoneEndOffset;
                zone.BufferIndex = bufferIndex;
            }

            // Finalize current buffer
            bufferEndOffset = previousZoneEndOffset;
            bufferMap.Add(bufferIndex, new Tuple<long, long>(bufferBeginOffset, bufferEndOffset));

            //            long initialChunkLength = chunkEndOffset - chunkBeginOffset;
            */
        /*

        IList<ZoneRegion> zoneRegions = computeZoneRegions(zones, w.BaseStream.Length);

        try
        {
            foreach (int bufferKey in bufferMap.Keys)
            {
                bufferBeginOffset = bufferMap[bufferKey].Item1;
                int bufferSize = (int)(bufferMap[bufferKey].Item2 - bufferBeginOffset);
                bufferBeginOffsets.Add(bufferBeginOffset);

                // Allocate buffer
                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "RewriteZonesHybrid : Allocating " + Utils.GetBytesReadable(bufferSize));
                MemoryStream buffer = new MemoryStream(bufferSize);
                buffers.Add(buffer);
                writers.Add(new BinaryWriter(buffer, Settings.DefaultTextEncoding));

                // Copy file data to buffer
                w.BaseStream.Seek(bufferBeginOffset, SeekOrigin.Begin);
                StreamUtils.CopyStream(w.BaseStream, buffer, bufferSize);
            }

            result = RewriteZonesDirect(writers, w, write, zones, dataToWrite, tagExists, bufferBeginOffsets, true);

            foreach (int bufferKey in bufferMap.Keys)
            {
                // -- Adjust file slot to new size of chunk --
                MemoryStream buffer = buffers[bufferKey];
                long tagBeginOffset = bufferMap[bufferKey].Item1;
                long initialBufferSize = bufferMap[bufferKey].Item2 - tagBeginOffset;
                long tagEndOffset = tagBeginOffset + initialBufferSize;

                // Need to build a larger file
                if (buffer.Length > initialBufferSize)
                {
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (buffer #" + bufferKey + ") : Lengthening (Δ=" + Utils.GetBytesReadable(buffer.Length - initialBufferSize) + ")");
                    StreamUtils.LengthenStream(w.BaseStream, tagEndOffset, (uint)(buffer.Length - initialBufferSize));
                }
                else if (buffer.Length < initialBufferSize) // Need to reduce file size
                {
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (buffer #" + bufferKey + ") : Shortening (Δ=" + Utils.GetBytesReadable(buffer.Length - initialBufferSize) + ")");
                    StreamUtils.ShortenStream(w.BaseStream, tagEndOffset, (uint)(initialBufferSize - buffer.Length));
                }

                // Copy tag contents to the new slot
                w.BaseStream.Seek(tagBeginOffset, SeekOrigin.Begin);
                buffer.Seek(0, SeekOrigin.Begin);

                StreamUtils.CopyStream(buffer, w.BaseStream);
            }
        }
        finally // Make sure everything's properly disallocated
        {
            foreach (MemoryStream stream in buffers) stream.Close();
        }
        */


        /*
        w.BaseStream.Seek(chunkBeginOffset, SeekOrigin.Begin);

        using (MemoryStream chunk = new MemoryStream((int)initialChunkLength))
        {
            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "RewriteZonesBuffered : Allocating " + Utils.GetBytesReadable(initialChunkLength));

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
*/

        private IList<ZoneRegion> computeZoneRegions(ICollection<Zone> zones, long fileSize)
        {
            IList<ZoneRegion> result = new List<ZoneRegion>();

            bool isFirst = true;
            bool embedderProcessed = false;

            bool previousIsResizable = false;
            long previousZoneEndOffset = -1;
            int regionId = 0;
            ZoneRegion region = new ZoneRegion(regionId++);

            foreach (Zone zone in zones)
            {
                if (isFirst) region.IsBufferable = zone.IsResizable;

                long zoneBeginOffset = getFirstRecordedOffset(zone);
                long zoneEndOffset = getLastRecordedOffset(zone);

                if (embedder != null && !embedderProcessed && implementedTagType == MetaDataIOFactory.TAG_ID3V2)
                {
                    zoneBeginOffset = Math.Min(zoneBeginOffset, embedder.Id3v2Zone.Offset);
                    zoneEndOffset = Math.Max(zoneEndOffset, embedder.Id3v2Zone.Offset + embedder.Id3v2Zone.Size);
                    embedderProcessed = true;
                }

                // If current zone is distant to the previous by more than 20% of total file size, create another region
                // If current zone has not the same IsResizable value as the previous, create another region
                if (!isFirst &&
                    (
                        (zone.IsResizable && zoneBeginOffset - previousZoneEndOffset > fileSize * 0.2)
                        || (previousIsResizable != zone.IsResizable)
                    )
                    )
                {
                    result.Add(region);
                    region = new ZoneRegion(regionId++);
                    region.IsBufferable = zone.IsResizable;
                }

                previousZoneEndOffset = zoneEndOffset;
                previousIsResizable = zone.IsResizable;
                region.Zones.Add(zone);
                isFirst = false;
            }

            // Finalize current region
            result.Add(region);

            return result;
        }

        private static long getFirstRecordedOffset(ICollection<Zone> zones)
        {
            long result = long.MaxValue;
            if (zones != null)
                foreach (Zone zone in zones)
                    result = Math.Min(result, getFirstRecordedOffset(zone));

            return result;
        }

        private static long getFirstRecordedOffset(Zone zone)
        {
            long result = long.MaxValue;
            if (zone != null)
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
                    result = Math.Max(result, getLastRecordedOffset(zone));

            return result;
        }

        private static long getLastRecordedOffset(Zone zone)
        {
            long result = 0;
            if (zone != null)
            {
                result = Math.Max(result, zone.Offset + zone.Size);
                foreach (FrameHeader header in zone.Headers)
                    result = Math.Max(result, header.Position);
            }
            return result;
        }
    }
}
