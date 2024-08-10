using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SpawnDev.EBML
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Returns the maximum number of bytes that can be read starting from the given offset<br />
        /// If the offset &gt;= length or offset &lt; 0 or !CanRead, 0 is returned
        /// </summary>
        /// <param name="_this"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static long MaxReadableCount(this Stream _this, long offset)
        {
            if (!_this.CanRead || offset < 0 || offset >= _this.Length || _this.Length == 0) return 0;
            return _this.Length - offset;
        }
        public static long MaxReadableCount(this Stream _this) => _this.MaxReadableCount(_this.Position);
        public static long GetReadableCount(this Stream _this, long maxCount)
        {
            return _this.GetReadableCount(_this.Position, maxCount);
        }
        public static long GetReadableCount(this Stream _this, long offset, long maxCount)
        {
            if (maxCount <= 0) return 0;
            var bytesLeft = _this.MaxReadableCount(offset);
            return Math.Min(bytesLeft, maxCount);
        }
        public static byte[] ReadBytes(this Stream _this)
        {
            var readCount = _this.MaxReadableCount();
            var bytes = new byte[readCount];
            _this.Read(bytes, 0, bytes.Length);
            return bytes;
        }
        public static byte[] ReadBytes(this Stream _this, long count, bool requireCountExact = false)
        {
            var readCount = _this.GetReadableCount(count);
            if (readCount != count && requireCountExact) throw new Exception("Not available");
            var bytes = new byte[readCount];
            if (readCount == 0) return bytes;
            _this.Read(bytes, 0, bytes.Length);
            return bytes;
        }
        public static byte[] ReadBytes(this Stream _this, long offset, long count, bool requireCountExact = false)
        {
            var origPosition = _this.Position;
            _this.Position = offset;
            try
            {
                var readCount = _this.GetReadableCount(offset, count);
                if (readCount != count && requireCountExact) throw new Exception("Not available");
                var bytes = new byte[readCount];
                if (readCount == 0) return bytes;
                _this.Read(bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                _this.Position = origPosition;
            }
        }
        public static int ReadByte(this Stream _this, long offset)
        {
            var origPosition = _this.Position;
            _this.Position = offset;
            try
            {
                var ret = _this.ReadByte();
                return ret;
            }
            finally
            {
                _this.Position = origPosition;
            }
        }
        public static byte ReadByteOrThrow(this Stream _this, long offset)
        {
            var origPosition = _this.Position;
            _this.Position = offset;
            try
            {
                var ret = _this.ReadByte();
                if (ret == -1) throw new EndOfStreamException();
                return (byte)ret;
            }
            finally
            {
                _this.Position = origPosition;
            }
        }
        public static byte ReadByteOrThrow(this Stream _this)
        {
            var ret = _this.ReadByte();
            if (ret == -1) throw new EndOfStreamException();
            return (byte)ret;
        }

        #region EBML
        private static int GetFirstSetBitIndex(byte value, out byte leftover)
        {
            for (var i = 0; i < 8; i++)
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
        public static ulong ReadEBMLElementId(this Stream data, out bool isInvalid) => data.ReadEBMLVINT(out isInvalid);
        public static ulong ReadEBMLElementId(this Stream data) => data.ReadEBMLVINT(out var isInvalid);
        public static ulong ReadEBMLElementSize(this Stream data, out bool isUnknownSize) => data.ReadEBMLVINT(out isUnknownSize);
        public delegate bool ValidElementChildCheckDelegate(ulong[] parentIdChain, ulong childElementId);
        public delegate bool ValidElementChildCheckEnumDelegate(Enum[] parentIdChain, Enum childElementId);
        public static ulong ReadEBMLElementSize(this Stream data, ulong[] idChain, ValidElementChildCheckDelegate validChildCheck)
        {
            var size = data.ReadEBMLElementSize(out var isUnknownSize);
            if (isUnknownSize)
            {
                size = data.DetermineEBMLElementSize(idChain, validChildCheck);
            }
            return size;
        }
        public static ulong DetermineEBMLElementSize(this Stream stream, Enum[] idChain, ValidElementChildCheckEnumDelegate validChildCheck)
        {
            long startOffset = stream.Position;
            long pos = stream.Position;
            var enumType = idChain[0].GetType();
            while (true)
            {
                pos = stream.Position;
                if (stream.Position >= stream.Length) break;
                var id = stream.ReadEBMLElementId().ToEnum(enumType);
                var len = stream.ReadEBMLElementSize(out var isUnknownSize);
                var isAllowedChild = validChildCheck(idChain, id);
                if (!isAllowedChild)
                {
                    break;
                }
                if (isUnknownSize)
                {
                    var childIdChain = idChain.Concat(new Enum[] { id }).ToArray();
                    len = stream.DetermineEBMLElementSize(childIdChain, validChildCheck);
                }
                stream.Seek((long)len, SeekOrigin.Current);
            }
            stream.Position = startOffset;
            return (ulong)(pos - startOffset);
        }
        public static ulong DetermineEBMLElementSize(this Stream stream, ulong[] idChain, ValidElementChildCheckDelegate validChildCheck)
        {
            long startOffset = stream.Position;
            long pos = stream.Position;
            while (true)
            {
                pos = stream.Position;
                if (stream.Position >= stream.Length) break;
                var id = stream.ReadEBMLElementId();
                var len = stream.ReadEBMLElementSize(out var isUnknownSize);
                var isAllowedChild = validChildCheck(idChain, id);
                if (!isAllowedChild)
                {
                    break;
                }
                if (isUnknownSize)
                {
                    var childIdChain = idChain.Concat(new ulong[] { id }).ToArray();
                    len = stream.DetermineEBMLElementSize(childIdChain, validChildCheck);
                }
                stream.Seek((long)len, SeekOrigin.Current);
            }
            stream.Position = startOffset;
            return (ulong)(pos - startOffset);
        }
        public static Stream SkipEBMLVINT(this Stream data)
        {
            var firstByte = data.ReadByteOrThrow();
            var bitIndex = GetFirstSetBitIndex(firstByte, out var leftover);
            if (bitIndex > 0) data.Position += bitIndex;
            return data;
        }
        public static ulong ReadEBMLVINT(this Stream data)
        {
            var firstByte = data.ReadByteOrThrow();
            var bitIndex = GetFirstSetBitIndex(firstByte, out var leftover);
            if (bitIndex < 0)
            {
                ////vintDataAllOnes = false;
                // throw?
                return 0; // marker bit must be in first byte (verify correct response to this) 
            }
            var ulongBytes = new byte[8];
            var destIndex = 8 - bitIndex;
            ulongBytes[destIndex - 1] = leftover;
            if (bitIndex > 0) data.Read(ulongBytes, destIndex, bitIndex);
            var ret = BigEndian.ToUInt64(ulongBytes);
            return ret;
        }
        public static ulong ReadEBMLVINT(this Stream data, out bool vintDataAllOnes)
        {
            var firstByte = data.ReadByteOrThrow();
            var bitIndex = GetFirstSetBitIndex(firstByte, out var leftover);
            if (bitIndex < 0)
            {
                vintDataAllOnes = false;
                // throw?
                return 0; // marker bit must be in first byte (verify correct response to this) 
            }
            var ulongBytes = new byte[8];
            var destIndex = 8 - bitIndex;
            ulongBytes[destIndex - 1] = leftover;
            if (bitIndex > 0) data.Read(ulongBytes, destIndex, bitIndex);
            var ret = BigEndian.ToUInt64(ulongBytes);
            vintDataAllOnes = EBMLConverter.IsUnknownSizeVINT(ret, bitIndex + 1);
            return ret;
        }
        public static ulong ReadEBMLUInt(this Stream stream, int size)
        {
            var bytes = new byte[8];
            var destIndex = 8 - size;
            if (size > 0)
            {
                var cnt = stream.Read(bytes, destIndex, size);
                if (cnt != size) throw new Exception("Not enough data");
            }
            return BigEndian.ToUInt64(bytes);
        }
        public static long ReadEBMLInt(this Stream stream, int size)
        {
            var bytes = new byte[8];
            var destIndex = 8 - size;
            if (size > 0)
            {
                var cnt = stream.Read(bytes, destIndex, size);
                if (cnt != size) throw new Exception("Not enough data");
            }
            return BigEndian.ToInt64(bytes);
        }
        public static double ReadEBMLFloat(this Stream stream, int size)
        {
            if (size == 4)
            {
                return BigEndian.ToSingle(stream.ReadBytes(size, true));
            }
            else if (size == 8)
            {
                return BigEndian.ToDouble(stream.ReadBytes(size, true));
            }
            return 0;
        }
        public static string ReadEBMLString(this Stream stream, int size, Encoding? encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            return encoding.GetString(stream.ReadBytes(size, true)).TrimEnd('\0');
        }
        private static readonly double TimeScale = 1000000;
        public static readonly DateTime DateTimeReferencePoint = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static DateTime ReadEBMLDate(this Stream stream, int size)
        {
            if (size == 0) return DateTimeReferencePoint;
            var timeOffset = stream.ReadEBMLInt(size);
            return DateTimeReferencePoint + TimeSpan.FromMilliseconds(timeOffset / TimeScale);
        }
        #endregion
    }
}
