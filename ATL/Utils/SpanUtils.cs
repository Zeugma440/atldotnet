using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATL
{
    /// <summary>
    /// Misc. utilities for Spans
    /// </summary>
    internal static partial class SpanUtils
    {
        /// <summary>
        /// Decode an unsigned Big-Endian 24-bit integer from the given array of bytes, starting from the given offset
        /// </summary>
        /// <param name="data">Array of bytes to read value from</param>
        /// <param name="offset">Offset to read value from (default : 0)</param>
        /// <returns>Decoded value</returns>
        public static uint DecodeBEUInt24(ReadOnlySpan<byte> data, int offset = 0)
        {
            if (data.Length - offset < 3) throw new InvalidDataException($"Value should at least contain 3 bytes after offset; actual size={data.Length - offset} bytes");
            return (uint)(data[offset] << 16 | data[offset + 1] << 8 | data[offset + 2]);
        }

        /// <summary>
        /// Decode a signed 32-bit integer from a 4-byte array using the "synch-safe" convention
        /// as to ID3v2 definition (§6.2)
        /// NB : The actual capacity of the integer thus reaches 28 bits
        /// </summary>
        /// <param name="data">4-byte array containing to convert</param>
        /// <returns>Decoded Int32</returns>
        public static int DecodeSynchSafeInt32(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4) throw new ArgumentException($"Array length has to be at least 4 bytes; found : {data.Length} bytes");

            return
                data[0] * 0x200000 +   //2^21
                data[1] * 0x4000 +     //2^14
                data[2] * 0x80 +       //2^7
                data[3];
        }

        /// <summary>
        /// Extracts an integer from the given byte Span, using the given bits position and count, with little-endian convention
        /// Credits go to https://stackoverflow.com/questions/63788666/c-sharp-fastest-method-get-integer-from-part-of-bits
        /// </summary>
        /// <param name="source">Span to read data from</param>
        /// <param name="bitPosition">Position of the first _bit_ to read (scale is x8 compared to classic byte positioning)</param>
        /// <param name="bitCount">Number of bits to read</param>
        public static int ReadBits(ReadOnlySpan<byte> source, int bitPosition, int bitCount)
        {
            if (bitPosition + bitCount > source.Length * 8) throw new NotSupportedException("Incompatible parameters");
            if (bitPosition + bitCount > 32) throw new NotSupportedException("Only supports 32-bit input or less");

            Span<byte> buffer = stackalloc byte[4];
            source[..Math.Min(4, source.Length)].CopyTo(buffer);
            int full = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            int mask = (bitCount == 32) ? int.MaxValue : (1 << bitCount) - 1;
            return (full >> bitPosition) & mask;
        }

        /// <summary>
        /// Extracts an integer from the given byte Span, using the given bits position and count, with big-endian convention
        /// </summary>
        /// <param name="source">Span to read data from</param>
        /// <param name="bitPosition">Position of the first _bit_ to read (scale is x8 compared to classic byte positioning)</param>
        /// <param name="bitCount">Number of bits to read</param>
        public static uint ReadBitsBE(ReadOnlySpan<byte> source, int bitPosition, int bitCount)
        {
            if (bitPosition + bitCount > source.Length * 8) throw new NotSupportedException("Incompatible parameters");
            if (bitCount is < 1 or > 32) throw new NotSupportedException("Only supports 32-bit input or less");

            int byteIndex = bitPosition >> 3;      // startBit / 8
            int bitOffset = bitPosition & 7;       // startBit % 8
            int bytesNeeded = (bitOffset + bitCount + 7) >> 3;

            ulong raw;
            if (byteIndex + 8 <= source.Length)
            {
                // Fast path: 1 unaligned 64-bit load + hardware BSWAP
                raw = BinaryPrimitives.ReadUInt64BigEndian(source.Slice(byteIndex));
            }
            else
            {
                // Safe fallback near the end of the buffer (reads 1 to 5 bytes)
                raw = 0;
                for (int i = 0; i < bytesNeeded; i++)
                {
                    raw |= (ulong)source[byteIndex + i] << (56 - (i << 3));
                }
            }

            // Shift target bits to the least-significant position and mask
            int rightShift = 64 - bitOffset - bitCount;
            uint mask = bitCount == 32 ? uint.MaxValue : (1u << bitCount) - 1;

            return (uint)((raw >> rightShift) & mask);
        }

        public static void WriteUInt16(Stream s, ushort data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, data);
            s.Write(buffer[..2]);
        }

        public static void WriteInt32(Stream s, int data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buffer, data);
            s.Write(buffer[..4]);
        }

        public static void WriteUInt32(Stream s, uint data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, data);
            s.Write(buffer[..4]);
        }

        public static void WriteBEInt32(Stream s, int data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, data);
            s.Write(buffer[..4]);
        }

        public static void WriteBEUInt32(Stream s, uint data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, data);
            s.Write(buffer[..4]);
        }

        public static void WriteUInt64(Stream s, ulong data, Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, data);
            s.Write(buffer[..8]);
        }

    }
}