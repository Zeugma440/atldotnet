using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Initial credits go to Jackson Dunstan for his article (http://jacksondunstan.com/articles/3568)

/*
 *
 * stream ------------[================>=======================]--------------------------------------*EOS
 *                    ^                ^                       ^
 *                    bufferOffset     cursorPosition          streamPosition
 *                    (absolute)       (relative to buffer)    (absolute)
 */

// TODO - integrate StreamUtils methods to work with embedded buffer
namespace ATL
{
    public class BufferedBinaryReader : IDisposable
    {
        private const int BUFFER_SIZE = 512;

        private readonly Stream stream;
        private readonly byte[] buffer;
        private readonly int bufferDefaultSize;
        private readonly long streamSize;

        private long bufferOffset;
        private int cursorPosition;
        private long streamPosition;
        private int bufferSize;

        public long Position
        {
            get { return bufferOffset + cursorPosition; }
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
            streamPosition = stream.Position;
            bufferOffset = streamPosition;
        }

        public BufferedBinaryReader(Stream stream, int bufferSize)
        {
            this.stream = stream;
            bufferDefaultSize = bufferSize;
            buffer = new byte[bufferSize];
            streamSize = stream.Length;
            streamPosition = stream.Position;
            bufferOffset = streamPosition;
        }

        // NB : cannot handle when previousBytesToKeep > bufferSize
        private bool fillBuffer(int previousBytesToKeep = 0)
        {
            if (previousBytesToKeep > 0) Array.Copy(buffer, cursorPosition, buffer, 0, previousBytesToKeep);
            int bytesToRead = (int)Math.Min(bufferDefaultSize-previousBytesToKeep, streamSize - streamPosition - previousBytesToKeep);

            if (bytesToRead > 0)
            {
                bufferOffset = streamPosition;
                stream.Read(buffer, previousBytesToKeep, bytesToRead);
                streamPosition += bytesToRead;
                bufferSize = bytesToRead;
                cursorPosition = 0;
                return true;
            }

            return false;
        }

        private void prepareBuffer(int bytesToRead)
        {
            if (bufferSize - cursorPosition < bytesToRead)
            {
                fillBuffer(Math.Max(0, bufferSize - cursorPosition));
            }
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            long delta = 0; // Distance between absolute cursor position and specified position in bytes

            if (origin.Equals(SeekOrigin.Current)) delta = offset;
            else if (origin.Equals(SeekOrigin.Begin))
            {
                delta = offset - Position;
            }
            else if (origin.Equals(SeekOrigin.End))
            {
                delta = (streamSize + offset) - Position;
            }

            if (0 == delta) return;
            else if (delta < 0)
            {
                // If cursor is still within buffer, jump within buffer
                if ((cursorPosition + delta < bufferSize) && (cursorPosition + delta >= 0))
                {
                    cursorPosition += (int)delta;
                } else // Jump outside buffer : move the whole buffer at the beginning of the zone to read
                {
                    streamPosition = bufferOffset + cursorPosition + delta;
                    stream.Position = streamPosition;
                    fillBuffer();
                }
            } else if (cursorPosition + delta < bufferSize) // Jump within buffer
            {
                cursorPosition += (int)delta;
            } else // Jump outside buffer: move the whole buffer at the beginning of the zone to read
            {
                streamPosition = bufferOffset + cursorPosition + delta;
                stream.Position = streamPosition;
                fillBuffer();
            }
        }

        public int Read([In, Out] byte[] buffer, int offset, int count)
        {
            // Bytes to read are all already buffered
            if (count <= bufferSize - cursorPosition)
            {
                prepareBuffer(count);
                Array.Copy(this.buffer, cursorPosition, buffer, offset, count);
                cursorPosition += count;
                return count;
            }
            else
            {
                // First retrieve buffered data if possible
                int availableBytes = bufferSize - cursorPosition;
                if (availableBytes > 0)
                {
                    Array.Copy(this.buffer, cursorPosition, buffer, offset, availableBytes);
                } else
                {
                    availableBytes = 0;
                }

                // Then retrieve the rest by reading the stream
                stream.Read(buffer, offset + availableBytes, count - availableBytes);

                streamPosition += count - availableBytes;
                stream.Position = streamPosition;

                cursorPosition += count; // Virtual position outside buffer zone

                return count;
            }
        }

        public byte[] ReadBytes(int nbBytes)
        {
            byte[] buffer = new byte[nbBytes];

            Read(buffer, 0, nbBytes);

            return buffer;
        }

        public byte ReadByte()
        {
            prepareBuffer(1);
            byte val = buffer[cursorPosition];
            cursorPosition++;
            return val;
        }

        public ushort ReadUInt16()
        {
            prepareBuffer(2);
            ushort val = (ushort)(buffer[cursorPosition] | buffer[cursorPosition + 1] << 8);
            cursorPosition += 2;
            return val;
        }

        public short ReadInt16()
        {
            prepareBuffer(2);
            short val = (short)(buffer[cursorPosition] | buffer[cursorPosition + 1] << 8);
            cursorPosition += 2;
            return val;
        }

        public uint ReadUInt32()
        {
            prepareBuffer(4);
            uint val = (uint)(buffer[cursorPosition] | buffer[cursorPosition + 1] << 8 | buffer[cursorPosition + 2] << 16 | buffer[cursorPosition + 3] << 24);
            cursorPosition += 4;
            return val;
        }

        public int ReadInt32()
        {
            prepareBuffer(4);
            int val = (buffer[cursorPosition] | buffer[cursorPosition + 1] << 8 | buffer[cursorPosition + 2] << 16 | buffer[cursorPosition + 3] << 24);
            cursorPosition += 4;
            return val;
        }

        public ulong ReadUInt64()
        {
            prepareBuffer(8);
            ulong val = (ulong)(buffer[cursorPosition] | buffer[cursorPosition + 1] << 8 | buffer[cursorPosition + 2] << 16 | buffer[cursorPosition + 3] << 24 | buffer[cursorPosition + 4] << 32 | buffer[cursorPosition + 5] << 40 | buffer[cursorPosition + 6] << 48 | buffer[cursorPosition + 7] << 56);
            cursorPosition += 8;
            return val;
        }

        public long ReadInt64()
        {
            prepareBuffer(8);
            long val = (long)(buffer[cursorPosition] | buffer[cursorPosition + 1] << 8 | buffer[cursorPosition + 2] << 16 | buffer[cursorPosition + 3] << 24 | buffer[cursorPosition + 4] << 32 | buffer[cursorPosition + 5] << 40 | buffer[cursorPosition + 6] << 48 | buffer[cursorPosition + 7] << 56);
            cursorPosition += 8;
            return val;
        }

        public void Dispose()
        {
            stream.Close();
        }
    }
}
