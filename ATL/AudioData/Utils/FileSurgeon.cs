using Commons;
using System;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.FileStructureHelper;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Helper class called to write into files, optimizing memory and I/O speed according to the rewritten areas
    /// </summary>
    class FileSurgeon
    {
        /// <summary>
        /// Modes for zone block modification
        /// </summary>
        public enum WriteMode
        {
            /// <summary>
            /// Replace : existing block is replaced by written data
            /// </summary>
            REPLACE = 0,
            /// <summary>
            /// Overwrite : written data overwrites existing block (non-overwritten parts are kept as is)
            /// </summary>
            OVERWRITE = 1
        }

        /// <summary>
        /// Modes for zone management
        /// NB : ON_DISK mode can be forced client-side by using <see cref="Settings.ForceDiskIO"/>
        /// </summary>
        public enum ZoneManagement
        {
            /// <summary>
            /// Modifications are performed directly on disk; adapted for small files or single zones
            /// </summary>
            ON_DISK = 0,
            /// <summary>
            /// Modifications are performed in a memory buffer, then written on disk in one go
            /// </summary>
            BUFFERED = 1
        }

        /// <summary>
        /// Buffering region
        /// Describes a group of overlapping, contiguous or neighbouring <see cref="FileStructureHelper.Zone"/>s that can be buffered together for I/O optimization
        /// Two Zones stop belonging to the same region if they are distant by more than <see cref="REGION_DISTANCE_THRESHOLD"/>% of the total file size
        /// </summary>
        private class ZoneRegion
        {
            public ZoneRegion(int id)
            {
                if (-1 == id) throw new ArgumentException("-1 is a reserved value that cannot be attributed");
                Id = id;
            }

            /// <summary>
            /// ID of the region
            /// Used for computation purposes only
            /// Must be unique 
            /// Must be different than -1 which is a reserved value for "unbuffered area" used in <see cref="FileStructureHelper"/>
            /// </summary>
            public readonly int Id;
            /// <summary>
            /// True if the region is bufferable; false if not (i.e. non-resizable zones)
            /// </summary>
            public bool IsBufferable = true;
            /// <summary>
            /// Zones belonging to the region
            /// </summary>
            public IList<Zone> Zones = new List<Zone>();

            public long StartOffset => FileSurgeon.getLowestOffset(Zones);

            public long EndOffset => FileSurgeon.getHighestOffset(Zones);

            public int Size => (int)(EndOffset - StartOffset);

            public override string ToString()
            {
                return "#" + Id + " : " + StartOffset + "->" + EndOffset + "(" + Utils.GetBytesReadable(Size) + ") IsBufferable = " + IsBufferable;
            }
        }

        /// <summary>
        /// % of total stream (~file) size under which two neighbouring Zones can be grouped into the same Region
        /// </summary>
        private static readonly double REGION_DISTANCE_THRESHOLD = 0.2;

        private readonly FileStructureHelper structureHelper;
        private readonly IMetaDataEmbedder embedder;

        private readonly int implementedTagType;
        private readonly int defaultTagOffset;

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

            return RewriteZones(w, write, zones, dataToWrite, tagExists, mode == ZoneManagement.BUFFERED);
        }

        /// <summary>
        /// Rewrites zones that have to be rewritten
        ///     - Works region after region, buffering them if needed
        ///     - Put each zone into memory and update them using the given WriteDelegate
        ///     - Adjust file size and region headers accordingly
        /// </summary>
        /// <param name="fullScopeWriter">BinaryWriter opened on the data stream (usually, contents of an audio file) to be rewritten</param>
        /// <param name="write">Delegate to the write method of the <see cref="IMetaDataIO"/> to be used to update the data stream</param>
        /// <param name="zones">Zones to rewrite</param>
        /// <param name="dataToWrite">Metadata to update the zones with</param>
        /// <param name="tagExists">True if the tag already exists on the current data stream; false if not</param>
        /// <param name="useBuffer">True if I/O has to be buffered. Makes I/O faster but consumes more RAM.</param>
        /// <returns>True if the operation succeeded; false if it something unexpected happened during the processing</returns>
        private bool RewriteZones(
            BinaryWriter fullScopeWriter,
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

            IList<ZoneRegion> zoneRegions = computeZoneRegions(zones, fullScopeWriter.BaseStream.Length);
            BinaryWriter writer;

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
                            if (structureHelper != null)
                                fullScopeWriter.BaseStream.Seek(structureHelper.getCorrectedOffset(region.StartOffset), SeekOrigin.Begin);
                            else // for classes that don't use FileStructureHelper(FLAC)
                                fullScopeWriter.BaseStream.Seek(region.StartOffset + globalCumulativeDelta, SeekOrigin.Begin);

                            StreamUtils.CopyStream(fullScopeWriter.BaseStream, buffer, initialBufferSize);
                        }

                        writer = new BinaryWriter(buffer, Settings.DefaultTextEncoding);
                        globalOffsetCorrection = region.StartOffset;
                    }
                    else
                    {
                        isBuffered = false;
                        writer = fullScopeWriter;
                        globalOffsetCorrection = 0;
                    }

                    foreach (Zone zone in region.Zones)
                    {
                        oldTagSize = zone.Size;

                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "------ ZONE " + zone.Name + "@" + zone.Offset);
                        Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Allocating " + Utils.GetBytesReadable(zone.Size));

                        // Write new tag to a MemoryStream
                        using (MemoryStream s = new MemoryStream((int)zone.Size))
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
                                result &= structureHelper.RewriteHeaders(fullScopeWriter, isBuffered ? writer : null, delta, action, zone.Name, globalOffsetCorrection, isBuffered ? region.Id : -1);
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
                            StreamUtils.LengthenStream(fullScopeWriter.BaseStream, tagEndOffset, (uint)(buffer.Length - initialBufferSize));
                        }
                        else if (buffer.Length < initialBufferSize) // Need to reduce file size
                        {
                            Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Disk stream operation (buffer) : Shortening (delta=" + Utils.GetBytesReadable(buffer.Length - initialBufferSize) + ")");
                            StreamUtils.ShortenStream(fullScopeWriter.BaseStream, tagEndOffset, (uint)(initialBufferSize - buffer.Length));
                        }

                        // Copy tag contents to the new slot
                        fullScopeWriter.BaseStream.Seek(region.StartOffset, SeekOrigin.Begin);
                        buffer.Seek(0, SeekOrigin.Begin);

                        StreamUtils.CopyStream(buffer, fullScopeWriter.BaseStream);
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

            // Post-processing changes
            if (structureHelper != null && structureHelper.ZoneNames.Contains(FileStructureHelper.POST_PROCESSING_ZONE_NAME))
            {
                Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Post-processing");
                structureHelper.PostProcessing(fullScopeWriter);
            }

            return result;
        }

        /// <summary>
        /// Build buffering Regions according to the given zones and total stream (usually file) size
        /// </summary>
        /// <param name="zones">Zones to calculate Regions from, ordered by their offset</param>
        /// <param name="streamSize">Total size of the corresponding file, in bytes</param>
        /// <returns>Buffering Regions containing the given zones</returns>
        private IList<ZoneRegion> computeZoneRegions(ICollection<Zone> zones, long streamSize)
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

                long zoneBeginOffset = getLowestOffset(zone);
                long zoneEndOffset = getHighestOffset(zone);

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
                        (zone.IsResizable && zoneBeginOffset - previousZoneEndOffset > streamSize * REGION_DISTANCE_THRESHOLD)
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

        /// <summary>
        /// Get the lowest offset among the given zones
        /// Searches through zone offsets _and_ header offsets
        /// </summary>
        /// <param name="zones">Zones to examine</param>
        /// <returns>Lowest offset value among the given zones' zone offsets and header offsets</returns>
        private static long getLowestOffset(ICollection<Zone> zones)
        {
            long result = long.MaxValue;
            if (zones != null)
                foreach (Zone zone in zones)
                    result = Math.Min(result, getLowestOffset(zone));

            return result;
        }

        /// <summary>
        /// Get the lowest offset among the given zone
        /// Searches through zone offsets _and_ header offsets
        /// </summary>
        /// <param name="zone">Zone to examine</param>
        /// <returns>Lowest offset value among the given zone's zone offsets and header offsets</returns>
        private static long getLowestOffset(Zone zone)
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

        /// <summary>
        /// Get the highest offset among the given zones
        /// Searches through zone offsets _and_ header offsets
        /// </summary>
        /// <param name="zones">Zones to examine</param>
        /// <returns>Highest offset value among the given zones' zone offsets and header offsets</returns>
        private static long getHighestOffset(ICollection<Zone> zones)
        {
            long result = 0;
            if (zones != null)
                foreach (Zone zone in zones)
                    result = Math.Max(result, getHighestOffset(zone));

            return result;
        }

        /// <summary>
        /// Get the highest offset among the given zone
        /// Searches through zone offsets _and_ header offsets
        /// </summary>
        /// <param name="zone">Zone to examine</param>
        /// <returns>Highest offset value among the given zone's zone offsets and header offsets</returns>
        private static long getHighestOffset(Zone zone)
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
