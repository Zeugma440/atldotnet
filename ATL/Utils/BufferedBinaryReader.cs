using System;
using System.IO;
using System.Runtime.InteropServices;

// Initial inspiration go to Jackson Dunstan for his article (http://jacksondunstan.com/articles/3568)

namespace ATL
{
    /// <summary>
    /// Reads data from the given Stream using a forward buffer in order to reduce disk stress and have better control of when data is actually read from the disk.
    /// 
    /// NB1 : Using BufferedBinaryReader instead of the classic BinaryReader create a 10% speed gain on the dev environment (MS .NET under Windows)
    /// 
    /// NB2 : The interface of this class is designed to be called exactly like a BinaryReader in order to facilitate swapping in classes that use BinaryReader
    /// However, is does _not_ give access to BaseStream, in order to keep control on buffer and cursor positions.
    /// 
    /// NB3 : This class implements Stream in order to be reusable in methods that take Stream as an input
    /// </summary>
    public class BufferedBinaryReader : Stream, IDisposable
    {
        private const int DEFAULT_BUFFER_SIZE = 512;

        private readonly Stream stream;
        private readonly int bufferDefaultSize;
        private readonly long streamSize;

        /*
         *
         * stream ------------[================>=======================]--------------------------------------*EOS
         *                    ^                ^                       ^
         *                    bufferOffset     cursorPosition          streamPosition
         *                    (absolute)       (relative to buffer)    (absolute)
         */
        private byte[] buffer;
        private long bufferOffset;
        private int cursorPosition; // NB : cursorPosition can be > bufferSize in certain cases when BufferedBinaryReader has to read a chunk of data larger than bufferSize
        private long streamPosition;
        private int bufferSize; // NB : bufferSize can be < DEFAULT_BUFFER_SIZE when bufferOffset nears the end of the stream (not enough remaining bytes to fill the whole buffer space)

        public override long Position
        {
            get { return bufferOffset + cursorPosition; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        public override long Length
        {
            get { return streamSize; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public BufferedBinaryReader(Stream stream)
        {
            this.stream = stream;
            bufferDefaultSize = DEFAULT_BUFFER_SIZE;
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

        public override long Seek(long offset, SeekOrigin origin)
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

            if (0 == delta) return Position;
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
            return Position;
        }

        public override int Read([In, Out] byte[] buffer, int offset, int count)
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

        public new byte ReadByte()
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

        public new void Dispose()
        {
            Flush();
            stream.Close();
        }

        public override void Flush()
        {
            buffer = null;
        }

        // This class is a reader helper only
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        // This class is a reader helper only
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
