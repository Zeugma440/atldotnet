using System;
using System.Collections.Generic;
using System.IO;

namespace ATL.AudioData
{
    public class FileStructureHelper
    {
        public const string DEFAULT_ZONE_NAME = "default";

        public const int ACTION_EDIT    = 0;
        public const int ACTION_ADD     = 1;
        public const int ACTION_DELETE  = 2;

        public class FrameHeader
        {
            public const byte TYPE_COUNTER = 0;
            public const byte TYPE_SIZE = 1;
            public const byte TYPE_INDEX = 2;

            public byte Type;
            public long Position;
            public object Value;
            public bool IsLittleEndian;

            public FrameHeader(byte type, long position, object value, bool isLittleEndian = true)
            {
                Type = type;  Position = position; Value = value; IsLittleEndian = isLittleEndian;
            }
        }

        // Description of a chunk/frame within a structured file 
        // Useful to rewrite after editing the chunk (e.g. adding/removing metadata)
        public class Zone
        {
            public string Name;                         // Name
            public long Offset;                         // Offset at the time of its reading, in bytes
            public int Size;                            // Size at the time of its reading, in bytes
            public byte[] CoreSignature;                // Header that has to be written no matter what, even if the zone does not contain any data
            public byte Flag;                           // Generic usage flag for storing information
            // insert padding size information here ?
            public IList<FrameHeader> Headers;          // Size descriptors and item counters referencing the zone elsehwere on the file

            public Zone(string name, long offset, int size, byte[] coreSignature, byte flag = 0)
            {
                Name = name; Offset = offset; Size = size; CoreSignature = coreSignature; Flag = flag;
                Headers = new List<FrameHeader>();
            }

            public void Clear()
            {
                if (Headers != null) Headers.Clear();
            }
        }

        private IDictionary<string, Zone> zones;
        private IDictionary<string,KeyValuePair<long, long>> dynamicOffsetCorrection = new Dictionary<string,KeyValuePair<long,long>>();
        private bool isLittleEndian;


        public ICollection<string> ZoneNames
        {
            get { return zones.Keys;  }
        }

        public ICollection<Zone> Zones
        {
            get { return zones.Values; }
        }


        public FileStructureHelper(bool isLittleEndian = true)
        {
            this.isLittleEndian = isLittleEndian;
            zones = new Dictionary<string, Zone>();
        }

        public void Clear()
        {
            if (null != zones)
            {
                foreach(string s in zones.Keys)
                {
                    zones[s].Clear();
                }
                zones.Clear();
            }
        }

        public Zone GetZone(string name)
        {
            if (zones.ContainsKey(name)) return zones[name]; else return null;
        }

        public void AddZone(Zone zone)
        {
            AddZone(zone.Offset, zone.Size, zone.CoreSignature, zone.Name);
        }

        public void AddZone(long offset, int size, string name = DEFAULT_ZONE_NAME)
        {
            AddZone(offset, size, new byte[0], name);
        }

        public void AddZone(long offset, int size, byte[] coreSignature, string zone = DEFAULT_ZONE_NAME)
        {
            if (!zones.ContainsKey(zone))
            {
                zones.Add(zone, new Zone(zone, offset, size, coreSignature));
            }
            else // Existing zone might already contain headers
            {
                zones[zone].Name = zone;
                zones[zone].Offset = offset;
                zones[zone].Size = size;
                zones[zone].CoreSignature = coreSignature;
            }
        }

        public void AddCounter(long position, object value, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, FrameHeader.TYPE_COUNTER, position, value, isLittleEndian);
        }

        public void AddSize(long position, object value, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, FrameHeader.TYPE_SIZE, position, value, isLittleEndian);
        }

        public void AddIndex(long position, object value, string zone = DEFAULT_ZONE_NAME)
        {
            addZoneHeader(zone, FrameHeader.TYPE_INDEX, position, value, isLittleEndian);
        }

        private void addZoneHeader(string zone, byte type, long position, object value, bool isLittleEndian)
        {
            if (!zones.ContainsKey(zone)) // Might happen when reading header frames of containing upper frames, without having reached tag frame itself
            {
                AddZone(0, 0, zone);
            }
            zones[zone].Headers.Add(new FrameHeader(type, position, value, isLittleEndian));
        }

        // NB : this method should perform quite badly -- evolve to using position-based dictionary if any performance issue arise
        private void updateAcrossEntireCollection(long position, object newValue)
        {
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

        private static byte[] addToValue(object value, int delta, out object updatedValue)
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

        public bool RewriteMarkers(BinaryWriter w, int deltaSize, int action, string zone = DEFAULT_ZONE_NAME)
        {
            bool result = true;
            int delta;
            long offsetCorrection;
            byte[] value;
            object updatedValue;

            if (zones != null && zones.ContainsKey(zone))
            {
                foreach (FrameHeader header in zones[zone].Headers)
                {
                    offsetCorrection = 0;
                    delta = 0;
                    foreach(KeyValuePair<long, long> offsetDelta in dynamicOffsetCorrection.Values)
                    {
                        if (header.Position >= offsetDelta.Key) offsetCorrection += offsetDelta.Value;
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

                    if ((FrameHeader.TYPE_COUNTER == header.Type || FrameHeader.TYPE_SIZE == header.Type) && (delta != 0))
                    {
                        w.BaseStream.Seek(header.Position + offsetCorrection, SeekOrigin.Begin);

                        value = addToValue(header.Value, delta, out updatedValue);

                        if (null == value) throw new NotSupportedException("Value type not supported for " + zone + "@" + header.Position + " : " + header.Value.GetType());

                        // The very same frame header is referenced from another frame and must be updated to its new value
                        updateAcrossEntireCollection(header.Position, updatedValue);

                        if (!header.IsLittleEndian) Array.Reverse(value);

                        w.Write(value);
                    }
                    else if (FrameHeader.TYPE_INDEX == header.Type)
                    {
                        w.BaseStream.Seek(header.Position + offsetCorrection, SeekOrigin.Begin);
                        value = null;

                        if (action != ACTION_DELETE)
                        {
                            if (header.Value is long)
                            {
                                value = BitConverter.GetBytes((long)zones[zone].Offset + offsetCorrection);
                            }

                            if (!header.IsLittleEndian) Array.Reverse(value);
                        }
                        else
                        {
                            if (header.Value is long)
                            {
                                value = BitConverter.GetBytes((long)0);
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