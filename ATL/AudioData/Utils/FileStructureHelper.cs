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
        public const string DEFAULT_ZONE_NAME = "default"; // Default zone name to be used when no naming is necessary (simple cases where there is a but a single Zone to describe)
        public const string PADDING_ZONE_NAME = "padding"; // Zone name to be used for padding

        // Type of action to react to
        public const int ACTION_EDIT = 0; // Existing zone is edited, and not removed
        public const int ACTION_ADD = 1; // New zone is added
        public const int ACTION_DELETE = 2; // Existing zone is removed

        /// <summary>
        /// Container class describing a frame header
        /// </summary>
        public class FrameHeader
        {
            // Header types
            public const byte TYPE_COUNTER = 0;  // Counter : counts the underlying number of frames
            public const byte TYPE_SIZE = 1;     // Size : documents the size of a given frame / group of frames
            public const byte TYPE_INDEX = 2;    // Index (absolute) : documents the offset (position of 1st byte) of a given frame
            public const byte TYPE_RINDEX = 3;   // Index (relative) : documents the offset (position of 1st byte) of a given frame, relative to the header's position

            /// <summary>
            /// Header type (allowed values are TYPE_XXX within FrameHeader class)
            /// </summary>
            public readonly byte Type;
            /// <summary>
            /// Position of the header
            /// </summary>
            public readonly long Position;
            /// <summary>
            /// True if header value is stored using little-endian convention; false if big-endian
            /// </summary>
            public readonly bool IsLittleEndian;
            /// <summary>
            /// Current value of the header (counter : number of frames / size : frame size / index : frame index (absolute) / rindex : frame index (relative to header position))
            /// </summary>
            public object Value;

            /// <summary>
            /// Constructs a new frame header using the given field values
            /// </summary>
            public FrameHeader(byte type, long position, object value, bool isLittleEndian = true)
            {
                Type = type; Position = position; Value = value; IsLittleEndian = isLittleEndian;
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
            public int Size;
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
            /// Construct a new Zone using the given field values
            /// </summary>
            public Zone(string name, long offset, int size, byte[] coreSignature, bool isDeletable = true, byte flag = 0)
            {
                Name = name; Offset = offset; Size = size; CoreSignature = coreSignature; IsDeletable = isDeletable; Flag = flag;
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
        //      Dictionary key  : zone name
        //      KVP Key         : initial end offset of given zone (i.e. position of last byte within zone)
        //      KVP Value       : variation applied to given zone (can be positive or negative)
        private readonly IDictionary<string, KeyValuePair<long, long>> dynamicOffsetCorrection = new Dictionary<string, KeyValuePair<long, long>>();

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
        /// Recorded zones
        /// </summary>
        public ICollection<Zone> Zones
        {
            get
            {
                // 1. Ignore zones declared but not added
                // 2. Sort by offset
                return zones.Values.Where(zone => zone.Offset > -1).OrderBy(zone => zone.Offset).ToList();
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
        }

        /// <summary>
        /// Clears all recorded Zones
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
        public void AddZone(long offset, int size, string name = DEFAULT_ZONE_NAME, bool isDeletable = true)
        {
            AddZone(offset, size, new byte[0], name, isDeletable);
        }

        /// <summary>
        /// Record a new zone using the given fields
        /// </summary>
        public void AddZone(long offset, int size, byte[] coreSignature, string zone = DEFAULT_ZONE_NAME, bool isDeletable = true)
        {
            if (!zones.ContainsKey(zone))
            {
                zones.Add(zone, new Zone(zone, offset, size, coreSignature, isDeletable));
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
        public void AddCounter(long position, object value, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, FrameHeader.TYPE_COUNTER, position, value, isLittleEndian);
        }

        /// <summary>
        /// Record a new Size-type header using the given fields and attach it to the zone of given name
        /// </summary>
        public void AddSize(long position, object value, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, FrameHeader.TYPE_SIZE, position, value, isLittleEndian);
        }

        /// <summary>
        /// Record a new Index-type header using the given fields and attach it to the zone of given name
        /// </summary>
        public void AddIndex(long position, object value, bool relative = false, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, relative ? FrameHeader.TYPE_RINDEX : FrameHeader.TYPE_INDEX, position, value, isLittleEndian);
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
        private void addZoneHeader(string zone, byte type, long position, object value, bool isLittleEndian)
        {
            if (!zones.ContainsKey(zone)) DeclareZone(zone);
            zones[zone].Headers.Add(new FrameHeader(type, position, value, isLittleEndian));
        }

        public long GetFirstRecordedOffset()
        {
            long result = long.MaxValue;
            if (zones != null)
                foreach (Zone zone in zones.Values)
                {
                    result = Math.Min(result, zone.Offset);
                    foreach (FrameHeader header in zone.Headers)
                        result = Math.Min(result, header.Position);
                }
            return result;
        }

        public long GetLastRecordedOffset()
        {
            long result = 0;
            if (zones != null)
                foreach (Zone zone in zones.Values)
                {
                    result = Math.Max(result, zone.Offset + zone.Size);
                    foreach (FrameHeader header in zone.Headers)
                        result = Math.Max(result, header.Position);
                }
            return result;
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
                updatedValue = (long)((long)value + delta);
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

        private static bool isValueGT(object value, long comparison)
        {
            if (value is int)
                return (int)value > comparison;
            else if (value is uint)
                return (uint)value > comparison;
            else if (value is long)
                return (long)value > comparison;
            else
                throw new NotSupportedException("Value type not supported in comparison");
        }

        /// <summary>
        /// Rewrite all zone headers in the given stream according to the given size evolution and the given action
        /// </summary>
        /// <param name="w">Stream to write modifications to</param>
        /// <param name="deltaSize">Evolution of zone size (in bytes; positive or negative)</param>
        /// <param name="action">Action applied to zone</param>
        /// <param name="zone">Name of zone</param>
        /// <returns></returns>
        public bool RewriteHeaders(BinaryWriter w, long deltaSize, int action, string zone = DEFAULT_ZONE_NAME, long globalOffsetCorrection = 0)
        {
            bool result = true;
            long delta;
            long offsetPositionCorrection;
            long offsetValueCorrection = 0;
            byte[] value;
            object updatedValue;

            if (zones != null && zones.ContainsKey(zone))
            {
                foreach (FrameHeader header in zones[zone].Headers)
                {
                    offsetPositionCorrection = -globalOffsetCorrection;
                    delta = 0;
                    foreach (KeyValuePair<long, long> offsetDelta in dynamicOffsetCorrection.Values)
                    {
                        if (header.Position >= offsetDelta.Key) offsetPositionCorrection += offsetDelta.Value;

                        if ((FrameHeader.TYPE_INDEX == header.Type || FrameHeader.TYPE_RINDEX == header.Type) && isValueGT(header.Value,offsetDelta.Key)) offsetValueCorrection += offsetDelta.Value;
                    }

                    if (FrameHeader.TYPE_COUNTER == header.Type)
                    {
                        switch (action)
                        {
                            case ACTION_ADD: delta = 1; break;
                            case ACTION_DELETE: delta = -1; break;
                            default: delta = 0; break;
                        }

                    }
                    else if (FrameHeader.TYPE_SIZE == header.Type)
                    {
                        delta = deltaSize;
                        if (!dynamicOffsetCorrection.ContainsKey(zone))
                        {
                            dynamicOffsetCorrection.Add(zone, new KeyValuePair<long, long>(zones[zone].Offset + zones[zone].Size, deltaSize));
                        }
                    }

                    if ((FrameHeader.TYPE_COUNTER == header.Type || FrameHeader.TYPE_SIZE == header.Type))
                    {
                        w.BaseStream.Seek(header.Position + offsetPositionCorrection, SeekOrigin.Begin);

                        value = addToValue(header.Value, delta, out updatedValue);

                        if (null == value) throw new NotSupportedException("Value type not supported for " + zone + "@" + header.Position + " : " + header.Value.GetType());

                        // The very same frame header is referenced from another frame and must be updated to its new value
                        updateAllHeadersAtPosition(header.Position, updatedValue);

                        if (!header.IsLittleEndian) Array.Reverse(value);

                        w.Write(value);
                    }
                    else if (FrameHeader.TYPE_INDEX == header.Type || FrameHeader.TYPE_RINDEX == header.Type)
                    {
                        long headerPosition = header.Position + offsetPositionCorrection;
                        w.BaseStream.Seek(headerPosition, SeekOrigin.Begin);
                        value = null;

                        long headerOffsetCorrection = (FrameHeader.TYPE_RINDEX == header.Type) ? headerPosition : 0;

                        if (action != ACTION_DELETE)
                        {
                            if (header.Value is long)
                            {
                                value = BitConverter.GetBytes((long)zones[zone].Offset + offsetValueCorrection - headerOffsetCorrection);
                            }
                            else if (header.Value is int)
                            {
                                value = BitConverter.GetBytes((int)(zones[zone].Offset + offsetValueCorrection - headerOffsetCorrection));
                            }
                            else if (header.Value is uint)
                            {
                                value = BitConverter.GetBytes((uint)(zones[zone].Offset + offsetValueCorrection - headerOffsetCorrection));
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
                    }
                }
            }

            return result;
        }

    }
}