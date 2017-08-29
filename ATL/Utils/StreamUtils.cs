using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL
{
	/// <summary>
	/// Misc. utilities used by binary readers
	/// </summary>
	public static class StreamUtils
	{	
		// Size of the buffer used by memory stream copy methods
		private const int BUFFERSIZE = 512;

        /// <summary>
        /// Handler signature to be used when needing to process a MemoryStream
        /// </summary>
        public delegate void StreamHandlerDelegate(ref MemoryStream stream);


		/// <summary>
		/// Determines if the contents of a string (character by character) is the same
		/// as the contents of a char array
		/// </summary>
		/// <param name="a">String to be tested</param>
		/// <param name="b">Char array to be tested</param>
		/// <returns>True if both contain the same character sequence; false if not</returns>
		public static bool StringEqualsArr(String a, char[] b)
		{
            return ArrEqualsArr(a.ToCharArray(), b);
		}


		/// <summary>
		/// Determines if two char arrays have the same contents
		/// </summary>
		/// <param name="a">First array to be tested</param>
		/// <param name="b">Second array to be tested</param>
		/// <returns>True if both arrays have the same contents; false if not</returns>
		public static bool ArrEqualsArr(char[] a, char[] b)
		{			
			if (b.Length != a.Length) return false;
			for (int i=0; i<b.Length; i++)
			{
				if (a[i] != b[i]) return false;
			}
			return true;
		}

        /// <summary>
        /// Determines if two byte arrays have the same contents
        /// </summary>
        /// <param name="a">First array to be tested</param>
        /// <param name="b">Second array to be tested</param>
        /// <returns>True if both arrays have the same contents; false if not</returns>
        public static bool ArrEqualsArr(byte[] a, byte[] b)
        {
            if (b.Length != a.Length) return false;
            for (int i = 0; i < b.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }


        /// <summary>
        /// Reads a given number of one-byte chars from the provided source
        /// (this method is there because the default behaviour of .NET's binary char reading
        /// tries to read unicode stuff, thus reading two bytes in a row from time to time :S)
        /// </summary>
        /// <param name="r">Source to read from</param>
        /// <param name="length">Number of one-byte chars to read</param>
        /// <returns>Array of chars read from the source</returns>
        
        // TODO : limit use in favour of Encoding.ASCII.GetString(r.ReadBytes) or Utils.Latin1Encoding.GetString(r.ReadBytes)
        public static char[] ReadOneByteChars(BinaryReader r, int length)
        {
            return ReadOneByteChars(r.BaseStream, length);
        }
        public static char[] ReadOneByteChars(Stream s, int length)
		{
			byte[] byteArr = new byte[length];
            char[] result = new char[length];

            s.Read(byteArr, 0, length);
			for (int i=0; i<length; i++)
			{
				result[i] = (char)byteArr[i];
			}

			return result;
		}

		/// <summary>
		/// Reads one one-byte char from the provided source
		/// </summary>
		/// <param name="r">Source to read from</param>
		/// <returns>Chars read from the source</returns>
		public static char ReadOneByteChar(BinaryReader r)
		{
			return (char)r.ReadByte();
		}

        /// <summary>
        /// Reads one one-byte char from the provided source
        /// </summary>
        /// <param name="r">Source to read from</param>
        /// <returns>Chars read from the source</returns>
        public static char ReadOneByteChar(Stream r)
        {
            return (char)r.ReadByte();
        }

        /// <summary>
        /// Copies a given number of bytes from a stream to another
        /// </summary>
        /// <param name="mTo">Target stream</param>
        /// <param name="mFrom">Source stream</param>
        /// <param name="length">Number of bytes to be copied</param>
        [Obsolete("use CopyStream")]
        public static void CopyStreamFrom(Stream mTo, Stream mFrom, long length)
		{
            BinaryWriter w = new BinaryWriter(mTo);
            BinaryReader r = new BinaryReader(mFrom);
            CopyStreamFrom(w, r, length);
		}

        /// <summary>
        /// Writes a given number of bytes from a stream to a writer
        /// </summary>
        /// <param name="mTo">Writer to be used</param>
        /// <param name="mFrom">Source stream</param>
        /// <param name="length">Number of bytes to be copied</param>
        [Obsolete("use CopyStream")]
        public static void CopyStreamFrom(BinaryWriter w, Stream mFrom, long length)
		{
            BinaryReader r = new BinaryReader(mFrom);
            CopyStreamFrom(w, r, length);
		}

        /// <summary>
        /// Writes a given number of bytes from a reader to a stream
        /// </summary>
        /// <param name="mTo">Target stream</param>
        /// <param name="r">Reader to be used</param>
        /// <param name="length">Number of bytes to be copied</param>
        [Obsolete("use CopyStream")]
        public static void CopyStreamFrom(Stream mTo, BinaryReader r, long length)
		{
            BinaryWriter w = new BinaryWriter(mTo);
            CopyStreamFrom(w, r, length);
		}

		/// <summary>
		/// Writes a given number of bytes from a reader to a writer
		/// </summary>
		/// <param name="mTo">Writer to be used</param>
		/// <param name="r">Reader to be used</param>
		/// <param name="length">Number of bytes to be copied</param>
        [Obsolete("use CopyStream")]
		public static void CopyStreamFrom(BinaryWriter w, BinaryReader r, long length)
		{			
			long effectiveLength;
			long initialPosition;

			initialPosition = r.BaseStream.Position;
			if (0 == length) effectiveLength = r.BaseStream.Length; else effectiveLength = length;

			while (r.BaseStream.Position < initialPosition+effectiveLength && r.BaseStream.Position < r.BaseStream.Length)
				w.Write(r.ReadBytes(BUFFERSIZE));
		}

        /// <summary>
        /// Copies a given number of bytes from a given stream to another, starting at current stream positions
        /// i.e. first byte will be read at from.Position and written at to.Position
        /// NB : This method cannot be used to move data within one single stream; use CopySameStream instead
        /// </summary>
        /// <param name="from">Stream to start copy from</param>
        /// <param name="to">Stream to copy to</param>
        /// <param name="length">Number of bytes to copy (optional; default = 0 = all bytes until the end of the stream)</param>
        public static void CopyStream(Stream from, Stream to, long length = 0)
        {
            byte[] data = new byte[BUFFERSIZE];
            long bytesToRead;
            int bufSize;
            long i = 0;

            if (0 == length) bytesToRead = from.Length-from.Position; else bytesToRead = Math.Min(from.Length - from.Position,length);

            while (i< bytesToRead)
            {
                bufSize = (int)Math.Min(BUFFERSIZE, bytesToRead - i); // Plain dirty cast is used here for performance's sake
                from.Read(data, 0, bufSize);
                to.Write(data, 0, bufSize);
                i += bufSize;
            }
        }

        public static void CopySameStream(Stream s, long offsetFrom, long offsetTo, int length, int bufferSize = BUFFERSIZE)
        {
            if (offsetFrom == offsetTo) return;

            byte[] data = new byte[bufferSize];
            int bufSize;
            int written = 0;
            bool forward = (offsetTo > offsetFrom);

            while (written < length)
            {
                bufSize = Math.Min(bufferSize, length - written);
                if (forward)
                {
                    s.Seek(offsetFrom + length - written - bufSize, SeekOrigin.Begin);
                    s.Read(data, 0, bufSize);
                    s.Seek(offsetTo + length - written -bufSize, SeekOrigin.Begin);
                } else
                {
                    s.Seek(offsetFrom + written, SeekOrigin.Begin);
                    s.Read(data, 0, bufSize);
                    s.Seek(offsetTo + written, SeekOrigin.Begin);
                }
                s.Write(data, 0, bufSize);
                written += bufSize;
            }
        }

        /// <summary>
        /// Read a given number of bytes from a given stream, starting at current stream position
        /// i.e. first byte will be read at from.Position, EXCEPT if the stream has already reached its end, in which case reading restarts at its beginning
        /// </summary>
        /// <param name="from">Stream to read data from</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>Bytes read from the stream</returns>
        public static byte[] ReadBinaryStream(Stream from, long length = 0)
        {
            byte[] buffer = new byte[BUFFERSIZE];
            long bytesToRead;
            int bufSize;
            long i = 0;

            if (from.Position == from.Length) from.Seek(0, SeekOrigin.Begin);

            if (0 == length) bytesToRead = from.Length - from.Position; else bytesToRead = Math.Min(from.Length - from.Position, length);
            byte[] result = new byte[bytesToRead];

            while (i < bytesToRead)
            {
                bufSize = (int)Math.Min(BUFFERSIZE, bytesToRead - i); // Plain dirty cast is used here for performance's sake
                from.Read(buffer, 0, bufSize);
                Array.Copy(buffer, 0, result, i, bufSize);
                i += bufSize;
            }

            return result;
        }

        /// <summary>
        /// Remove a portion of bytes within the given stream
        /// </summary>
        /// <param name="s">Stream to process; must be accessible for reading and writing</param>
        /// <param name="endOffset">End offset of the portion of bytes to remove</param>
        /// <param name="delta">Number of bytes to remove</param>
        public static void ShortenStream(Stream s, long endOffset, uint delta) 
        {
            int bufSize;
            byte[] data = new byte[BUFFERSIZE];
            long newIndex = endOffset - delta;
            long initialLength = s.Length;
            long i = 0;

            while (i < initialLength - endOffset) // Forward loop
            {
                bufSize = (int)Math.Min(BUFFERSIZE, initialLength - endOffset - i);
                s.Seek(endOffset + i, SeekOrigin.Begin);
                s.Read(data, 0, bufSize);
                s.Seek(newIndex + i, SeekOrigin.Begin);
                s.Write(data, 0, bufSize);
                i += bufSize;
            }

            s.SetLength(initialLength - delta);
        }

        /// <summary>
        /// Add bytes within the given stream
        /// </summary>
        /// <param name="s">Stream to process; must be accessible for reading and writing</param>
        /// <param name="oldIndex">Offset where to add new bytes</param>
        /// <param name="delta">Number of bytes to add</param>
        /// <param name="fillZeroes">If true, new bytes will all be zeroes (optional; default = false)</param>
        public static void LengthenStream(Stream s, long oldIndex, uint delta, bool fillZeroes = false)
        {
            byte[] data = new byte[BUFFERSIZE];
            long newIndex = oldIndex + delta;
            long oldLength = s.Length;
            long newLength = s.Length + delta;
            s.SetLength(newLength);

            long i = 0;
            int bufSize;

            while (newLength - i > newIndex) // Backward loop
            {
                bufSize = (int)Math.Min(BUFFERSIZE, newLength - newIndex - i);
                s.Seek(-i - bufSize - delta, SeekOrigin.End); // Seeking is done from the "modified" end (new length) => substract delta
                s.Read(data, 0, bufSize);
                s.Seek(-i - bufSize, SeekOrigin.End);
                s.Write(data, 0, bufSize);
                i += bufSize;
            }

            if (fillZeroes)
            {
                // Fill the location of old copied data with zeroes
                s.Seek(oldIndex, SeekOrigin.Begin);
                for (i = oldIndex; i < newIndex; i++) s.WriteByte(0);
            }
        }

        /// <summary>
        /// Switches the format of an Int64 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static ulong ReverseUInt64(ulong n)
        {
            byte b0;
            byte b1;
            byte b2;
            byte b3;
            byte b4;
            byte b5;
            byte b6;
            byte b7;

            b0 = (byte) ((n & 0x00000000000000FF) >> 0);
            b1 = (byte) ((n & 0x000000000000FF00) >> 8);
            b2 = (byte) ((n & 0x0000000000FF0000) >> 16);
            b3 = (byte) ((n & 0x00000000FF000000) >> 24);
            b4 = (byte) ((n & 0x000000FF00000000) >> 32);
            b5 = (byte) ((n & 0x0000FF0000000000) >> 40);
            b6 = (byte) ((n & 0x00FF000000000000) >> 48);
            b7 = (byte) ((n & 0xFF00000000000000) >> 56);

            return (ulong)((b0 << 56) | (b1 << 48) | (b2 << 40) | (b3 << 32) | (b4 << 24) | (b5 << 16) | (b6 << 8) | (b7 << 0));
        }

        public static long ReverseInt64(long n)
        {
            /*
            byte b0;
            byte b1;
            byte b2;
            byte b3;
            byte b4;
            byte b5;
            byte b6;
            byte b7;

            b0 = (byte)((n & 0x00000000000000FF) >> 0);
            b1 = (byte)((n & 0x000000000000FF00) >> 8);
            b2 = (byte)((n & 0x0000000000FF0000) >> 16);
            b3 = (byte)((n & 0x00000000FF000000) >> 24);
            b4 = (byte)((n & 0x000000FF00000000) >> 32);
            b5 = (byte)((n & 0x0000FF0000000000) >> 40);
            b6 = (byte)((n & 0x00FF000000000000) >> 48);
            b7 = (byte)((n & 0xFF00000000000000) >> 56); // <-- type incompatibility issue there

            return (b0 << 56) | (b1 << 48) | (b2 << 40) | (b3 << 32) | (b4 << 24) | (b5 << 16) | (b6 << 8) | (b7 << 0);
            */

            // Above code does not work due to 0xFF00000000000000 not being an unsigned long
            // Below code does work but is 3 times slower
            byte[] binary = BitConverter.GetBytes(n);
            Array.Reverse(binary);
            return BitConverter.ToInt64(binary, 0);
        }

        public static uint DecodeBEUInt32(byte[] data)
        {
            if (data.Length != 4) throw new InvalidDataException("data should be 4 bytes long; found" + data.Length + " bytes");
            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | (data[3] << 0));
        }

        public static int DecodeBEInt32(byte[] data)
        {
            if (data.Length != 4) throw new InvalidDataException("data should be 4 bytes long; found" + data.Length + " bytes");
            return (data[0] << 24) | (data[1] << 16) | (data[2] << 8) | (data[3] << 0);
        }

        public static uint DecodeBEUInt24(byte[] data)
        {
            if (data.Length != 3) throw new InvalidDataException("data should be 3 bytes long; found" + data.Length + " bytes");
            return (uint)((data[0] << 16) | (data[1] << 8) | (data[2] << 0));
        }

        public static int DecodeBEInt24(byte[] data)
        {
            if (data.Length != 3) throw new InvalidDataException("data should be 3 bytes long; found" + data.Length + " bytes");
            return (data[0] << 16) | (data[1] << 8) | (data[2] << 0);
        }

        public static ushort DecodeBEUInt16(byte[] data)
        {
            if (data.Length != 2) throw new InvalidDataException("data should be 2 bytes long; found" + data.Length + " bytes");
            return (ushort)((data[0] << 8) | (data[1] << 0));
        }

        public static short DecodeBEInt16(byte[] data)
        {
            if (data.Length != 2) throw new InvalidDataException("data should be 2 bytes long; found" + data.Length + " bytes");
            return (short)((data[0] << 8) | (data[1] << 0));
        }


        /// <summary>
        /// Switches the format of an unsigned Int32 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static uint ReverseUInt32(uint n)
        {
            byte b0;
            byte b1;
            byte b2;
            byte b3;

            b0 = (byte)((n & 0x000000FF) >> 0);
            b1 = (byte)((n & 0x0000FF00) >> 8);
            b2 = (byte)((n & 0x00FF0000) >> 16);
            b3 = (byte)((n & 0xFF000000) >> 24);

            return (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | (b3 << 0));
        }

        /// <summary>
        /// Switches the format of a signed Int32 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static int ReverseInt32(int n)
		{
			byte b0;
			byte b1;
			byte b2;
			byte b3;

			b0 = (byte)((n & 0x000000FF) >> 0); 
			b1 = (byte)((n & 0x0000FF00) >> 8); 
			b2 = (byte)((n & 0x00FF0000) >> 16); 
			b3 = (byte)((n & 0xFF000000) >> 24); 
			
			return (b0 << 24) | (b1 << 16) | (b2 << 8) | (b3 << 0);
        }

        /// <summary>
        /// Switches the format of an unsigned Int16 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static ushort ReverseUInt16(ushort n)
        {
            byte b0;
            byte b1;

            b0 = (byte)((n & 0x00FF) >> 0);
            b1 = (byte)((n & 0xFF00) >> 8);

            return (ushort)((b0 << 8) | (b1 << 0));
        }

        /// <summary>
        /// Switches the format of a signed Int16 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static short ReverseInt16(short n)
        {
            byte b0;
            byte b1;

            b0 = (byte)((n & 0x00FF) >> 0);
            b1 = (byte)((n & 0xFF00) >> 8);

            return (short)((b0 << 8) | (b1 << 0));
        }

        /// <summary>
        /// Guesses the encoding from the file Byte Order Mark (BOM)
        /// http://en.wikipedia.org/wiki/Byte_order_mark 
        /// NB : This obviously only works for files that actually start with a BOM
        /// </summary>
        /// <param name="file">FileStream to read from</param>
        /// <returns>Detected encoding; system Default if detection failed</returns>
        public static Encoding GetEncodingFromFileBOM(FileStream file)
        {
            Encoding result;
            byte[] bom = new byte[4]; // Get the byte-order mark, if there is one
            file.Read(bom, 0, 4);
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) // utf-8
            {
                result = Encoding.UTF8;
            }
            else if (bom[0] == 0xfe && bom[1] == 0xff) // utf-16 and ucs-2
            {
                result = Encoding.BigEndianUnicode;
            }
            else if (bom[0] == 0xff && bom[1] == 0xfe) // ucs-2le, ucs-4le, and ucs-16le
            {
                result = Encoding.Unicode;
            }
            else if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) // ucs-4 / UTF-32
            {
                result = Encoding.UTF32;
            }
            else
            {
                // There might be some cases where the Default encoding reads illegal characters
                // e.g. "ß" encoded in Windows-1250 gives an illegal character when read with Chinese-simplified (gb2312)
                result = Encoding.Default;
            }

            // Now reposition the file cursor back to the start of the file
            file.Seek(0, System.IO.SeekOrigin.Begin);
            return result;
        }

        /// <summary>
        /// The method to Decode your Base64 strings.
        /// </summary>
        /// <param name="encodedData">The String containing the characters to decode.</param>
        /// <param name="s">The Stream where the resulting decoded data will be written.</param>
        /// Source : http://blogs.microsoft.co.il/blogs/mneiter/archive/2009/03/22/how-to-encoding-and-decoding-base64-strings-in-c.aspx
        [Obsolete("use Utils.DecodeFrom64")]
        public static void DecodeFrom64(byte[] encodedData, Stream s)
        {
            if (encodedData.Length % 4 > 0) throw new FormatException("Size must me multiple of 4");

            char[] encodedDataChar = new char[encodedData.Length];
            for (int i = 0; i < encodedData.Length;i++ )
            {
                encodedDataChar[i] = System.Convert.ToChar(encodedData[i]);
            }
            byte[] convertedData = System.Convert.FromBase64CharArray(encodedDataChar, 0, encodedDataChar.Length);

            s.Write(convertedData, 0, convertedData.Length);
        }

        /// <summary>
        /// Reads a null-terminated String from the given BinaryReader
        /// Returns with the BinaryReader positioned after the last null-character(s)
        /// </summary>
        /// <param name="r">BinaryReader positioned at the beginning of the String to be read</param>
        /// <param name="encoding">Encoding based on ID3v2.4 conventions (see below)</param>
        /// <returns>Read value</returns>
        ///  $00   ISO-8859-1 [ISO-8859-1]. Terminated with $00.
        ///  $01   UTF-16 [UTF-16] encoded Unicode [UNICODE] with BOM. All
        ///   strings in the same frame SHALL have the same byteorder.
        ///  Terminated with $00 00.
        ///  $02   UTF-16BE [UTF-16] encoded Unicode [UNICODE] without BOM.
        ///  Terminated with $00 00.
        ///  $03   UTF-8 [UTF-8] encoded Unicode [UNICODE]. Terminated with $00.
        [Obsolete("use ReadNullTerminatedString(BinaryReader, Encoding)")]
        public static String ReadNullTerminatedString(BinaryReader r, int encoding)
        {
            return ReadNullTerminatedString(r, GetEncodingFromID3v2Encoding(encoding));
        }

        [Obsolete("marked for deletion; belongs to ID3v2")]
        public static Encoding GetEncodingFromID3v2Encoding(int encoding)
        {
            if (0 == encoding) return Encoding.GetEncoding("ISO-8859-1"); // aka ISO Latin-1
            else if (1 == encoding) return Encoding.Unicode;
            else if (2 == encoding) return Encoding.BigEndianUnicode;
            else if (3 == encoding) return Encoding.UTF8;
            else return Encoding.Default;
        }

        /// <summary>
        /// Reads a null-terminated String from the given BinaryReader, according to the given Encoding
        /// Returns with the BinaryReader positioned after the last null-character(s)
        /// </summary>
        /// <param name="r">BinaryReader positioned at the beginning of the String to be read</param>
        /// <param name="encoding">Encoding to use for reading the stream</param>
        /// <returns>Read value</returns>
        public static String ReadNullTerminatedString(BinaryReader r, Encoding encoding)
        {
            return readNullTerminatedString(r.BaseStream, encoding, 0, false);
        }
        public static String ReadNullTerminatedString(Stream s, Encoding encoding)
        {
            return readNullTerminatedString(s, encoding, 0, false);
        }

        /// <summary>
        /// Reads a null-terminated String from the given BinaryReader, according to the given Encoding, within a given limit of bytes
        /// Returns with the BinaryReader positioned at (start+limit)
        /// </summary>
        /// <param name="r">BinaryReader positioned at the beginning of the String to be read</param>
        /// <param name="encoding">Encoding to use for reading the stream</param>
        /// <param name="limit">Maximum number of bytes to read</param>
        /// <returns>Read value</returns>
        public static String ReadNullTerminatedStringFixed(BinaryReader r, Encoding encoding, int limit)
        {
            return readNullTerminatedString(r.BaseStream, encoding, limit, true);
        }

        /// <summary>
        /// Reads a null-terminated string using the giver BinaryReader
        /// </summary>
        /// <param name="r">Stream reader to read the string from</param>
        /// <param name="encoding">Encoding to use to parse the read bytes into the resulting String</param>
        /// <param name="limit">Limit (in bytes) of read data (0=unlimited)</param>
        /// <param name="moveStreamToLimit">Indicates if the stream has to advance to the limit before returning</param>
        /// <returns>The string read, without the zeroes at its end</returns>
        private static String readNullTerminatedString(Stream r, Encoding encoding, int limit, bool moveStreamToLimit)
        {
            int nbChars = (encoding.Equals(Encoding.BigEndianUnicode) || encoding.Equals(Encoding.Unicode)) ? 2 : 1;
            byte[] readBytes = new byte[limit > 0 ? limit : 100];
            byte[] buffer = new byte[2];
            int nbRead = 0;
            long streamLength = r.Length;
            long streamPos = r.Position;

            while (streamPos < streamLength && ( (0 == limit) || (nbRead < limit) ) )
            {
                // Read the size of a character
                r.Read(buffer, 0, nbChars);

                if ( (1 == nbChars) && (0 == buffer[0]) ) // Null character read for single-char encodings
                {
                    break;
                }
                else if ( (2 == nbChars) && (0 == buffer[0]) && (0 == buffer[1]) ) // Null character read for two-char encodings
                {
                    break;
                }
                else // All clear; store the read char in the byte array
                {
                    if (readBytes.Length < nbRead + nbChars) Array.Resize<byte>(ref readBytes, readBytes.Length + 100);

                    readBytes[nbRead] = buffer[0];
                    if (2 == nbChars) readBytes[nbRead+1] = buffer[1];
                    nbRead += nbChars;
                    streamPos += nbChars;
                }
            }

            if (moveStreamToLimit && nbRead < limit) r.Seek(limit - nbRead, SeekOrigin.Current);

            return encoding.GetString(readBytes,0,nbRead);
        }

        /// <summary>
        /// Extracts a Int32 from a byte array using the "synch-safe" convention
        /// as to ID3v2 definition (§6.2)
        /// </summary>
        /// <param name="bytes">Byte array containing data
        /// NB : Array size can vary from 1 to 5 bytes, as only 7 bits of each is actually used
        /// </param>
        /// <returns>Decoded Int32</returns>
        public static int DecodeSynchSafeInt(byte[] bytes)
        {
            if (bytes.Length > 5) throw new Exception("Array too long : has to be 1 to 5 bytes; found : " + bytes.Length + " bytes");
            int result = 0;

            for (int i = 0; i < bytes.Length; i++)
            {
                result += bytes[i] * (int)Math.Floor(Math.Pow(2, (7 * (bytes.Length - 1 - i))));
            }
            return result;
        }

        /// <summary>
        /// Decodes a Int32 from a 4-byte array using the "synch-safe" convention
        /// as to ID3v2 definition (§6.2)
        /// NB : The actual capacity of the integer thus reaches 28 bits
        /// </summary>
        /// <param name="bytes">4-byte array containing to convert</param>
        /// <returns>Decoded Int32</returns>
        public static int DecodeSynchSafeInt32(byte[] bytes)
        {
            if (bytes.Length != 4) throw new Exception("Array length has to be 4 bytes; found : "+bytes.Length+" bytes");

            return                 
                bytes[0] * 0x200000 +   //2^21
                bytes[1] * 0x4000 +     //2^14
                bytes[2] * 0x80 +       //2^7
                bytes[3];
        }

        /// <summary>
        /// Encodes an Int32 to a 4-byte array using the "synch-safe" convention
        /// as to ID3v2 definition (§6.2)
        /// </summary>
        /// <param name="value">Value to encode</param>
        /// <param name="nbBytes">Number of bytes to encode to (can be 1 to 5)</param>
        /// <returns>Encoded array of bytes</returns>
        public static byte[] EncodeSynchSafeInt(int value, int nbBytes)
        {
            if ((nbBytes < 1) || (nbBytes > 5)) throw new Exception("nbBytes has to be 1 to 5; found : " + nbBytes);
            byte[] result = new byte[nbBytes];
            int range;

            for (int i = 0; i < nbBytes; i++)
            {
                range = (7 * (nbBytes - 1 - i));
                result[i] = (byte)( (value & (0x7F << range)) >> range);
            }

            return result;
        }

        /// <summary>
        /// Encodes a Int32 to a 4-byte array using the "synch-safe" convention
        /// as to ID3v2 definition (§6.2)
        /// </summary>
        /// <param name="value">Integer to be encoded</param>
        /// <returns>Encoded array of bytes</returns>
        public static byte[] EncodeSynchSafeInt32(int value)
        {
            byte[] result = new byte[4];
            result[0] = (byte)((value & 0xFE00000) >> 21);
            result[1] = (byte)((value & 0x01FC000) >> 14);
            result[2] = (byte)((value & 0x0003F80) >> 7);
            result[3] = (byte)((value & 0x000007F));

            return result;
        }

        public static uint DecodeBEUInt24(byte[] value, int offset = 0)
        {
            if (value.Length - offset < 3) throw new InvalidDataException("Value should at least contain 3 bytes after offset; actual size="+(value.Length-offset)+" bytes");
            return (uint)value[offset] << 16 | (uint)value[offset + 1] << 8 | (uint)value[offset + 2];
        }

        public static byte[] EncodeBEUInt24(uint value)
        {
            if (value > 0x00FFFFFF) throw new InvalidDataException("Value should not be higher than "+0x00FFFFFF+"; actual value="+value);
            
            // Output has to be big-endian
            return new byte[3] { (byte)((value & 0x00FF0000) >> 16), (byte)((value & 0x0000FF00) >> 8), (byte)(value & 0x000000FF) };
        }

        /// <summary>
        /// Finds a byte sequence within a stream
        /// </summary>
        /// <param name="r">Stream to search into</param>
        /// <param name="sequence">Sequence to find</param>
        /// <param name="forward">True if the sequence to find has to be looked for after current position; false instead</param>
        /// <param name="maxDistance">Maximum distance (in bytes) of the sequence to find.
        /// Put 0 for an infinite distance</param>
        /// <returns>
        ///     true if the sequence has been found; the stream will be positioned on the 1st byte following the sequence
        ///     false if the sequence has not been found; the stream will keep its initial position
        /// </returns>
        public static bool FindSequence(Stream stream, byte[] sequence, long limit = 0)
        {
            int BUFFER_SIZE = 512;
            byte[] readBuffer = new byte[BUFFER_SIZE];

            int remainingBytes, bytesToRead;
            int iSequence = 0;
            int readBytes = 0;
            long initialPos = stream.Position;

            remainingBytes = (int)((limit > 0) ? Math.Min(stream.Length - stream.Position, limit) : stream.Length - stream.Position);

            while (remainingBytes > 0)
            {
                bytesToRead = Math.Min(remainingBytes, BUFFER_SIZE);

                stream.Read(readBuffer, 0, bytesToRead);

                for (int i = 0; i < bytesToRead; i++)
                {
                    if (sequence[iSequence] == readBuffer[i]) iSequence++;
                    else if (iSequence > 0) iSequence = 0;

                    if (sequence.Length == iSequence)
                    {
                        stream.Position = initialPos + readBytes + i + 1;
                        return true;
                    }
                }

                remainingBytes -= bytesToRead;
                readBytes += bytesToRead;
            }

            // If we're here, the sequence hasn't been found
            stream.Position = initialPos;
            return false;
        }

        /// <summary>
        /// Reads the given number of bits from the given position and converts it to an unsigned int32
        /// according to big-endian convention
        /// 
        /// NB : reader position _always_ progresses by 4, no matter how many bits are needed
        /// </summary>
        /// <param name="source">BinaryReader to read the data from</param>
        /// <param name="bitPosition">Position of the first _bit_ to read (scale is x8 compared to classic byte positioning) </param>
        /// <param name="bitCount">Number of bits to read</param>
        /// <returns>Unsigned int32 formed from read bits, according to big-endian convention</returns>
        public static uint ReadBits(BinaryReader source, int bitPosition, int bitCount)
        {
            if (bitCount < 1 || bitCount > 32) throw new NotSupportedException("Bit count must be between 1 and 32");
            byte[] buffer = new byte[4];

            // Read a number of bits from file at the given position
            source.BaseStream.Seek(bitPosition / 8, SeekOrigin.Begin); // integer division =^ div
            buffer = source.ReadBytes(4);
            uint result = (uint)((buffer[0] << 24) + (buffer[1] << 16) + (buffer[2] << 8) + buffer[3]);
            result = (result << (bitPosition % 8)) >> (32 - bitCount);

            return result;
        }
    }
}
