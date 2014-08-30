using Commons;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ATL
{
	/// <summary>
	/// Misc. utilities used by binary readers
	/// </summary>
	public class StreamUtils
	{	
		// Size of the buffer used for memory stream copies
		// (see CopyMemoryStreamFrom method)
		private const int BUFFERSIZE = 1;

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
		public static char[] ReadOneByteChars(BinaryReader r, int length)
		{
			byte[] byteArr;
			char[] result = new char[length];

			byteArr = r.ReadBytes(length);
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
		public static void CopyMemoryStreamFrom(Stream mTo, Stream mFrom, long length)
		{
			BinaryWriter w = new BinaryWriter(mTo);
			BinaryReader r = new BinaryReader(mFrom);
			CopyMemoryStreamFrom(w, r, length);
		}

		/// <summary>
		/// Writes a given number of bytes from a stream to a writer
		/// </summary>
		/// <param name="mTo">Writer to be used</param>
		/// <param name="mFrom">Source stream</param>
		/// <param name="length">Number of bytes to be copied</param>
		public static void CopyMemoryStreamFrom(BinaryWriter w, Stream mFrom, long length)
		{
			BinaryReader r = new BinaryReader(mFrom);			
			CopyMemoryStreamFrom(w, r, length);
		}

		/// <summary>
		/// Writes a given number of bytes from a reader to a stream
		/// </summary>
		/// <param name="mTo">Target stream</param>
		/// <param name="r">Reader to be used</param>
		/// <param name="length">Number of bytes to be copied</param>
		public static void CopyMemoryStreamFrom(Stream mTo, BinaryReader r, long length)
		{
			BinaryWriter w = new BinaryWriter(mTo);
			CopyMemoryStreamFrom(w, r, length);
		}

		/// <summary>
		/// Writes a given number of bytes from a reader to a writer
		/// </summary>
		/// <param name="mTo">Writer to be used</param>
		/// <param name="r">Reader to be used</param>
		/// <param name="length">Number of bytes to be copied</param>
		public static void CopyMemoryStreamFrom(BinaryWriter w, BinaryReader r, long length)
		{			
			long effectiveLength;
			long initialPosition;

			initialPosition = r.BaseStream.Position;
			if (0 == length) effectiveLength = r.BaseStream.Length; else effectiveLength = length;

			while (r.BaseStream.Position < initialPosition+effectiveLength)
				w.Write(r.ReadBytes(BUFFERSIZE));
		}

        /// <summary>
        /// Switches the format of an Int64 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static Int64 ReverseInt64(UInt64 n)
        {
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
            b7 = (byte)((n & 0xFF00000000000000) >> 56);

            return (b0 << 56) | (b1 << 48) | (b2 << 40) | (b3 << 32) | (b4 << 24) | (b5 << 16) | (b6 << 8) | (b7 << 0);
        }

		/// <summary>
		/// Switches the format of an Int32 between big endian and little endian
		/// </summary>
		/// <param name="n">value to convert</param>
		/// <returns>converted value</returns>
		public static Int32 ReverseInt32(Int32 n)
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
        /// Switches the format of an Int16 between big endian and little endian
        /// </summary>
        /// <param name="n">value to convert</param>
        /// <returns>converted value</returns>
        public static ushort ReverseInt16(ushort n)
        {
            byte b0;
            byte b1;

            b0 = (byte)((n & 0x00FF) >> 0);
            b1 = (byte)((n & 0xFF00) >> 8);

            return (ushort)((b0 << 8) | (b1 << 0));
        }

        // Guesses the encoding from the file Byte Order Mark (BOM)
        // NB : This obviously only works for files that actually start with a BOM
        // http://en.wikipedia.org/wiki/Byte_order_mark
        public static Encoding GetEncodingFromFileBOM(ref FileStream file)
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

        // Converts a list of chars into a Long (big-endian evaluated)
        public static long GetLongFromChars(char[] chars)
        {
            if (chars.Length > 8) throw new Exception("Long cannot be read from a record larger than 8 bytes");
            long result = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                result = (result << 8) + chars[i];
            }
            return result;
        }

        // Converts a list of chars into an Int (big-endian evaluated)
        public static int GetIntFromChars(char[] chars)
        {
            if (chars.Length > 4) throw new Exception("Long cannot be read from a record larger than 4 bytes");
            int result = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                result = (result << 8) + chars[i];
            }
            return result;
        }

        /// <summary>
        /// The method to Decode your Base64 strings.
        /// </summary>
        /// <param name="encodedData">The String containing the characters to decode.</param>
        /// <param name="s">The Stream where the resulting decoded data will be written.</param>
        /// Source : http://blogs.microsoft.co.il/blogs/mneiter/archive/2009/03/22/how-to-encoding-and-decoding-base64-strings-in-c.aspx
        public static void DecodeFrom64(byte[] encodedData, Stream s)
        {

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
        public static String ReadNullTerminatedString(BinaryReader r, int encoding)
        {
            /*
            int nbBytesPerChar = (1 == encoding || 2 == encoding)?2:1;
            char[] endingChars = (1 == encoding || 2 == encoding) ? new char[] { '\0', '\0' } : new char[]  { '\0' };
            char[] c = StreamUtils.ReadOneByteChars(r, nbBytesPerChar);
            String result = "";
            while (!StreamUtils.ArrEqualsArr(c,endingChars))
            {
                result += new String(c);
                c = StreamUtils.ReadOneByteChars(r, nbBytesPerChar);
            }
            // Strip all Unicode null-chars
            return Utils.StripZeroChars(result);
             */
            return ReadNullTerminatedString(r, GetEncodingFromID3v2Encoding(encoding));
        }

        public static Encoding GetEncodingFromID3v2Encoding(int encoding)
        {
            if (0 == encoding) return Encoding.GetEncoding("ISO-8859-1"); // aka ISO Latin-1
            else if (1 == encoding) return Encoding.Unicode;
            else if (2 == encoding) return Encoding.BigEndianUnicode;
            else if (3 == encoding) return Encoding.UTF8;
            else return Encoding.Default;
        }

        public static String ReadNullTerminatedString(BinaryReader r, Encoding encoding)
        {
            return ReadNullTerminatedString(r, encoding, 0, false);
        }

        public static String ReadNullTerminatedStringFixed(BinaryReader r, Encoding encoding, int limit)
        {
            return ReadNullTerminatedString(r, encoding, limit, true);
        }

        /// <summary>
        /// Reads a null-terminated string using the giver StreamReader
        /// </summary>
        /// <param name="r">Stream reader to be used to read the string</param>
        /// <param name="encoding">Encoding to use to parse the read bytes into the resulting String</param>
        /// <param name="limit">Limit (in bytes) of read data (0=unlimited)</param>
        /// <param name="moveStreamToLimit">Indicates if the stream has to advance to the limit before returning</param>
        /// <returns>The string read, without the zeroes at its end</returns>
        private static String ReadNullTerminatedString(BinaryReader r, Encoding encoding, int limit, Boolean moveStreamToLimit)
        {
            int nbEndingZeroChars = (encoding.Equals(Encoding.BigEndianUnicode) || encoding.Equals(Encoding.Unicode)) ? 2 : 1;
            //char[] endingChars = (encoding.Equals(Encoding.BigEndianUnicode) || encoding.Equals(Encoding.Unicode)) ? new char[] { '\0', '\0' } : new char[] { '\0' };
            IList<byte> readBytes = new List<byte>();
            long distance = 0;

            while (r.BaseStream.Position < r.BaseStream.Length && ( (0 == limit) || (distance < limit) ) )
            {
                readBytes.Add(r.ReadByte());
                distance++;

                if ((1 == nbEndingZeroChars) && (0 == readBytes[readBytes.Count - 1]))
                {
                    readBytes.RemoveAt(readBytes.Count - 1);
                    break;
                }
                if ((2 == nbEndingZeroChars) && (readBytes.Count > 1) && (0 == readBytes[readBytes.Count - 1] + readBytes[readBytes.Count - 2]))
                {
                    readBytes.RemoveAt(readBytes.Count - 1);
                    readBytes.RemoveAt(readBytes.Count - 1);
                    break;
                }
            }

            if (moveStreamToLimit && distance < limit) r.BaseStream.Seek(limit - distance, SeekOrigin.Current);

            byte[] readBytesArr = new byte[readBytes.Count];
            readBytes.CopyTo(readBytesArr,0);
            return encoding.GetString(readBytesArr);
        }

        // Checks if the JPEG picture scanned by the given stream
        // may contain abnormalities (0xff 0x00 instead of 0xff)
        public static bool isBrokenJpeg(Stream s)
        {
            int[] header = new int[5];
            s.Seek(0, SeekOrigin.Begin);
            header[0] = s.ReadByte();
            header[1] = s.ReadByte();
            header[2] = s.ReadByte();
            header[3] = s.ReadByte();
            header[4] = s.ReadByte();

            return (header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff && (header[3] == 0x00 && header[4] == 0xe0));
        }

        // Clean abnormalities in JPEG binary streams
        //   - Badly formatted data (0xff 0x00 => 0xff)
        public static MemoryStream FixBrokenJPEG(ref MemoryStream s)
        {
            MemoryStream mem = new MemoryStream(s.Capacity);

            using(BinaryReader r = new BinaryReader(s))
            using (BinaryWriter w = new BinaryWriter(mem))
            {
                byte prevB = 0;
                byte b;

                s.Seek(0, SeekOrigin.Begin);
                while (s.Position < s.Length)
                {
                    b = r.ReadByte();
                    if ((0xFF == prevB) && (0x00 == b)) b = r.ReadByte();

                    w.Write(b);
                    prevB = b;
                }
            }

            return mem;
        }

        // Extracts a "synch-safe" Int32 from a byte array
        // according to ID3v2 definition (§6.2)
        public static int ExtractSynchSafeInt32(byte[] bytes)
        {
            return                 
                bytes[0] * 0x200000 +
                bytes[1] * 0x4000 +
                bytes[2] * 0x80 +
                bytes[3];
        }

        /// <summary>
        /// Finds a byte sequence within a stream
        /// </summary>
        /// <param name="r">Stream to search into</param>
        /// <param name="sequence">Sequence to find</param>
        /// <param name="maxDistance">Maximum distance (in bytes) of the sequence to find.
        /// Put 0 for an infinite distance</param>
        /// <returns>
        ///     true if the sequence has been found; the stream will be positioned on the 1st byte following the sequence
        ///     false if the sequence has not been found; the stream will keep its initial position
        /// </returns>
        public static bool FindSequence(ref BinaryReader r, byte[] sequence, long maxDistance)
        {
            byte[] window = r.ReadBytes(sequence.Length);
            long initialPos = r.BaseStream.Position;
            long distance = 0;
            bool found = false;
            int i;

            while (r.BaseStream.Position < r.BaseStream.Length || (maxDistance > 0 && distance > maxDistance) )
            {
                if (ArrEqualsArr(sequence, window))
                {
                    found = true;
                    break;
                }

                for (i = 0; i < window.Length - 1; i++)
                {
                    window[i] = window[i + 1];
                }
                window[window.Length - 1] = r.ReadByte();

                distance++;
            }

            if (!found) r.BaseStream.Position = initialPos;

            return found;
        }
    }
}
