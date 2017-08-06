using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    class FileStructureHelper
    {
        // Description of a chunk/frame within a structured file 
        // Useful to rewrite after editing the chunk (e.g. adding/removing metadata)
        public class StructInfo
        {
            public const byte TYPE_COUNTER = 0;
            public const byte TYPE_SIZE = 1;

            public byte Type;
            public long Position;
            public object Value;
            public bool IsLittleEndian;

            public StructInfo(byte type, long position, object value, bool isLittleEndian = true)
            {
                Type = type;  Position = position; Value = value; IsLittleEndian = isLittleEndian;
            }
        }

        private IDictionary<string, IList<StructInfo>> frames;
        private bool isLittleEndian;


        public FileStructureHelper(bool isLittleEndian = true)
        {
            this.isLittleEndian = isLittleEndian;
            frames = new Dictionary<string, IList<StructInfo>>();
        }

        public void Clear()
        {
            if (null != frames)
            {
                foreach(string s in frames.Keys)
                {
                    frames[s].Clear();
                }
                frames.Clear();
            }
        }

        public void AddCounter(long position, object value, string zone = "main")
        {
            addElement(zone, StructInfo.TYPE_COUNTER, position, value, isLittleEndian);
        }

        public void AddSize(long position, object value, string zone = "main")
        {
            addElement(zone, StructInfo.TYPE_SIZE, position, value, isLittleEndian);
        }

        private void addElement(string zone, byte type, long position, object value, bool isLittleEndian)
        {
            if (!frames.ContainsKey(zone)) frames.Add(zone, new List<StructInfo>());
            frames[zone].Add(new StructInfo(type, position, value, isLittleEndian));
        }

        public bool RewriteMarkers(ref BinaryWriter w, int deltaSize, string zone = "main")
        {
            bool result = true;
            byte[] value;

            if (frames != null && frames.ContainsKey(zone) && deltaSize != 0)
            {
                foreach (StructInfo info in frames[zone])
                {
                    w.BaseStream.Seek(info.Position, SeekOrigin.Begin);

                    // TODO make the difference between TYPE_SIZE and TYPE_COUNTER

                    if (info.Value is byte) value = BitConverter.GetBytes((byte)((byte)info.Value + deltaSize));
                    else if (info.Value is short) value = BitConverter.GetBytes((short)((short)info.Value + deltaSize));
                    else if (info.Value is ushort) value = BitConverter.GetBytes((ushort)((ushort)info.Value + deltaSize));
                    else if (info.Value is int) value = BitConverter.GetBytes((int)info.Value + deltaSize);
                    else if (info.Value is uint) value = BitConverter.GetBytes((uint)((uint)info.Value + deltaSize));
                    else if (info.Value is long) value = BitConverter.GetBytes((long)info.Value + deltaSize);
                    else if (info.Value is ulong) // Need to tweak because ulong + int is illegal according to the compiler
                    {
                        if (deltaSize > 0) value = BitConverter.GetBytes((ulong)info.Value + (ulong)deltaSize);
                        else value = BitConverter.GetBytes((ulong)info.Value - (ulong)(-deltaSize));
                    }
                    else
                    {
                        throw new NotSupportedException("Value type not detected");
                    }

                    if (!info.IsLittleEndian) Array.Reverse(value);

                    w.Write(value);
                }
            }

            return result;
        }

    }
}