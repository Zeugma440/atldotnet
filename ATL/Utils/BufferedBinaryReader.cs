using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Initial credits go to Jackson Dunstan for his article (http://jacksondunstan.com/articles/3568)
namespace ATL
{
    public class BufferedBinaryReader : IDisposable
    {
        private const int BUFFER_SIZE = 512;

        private readonly Stream stream;
        private readonly byte[] buffer;
        private readonly int bufferDefaultSize;
        private readonly long streamSize;

        private int bufferOffset;
        private int numBufferedBytes;
        private long streamOffset = 0;
        private int bufferSize;

        public long Position
        {
            get { return streamOffset-(bufferSize-bufferOffset); }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public long Length
        {
            get { return streamSize; }
        }

        public BufferedBinaryReader(Stream stream)
        {
            this.stream = stream;
            bufferDefaultSize = BUFFER_SIZE;
            buffer = new byte[bufferDefaultSize];
            streamSize = stream.Length;
            streamOffset = stream.Position;
        }

        public BufferedBinaryReader(Stream stream, int bufferSize)
        {
            this.stream = stream;
            bufferDefaultSize = bufferSize;
            buffer = new byte[bufferSize];
            streamSize = stream.Length;
            streamOffset = stream.Position;
        }

        // NB : cannot handle when previousBytesToKeep > bufferSize
        private bool fillBuffer(int previousBytesToKeep = 0)
        {
            if (previousBytesToKeep > 0) Array.Copy(buffer, bufferOffset, buffer, 0, previousBytesToKeep);
            int bytesToRead = (int)Math.Min(bufferDefaultSize-previousBytesToKeep, streamSize - streamOffset - previousBytesToKeep);

            if (bytesToRead > 0)
            {
                stream.Read(buffer, (int)previousBytesToKeep, bytesToRead);
                numBufferedBytes += bytesToRead;
                streamOffset += bytesToRead;
                bufferSize = bytesToRead;
                bufferOffset = 0;
                return true;
            }

            return false;
        }

        private void prepareBuffer(int bytesToRead, bool skip = false)
        {
            if (bufferSize - bufferOffset < bytesToRead)
            {
                if (skip) fillBuffer();
                else fillBuffer(bufferSize - bufferOffset);
            }
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            long delta = 0;

            if (origin.Equals(SeekOrigin.Current)) delta = offset;
            else if (origin.Equals(SeekOrigin.Begin))
            {
                delta = offset - streamOffset;
            }
            else if (origin.Equals(SeekOrigin.End))
            {
                delta = streamSize + offset - streamOffset;
            }

            // TODO - optimize by detected "small" jumps -> only move within the limits of current buffer
            if (0 == delta) return;
            else if (delta < 0)
            {
                streamOffset += delta;
                stream.Position = streamOffset;
                fillBuffer();
            } else if (delta < bufferDefaultSize) // Optimize this
            {
                Skip((int)delta);
            } else
            {
                streamOffset += delta;
                stream.Position = streamOffset;
                fillBuffer();
            }
        }

        public void Skip(int nbBytes)
        {
            prepareBuffer(nbBytes, true);
            bufferOffset += nbBytes;
        }

        public int Read([In, Out] byte[] buffer, int offset, int count)
        {
            if (count <= bufferSize - bufferOffset)
            {
                prepareBuffer(count);
                Array.Copy(this.buffer, bufferOffset, buffer, offset, count);
                bufferOffset += count;
                return count;
            }
            else
            {
                // First retrieve buffered data
                int availableBytes = bufferSize - bufferOffset;
                Array.Copy(this.buffer, bufferOffset, buffer, 0, availableBytes);

                // Then retrieve the rest by reading the stream
                stream.Read(buffer, availableBytes, count - availableBytes);

                bufferOffset = bufferSize; // Force buffer refill on next read
                streamOffset += count - availableBytes;
                return count;
            }
        }

        public byte[] ReadBytes(int nbBytes)
        {
            if (nbBytes <= bufferSize - bufferOffset)
            {
                prepareBuffer(nbBytes);
                byte[] val = new byte[nbBytes];
                Array.Copy(buffer, bufferOffset, val, 0, nbBytes);
                bufferOffset += nbBytes;
                return val;
            } else
            {
                byte[] result = new byte[nbBytes];

                // First retrieve buffered data
                int availableBytes = bufferSize - bufferOffset;
                Array.Copy(buffer, bufferOffset, result, 0, availableBytes);

                // Then retrieve the rest by reading the stream
                stream.Read(result, availableBytes, nbBytes - availableBytes);

                bufferOffset = bufferSize; // Force buffer refill on next read
                streamOffset += nbBytes - availableBytes;
                return result;
            }
        }

        public byte ReadByte()
        {
            prepareBuffer(1);
            byte val = buffer[bufferOffset];
            bufferOffset++;
            return val;
        }

        public ushort ReadUInt16()
        {
            prepareBuffer(2);
            ushort val = (ushort)(buffer[bufferOffset] | buffer[bufferOffset + 1] << 8);
            bufferOffset += 2;
            return val;
        }

        public int ReadInt32()
        {
            prepareBuffer(4);
            int val = (buffer[bufferOffset] | buffer[bufferOffset + 1] << 8 | buffer[bufferOffset + 2] << 16 | buffer[bufferOffset + 3] << 24);
            bufferOffset += 4;
            return val;
        }

        public void Dispose()
        {
            stream.Close();
        }
    }
}
