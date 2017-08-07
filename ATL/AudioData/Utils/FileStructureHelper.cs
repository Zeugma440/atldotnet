using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    public class FileStructureHelper
    {
        public const string DEFAULT_ZONE = "default";

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

        public class Frame
        {
            public string Zone;
            public long Offset;
            public int Size;
            public byte[] CoreSignature;
            public IList<FrameHeader> Headers;

            public Frame(string zone, long offset, int size, byte[] coreSignature)
            {
                Zone = zone; Offset = offset; Size = size; CoreSignature = coreSignature;
                Headers = new List<FrameHeader>();
            }

            public void Clear()
            {
                if (Headers != null) Headers.Clear();
            }
        }

        private IDictionary<string, Frame> frames;
        private bool isLittleEndian;


        public ICollection<string> Zones
        {
            get { return frames.Keys;  }
        }

        public ICollection<Frame> Frames
        {
            get { return frames.Values; }
        }


        public FileStructureHelper(bool isLittleEndian = true)
        {
            this.isLittleEndian = isLittleEndian;
            frames = new Dictionary<string, Frame>();
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

        public Frame GetFrame(string zone)
        {
            if (frames.ContainsKey(zone)) return frames[zone]; else return null;
        }

        public void AddFrame(long offset, int size, string zone = DEFAULT_ZONE)
        {
            AddFrame(offset, size, new byte[0], zone);
        }

        public void AddFrame(long offset, int size, byte[] coreSignature, string zone = DEFAULT_ZONE)
        {
            Frame frame = new Frame(zone, offset, size, coreSignature);
            if (!frames.ContainsKey(zone))
            {
                frames.Add(zone, frame);
            }
            else // Recorded frame might already contain headers
            {
                frame.Headers = frames[zone].Headers;
                frames[zone] = frame;
            }
        }

        public void AddCounter(long position, object value, string zone = DEFAULT_ZONE)
        {
            addFrameHeader(zone, FrameHeader.TYPE_COUNTER, position, value, isLittleEndian);
        }

        public void AddSize(long position, object value, string zone = DEFAULT_ZONE)
        {
            addFrameHeader(zone, FrameHeader.TYPE_SIZE, position, value, isLittleEndian);
        }

        private void addFrameHeader(string zone, byte type, long position, object value, bool isLittleEndian)
        {
            if (!frames.ContainsKey(zone)) // Might happen when reading header frames of containing upper frames, without having reached tag frame itself
            {
                AddFrame(0, 0, zone);
            }
            frames[zone].Headers.Add(new FrameHeader(type, position, value, isLittleEndian));
        }

        public bool RewriteMarkers(ref BinaryWriter w, int deltaSize, string zone = DEFAULT_ZONE)
        {
            bool result = true;
            byte[] value;

            if (frames != null && frames.ContainsKey(zone) && deltaSize != 0)
            {
                foreach (FrameHeader info in frames[zone].Headers)
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