using System;
using System.Linq;

namespace SpawnDev.EBML.Extensions
{
    public static class BigEndian
    {
        public static float ToSingle(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToSingle(bytes, startIndex);
            return BitConverter.ToSingle(bytes.ReverseSegment(startIndex, sizeof(float)));
        }
        public static double ToDouble(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToDouble(bytes, startIndex);
            return BitConverter.ToDouble(bytes.ReverseSegment(startIndex, sizeof(double)));
        }
        public static ushort ToUInt16(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToUInt16(bytes, startIndex);
            return BitConverter.ToUInt16(bytes.ReverseSegment(startIndex, sizeof(ushort)));
        }
        public static short ToInt16(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToInt16(bytes, startIndex);
            return BitConverter.ToInt16(bytes.ReverseSegment(startIndex, sizeof(short)));
        }
        public static uint ToUInt32(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToUInt32(bytes, startIndex);
            return BitConverter.ToUInt32(bytes.ReverseSegment(startIndex, sizeof(uint)));
        }
        public static int ToInt32(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToInt32(bytes, startIndex);
            return BitConverter.ToInt32(bytes.ReverseSegment(startIndex, sizeof(int)));
        }
        public static ulong ToUInt64(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToUInt64(bytes, startIndex);
            return BitConverter.ToUInt64(bytes.ReverseSegment(startIndex, sizeof(ulong)));
        }
        public static long ToInt64(byte[] bytes, int startIndex = 0)
        {
            if (!BitConverter.IsLittleEndian) return BitConverter.ToInt64(bytes, startIndex);
            return BitConverter.ToInt64(bytes.ReverseSegment(startIndex, sizeof(long)));
        }
        public static byte[] ReverseSegment(this byte[] bytes, int startIndex, int size)
        {
            var chunk = new byte[size];
            for (var i = 0; i < size; i++) chunk[size - (i + 1)] = bytes[startIndex + i];
            return chunk;
        }
        public static byte[] GetBytes(float value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(double value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(ushort value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(short value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(uint value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(int value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(long value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] GetBytes(ulong value) => BitConverter.GetBytes(value).ReverseIfLittleEndian();
        public static byte[] ReverseIfLittleEndian(this byte[] _this) => !BitConverter.IsLittleEndian ? _this : _this.Reverse().ToArray();
        public static byte[] ReverseArray(this byte[] _this) => _this.Reverse().ToArray();
    }
}
