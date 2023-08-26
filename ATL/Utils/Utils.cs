using ATL;
using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;

namespace Commons
{
    /// <summary>
    /// General utility class
    /// </summary>
    internal static class Utils
    {
        private static readonly IDictionary<string, Encoding> encodingCache = new Dictionary<string, Encoding>();

        /// <summary>
        /// 'ZERO WIDTH NO-BREAK SPACE' invisible character, sometimes used by certain tagging softwares
        /// Looks like a BOM unfortunately converted into an unicode character :/
        /// </summary>
        public static readonly string UNICODE_INVISIBLE_EMPTY = "\uFEFF";


        /// <summary>
        /// ISO-8859-1 encoding
        /// </summary>
        public static Encoding Latin1Encoding { get; } = Encoding.GetEncoding("ISO-8859-1");


        /// <summary>
        /// Transform the given string so that is becomes non-null
        /// </summary>
        /// <param name="value">String to protect</param>
        /// <returns>Given string if non-null; else empty string</returns>
        public static string ProtectValue(string value)
        {
            return value ?? "";
        }

        /// <summary>
        /// Extract the year from the given DateTime; "" if DateTime is its minimum value
        /// </summary>
        /// <param name="value">DateTime to extract the year from</param>
        /// <returns>Year from the given DateTime, or "" if not set</returns>
        public static string ProtectYear(DateTime value)
        {
            return DateTime.MinValue == value ? "" : value.Year.ToString();
        }

        /// <summary>
        /// Format the given duration using the following format
        ///     DDdHH:MM:SS.UUUU
        ///     
        ///  Where
        ///     DD is the number of days, if applicable (i.e. durations of less than 1 day won't display the "DDd" part)
        ///     HH is the number of hours, if applicable (i.e. durations of less than 1 hour won't display the "HH:" part)
        ///     MM is the number of minutes
        ///     SS is the number of seconds
        ///     UUUU is the number of milliseconds
        /// </summary>
        /// <param name="milliseconds">Duration to format (in milliseconds)</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static string EncodeTimecode_ms(long milliseconds)
        {
            long seconds = Convert.ToInt64(Math.Floor(milliseconds / 1000.00));

            return EncodeTimecode_s(seconds) + "." + (milliseconds - seconds * 1000);
        }

        /// <summary>
        /// Format the given duration using the following format
        ///     DDdHH:MM:SS
        ///     
        ///  Where
        ///     DD is the number of days, if applicable (i.e. durations of less than 1 day won't display the "DDd" part)
        ///     HH is the number of hours, if applicable (i.e. durations of less than 1 hour won't display the "HH:" part)
        ///     MM is the number of minutes
        ///     SS is the number of seconds
        /// </summary>
        /// <param name="seconds">Duration to format (in seconds)</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static string EncodeTimecode_s(long seconds)
        {
            int h;
            long m;
            string hStr, mStr, sStr;
            long s;
            int d;

            h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            m = Convert.ToInt64(Math.Floor((seconds - 3600.00 * h) / 60));
            s = seconds - 60 * m - 3600 * h;
            d = Convert.ToInt32(Math.Floor(h / 24.00));
            if (d > 0) h = h - 24 * d;

            hStr = h.ToString();
            if (1 == hStr.Length) hStr = "0" + hStr;
            mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

            if (d > 0) return d + "d " + hStr + ":" + mStr + ":" + sStr;
            if (h > 0) return hStr + ":" + mStr + ":" + sStr;
            return mStr + ":" + sStr;
        }

        /// <summary>
        /// Convert the duration of the given timecode to milliseconds
        /// Supported formats : hh:mm, hh:mm:ss.ddd, mm:ss, hh:mm:ss and mm:ss.ddd
        /// </summary>
        /// <param name="timeCode">Timecode to convert</param>
        /// <returns>Duration of the given timecode expressed in milliseconds if succeeded; -1 if failed</returns>
        public static int DecodeTimecodeToMs(string timeCode)
        {
            int result = -1;
            DateTime dateTime;
            bool valid = false;

            if (DateTime.TryParse(timeCode, out dateTime)) // Handle classic cases hh:mm, hh:mm:ss.ddd (the latter being the spec)
            {
                valid = true;
                result = dateTime.Millisecond;
                result += dateTime.Second * 1000;
                result += dateTime.Minute * 60 * 1000;
                result += dateTime.Hour * 60 * 60 * 1000;
            }
            else // Handle mm:ss, hh:mm:ss and mm:ss.ddd
            {
                int days = 0;
                int hours = 0;
                int minutes = 0;
                int seconds = 0;
                int milliseconds = 0;

                if (timeCode.Contains(':'))
                {
                    valid = true;
                    string[] parts = timeCode.Split(':');
                    if (parts[^1].Contains('.'))
                    {
                        string[] subPart = parts[^1].Split('.');
                        parts[^1] = subPart[0];
                        milliseconds = int.Parse(subPart[1]);
                    }
                    seconds = int.Parse(parts[^1]);
                    minutes = int.Parse(parts[^2]);
                    if (parts.Length >= 3)
                    {
                        string[] subPart = parts[^3].Split('d');
                        if (subPart.Length > 1)
                        {
                            days = int.Parse(subPart[0].Trim());
                            hours = int.Parse(subPart[1].Trim());
                        }
                        else
                        {
                            hours = int.Parse(subPart[0]);
                        }
                    }

                    result = milliseconds;
                    result += seconds * 1000;
                    result += minutes * 60 * 1000;
                    result += hours * 60 * 60 * 1000;
                    result += days * 24 * 60 * 60 * 1000;
                }
            }

            if (!valid) result = -1;

            return result;
        }

        /// <summary>
        /// Strip the given string from all ending null '\0' characters
        /// </summary>
        /// <param name="iStr">String to process</param>
        /// <returns>Given string, without any ending null character</returns>
        public static string StripEndingZeroChars(string iStr)
        {
            //return Regex.Replace(iStr, @"\0+\Z", "");  Too expensive
            int i = iStr.Length;
            while (i > 0 && '\0' == iStr[i - 1]) i--;

            return iStr.Substring(0, i);
        }

        /// <summary>
        /// Transform the given number to format with the given length, expressed in number of characters
        ///  - If the given length is shorter than the actual length of the string, it will be truncated
        ///  - If the given length is longer than the actual length of the string, it will be right/left-padded with a given character
        /// </summary>
        /// <param name="value">Value to transform</param>
        /// <param name="length">Target length of the final string</param>
        /// <param name="paddingChar">Character to use if padding is needed</param>
        /// <param name="padRight">True if the padding has to be done on the right-side of the target string; 
        /// false if the padding has to be done on the left-side (optional; default value = true)</param>
        /// <returns>Reprocessed string of given length, according to rules documented in the method description</returns>
        public static string BuildStrictLengthString(int value, int length, char paddingChar, bool padRight = true)
        {
            return BuildStrictLengthString(value.ToString(), length, paddingChar, padRight);
        }
        /// <summary>
        /// Transform the given string to format with the given length, expressed in number of characters
        ///  - If the given length is shorter than the actual length of the string, it will be truncated
        ///  - If the given length is longer than the actual length of the string, it will be right/left-padded with a given character
        /// </summary>
        /// <param name="value">Value to transform</param>
        /// <param name="length">Target length of the final string</param>
        /// <param name="paddingChar">Character to use if padding is needed</param>
        /// <param name="padRight">True if the padding has to be done on the right-side of the target string; 
        /// false if the padding has to be done on the left-side (optional; default value = true)</param>
        /// <returns>Reprocessed string of given length, according to rules documented in the method description</returns>
        public static string BuildStrictLengthString(string value, int length, char paddingChar, bool padRight = true)
        {
            string result = value ?? "";

            if (result.Length > length) result = result.Substring(0, length);
            else if (result.Length < length)
            {
                if (padRight) result = result.PadRight(length, paddingChar);
                else result = result.PadLeft(length, paddingChar);
            }

            return result;
        }

        /// <summary>
        /// Transform the given string to format with the given length, expressed in number of bytes
        ///  - If the given length is shorter than the actual length of the string, it will be truncated
        ///  - If the given length is longer than the actual length of the string, it will be right/left-padded with a given byte
        /// </summary>
        /// <param name="value">String to transform</param>
        /// <param name="targetLength">Target length of the final string</param>
        /// <param name="paddingByte">Byte to use if padding is needed</param>
        /// <param name="encoding">Encoding to use to represent the given string in binary format</param>
        /// <param name="padRight">True if the padding has to be done on the right-side of the target string; 
        /// false if the padding has to be done on the left-side (optional; default value = true)</param>
        /// <returns>Reprocessed string of given length, in binary format, according to rules documented in the method description</returns>
        public static byte[] BuildStrictLengthStringBytes(string value, int targetLength, byte paddingByte, Encoding encoding, bool padRight = true)
        {
            byte[] result;

            byte[] data = encoding.GetBytes(value);
            while (data.Length > targetLength)
            {
                value = value.Remove(value.Length - 1);
                data = encoding.GetBytes(value);
            }

            if (data.Length < targetLength)
            {
                result = new byte[targetLength];
                if (padRight)
                {
                    Array.Copy(data, result, data.Length);
                    for (int i = data.Length; i < result.Length; i++) result[i] = paddingByte;
                }
                else
                {
                    Array.Copy(data, 0, result, result.Length - data.Length, data.Length);
                    for (int i = 0; i < (result.Length - data.Length); i++) result[i] = paddingByte;
                }
            }
            else
            {
                result = data;
            }

            return result;
        }

        /// <summary>
        /// Covert given string value to boolean.
        ///   - Returns true if string represents a non-null numeric value or the word "true"
        ///   - Returns false if not
        ///   
        /// NB : This implementation exists because default .NET implementation has a different convention as for parsing numbers
        /// </summary>
        /// <param name="value">Value to be converted</param>
        /// <returns>Resulting boolean value</returns>
        public static bool ToBoolean(string value)
        {
            if (value != null)
            {
                value = value.Trim();

                if (value.Length > 0)
                {
                    // Numeric convert
                    float f;
                    if (float.TryParse(value, out f))
                    {
                        return f != 0;
                    }
                    else
                    {
                        value = value.ToLower();
                        return "true".Equals(value);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Decode the given Base64 string
        /// Source : http://blogs.microsoft.co.il/blogs/mneiter/archive/2009/03/22/how-to-encoding-and-decoding-base64-strings-in-c.aspx
        /// </summary>
        /// <param name="encodedData">String containing the characters to decode</param>
        /// <returns>Decoded data</returns>
        public static byte[] DecodeFrom64(byte[] encodedData)
        {
            if (encodedData.Length % 4 > 0) throw new FormatException("Size must me multiple of 4");

            char[] encodedDataChar = new char[encodedData.Length];
            Latin1Encoding.GetChars(encodedData, 0, encodedData.Length, encodedDataChar, 0); // Optimized for large data

            return Convert.FromBase64CharArray(encodedDataChar, 0, encodedDataChar.Length);
        }

        /// <summary>
        /// Convert the given input to a Base64 UUencoded output
        /// </summary>
        /// <param name="data">Data to be encoded</param>
        /// <returns>Encoded data</returns>
        public static byte[] EncodeTo64(byte[] data)
        {
            // Each 3 byte sequence in the source data becomes a 4 byte
            // sequence in the character array. 
            long arrayLength = (long)((4.0d / 3.0d) * data.Length);

            // If array length is not divisible by 4, go up to the next
            // multiple of 4.
            if (arrayLength % 4 != 0)
            {
                arrayLength += 4 - arrayLength % 4;
            }

            char[] dataChar = new char[arrayLength];

            Convert.ToBase64CharArray(data, 0, data.Length, dataChar, 0);

            return Utils.Latin1Encoding.GetBytes(dataChar);
        }

        /// <summary>
        /// Indicate if the given string is exclusively composed of digital characters
        /// 
        /// NB1 : decimal separators '.' and ',' are tolerated except if allowsOnlyIntegers argument is set to True
        /// NB2 : whitespaces ' ' are not tolerated
        /// NB3 : any alternate notation (e.g. exponent, hex) is not tolerated
        /// </summary>
        /// <param name="s">String to analyze</param>
        /// <param name="allowsOnlyIntegers">Set to True if IsNumeric should reject decimal values; default = false</param>
        /// <param name="allowsSigned">Set to False if IsNumeric should reject signed values; default = true</param>
        /// <returns>True if the string is a digital value; false if not</returns>
        public static bool IsNumeric(string s, bool allowsOnlyIntegers = false, bool allowsSigned = true)
        {
            if (string.IsNullOrEmpty(s)) return false;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '.' || s[i] == ',')
                {
                    if (allowsOnlyIntegers) return false;
                }
                else
                {
                    if (!(char.IsDigit(s[i]) || (allowsSigned && s[i] == '-'))) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Indicate if the given character represents a non-decimal digit (0..9)
        /// </summary>
        /// <param name="c">Character to analyze</param>
        /// <returns>True if char is between 0..9; false instead</returns>
        public static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        /// <summary>
        /// Indicate if the given string is hexadecimal notation
        /// </summary>
        /// <param name="s">String to analyze</param>
        /// <returns>True if the string is a hexadecimal notation; false if not</returns>
        public static bool IsHex(string s)
        {
            if ((null == s) || (0 == s.Length)) return false;

            if (s.Length % 2 > 0) return false; // Hex notation always uses two characters for every byte

            char c;

            for (int i = 0; i < s.Length; i++)
            {
                c = char.ToUpper(s[i]);
                if (!char.IsDigit(c) && c != 'A' && c != 'B' && c != 'C' && c != 'D' && c != 'E' && c != 'F') return false;
            }

            return true;
        }

        /// <summary>
        /// Parse the given string into a float value; returns 0 if parsing fails
        /// </summary>
        /// <param name="s">String to be parsed</param>
        /// <returns>Parsed value; 0 if a parsing issue has been encountered</returns>
        public static double ParseDouble(string s)
        {
            if (!IsNumeric(s)) return 0;

            string[] parts = s.Split(new char[] { ',', '.' });

            if (parts.Length > 2) return 0;
            else if (1 == parts.Length) return double.Parse(s);
            else // 2 == parts.Length
            {
                double decimalDivisor = Math.Pow(10, parts[1].Length);
                double result = double.Parse(parts[0]);
                if (result >= 0) return result + double.Parse(parts[1]) / decimalDivisor;
                else return result - double.Parse(parts[1]) / decimalDivisor;
            }
        }

        /// <summary>
        /// Return the human-readable file size for an arbitrary, 64-bit file size 
        /// The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        /// Source : https://www.somacon.com/p576.php
        /// </summary>
        /// <param name="i">Size to display in a human-readable form</param>
        /// <returns>Given size displayed in a human-readable form</returns>
        public static string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }

        private static Encoding getEncodingCached(string code)
        {
            if (!encodingCache.ContainsKey(code)) encodingCache[code] = Encoding.GetEncoding(code);
            return encodingCache[code];
        }

        /// <summary>
        /// Guess the given stream's encoding
        /// </summary>
        /// <param name="s">Stream to guess the encoding for</param>
        /// <returns>Guessed encoding, if any; else Settings.DefaultTextEncoding</returns>
        public static Encoding GuessTextEncoding(Stream s)
        {
            Ude.CharsetDetector cdet = new Ude.CharsetDetector();
            cdet.Feed(s);
            cdet.DataEnd();
            if (cdet.Charset != null) return getEncodingCached(cdet.Charset);
            else return Settings.DefaultTextEncoding;
        }

        /// <summary>
        /// Trace the given Exception 
        /// - To the ATL logger 
        /// - To the Console, if Settings.OutputStacktracesToConsole is true
        /// </summary>
        /// <param name="e">Exception to trace</param>
        /// <param name="level">Trace level (default : LV_ERROR)=</param>
        public static void TraceException(Exception e, int level = Log.LV_ERROR)
        {
            if (Settings.OutputStacktracesToConsole)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner Exception BEGIN");
                    Console.WriteLine(e.InnerException.Message);
                    Console.WriteLine(e.InnerException.StackTrace);
                    Console.WriteLine("Inner Exception END");
                }
            }
            LogDelegator.GetLogDelegate()(level, e.Message);
            LogDelegator.GetLogDelegate()(level, e.StackTrace);
            if (e.InnerException != null)
            {
                LogDelegator.GetLogDelegate()(level, "Inner Exception BEGIN");
                LogDelegator.GetLogDelegate()(level, e.InnerException.Message);
                LogDelegator.GetLogDelegate()(level, e.InnerException.StackTrace);
                LogDelegator.GetLogDelegate()(level, "Inner Exception END");
            }
        }
    }
}
