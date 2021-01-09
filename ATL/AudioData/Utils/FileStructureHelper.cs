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
            /// True if header value is stored using little-endian convention; false if big-endian
            /// </summary>
            public readonly bool IsLittleEndian;
            /// <summary>
            /// Zone where the header is located physically
            /// </summary>
            public readonly string ParentZone;
            /// <summary>
            /// Zone to which the header value is pointing to (for post-processing only)
            /// </summary>
            public readonly string ValueZone;
            /// <summary>
            /// Current value of the header (counter : number of frames / size : frame size / index : frame index (absolute) / rindex : frame index (relative to header position))
            /// </summary>
            public object Value;

            /// <summary>
            /// Constructs a new frame header using the given field values
            /// </summary>
            public FrameHeader(TYPE type, long position, object value, bool isLittleEndian = true, string parentZone = "", string valueZone = "")
            {
                Type = type; Position = position; Value = value; IsLittleEndian = isLittleEndian; ParentZone = parentZone; ValueZone = valueZone;
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
            public string Name;
            /// <summary>
            /// Offset in bytes
            /// </summary>
            public long Offset;
            /// <summary>
            /// Size in bytes
            /// </summary>
            public long Size;
            /// <summary>
            /// Data sequence that has to be written in the zone when the zone does not contain any other data
            /// </summary>
            public byte[] CoreSignature;
            /// <summary>
            /// Indicates whether the zone contents are deletable by ATL (e.g. non-metadata zone is not deletable)
            /// </summary>
            public bool IsDeletable;
            /// <summary>
            /// Generic usage flag for storing information
            /// </summary>
            public byte Flag;
            /// <summary>
            /// Size descriptors and item counters referencing the zone elsehwere on the file
            /// </summary>
            public IList<FrameHeader> Headers;
            /// <summary>
            /// True if the zone might shrink or enlarge, false if it must keep its original size
            /// </summary>
            public bool IsResizable;

            /// <summary>
            /// Construct a new Zone using the given field values
            /// </summary>
            public Zone(string name, long offset, long size, byte[] coreSignature, bool isDeletable = true, byte flag = 0, bool resizable = true)
            {
                Name = name; Offset = offset; Size = size; CoreSignature = coreSignature; IsDeletable = isDeletable; Flag = flag; IsResizable = resizable;
                Headers = new List<FrameHeader>();
            }

            /// <summary>
            /// Remove all headers
            /// </summary>
            public void Clear()
            {
                if (Headers != null) Headers.Clear();
            }
        }

        // Recorded zones
        private readonly IDictionary<string, Zone> zones;

        // Stores offset variations caused by zone editing (add/remove/shrink/expand) within current file
        //      1st dictionary key  : region id
        //      2nd dictionary key  : zone name
        //      KVP Key         : initial end offset of given zone (i.e. position of last byte within zone)
        //      KVP Value       : variation applied to given zone (can be positive or negative)
        private readonly IDictionary<int, IDictionary<string, KeyValuePair<long, long>>> dynamicOffsetCorrection = new Dictionary<int, IDictionary<string, KeyValuePair<long, long>>>();

        // True if attached file uses little-endian convention for number representation; false if big-endian
        private readonly bool isLittleEndian;


        /// <summary>
        /// Names of recorded zones
        /// </summary>
        public ICollection<string> ZoneNames
        {
            get { return zones.Keys; }
        }

        /// <summary>
        /// Recorded zones, sorted by offset
        /// </summary>
        public ICollection<Zone> Zones
        {
            get
            {
                // 1. Ignore zones declared but not added
                // 2. Sort by offset
                return zones.Values.Where(zone => zone.Offset > -1).OrderBy(zone => zone.Offset).ThenBy(zone => zone.Name).ToList();
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
            dynamicOffsetCorrection.Add(-1, new Dictionary<string, KeyValuePair<long, long>>());
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
            dynamicOffsetCorrection.Add(-1, new Dictionary<string, KeyValuePair<long, long>>());
        }

        /// <summary>
        /// Retrieve a zone by its name
        /// </summary>
        /// <param name="name">Name of the zone to retrieve</param>
        /// <returns>The zone corresponding to the given name; null if not found</returns>
        public Zone GetZone(string name)
        {
            if (zones.ContainsKey(name)) return zones[name]; else return null;
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
            AddZone(offset, size, new byte[0], name, isDeletable, resizable);
        }

        /// <summary>
        /// Record a new zone using the given fields
        /// </summary>
        public void AddZone(long offset, long size, byte[] coreSignature, string zone = DEFAULT_ZONE_NAME, bool isDeletable = true, bool resizable = true)
        {
            if (!zones.ContainsKey(zone))
            {
                zones.Add(zone, new Zone(zone, offset, size, coreSignature, isDeletable, 0, resizable));
            }
            else // Existing zone might already contain headers
            {
                zones[zone].Name = zone;
                zones[zone].Offset = offset;
                zones[zone].Size = size;
                zones[zone].CoreSignature = coreSignature;
                zones[zone].IsDeletable = isDeletable;
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
        public void AddPostProcessingIndex(long pendingPosition, object value, bool relative, string valueZone, string positionZone, string parentZone = "")
        {
            long finalPosition = getCorrectedOffset(zones[positionZone].Offset) + pendingPosition;
            //long finalPosition = zones[positionZone].Offset + pendingPosition;

            AddZone(finalPosition, 0, POST_PROCESSING_ZONE_NAME);
            addZoneHeader(POST_PROCESSING_ZONE_NAME, relative ? FrameHeader.TYPE.RelativeIndex : FrameHeader.TYPE.Index, finalPosition, value, isLittleEndian, parentZone, valueZone);
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
        private void addZoneHeader(string zone, FrameHeader.TYPE type, long position, object value, bool isLittleEndian, string parentZone = "", string valueZone = "")
        {
            if (!zones.ContainsKey(zone)) DeclareZone(zone);
            zones[zone].Headers.Add(new FrameHeader(type, position, value, isLittleEndian, parentZone, valueZone));
        }

        /// <summary>
        /// Update all headers at the given position to the given value
        /// (useful when multiple zones refer to the very same header)
        /// </summary>
        /// <param name="position">Position of header to be updated</param>
        /// <param name="newValue">New value to be assigned to header</param>
        private void updateAllHeadersAtPosition(long position, object newValue)
        {
            // NB : this method should perform quite badly -- evolve to using position-based dictionary if any performance issue arise
            foreach (Zone frame in zones.Values)
            {
                foreach (FrameHeader header in frame.Headers)
                {
                    if (position == header.Position)
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
        /// <returns>Resulting value after the addition, encoded into an array of bytes, as the same type of the reference value</returns>
        private static byte[] addToValue(object value, long delta, out object updatedValue)
        {
            if (value is byte)
            {
                updatedValue = (byte)((byte)value + delta);
                return new byte[1] { (byte)updatedValue };
            }
            else if (value is short)
            {
                updatedValue = (short)((short)value + delta);
                return BitConverter.GetBytes((short)updatedValue);
            }
            else if (value is ushort)
            {
                updatedValue = (ushort)((ushort)value + delta);
                return BitConverter.GetBytes((ushort)updatedValue);
            }
            else if (value is int)
            {
                updatedValue = (int)((int)value + delta);
                return BitConverter.GetBytes((int)updatedValue);
            }
            else if (value is uint)
            {
                updatedValue = (uint)((uint)value + delta);
                return BitConverter.GetBytes((uint)updatedValue);
            }
            else if (value is long)
            {
                updatedValue = (long)value + delta;
                return BitConverter.GetBytes((long)updatedValue);
            }
            else if (value is ulong) // Need to tweak because ulong + int is illegal according to the compiler
            {
                if (delta > 0)
                {
                    updatedValue = (ulong)value + (ulong)delta;
                }
                else
                {
                    updatedValue = (ulong)value - (ulong)(-delta);
                }
                return BitConverter.GetBytes((ulong)updatedValue);
            }
            else
            {
                updatedValue = value;
                return null;
            }
        }

        private static bool isValueGT(object value, long addition, long comparison)
        {
            if (value is int)
                return (int)value + addition >= comparison;
            else if (value is uint)
                return (uint)value + addition >= comparison;
            else if (value is long)
                return (long)value + addition >= comparison;
            else
                throw new NotSupportedException("Value type not supported in comparison");
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
        /// <returns>Corrected offset</returns>
        public long getCorrectedOffset(long offset)
        {
            long offsetPositionCorrection = 0;
            foreach (KeyValuePair<long, long> offsetDelta in dynamicOffsetCorrection[-1].Values) // Search in global repo
                if (offset >= offsetDelta.Key) offsetPositionCorrection += offsetDelta.Value;

            return offset + offsetPositionCorrection;
        }

        /// <summary>
        /// Perform post-processing modifications to the given stream
        /// </summary>
        /// <param name="writer">Stream to write modifications to</param>
        public void PostProcessing(BinaryWriter writer)
        {
            RewriteHeaders(writer, null, 0, ACTION.Edit, POST_PROCESSING_ZONE_NAME);
        }

        /// <summary>
        /// Rewrite all zone headers in the given stream according to the given size evolution and the given action
        /// </summary>
        /// <param name="fullScopeWriter">Full stream to write modifications to</param>
        /// <param name="bufferedWriter">Buffered stream to write modifications to</param>
        /// <param name="deltaSize">Evolution of zone size (in bytes; positive or negative)</param>
        /// <param name="action">Action applied to zone</param>
        /// <param name="zone">Name of zone to process</param>
        /// <param name="globalOffsetCorrection">Offset correction to apply to the zone to process</param>
        /// <param name="regionId">ID of the current buffer region; -1 if working on the file itself (global offset correction)</param>
        /// <returns></returns>
        public bool RewriteHeaders(
            BinaryWriter fullScopeWriter,
            BinaryWriter bufferedWriter,
            long deltaSize,
            ACTION action,
            string zone = DEFAULT_ZONE_NAME,
            long globalOffsetCorrection = 0,
            int regionId = -1)
        {
            long delta;
            long offsetPositionCorrection;
            long offsetValueCorrection;
            byte[] value;
            object updatedValue;
            bool passedParentZone;
            bool passedValueZone;


            if (null == zones) return false;
            if (!zones.ContainsKey(zone)) return true; // No effect


            // Get the dynamic correction map from the proper region
            IDictionary<string, KeyValuePair<long, long>> localDynamicOffsetCorrection;
            if (!dynamicOffsetCorrection.ContainsKey(regionId))
                dynamicOffsetCorrection.Add(regionId, new Dictionary<string, KeyValuePair<long, long>>());

            localDynamicOffsetCorrection = dynamicOffsetCorrection[regionId];

            // Don't reprocess the position of a post-processing zone
            bool isPostReprocessing = zone == POST_PROCESSING_ZONE_NAME;

            // == Update the current zone's headers
            foreach (FrameHeader header in zones[zone].Headers)
            {
                // === Update values
                offsetPositionCorrection = -globalOffsetCorrection;
                offsetValueCorrection = 0;
                delta = 0;
                passedParentZone = false;
                passedValueZone = false;

                foreach (string dynamicZone in localDynamicOffsetCorrection.Keys)
                {
                    // Don't need to process zones located further than we are
                    if (dynamicZone == header.ParentZone) passedParentZone = true;
                    if (dynamicZone == header.ValueZone) passedValueZone = true;

                    KeyValuePair<long, long> offsetDelta = localDynamicOffsetCorrection[dynamicZone];

                    if (header.Position >= offsetDelta.Key && !passedParentZone && !isPostReprocessing) offsetPositionCorrection += offsetDelta.Value;

                    if ((FrameHeader.TYPE.Index == header.Type || FrameHeader.TYPE.RelativeIndex == header.Type) && isValueGT(header.Value, 0, offsetDelta.Key) && !passedValueZone) offsetValueCorrection += offsetDelta.Value;
                }

                // If we're about to write outside the buffered writer and the full-scope writer is available, switch to it
                BinaryWriter w;
                if (null == bufferedWriter) w = fullScopeWriter;
                else if (header.Position + offsetPositionCorrection < 0 || header.Position + offsetPositionCorrection > bufferedWriter.BaseStream.Length)
                {
                    if (null == fullScopeWriter) throw new InvalidDataException("Trying to write outside the buffered writer");
                    Logging.LogDelegator.GetLogDelegate()(Logging.Log.LV_DEBUG, "Trying to write outside the buffered writer - switching to full-scope writer");
                    w = fullScopeWriter;
                    offsetPositionCorrection = 0;
                }
                else w = bufferedWriter;


                if (FrameHeader.TYPE.Counter == header.Type)
                {
                    switch (action)
                    {
                        case ACTION.Add: delta = 1; break;
                        case ACTION.Delete: delta = -1; break;
                        default: delta = 0; break;
                    }

                }
                else if (FrameHeader.TYPE.Size == header.Type)
                {
                    delta = deltaSize;
                }

                // === Rewrite headers

                // If we're going to delete the zone, and the header is located inside it, don't write it !
                if (header.ParentZone == zone && ACTION.Delete == action) continue;

                if ((FrameHeader.TYPE.Counter == header.Type || FrameHeader.TYPE.Size == header.Type))
                {
                    w.BaseStream.Seek(header.Position + offsetPositionCorrection, SeekOrigin.Begin);

                    value = addToValue(header.Value, delta, out updatedValue);

                    if (null == value) throw new NotSupportedException("Value type not supported for " + zone + "@" + header.Position + " : " + header.Value.GetType());

                    // The very same frame header is referenced from another frame and must be updated to its new value
                    updateAllHeadersAtPosition(header.Position, updatedValue);

                    if (!header.IsLittleEndian) Array.Reverse(value);

                    w.Write(value);
                }
                else if (FrameHeader.TYPE.Index == header.Type || FrameHeader.TYPE.RelativeIndex == header.Type)
                {
                    long headerPosition = header.Position + offsetPositionCorrection;
                    w.BaseStream.Seek(headerPosition, SeekOrigin.Begin);
                    value = null;

                    long headerOffsetCorrection = (FrameHeader.TYPE.RelativeIndex == header.Type) ? headerPosition : 0;

                    if (action != ACTION.Delete)
                    {
                        if (header.Value is long)
                        {
                            value = BitConverter.GetBytes((long)header.Value + offsetValueCorrection - headerOffsetCorrection);
                        }
                        else if (header.Value is int)
                        {
                            value = BitConverter.GetBytes((int)((int)header.Value + offsetValueCorrection - headerOffsetCorrection));
                        }
                        else if (header.Value is uint)
                        {
                            value = BitConverter.GetBytes((uint)((uint)header.Value + offsetValueCorrection - headerOffsetCorrection));
                        }

                        if (!header.IsLittleEndian) Array.Reverse(value);
                    }
                    else
                    {
                        if (header.Value is long)
                        {
                            value = BitConverter.GetBytes((long)0);
                        }
                        else if (header.Value is int)
                        {
                            value = BitConverter.GetBytes((int)0);
                        }
                        else if (header.Value is uint)
                        {
                            value = BitConverter.GetBytes((uint)0);
                        }
                    }

                    if (null == value) throw new NotSupportedException("Value type not supported for index in " + zone + "@" + header.Position + " : " + header.Value.GetType());

                    w.Write(value);
                } // Index & relative index types
            } // Loop through headers

            // Update local dynamic offset
            if (!localDynamicOffsetCorrection.ContainsKey(zone))
                localDynamicOffsetCorrection.Add(zone, new KeyValuePair<long, long>(zones[zone].Offset + zones[zone].Size, deltaSize));

            // If applicable, update global dynamic offset
            if (regionId > -1)
            {
                IDictionary<string, KeyValuePair<long, long>> globalRegion = dynamicOffsetCorrection[-1];
                // Add new region
                if (!globalRegion.ContainsKey(zone))
                    globalRegion.Add(zone, new KeyValuePair<long, long>(zones[zone].Offset + zones[zone].Size, deltaSize));
                else // Increment current delta to existing region
                {
                    KeyValuePair<long, long> currentValues = globalRegion[zone];
                    globalRegion[zone] = new KeyValuePair<long, long>(currentValues.Key, currentValues.Value + deltaSize);
                }
            }

            return true;
        }

    }
}