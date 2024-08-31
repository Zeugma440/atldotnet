﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Commons;

namespace ATL.AudioData
{
    internal class EBMLReader
    {
        public enum SeekResult
        {
            NOT_FOUND = 0,
            FOUND_MATCH = 1,
            FOUND_NO_MATCH = 2
        }

        private readonly byte[] buffer = new byte[8];

        internal EBMLReader(Stream s) { this.BaseStream = s; }

        public long Position => BaseStream.Position;

        public Stream BaseStream { get; }


        internal long readVint(bool raw = false)
        {
            BaseStream.Read(buffer, 0, 1);
            int nbBytes = 0;
            for (int i = 0; i < EBMLHelper.SizeMasks.Length; i++)
            {
                if ((buffer[0] & EBMLHelper.SizeMasks[i]) > 0)
                {
                    nbBytes = i + 1;
                    break;
                }
            }

            // Neutralize Vint marker
            if (!raw) buffer[0] = (byte)(buffer[0] & EBMLHelper.DataMasks[nbBytes - 1]);

            // Get extra bytes if needed
            if (nbBytes > 1) BaseStream.Read(buffer, 1, nbBytes - 1);

            // Unknown size (vint data are all 1's)
            if ((byte)(buffer[0] & EBMLHelper.DataMasks[nbBytes - 1]) == EBMLHelper.DataMasks[nbBytes - 1])
            {
                if (1 == nbBytes) return -1;
                bool allOnes = true;
                for (int i = 1; i < nbBytes; i++)
                {
                    if ((buffer[i] & 0xFF) != 0xFF)
                    {
                        allOnes = false;
                        break;
                    }
                }
                if (allOnes) return -1;
            }

            // Decode buffer
            switch (nbBytes)
            {
                case 2:
                    return StreamUtils.DecodeBEUInt16(buffer);
                case 3:
                    return StreamUtils.DecodeBEUInt24(buffer);
                case 4:
                    return StreamUtils.DecodeBEUInt32(buffer);
                case 5:
                    return (long)StreamUtils.DecodeBEUInt40(buffer);
                // TODO 48 and 56 bits longs
                case 8:
                    return (long)StreamUtils.DecodeBEUInt64(buffer);
                default: return buffer[0];
            }
        }

        public long seek(long size, SeekOrigin origin = SeekOrigin.Begin)
        {
            return BaseStream.Seek(size, origin);
        }

        public bool enterContainer(long id)
        {
            return id == readVint(true);
        }

        // Seek the given element at the current container level
        // Given stream must be positioned before the container's size descriptor
        // Returns true with the given stream positioned before the size descriptor; false if not found
        public List<long> seekElements(long id)
        {
            List<long> results = new List<long>();
            var seekTo = Math.Min(BaseStream.Position + readVint(), BaseStream.Length);
            while (BaseStream.Position < seekTo)
            {
                var eltId = readVint(true);
                if (eltId == id) results.Add(BaseStream.Position);

                var size = readVint();
                BaseStream.Seek(size, SeekOrigin.Current);
            }
            return results;
        }

        // Seek the given element at the current container level
        // Given stream must be positioned before the container's size descriptor
        // Returns true with the given stream positioned before the size descriptor; false if not found
        public bool seekElement(long id)
        {
            return SeekResult.FOUND_MATCH == seekElement(id, null);
        }

        public SeekResult seekElement(long id, ISet<Tuple<long, int>> criteria)
        {
            SeekResult result = SeekResult.NOT_FOUND;
            long resultOffset = -1;

            var seekTo = Math.Min(BaseStream.Position + readVint(), BaseStream.Length);
            while (result != SeekResult.FOUND_MATCH && BaseStream.Position < seekTo)
            {
                var eltId = readVint(true);
                if (eltId == id)
                {
                    // Simple search by ID
                    if (null == criteria || 0 == criteria.Count) result = SeekResult.FOUND_MATCH;
                    else
                    {
                        // Is there a criteria matching current element ?
                        var crit = criteria.FirstOrDefault(c => c.Item1 == id);
                        if (crit != null)
                        {
                            if (crit.Item2 > -1)
                            {
                                result = (ulong)crit.Item2 == readUint() ? SeekResult.FOUND_MATCH : SeekResult.FOUND_NO_MATCH;
                            }
                            else
                            {
                                result = SeekResult.FOUND_MATCH;
                            }
                            resultOffset = BaseStream.Position;
                        }
                        else
                        {
                            // Criteria are for children elements
                            long loopOffset = BaseStream.Position;
                            int nbFound = 0;
                            foreach (var c in criteria)
                            {
                                var crits = new HashSet<Tuple<long, int>> { new Tuple<long, int>(c.Item1, c.Item2) };
                                var res = seekElement(c.Item1, crits);
                                if (res != SeekResult.FOUND_NO_MATCH) nbFound++;
                                BaseStream.Position = loopOffset;
                            }
                            if (nbFound == criteria.Count) result = SeekResult.FOUND_MATCH;
                        }
                    }
                }
                else
                {
                    var size = readVint();
                    BaseStream.Seek(size, SeekOrigin.Current);
                }
            }
            if (resultOffset > -1) BaseStream.Seek(resultOffset, SeekOrigin.Begin);
            return result;
        }

        // Given stream must be positioned before the container's size descriptor
        public ulong readUint()
        {
            var nbBytes = readVint();
            if (0 == nbBytes) return 0;

            BaseStream.Read(buffer, 0, (int)nbBytes);
            // Decode buffer
            switch (nbBytes)
            {
                case 2:
                    return StreamUtils.DecodeBEUInt16(buffer);
                case 3:
                    return StreamUtils.DecodeBEUInt24(buffer);
                case 4:
                    return StreamUtils.DecodeBEUInt32(buffer);
                case 5:
                    return StreamUtils.DecodeBEUInt40(buffer);
                // TODO 48 and 56 bits longs
                case 8:
                    return StreamUtils.DecodeBEUInt64(buffer);
                default: return buffer[0];
            }
        }

        public double readFloat()
        {
            var nbBytes = readVint();
            if (0 == nbBytes) return 0;

            BaseStream.Read(buffer, 0, (int)nbBytes);
            // Decode buffer
            switch (nbBytes)
            {
                case 4:
                    return ToSingle(buffer);
                case 8:
                    return ToDouble(buffer);
                default: return buffer[0];
            }
        }

        private static float ToSingle(byte[] bytes, int startIndex = 0)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToSingle(bytes, startIndex);
        }
        private static double ToDouble(byte[] bytes, int startIndex = 0)
        {
            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
            return BitConverter.ToDouble(bytes, startIndex);
        }

        // Given stream must be positioned before the container's size descriptor
        public string readString()
        {
            var nbBytes = readVint();
            if (0 == nbBytes) return "";

            byte[] strBuf = new byte[nbBytes];
            BaseStream.Read(strBuf);
            return Utils.Latin1Encoding.GetString(strBuf);
        }

        // Given stream must be positioned before the container's size descriptor
        public string readUtf8String()
        {
            var nbBytes = readVint();
            if (0 == nbBytes) return "";

            byte[] strBuf = new byte[nbBytes];
            BaseStream.Read(strBuf);
            return Encoding.UTF8.GetString(strBuf);
        }

        // TODO gain memory by providing a "clamped" Stream using s instead of copying everything to a byte[]
        public byte[] readBinary()
        {
            var nbBytes = readVint();
            if (0 == nbBytes) return Array.Empty<byte>();

            byte[] result = new byte[nbBytes];
            BaseStream.Read(result, 0, (int)nbBytes);
            return result;
        }

        public byte[] readBytes(int nb)
        {
            byte[] result = new byte[nb];
            BaseStream.Read(result, 0, nb);
            return result;
        }
    }
}
