using System;
using System.IO;

namespace ATL.AudioData
{
    internal static class EBMLHelper
    {
        internal static readonly byte[] SizeMasks = { 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02, 0x01 };
        internal static readonly byte[] DataMasks = new byte[8];
        internal static readonly ulong[] MaxVintValues = new ulong[8];
        internal static readonly ulong[] MaxIntValues = new ulong[8];

        static EBMLHelper()
        {
            byte sizeMask = 0x00;
            for (int i = 0; i < DataMasks.Length; i++)
            {
                sizeMask |= SizeMasks[i];
                DataMasks[i] = (byte)(0xFF ^ sizeMask);
            }

            for (int i = 0; i < MaxVintValues.Length; i++)
            {
                MaxVintValues[i] = (ulong)Math.Pow(2, 7 * (i + 1)) - 1;
            }

            for (int i = 0; i < MaxIntValues.Length; i++)
            {
                MaxIntValues[i] = (ulong)Math.Pow(2, 8 * (i + 1)) - 1;
            }
        }

        private static byte[] encodeIntOptimized(ulong value, int nbBytes)
        {
            return nbBytes switch
            {
                < 2 => new[] { (byte)(value & 0x00000000000000FF) },
                < 3 => StreamUtils.EncodeBEUInt16((ushort)value),
                3 => StreamUtils.EncodeBEUInt24((uint)value),
                4 => StreamUtils.EncodeBEUInt32((uint)value),
                5 => StreamUtils.EncodeBEUInt40(value),
                _ => StreamUtils.EncodeBEUInt64(value)
            };
        }

        private static int getNbBytesForInt(ulong value)
        {
            for (int i = 0; i < MaxIntValues.Length; i++)
            {
                if (value >= MaxIntValues[i]) continue;
                return i + 1;
            }
            return 8;
        }

        internal static byte[] EncodeVint(ulong value, bool optimize = true)
        {
            // Determine number of bytes to use
            int nbBytes = 8;
            if (optimize)
            {
                for (int i = 0; i < MaxVintValues.Length; i++)
                {
                    if (value >= MaxVintValues[i]) continue;
                    nbBytes = i + 1;
                    break;
                }
            }

            // Add EBML width and marker
            ulong finalValue = value | ((ulong)SizeMasks[nbBytes - 1] << (8 * (nbBytes - 1)));

            // Build data
            return encodeIntOptimized(finalValue, nbBytes);
        }

        internal static void WriteElt(Stream s, ulong id, Stream data)
        {
            s.Write(encodeIntOptimized(id, getNbBytesForInt(id)));
            s.Write(EncodeVint((ulong)data.Length));
            StreamUtils.CopyStream(data, s);
        }

        internal static void WriteElt(Stream s, ulong id, byte[] data)
        {
            s.Write(encodeIntOptimized(id, getNbBytesForInt(id)));
            s.Write(EncodeVint((ulong)data.Length));
            s.Write(data, 0, data.Length);
        }

        internal static void WriteElt(Stream s, ulong id, int data)
        {
            WriteElt(s, id, encodeIntOptimized((ulong)data, getNbBytesForInt((ulong)data)));
        }

        internal static void WriteElt(Stream s, ulong id, ulong data)
        {
            WriteElt(s, id, encodeIntOptimized(data, getNbBytesForInt(data)));
        }

        internal static void WriteElt32(Stream s, ulong id, ulong data)
        {
            WriteElt(s, id, encodeIntOptimized(data, 4));
        }
    }
}
