using System;
using System.Text;

namespace Commons
{
    /// <summary>
    /// General utility class
    /// </summary>
    public static class Utils
    {
        private static Encoding latin1Encoding = Encoding.GetEncoding("ISO-8859-1");
        public static string UNICODE_INVISIBLE_EMPTY = "\uFEFF";


        /// <summary>
        /// Defines a delegate that does not carry any argument (useful for "pinging")
        /// </summary>
        public delegate void voidDelegate();

        public static Encoding Latin1Encoding { get { return latin1Encoding; } }


        /// <summary>
        /// Transforms the given string so that is becomes non-null
        /// </summary>
        /// <param name="value">String to protect</param>
        /// <returns>Given string if non-null; else empty string</returns>
        public static String ProtectValue(String value)
        {
            return (null == value) ? "" : value;
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
            String hStr, mStr, sStr;
            long s;
            int d;

            h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            m = Convert.ToInt64(Math.Floor((seconds - 3600.00 * h) / 60));
            s = seconds - (60 * m) - (3600 * h);
            d = Convert.ToInt32(Math.Floor(h / 24.00));
            if (d > 0) h = h - (24 * d);

            hStr = h.ToString();
            if (1 == hStr.Length) hStr = "0" + hStr;
            mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

            if (d > 0)
            {
                return d + "d " + hStr + ":" + mStr + ":" + sStr;
            }
            else
            {
                if (h > 0)
                {
                    return hStr + ":" + mStr + ":" + sStr;
                }
                else
                {
                    return mStr + ":" + sStr;
                }
            }
        }

        // TODO Doc
        static public int DecodeTimecodeToMs(string timeCode)
        {
            int result = -1;
            DateTime dateTime = new DateTime();
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

                if (timeCode.Contains(":"))
                {
                    valid = true;
                    string[] parts = timeCode.Split(':');
                    if (parts[parts.Length - 1].Contains("."))
                    {
                        string[] subPart = parts[parts.Length - 1].Split('.');
                        parts[parts.Length - 1] = subPart[0];
                        milliseconds = int.Parse(subPart[1]);
                    }
                    seconds = int.Parse(parts[parts.Length - 1]);
                    minutes = int.Parse(parts[parts.Length - 2]);
                    if (parts.Length >= 3)
                    {
                        string[] subPart = parts[parts.Length - 3].Split('d');
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
        /// Strips the given string from all ending null '\0' characters
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
        /// Transforms the given string to format with a given length
        ///  - If the given length is shorter than the actual length of the string, it will be truncated
        ///  - If the given length is longer than the actual length of the string, it will be right/left-padded with a given character
        /// </summary>
        /// <param name="value">String to transform</param>
        /// <param name="length">Target length of the final string</param>
        /// <param name="paddingChar">Character to use if padding is needed</param>
        /// <param name="padRight">True if the padding has to be done on the right-side of the target string; 
        /// false if the padding has to be done on the left-side (optional; default value = true)</param>
        /// <returns>Reprocessed string of given length, according to rules documented in the method description</returns>
        public static string BuildStrictLengthString(string value, int length, char paddingChar, bool padRight = true)
        {
            string result = (null == value) ? "" : value;

            if (result.Length > length) result = result.Substring(0, length);
            else if (result.Length < length)
            {
                if (padRight) result = result.PadRight(length, paddingChar);
                else result = result.PadLeft(length, paddingChar);
            }

            return result;
        }

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
            } else
            {
                result = data;
            }

            return result;
        }

        /// <summary>
        /// Coverts given string value to boolean.
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
                        return (f != 0);
                    }
                    else
                    {
                        value = value.ToLower();
                        return ("true".Equals(value));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The method to Decode your Base64 strings.
        /// </summary>
        /// <param name="encodedData">The String containing the characters to decode.</param>
        /// <param name="s">The Stream where the resulting decoded data will be written.</param>
        /// Source : http://blogs.microsoft.co.il/blogs/mneiter/archive/2009/03/22/how-to-encoding-and-decoding-base64-strings-in-c.aspx
        public static byte[] DecodeFrom64(byte[] encodedData)
        {
            if (encodedData.Length % 4 > 0) throw new FormatException("Size must me multiple of 4");

            char[] encodedDataChar = new char[encodedData.Length];
            Latin1Encoding.GetChars(encodedData, 0, encodedData.Length, encodedDataChar, 0); // Optimized for large data

            return System.Convert.FromBase64CharArray(encodedDataChar, 0, encodedDataChar.Length);
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

            System.Convert.ToBase64CharArray(data, 0, data.Length, dataChar, 0);

            return Utils.Latin1Encoding.GetBytes(dataChar);
        }

        /// <summary>
        /// Indicates if the given string is exclusively composed of digital charachers
        /// 
        /// NB1 : decimal separators '.' and ',' are tolerated except if allowsOnlyIntegers argument is set to True
        /// NB2 : whitespaces ' ' are not tolerated
        /// NB3 : any alternate notation (e.g. exponent, hex) is not tolerated
        /// </summary>
        /// <param name="s">String to analyze</param>
        /// <param name="allowsOnlyIntegers">Set to True if IsNumeric should reject decimal values; default = false</param>
        /// <returns>True if the string is a digital value; false if not</returns>
        public static bool IsNumeric(string s, bool allowsOnlyIntegers = false)
        {
            if ((null == s) || (0 == s.Length)) return false;

            for (int i = 0; i < s.Length; i++)
            {
                if ((s[i] == '.') || (s[i] == ','))
                {
                    if (allowsOnlyIntegers) return false;
                }
                else
                {
                    if (!char.IsDigit(s[i]) && !(s[i] == '-') ) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Indicates if the given string is hexadecimal notation
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
                if (!char.IsDigit(c) && c!='A' && c != 'B' && c != 'C' && c != 'D' && c != 'E' && c != 'F') return false;
            }

            return true;
        }

        /// <summary>
        /// Parses the given string into a Double value; returns 0 if parsing fails
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
    }
}
