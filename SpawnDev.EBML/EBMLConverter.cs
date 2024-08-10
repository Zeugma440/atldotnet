using System;
using System.Linq;
using System.Text;

namespace SpawnDev.EBML
{
    public static class EBMLConverter
    {
        public static byte[] ToVINTBytes(ulong x, int minOctets = 0)
        {
            int bytes;
            ulong flag;
            for (bytes = 1, flag = 0x80; x >= flag && bytes < 8; bytes++, flag *= 0x80) { }
            if (IsUnknownSizeVINT(x, bytes))
            {
                bytes += 1;
                flag *= 0x80;
            }
            while(bytes < minOctets)
            {
                bytes += 1;
                flag *= 0x80;
            }
            var ret = new byte[bytes];
            var value = flag + x;
            for (var i = bytes - 1; i >= 0; i--)
            {
                var c = value % 256;
                ret[i] = (byte)c;
                value = (value - c) / 256;
            }
            return ret;
        }
        public static int ToVINTByteSize(ulong x, int minOctets = 0)
        {
            int bytes;
            ulong flag;
            for (bytes = 1, flag = 0x80; x >= flag && bytes < 8; bytes++, flag *= 0x80) { }
            if (IsUnknownSizeVINT(x, bytes))
            {
                bytes += 1;
            }
            bytes = Math.Max(minOctets, bytes);
            return bytes;
        }
        public static byte[] ToUIntBytes(ulong value, int minOctets = 0)
        {
            var bytes = BigEndian.GetBytes(value).ToList();
            while (bytes.Count > 1 && bytes[0] == 0 && (minOctets <= 0 || bytes.Count > minOctets)) bytes.RemoveAt(0);
            return bytes.ToArray();
        }
        public static byte[] ToIntBytes(long value, int minOctets = 0)
        {
            var bytes = BigEndian.GetBytes(value).ToList();
            while (bytes.Count > 1 && bytes[0] == 0 && (minOctets <= 0 || bytes.Count > minOctets)) bytes.RemoveAt(0);
            return bytes.ToArray();
        }
        public static byte[] ToFloatBytes(double value)
        {
            return BigEndian.GetBytes(value);
        }
        public static byte[] ToFloatBytes(float value)
        {
            return BigEndian.GetBytes(value);
        }
        private static readonly double TimeScale = 1000000;
        public static readonly DateTime DateTimeReferencePoint = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static byte[] ToDateBytes(DateTime value, int minOctets = 0)
        {
            var timeOffset = (long)((value - DateTimeReferencePoint).TotalMilliseconds * TimeScale);
            return ToIntBytes(timeOffset, minOctets);
        }
        public static int GetFirstSetBitIndex(byte value, out byte leftover)
        {
            for (int i = 0; i < 8; i++)
            {
                var v = 1 << (7 - i);
                if ((value & v) != 0)
                {
                    leftover = (byte)(value - v);
                    return i;
                }
            }
            leftover = 0;
            return -1;
        }
        public static ulong ReadEBMLElementSize(byte[] data, out int vintSize, out bool isUnknownSize, int index = 0)
        {
            var size = ReadEBMLVINT(data, out vintSize, index);
            isUnknownSize = IsUnknownSizeVINT(size, vintSize);
            return size;
        }
        public static T ReadEBMLElementId<T>(byte[] data, out int vintSize, out bool isInvalid, int index = 0) where T : struct => (T)(object)ReadEBMLElementId(data, out vintSize, out isInvalid, index);
        public static ulong ReadEBMLElementId(byte[] data, out int vintSize, out bool isInvalid, int index = 0)
        {
            var size = ReadEBMLVINT(data, out vintSize, index);
            isInvalid = IsUnknownSizeVINT(size, vintSize);
            return size;
        }
        public static T ReadEBMLElementId<T>(byte[] data, out int vintSize, int index = 0) where T : struct => (T)(object)ReadEBMLElementId(data, out vintSize, index);
        public static ulong ReadEBMLElementId(byte[] data, out int vintSize, int index = 0) => ReadEBMLVINT(data, out vintSize, index);
        public static ulong ReadEBMLVINT(byte[] data, out int size, int index = 0)
        {
            var firstByte = data[index];
            var bitIndex = GetFirstSetBitIndex(firstByte, out var leftover);
            if (bitIndex < 0)
            {
                size = 0;
                return 0; // marker bit must be in first byte (verify correct response to this)
            }
            var ulongBytes = new byte[8];
            var destIndex = 8 - bitIndex;
            ulongBytes[destIndex - 1] = leftover;
            if (bitIndex > 0) Buffer.BlockCopy(data, index + 1, ulongBytes, destIndex, bitIndex);
            size = bitIndex + 1;
            return BigEndian.ToUInt64(ulongBytes);
        }
        public static ulong ReadEBMLUInt(byte[] data, int size, int index = 0)
        {
            var bytes = new byte[8];
            var destIndex = 8 - size;
            if (size > 0)
            {
                Buffer.BlockCopy(data, index, bytes, destIndex, size);
            }
            return BigEndian.ToUInt64(bytes);
        }
        public static long ReadEBMLInt(byte[] data, int size, int index = 0)
        {
            var bytes = new byte[8];
            var destIndex = 8 - size;
            if (size > 0)
            {
                Buffer.BlockCopy(data, index, bytes, destIndex, size);
            }
            return BigEndian.ToInt64(bytes);
        }
        public static double ReadEBMLFloat(byte[] data, int size, int index = 0)
        {
            if (size == 4)
            {
                return BigEndian.ToSingle(data, index);
            }
            else if (size == 8)
            {
                return BigEndian.ToDouble(data, index);
            }
            return 0;
        }
        public static string ReadEBMLString(byte[] data, int count, int index = 0, Encoding? encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            return encoding.GetString(data, index, count).TrimEnd('\0');
        }
        public static DateTime ReadEBMLDate(byte[] data, int size, int index = 0)
        {
            if (size == 0) return DateTimeReferencePoint;
            var timeOffset = ReadEBMLInt(data, size, index);
            return DateTimeReferencePoint + TimeSpan.FromMilliseconds(timeOffset / TimeScale);
        }
        public const ulong UnknownSizeVINT8 = 72057594037927935ul;
        public static ulong GetUnknownSizeValue(int size) => (ulong)Math.Pow(2, (size * 8) - size) - 1ul;
        public static bool IsUnknownSizeVINT(ulong value, int size) => value == GetUnknownSizeValue(size);
    }
}
