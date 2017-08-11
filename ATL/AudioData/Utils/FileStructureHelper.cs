using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    public class FileStructureHelper
    {
        public const string DEFAULT_ZONE_NAME = "default";

        public const int ACTION_EDIT    = 0;
        public const int ACTION_ADD     = 1;
        public const int ACTION_DELETE  = 2;

        // Description of a chunk/frame within a structured file 
        // Useful to rewrite after editing the chunk (e.g. adding/removing metadata)
        public class FrameHeader
        {
            public const byte TYPE_COUNTER = 0;
            public const byte TYPE_SIZE = 1;

            public byte Type;
            public long Position;
            public object Value;
            public bool IsLittleEndian;

            public FrameHeader(byte type, long position, object value, bool isLittleEndian = true)
            {
                Type = type;  Position = position; Value = value; IsLittleEndian = isLittleEndian;
            }
        }

        public class Zone
        {
            public string Name;
            public long Offset;
            public int Size;
            public byte[] CoreSignature;
            public IList<FrameHeader> Headers;

            public Zone(string name, long offset, int size, byte[] coreSignature)
            {
                Name = name; Offset = offset; Size = size; CoreSignature = coreSignature;
                Headers = new List<FrameHeader>();
            }

            public void Clear()
            {
                if (Headers != null) Headers.Clear();
            }
        }

        private IDictionary<string, Zone> zones;
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

        public bool RewriteMarkers(ref BinaryWriter w, int deltaSize, int action, string zone = DEFAULT_ZONE_NAME)
        {
            bool result = true;
            int delta;
            byte[] value;

            if (zones != null && zones.ContainsKey(zone))
            {
                foreach (FrameHeader header in zones[zone].Headers)
                {
                    if (FrameHeader.TYPE_COUNTER == header.Type)
                    {
                        switch (action)
                        {
                            case ACTION_ADD: delta = 1; break;
                            case ACTION_DELETE: delta = -1; break;
                            default: delta = 0; break;
                        }

                    } else {
                        delta = deltaSize;
                    }

                    if (delta != 0)
                    {
                        w.BaseStream.Seek(header.Position, SeekOrigin.Begin);

                        if (header.Value is byte) value = BitConverter.GetBytes((byte)((byte)header.Value + delta));
                        else if (header.Value is short) value = BitConverter.GetBytes((short)((short)header.Value + delta));
                        else if (header.Value is ushort) value = BitConverter.GetBytes((ushort)((ushort)header.Value + delta));
                        else if (header.Value is int) value = BitConverter.GetBytes((int)header.Value + delta);
                        else if (header.Value is uint) value = BitConverter.GetBytes((uint)((uint)header.Value + delta));
                        else if (header.Value is long) value = BitConverter.GetBytes((long)header.Value + delta);
                        else if (header.Value is ulong) // Need to tweak because ulong + int is illegal according to the compiler
                        {
                            if (deltaSize > 0) value = BitConverter.GetBytes((ulong)header.Value + (ulong)delta);
                            else value = BitConverter.GetBytes((ulong)header.Value - (ulong)(-delta));
                        }
                        else
                        {
                            throw new NotSupportedException("Value type not detected");
                        }

                        // The very same frame header is referenced from another frame and must be updated to its new value
                        updateAcrossEntireCollection(header.Position, value);

                        if (!header.IsLittleEndian) Array.Reverse(value);

                        w.Write(value);
                    }
                }
            }

            return result;
        }

    }
}