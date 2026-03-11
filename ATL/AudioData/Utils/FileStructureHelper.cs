using Commons;
using HashDepot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ATL.AudioData
{
    /// <summary>
    /// Helper class used to :
    ///   - Record location and size of specific chunks of data within a structured file, called "Zones"
    ///   - Record location, value and type of headers describing Zones
    ///   - Modify these headers as Zones appear, disappear, expand or shrink
    /// </summary>
    public class FileStructureHelper
    {
        /// <summary>
        /// Default zone name to be used when no naming is necessary (simple cases where there is a but a single Zone to describe)
        /// </summary>
        public const string DEFAULT_ZONE_NAME = "default";
        /// <summary>
        /// Zone name to be used for padding
        /// </summary>
        public const string PADDING_ZONE_NAME = "padding";
        /// <summary>
        /// Zone name to be used to store post-processing elements
        /// </summary>
        public const string POST_PROCESSING_ZONE_NAME = "post-processing";

        /// <summary>
        /// Type of action to react to
        /// </summary>
        public enum ACTION
        {
            /// <summary>
            /// No action
            /// </summary>
            None = -1,
            /// <summary>
            /// Existing zone is edited, and not removed
            /// </summary>
            Edit = 0,
            /// <summary>
            /// New zone is added
            /// </summary>
            Add = 1,
            /// <summary>
            /// Existing zone is removed
            /// </summary>
            Delete = 2
        };

        /// <summary>
        /// Container class describing a frame header
        /// </summary>
        public class FrameHeader
        {
            /// <summary>
            /// Header types
            /// </summary>
            public enum TYPE
            {
                /// <summary>
                /// Counter : counts the underlying number of frames
                /// </summary>
                Counter = 0,
                /// <summary>
                /// Size : documents the size of a given frame / group of frames
                /// </summary>
                Size = 1,
                /// <summary>
                /// Index (absolute) : documents the offset (position of 1st byte) of a given frame
                /// </summary>
                Index = 2,
                /// <summary>
                /// Index (relative) : documents the offset (position of 1st byte) of a given frame, relative to the header's position
                /// </summary>
                RelativeIndex = 3
            };

            /// <summary>
            /// Header type (allowed values are TYPE_XXX within FrameHeader class)
            /// </summary>
            public readonly TYPE Type;
            /// <summary>
            /// Position of the header
            /// </summary>
            public readonly long Position;
            /// <summary>
            /// Offset to apply to relative position (e.g. "relative to the offset of the container where the header is located")
            /// If set to 0, use the header's own offset
            /// </summary>
            public readonly long RelativityOffset;
            /// <summary>
            /// True if header value is stored using little-endian convention; false if big-endian
            /// </summary>
            public readonly bool IsLittleEndian;
            /// <summary>
            /// Zone where the header is located physically
            /// </summary>
            public readonly string ParentZone;
            /// <summary>
            /// Zone to which the header value is pointing to (index-type header only; for post-processing only)
            /// </summary>
            public readonly string ValueZone;
            /// <summary>
            /// Current value of the header (counter : number of frames / size : frame size / index : frame index (absolute) / rindex : frame index (relative to header position))
            /// </summary>
            public object Value { get; set; }

            /// <summary>
            /// Constructs a new frame header using the given field values
            /// </summary>
            public FrameHeader(TYPE type, long position, object value, bool isLittleEndian = true, string parentZone = "", string valueZone = "", long relativityOffset = 0)
            {
                Type = type; Position = position; Value = value; IsLittleEndian = isLittleEndian;
                ParentZone = parentZone; ValueZone = valueZone;
                RelativityOffset = relativityOffset;
            }
        }

        /// <summary>
        /// Container class describing a chunk/frame within a structured file 
        /// </summary>
        public class Zone
        {
            /// <summary>
            /// Zone name (any unique value will do; used as internal reference only)
            /// </summary>
            public string Name { get; set; }
            /// <summary>
            /// Offset in bytes
            /// </summary>
            public long Offset { get; set; }
            /// <summary>
            /// Size in bytes
            /// </summary>
            public long Size { get; set; }
            /// <summary>
            /// Data sequence that has to be written in the zone when the zone does not contain any other data
            /// </summary>
            public byte[] CoreSignature { get; set; }
            /// <summary>
            /// Indicates whether the zone contents are deletable by ATL (e.g. non-metadata zone is not deletable)
            /// </summary>
            public bool IsDeletable { get; set; }
            /// <summary>
            /// Generic usage flag for storing information
            /// </summary>
            public byte Flag { get; set; }
            /// <summary>
            /// Size descriptors and item counters referencing the zone elsehwere on the file
            /// </summary>
            public IList<FrameHeader> Headers { get; set; }
            /// <summary>
            /// True if the zone might shrink or enlarge, false if it must keep its original size
            /// </summary>
            public bool IsResizable { get; set; }
            /// <summary>
            /// True if the zone has been deleted
            /// </summary>
            public bool IsDeleted { get; set; }
            /// <summary>
            /// True if the zone can't be edited in any way
            /// </summary>
            public bool IsReadonly => 0 == Size && !IsResizable && !IsDeletable;

            /// <summary>
            /// Construct a new Zone using the given field values
            /// </summary>
            public Zone(string name, long offset, long size, byte[] coreSignature, bool isDeletable = true, byte flag = 0, bool resizable = true)
            {
                Name = name; Offset = offset; Size = size; CoreSignature = coreSignature; IsDeletable = isDeletable; Flag = flag; IsResizable = resizable;
                IsDeleted = false;
                Headers = new List<FrameHeader>();
            }

            /// <summary>
            /// Remove all headers
            /// </summary>
            public void Clear()
            {
                Headers?.Clear();
            }
        }

        private sealed class ZoneInfo
        {
            public string Name { get; }
            public int RegionId { get; }

            public ZoneInfo(string name, int regionId)
            {
                Name = name;
                RegionId = regionId;
            }

            public override string ToString()
            {
                return RegionId + ":" + Name;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;

                // Actually check the type, should not throw exception from Equals override
                if (obj.GetType() != this.GetType()) return false;

                return RegionId == ((ZoneInfo)obj).RegionId
                    && Name == ((ZoneInfo)obj).Name;
            }

            public override int GetHashCode()
            {
                return (int)FNV1a.Hash32(Utils.Latin1Encoding.GetBytes(ToString()));
            }
        }

        // Recorded zones
        private readonly IDictionary<string, Zone> zones;

        // Stores offset variations caused by zone editing (add/remove/shrink/expand) within current file
        //      1st dictionary key  : region id (-1 = file-wide offset correction that records cumulative changes across all regions)
        //      2nd dictionary key  : zone information
        //      KVP Key         : initial end offset of given zone (i.e. position of last byte within zone)
        //      KVP Value       : variation applied to given zone (can be positive or negative)
        private readonly IDictionary<int, IDictionary<ZoneInfo, KeyValuePair<long, long>>> dynamicOffsetCorrection = new Dictionary<int, IDictionary<ZoneInfo, KeyValuePair<long, long>>>();

        // True if attached file uses little-endian convention for number representation; false if big-endian
        private readonly bool isLittleEndian;

        // Auto-incremented index for internal naming of post-processing zones
        private int postProcessingIndex;


        /// <summary>
        /// Names of recorded zones
        /// </summary>
        public ICollection<string> ZoneNames => zones.Keys;

        /// <summary>
        /// Recorded zones, sorted by offset
        /// </summary>
        public ICollection<Zone> Zones
        {
            get
            {
                // 1. Ignore zones declared but not added
                // 2. Ignore deleted zones
                // 3. Sort by offset
                return zones.Values.Where(zone => zone.Offset > -1 && !zone.IsDeleted)
                    .OrderBy(zone => zone.Offset)
                    .ThenBy(zone => zone.Name)
                    .ToList();
            }
        }


        /// <summary>
        /// Construct a new FileStructureHelper
        /// </summary>
        /// <param name="isLittleEndian">True if unerlying file uses little-endian convention for number representation; false if big-endian</param>
        public FileStructureHelper(bool isLittleEndian = true)
        {
            this.isLittleEndian = isLittleEndian;
            zones = new Dictionary<string, Zone>();

            // Init global region
            dynamicOffsetCorrection.Add(-1, new Dictionary<ZoneInfo, KeyValuePair<long, long>>());
        }

        /// <summary>
        /// Clear all recorded Zones
        /// </summary>
        public void Clear()
        {
            if (null != zones)
            {
                foreach (string s in zones.Keys)
                {
                    zones[s].Clear();
                }
                zones.Clear();
            }
            dynamicOffsetCorrection.Clear();
            dynamicOffsetCorrection.Add(-1, new Dictionary<ZoneInfo, KeyValuePair<long, long>>());
        }

        /// <summary>
        /// Retrieve a zone by its name
        /// </summary>
        /// <param name="name">Name of the zone to retrieve</param>
        /// <returns>The zone corresponding to the given name; null if not found</returns>
        public Zone GetZone(string name)
        {
            return zones.TryGetValue(name, out var zone) ? zone : null;
        }

        /// <summary>
        /// Record a new zone by copying the given zone
        /// </summary>
        /// <param name="zone">Zone to be recorded</param>
        public void AddZone(Zone zone)
        {
            AddZone(zone.Offset, zone.Size, zone.CoreSignature, zone.Name);

            foreach (FrameHeader header in zone.Headers)
            {
                addZoneHeader(zone.Name, header.Type, header.Position, header.Value, header.IsLittleEndian);
            }
        }

        /// <summary>
        /// Record a new zone using the given fields
        /// </summary>
        public void AddZone(long offset, long size, string name = DEFAULT_ZONE_NAME, bool isDeletable = true, bool resizable = true)
        {
            AddZone(offset, size, Array.Empty<byte>(), name, isDeletable, resizable);
        }

        /// <summary>
        /// Record a new zone using the given fields
        /// </summary>
        public void AddZone(long offset, long size, byte[] coreSignature, string name = DEFAULT_ZONE_NAME, bool isDeletable = true, bool resizable = true)
        {
            if (!zones.ContainsKey(name))
            {
                zones.Add(name, new Zone(name, offset, size, coreSignature, isDeletable, 0, resizable));
            }
            else // Existing zone might already contain headers
            {
                zones[name].Name = name;
                zones[name].Offset = offset;
                zones[name].Size = size;
                zones[name].CoreSignature = coreSignature;
                zones[name].IsDeletable = isDeletable;
                zones[name].IsResizable = resizable;
            }
        }

        /// <summary>
        /// Remove the zone identified with the given name
        /// </summary>
        public void RemoveZone(string name)
        {
            zones.Remove(name);
        }

        /// <summary>
        /// Remove the zones starting with the given name
        /// </summary>
        public void RemoveZonesStartingWith(string name)
        {
            var keys = new HashSet<string>(zones.Keys); // Don't iterate directly on the collection we want to remove stuff from
            foreach (var zoneName in keys.Where(zoneName => zoneName.StartsWith(name, StringComparison.OrdinalIgnoreCase)))
            {
                zones.Remove(zoneName);
            }
        }

        /// <summary>
        /// Record a new Counter-type header using the given fields and attach it to the zone of given name
        /// </summary>
        public void AddCounter(long position, object value, string zone = DEFAULT_ZONE_NAME, string parentZone = "")
        {
            addZoneHeader(zone, FrameHeader.TYPE.Counter, position, value, isLittleEndian, parentZone);
        }

        /// <summary>
        /// Record a new Size-type header using the given fields and attach it to the zone of given name
        /// </summary>
        public void AddSize(long position, object value, string zone = DEFAULT_ZONE_NAME, string parentZone = "")
        {
            addZoneHeader(zone, FrameHeader.TYPE.Size, position, value, isLittleEndian, parentZone);
        }

        /// <summary>
        /// Record a new Index-type header using the given fields and attach it to the zone of given name
        /// </summary>
        public void AddIndex(long position, object value, bool relative = false, string zone = DEFAULT_ZONE_NAME, string parentZone = "")
        {
            addZoneHeader(zone, relative ? FrameHeader.TYPE.RelativeIndex : FrameHeader.TYPE.Index, position, value, isLittleEndian, parentZone);
        }

        /// <summary>
        /// Record a new Index-type header using the given fields and attach it to the zone of given name, using a position relative to that zone's offset
        /// </summary>
        public void AddPostProcessingIndex(long pendingPosition, object value, bool relative, string valueZone, string positionZone, string parentZone = "", long relativityOffset = 0)
        {
            long finalPosition = getCorrectedOffset(zones[positionZone].Offset) + pendingPosition;

            string zoneName = POST_PROCESSING_ZONE_NAME + "." + ++postProcessingIndex;
            AddZone(finalPosition, 0, zoneName);
            addZoneHeader(zoneName, relative ? FrameHeader.TYPE.RelativeIndex : FrameHeader.TYPE.Index, finalPosition, value, isLittleEndian, parentZone, valueZone, relativityOffset);
        }

        /// <summary>
        /// Declare a zone in advance; useful when reading header frames of containing upper frames, without having reached tag frame itself
        /// </summary>
        /// <param name="zone"></param>
        public void DeclareZone(string zone)
        {
            AddZone(-1, 0, zone);
        }

        /// <summary>
        /// Record a new header using the given fields and attach it to the zone of given name
        /// </summary>
        private void addZoneHeader(string zone, FrameHeader.TYPE type, long position, object value, bool iisLittleEndian, string parentZone = "", string valueZone = "", long relativityOffset = 0)
        {
            if (!zones.ContainsKey(zone)) DeclareZone(zone);
            zones[zone].Headers.Add(new FrameHeader(type, position, value, iisLittleEndian, parentZone, valueZone, relativityOffset));
        }

        /// <summary>
        /// Update all headers at the given position to the given value
        /// (useful when multiple zones refer to the very same header)
        /// </summary>
        /// <param name="position">Position of header to be updated</param>
        /// <param name="type">Type of header to be updated</param>
        /// <param name="newValue">New value to be assigned to header</param>
        private void updateAllHeadersAtPosition(long position, FrameHeader.TYPE type, object newValue)
        {
            // NB : this method should perform quite badly -- evolve to using position-based dictionary if any performance issue arises
            foreach (Zone frame in zones.Values)
            {
                foreach (FrameHeader header in frame.Headers)
                {
                    if (position == header.Position && type == header.Type)
                    {
                        header.Value = newValue;
                    }
                }
            }
        }

        /// <summary>
        /// Perform the addition between the two given values and encodes the result to an array of bytes, according to the type of the reference value
        /// </summary>
        /// <param name="value">Reference value</param>
        /// <param name="delta">Value to add</param>
        /// <param name="updatedValue">Updated value (out parameter; will be returned as same type as reference value)</param>
        /// <returns>Resulting value after the addition, encoded into an array of bytes, as the same type of the reference value. Empty array if failed.</returns>
        private static byte[] addToValue(object value, long delta, out object updatedValue)
        {
            switch (value)
            {
                case byte b:
                    updatedValue = (byte)(b + delta);
                    return new[] { (byte)updatedValue };
                case short s:
                    updatedValue = (short)(s + delta);
                    return BitConverter.GetBytes((short)updatedValue);
                case ushort value1:
                    updatedValue = (ushort)(value1 + delta);
                    return BitConverter.GetBytes((ushort)updatedValue);
                case int i:
                    updatedValue = (int)(i + delta);
                    return BitConverter.GetBytes((int)updatedValue);
                case uint u:
                    updatedValue = (uint)(u + delta);
                    return BitConverter.GetBytes((uint)updatedValue);
                case long l:
                    updatedValue = l + delta;
                    return BitConverter.GetBytes((long)updatedValue);
                // Need to tweak because ulong + int is illegal according to the compiler
                case ulong value1:
                    {
                        if (delta > 0)
                        {
                            updatedValue = value1 + (ulong)delta;
                        }
                        else
                        {
                            updatedValue = value1 - (ulong)-delta;
                        }
                        return BitConverter.GetBytes((ulong)updatedValue);
                    }
                default:
                    updatedValue = value;
                    return Array.Empty<byte>();
            }
        }

        private static bool isValueGT(object value, long addition, long comparison)
        {
            return value switch
            {
                byte b => b + addition >= comparison,
                ushort us => us + addition >= comparison,
                int i => i + addition >= comparison,
                uint u => u + addition >= comparison,
                long l => l + addition >= comparison,
                ulong ul => ul + (ulong)addition >= (ulong)comparison,
                _ => throw new NotSupportedException("Value type not supported in comparison")
            };
        }

        /// <summary>
        /// Return the the given zone's offset corrected according to the position shifts already applied by previous calls to <see cref="RewriteHeaders"/>
        /// e.g. if offset is 30 and 10 bytes have been inserted at position 15, corrected offset will be 40
        /// </summary>
        /// <param name="zone">Name of the zone to get the corrected offset for</param>
        /// <returns>Corrected offset of the zone with the given name</returns>
        public long getCorrectedOffset(string zone)
        {
            return getCorrectedOffset(zones[zone].Offset);
        }

        /// <summary>
        /// Return the the given offset corrected according to the position shifts already applied by previous calls to <see cref="RewriteHeaders"/>
        /// e.g. if offset is 30 and 10 bytes have been inserted at position 15, corrected offset will be 40
        /// </summary>
        /// <param name="offset">Offset to correct</param>
        /// <param name="excludeRegion">Id of the region whose offset corrections to ignore</param>
        /// <returns>Corrected offset</returns>
        public long getCorrectedOffset(long offset, int excludeRegion = -1)
        {
            long offsetPositionCorrection = 0;
            foreach (ZoneInfo info in dynamicOffsetCorrection[-1].Keys) // Search in global repo
            {
                if (-1 == excludeRegion || excludeRegion != info.RegionId)
                {
                    KeyValuePair<long, long> offsetDelta = dynamicOffsetCorrection[-1][info];
                    if (offset >= offsetDelta.Key) offsetPositionCorrection += offsetDelta.Value;
                }
            }

            return offset + offsetPositionCorrection;
        }

        /// <summary>
        /// Perform post-processing modifications to the given stream
        /// </summary>
        /// <param name="writer">Stream to write modifications to</param>
        public void PostProcessing(Stream writer)
        {
            foreach (var zoneName in zones.Keys.Where(zoneName => zoneName.StartsWith(POST_PROCESSING_ZONE_NAME)))
            {
                RewriteHeaders(writer, null, 0, ACTION.Edit, zoneName);
            }
        }

        /// <summary>
        /// Rewrite all zone headers in the given stream according to the given size evolution and the given action
        /// </summary>
        /// <param name="fullScopeWriter">Full stream to write modifications to</param>
        /// <param name="bufferedWriter">Buffered stream to write modifications to</param>
        /// <param name="deltaSize">Evolution of zone size (in bytes; positive or negative)</param>
        /// <param name="action">Action applied to zone</param>
        /// <param name="zoneName">Name of zone to process</param>
        /// <param name="globalOffsetCorrection">Offset correction to apply to the zone to process</param>
        /// <param name="regionId">ID of the current buffer region; -1 if working on the file itself (global offset correction)</param>
        /// <returns></returns>
        public bool RewriteHeaders(
            Stream fullScopeWriter,
            Stream bufferedWriter,
            long deltaSize,
            ACTION action,
            string zoneName = DEFAULT_ZONE_NAME,
            long globalOffsetCorrection = 0,
            int regionId = -1)
        {
            if (null == zones) return false;
            if (!zones.TryGetValue(zoneName, out Zone currentZone)) return true; // No effect


            // Get the dynamic correction map from the proper region
            if (!dynamicOffsetCorrection.ContainsKey(regionId))
                dynamicOffsetCorrection.Add(regionId, new Dictionary<ZoneInfo, KeyValuePair<long, long>>());

            var localDynamicOffsetCorrection = dynamicOffsetCorrection[regionId];

            // Don't reprocess the position of a post-processing zone
            bool isPostReprocessing = zoneName.StartsWith(POST_PROCESSING_ZONE_NAME);

            // == Update the current zone's headers
            foreach (FrameHeader header in currentZone.Headers)
            {
                // === Update values
                var offsetPositionCorrection = -globalOffsetCorrection;
                long offsetValueCorrection = 0;
                long delta = 0;
                var passedParentZone = false;
                var passedValueZone = false;

                foreach (ZoneInfo dynamicZone in localDynamicOffsetCorrection.Keys)
                {
                    // Don't need to process zones located further than we are
                    if (dynamicZone.Name == header.ParentZone) passedParentZone = true;
                    if (dynamicZone.Name == header.ValueZone) passedValueZone = true;
                    if (passedParentZone && passedValueZone) continue;

                    KeyValuePair<long, long> offsetDelta = localDynamicOffsetCorrection[dynamicZone];

                    if (header.Position >= offsetDelta.Key && !passedParentZone && !isPostReprocessing) offsetPositionCorrection += offsetDelta.Value;

                    if ((FrameHeader.TYPE.Index == header.Type || FrameHeader.TYPE.RelativeIndex == header.Type) && isValueGT(header.Value, 0, offsetDelta.Key) && !passedValueZone) offsetValueCorrection += offsetDelta.Value;
                }

                // If we're about to write outside the buffered writer and the full-scope writer is available, switch to it
                Stream s;
                if (null == bufferedWriter) s = fullScopeWriter;
                else if (header.Position + offsetPositionCorrection < 0 || header.Position + offsetPositionCorrection > bufferedWriter.Length)
                {
                    if (null == fullScopeWriter) throw new InvalidDataException("Trying to write outside the buffered writer");
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Trying to write outside the buffered writer - switching to full-scope writer");
                    s = fullScopeWriter;
                    offsetPositionCorrection = 0;
                }
                else s = bufferedWriter;


                // === Rewrite headers

                // If we're going to delete the zone, and the header is located inside it, don't write it !
                if (header.ParentZone == zoneName && ACTION.Delete == action) continue;

                byte[] value;
                switch (header.Type)
                {
                    case FrameHeader.TYPE.Counter:
                    case FrameHeader.TYPE.Size:
                    {
                        delta = header.Type switch
                        {
                            FrameHeader.TYPE.Counter => action switch
                            {
                                ACTION.Add => 1,
                                ACTION.Delete => -1,
                                _ => 0
                            },
                            FrameHeader.TYPE.Size => deltaSize,
                            _ => delta
                        };

                        s.Seek(header.Position + offsetPositionCorrection, SeekOrigin.Begin);

                        value = addToValue(header.Value, delta, out var updatedValue);

                        if (0 == value.Length) throw new NotSupportedException("Value type not supported for " + zoneName + "@" + header.Position + " : " + header.Value.GetType());

                        // The very same frame header is referenced from another frame and must be updated to its new value
                        updateAllHeadersAtPosition(header.Position, header.Type, updatedValue);

                        if (!header.IsLittleEndian) Array.Reverse(value);

                        s.Write(value, 0, value.Length);
                        break;
                    }
                    case FrameHeader.TYPE.Index:
                    case FrameHeader.TYPE.RelativeIndex:
                    {
                        long headerPosition = header.Position + offsetPositionCorrection;
                        long headerOffsetCorrection = 0;
                        if (FrameHeader.TYPE.RelativeIndex == header.Type)
                        {
                            headerOffsetCorrection = header.RelativityOffset > 0 ? header.RelativityOffset : headerPosition;
                        }

                        value = null;
                        if (action != ACTION.Delete)
                        {
                            value = header.Value switch
                            {
                                long headerValue => BitConverter.GetBytes(headerValue + offsetValueCorrection -
                                                                          headerOffsetCorrection),
                                int headerValue => BitConverter.GetBytes((int)(headerValue + offsetValueCorrection -
                                                                               headerOffsetCorrection)),
                                uint headerValue => BitConverter.GetBytes((uint)(headerValue + offsetValueCorrection -
                                    headerOffsetCorrection)),
                                ushort headerValue => BitConverter.GetBytes((ushort)(headerValue + offsetValueCorrection -
                                    headerOffsetCorrection)),
                                // WARNING : will look awful if applying deltas make the value > 255
                                byte headerValue => new[] { (byte)(headerValue + offsetValueCorrection - headerOffsetCorrection) },
                                _ => value
                            };

                            if (!header.IsLittleEndian) Array.Reverse(value);
                        }
                        else
                        {
                            value = header.Value switch
                            {
                                long => BitConverter.GetBytes((long)0),
                                int => BitConverter.GetBytes(0),
                                uint => BitConverter.GetBytes((uint)0),
                                ushort => BitConverter.GetBytes((ushort)0),
                                byte => BitConverter.GetBytes((byte)0),
                                _ => value
                            };
                        }

                        if (null == value) throw new NotSupportedException("Value type not supported for index in " + zoneName + "@" + header.Position + " : " + header.Value.GetType());

                        s.Seek(headerPosition, SeekOrigin.Begin);
                        s.Write(value, 0, value.Length); // Index & relative index types
                        break;
                    }
                }
            } // Loop through headers

            // Record size variations into dynamic offset corrections
            if (deltaSize == 0) return true;

            ZoneInfo zoneInfo = new ZoneInfo(zoneName, regionId);
            // Update local dynamic offset correction if non-null
            if (!localDynamicOffsetCorrection.ContainsKey(zoneInfo))
                localDynamicOffsetCorrection.Add(zoneInfo, new KeyValuePair<long, long>(currentZone.Offset + currentZone.Size, deltaSize));

            // If working with local dynamic offset correction, update global dynamic offset correction
            if (regionId <= -1) return true;

            IDictionary<ZoneInfo, KeyValuePair<long, long>> globalRegion = dynamicOffsetCorrection[-1];
            // Add new region
            if (!globalRegion.ContainsKey(zoneInfo))
                globalRegion.Add(zoneInfo, new KeyValuePair<long, long>(currentZone.Offset + currentZone.Size, deltaSize));
            else // Increment current delta to existing region
            {
                KeyValuePair<long, long> currentValues = globalRegion[zoneInfo];
                globalRegion[zoneInfo] = new KeyValuePair<long, long>(currentValues.Key, currentValues.Value + deltaSize);
            }

            return true;
        }

    }
}