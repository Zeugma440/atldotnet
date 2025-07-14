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
        /// Characters for CR LF (Carriage Return / Line Feed)
        /// </summary>
        public static readonly byte[] CR_LF = { 13, 10 };

        /// <summary>
        /// Decimal separators
        /// </summary>
        private static readonly char[] DECIMAL_SEPARATORS = { ',', '.' };

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
        /// Format the given duration using the following format
        ///     DDdHH:MM:SS.UUUU
        ///     OR
        ///     MM:SS.UUUU
        ///     
        ///  Where
        ///     DD is the number of days, if applicable (i.e. durations of less than 1 day won't display the "DDd" part)
        ///     HH is the number of hours, if applicable (i.e. durations of less than 1 hour won't display the "HH:" part)
        ///     MM is the number of minutes, when using MMSS format, this will extend beyond two digits if necessary
        ///     SS is the number of seconds
        ///     UUUU is the number of milliseconds
        /// </summary>
        /// <param name="milliseconds">Duration to format (in milliseconds)</param>
        /// <param name="useMmSsFormat">Format in MM:SS.UUUU format. Default is false</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static string EncodeTimecode_ms(long milliseconds, bool useMmSsFormat = false)
        {
            long seconds = Convert.ToInt64(Math.Floor(milliseconds / 1000.00));

            var encodedString = useMmSsFormat ? EncodeMmSsTimecode_s(seconds) : EncodeTimecode_s(seconds);

            return encodedString + "." + (milliseconds - seconds * 1000);
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
            var h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            var m = Convert.ToInt64(Math.Floor((seconds - 3600.00 * h) / 60));
            var s = seconds - 60 * m - 3600 * h;
            var d = Convert.ToInt32(Math.Floor(h / 24.00));
            if (d > 0) h = h - 24 * d;

            var hStr = h.ToString();
            if (1 == hStr.Length) hStr = "0" + hStr;
            var mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            var sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

            if (d > 0) return d + "d " + hStr + ":" + mStr + ":" + sStr;
            if (h > 0) return hStr + ":" + mStr + ":" + sStr;
            return mStr + ":" + sStr;
        }

        /// <summary>
        /// Format the given duration using the following format
        ///     MM:SS
        ///     
        ///  Where
        ///     MM is the number of minutes, this will extend beyond two digits if necessary
        ///     SS is the number of seconds
        /// </summary>
        /// <param name="seconds">Duration to format (in seconds)</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static string EncodeMmSsTimecode_s(long seconds)
        {
            var m = Convert.ToInt64(Math.Floor(seconds / 60.00));
            var s = seconds - 60 * m;

            var mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            var sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

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
            if (string.IsNullOrEmpty(timeCode)) return result;

            bool valid = false;

            if (DateTime.TryParse(timeCode, out var dateTime))
            {
                // Handle classic cases hh:mm, hh:mm:ss.ddd (the latter being the spec)
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
                    else if (parts[^1].Contains(','))
                    {
                        string[] subPart = parts[^1].Split(',');
                        parts[^1] = subPart[0];
                        milliseconds = int.Parse(subPart[1]);
                    }
                    var seconds = int.Parse(parts[^1]);
                    var minutes = int.Parse(parts[^2]);
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
        /// Check the given string against the YYYY-MM-DD or YYYY/MM/DD date formats
        /// </summary>
        /// <param name="dateStr">String to check</param>
        /// <returns>True if formatting is valid; false if not</returns>
        public static bool CheckDateFormat(string dateStr)
        {
            var parts = dateStr.Contains('/') ? dateStr.Split('/') : dateStr.Split('-');
            // General formatting
            if (parts.Length != 3) return false;
            if (parts[0].Length != 4 || !IsNumeric(parts[0])) return false;
            if (parts[1].Length != 2 || !IsNumeric(parts[1])) return false;
            if (parts[2].Length != 2 || !IsNumeric(parts[2])) return false;
            // Range checks
            var intVal = int.Parse(parts[0]);
            if (intVal < 1900) return false;
            intVal = int.Parse(parts[1]);
            if (intVal is < 0 or > 12) return false;
            intVal = int.Parse(parts[2]);
            return intVal is >= 0 and <= 31;
        }

        /// <summary>
        /// Check the given string against the hh:mm:ss time format
        /// </summary>
        /// <param name="timeStr">String to check</param>
        /// <returns>True if formatting is valid; false if not</returns>
        public static bool CheckTimeFormat(string timeStr)
        {
            var parts = timeStr.Split(':');
            // General formatting
            if (parts.Length != 3) return false;
            if (parts[0].Length != 2 || !IsNumeric(parts[0])) return false;
            if (parts[1].Length != 2 || !IsNumeric(parts[1])) return false;
            if (parts[2].Length != 2 || !IsNumeric(parts[2])) return false;
            // Range checks
            var intVal = int.Parse(parts[0]);
            if (intVal is < 0 or > 23) return false;
            intVal = int.Parse(parts[1]);
            if (intVal is < 0 or > 59) return false;
            intVal = int.Parse(parts[2]);
            return intVal is >= 0 and <= 59;
        }

        /// <summary>
		/// Try to extract a DateTime from a digits-only string
        /// Accepted values :
        ///     YYYY
        ///     YYYYMM
        ///     YYYYMMDD
		/// </summary>
		/// <param name="str">String to extract the date from</param>
        /// <param name="date">DateTime to populate</param>
		/// <returns>True if a valid DateTime has been found inside str (date will be valued); false instead (date will be null)</returns>
		public static bool TryExtractDateTimeFromDigits(string str, out DateTime? date)
        {
            date = null;
            if (!IsNumeric(str, true, false)) return false;

            switch (str.Length)
            {
                case 4:
                    int year = int.Parse(str);
                    if (year >= DateTime.MinValue.Year && year <= DateTime.MaxValue.Year)
                    {
                        date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        return true;
                    }
                    break;
                case 6:
                    year = int.Parse(str.Substring(0, 4));
                    int month = int.Parse(str.Substring(4, 2));
                    if (year >= DateTime.MinValue.Year && year <= DateTime.MaxValue.Year && month <= DateTime.MaxValue.Month)
                    {
                        date = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                        return true;
                    }
                    break;
                case 8:
                    year = int.Parse(str.Substring(0, 4));
                    month = int.Parse(str.Substring(4, 2));
                    int day = int.Parse(str.Substring(6, 2));
                    if (year >= DateTime.MinValue.Year && year <= DateTime.MaxValue.Year && month <= DateTime.MaxValue.Month && day <= DateTime.MaxValue.Day)
                    {
                        date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                        return true;
                    }
                    break;
            }
            return false;
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

            if (result.Length > length) result = result[..length];
            else if (result.Length < length)
            {
                result = padRight ? result.PadRight(length, paddingChar) : result.PadLeft(length, paddingChar);
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
            if (value == null) return false;

            value = value.Trim();
            if (value.Length <= 0) return false;

            // Numeric conversion
            if (float.TryParse(value, out var f)) return !ApproxEquals(f, 0);

            // Boolean conversion
            value = value.ToLower();
            return "true".Equals(value);
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
                char t = s[i];
                if (t == '.' || t == ',')
                {
                    if (allowsOnlyIntegers) return false;
                }
                else
                {
                    if (!(char.IsDigit(t) || (allowsSigned && 0 == i && t == '-'))) return false;
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
            return c is >= '0' and <= '9';
        }

        /// <summary>
        /// Returns the first successive series of digits converted to integer
        /// NB : Doesn't detect negative integers ('-' char is ignored)
        /// </summary>
        /// <param name="s">String to extract the integer from</param>
        /// <returns>First successive series of digits of the given string converted to integer; -1 if nothing found</returns>
        public static int ParseFirstIntegerPart(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;

            bool insideInt = false;
            StringBuilder sb = new StringBuilder();
            foreach (var t in s)
            {
                if (IsDigit(t))
                {
                    if (!insideInt) insideInt = true;
                    sb.Append(t);
                }
                else
                {
                    if (insideInt) break;
                }
            }

            if (sb.Length > 0)
            {
                long number = long.Parse(sb.ToString());
                if (number > int.MaxValue) number = 0;
                return (int)number;
            }
            return -1;
        }

        /// <summary>
        /// Indicate if the given string is hexadecimal notation
        /// </summary>
        /// <param name="s">String to analyze</param>
        /// <returns>True if the string is a hexadecimal notation; false if not</returns>
        public static bool IsHex(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (1 == s.Length % 2) return false; // Hex notation always uses two characters for every byte

            foreach (var t in s)
            {
                var c = char.ToUpper(t);
                if (!char.IsDigit(c) && c != 'A' && c != 'B' && c != 'C' && c != 'D' && c != 'E' && c != 'F') return false;
            }

            return true;
        }

        /// <summary>
        /// Parse the given string into an array of bytes, decoding the given string as an hexadecimal value
        /// </summary>
        /// <param name="s">String to be parsed</param>
        /// <returns>Parsed value; empty array if a parsing issue has been encountered</returns>
        public static byte[] ParseHex(string s)
        {
            if (!IsHex(s)) return Array.Empty<byte>();

            byte[] arr = new byte[s.Length >> 1];
            for (int i = 0; i < s.Length >> 1; ++i)
            {
                arr[i] = (byte)((getHexVal(s[i << 1]) << 4) + getHexVal(s[(i << 1) + 1]));
            }

            return arr;
        }

        public static int getHexVal(char hex)
        {
            int val = (byte)hex;
            var letters = val < 97 ? 55 : 87;
            return val - (val < 58 ? 48 : letters);
        }

        /// <summary>
        /// Parse the given string into a float value; returns 0 if parsing fails
        /// </summary>
        /// <param name="s">String to be parsed</param>
        /// <returns>Parsed value; 0 if a parsing issue has been encountered</returns>
        public static double ParseDouble(string s)
        {
            if (!IsNumeric(s)) return double.NaN;

            string[] parts = s.Split(DECIMAL_SEPARATORS);

            switch (parts.Length)
            {
                case > 2:
                    return double.NaN;
                case 1:
                    return double.Parse(s);
            }

            // Other possibilities : 2 == parts.Length
            double decimalDivisor = Math.Pow(10, parts[1].Length);
            double result = double.Parse(parts[0]);
            if (result >= 0) return result + double.Parse(parts[1]) / decimalDivisor;
            return result - double.Parse(parts[1]) / decimalDivisor;
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
            long absolute_i = i < 0 ? -i : i;
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = i >> 50;
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = i >> 40;
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = i >> 30;
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = i >> 20;
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = i >> 10;
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
            readable = readable / 1024;
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
            return Settings.DefaultTextEncoding;
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
                    Console.WriteLine(@"Inner Exception BEGIN");
                    Console.WriteLine(e.InnerException.Message);
                    Console.WriteLine(e.InnerException.StackTrace);
                    Console.WriteLine(@"Inner Exception END");
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

        /// <summary>
        /// Indicate whether the given double is approiximately equal to the given value
        /// (tolerance is 0.001)
        /// </summary>
        /// <param name="f">Double to compare</param>
        /// <param name="val">Value to compare to</param>
        /// <returns>True if both values are approximately equal; false if not</returns>
        public static bool ApproxEquals(double f, int val)
        {
            return Math.Abs(f - val) < 0.001;
        }

        /// <summary>
        /// Generate a long random positive number between the given limits, using the given generator
        /// </summary>
        /// <param name="min">Lower limit (included; must be positive) - default : 0</param>
        /// <param name="max">Higher limit (included; must be positive) - default : long.MaxValue</param>
        /// <param name="rand"></param>
        /// <returns></returns>
        public static ulong LongRandom(Random rand, long min = 0, long max = long.MaxValue)
        {
            byte[] buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (ulong)(Math.Abs(longRand % (max - min)) + min);
        }
    }
}
