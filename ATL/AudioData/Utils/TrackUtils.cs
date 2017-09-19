using Commons;
using System;
using System.Text.RegularExpressions;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate values extracted from tracks metadata
    /// </summary>
    public static class TrackUtils
    {
        /// <summary>
        /// Extract the track number from the given string
        /// </summary>
        /// <param name="TrackString">Raw "track" field in string form</param>
        /// <returns>Track number, in integer form; 0 if no track number has been found</returns>
        public static ushort ExtractTrackNumber(String str)
        {
            // == Optimizations (Regex are too damn expensive to use them lazily)

            // Invalid inputs
            if (null == str) return 0;
            str = str.Trim();
            if (str.Length < 1) return 0;

            // Obvious case : string begins with a number
            int i = 0;
            while (char.IsNumber(str[i]))
            {
                i++;
                if (str.Length == i) break;
            }

            if (i > 0) return ushort.Parse(str.Substring(0, i));


            // == If everything above fails...

            // This case covers both single track numbers and (trk/total) formatting
            Regex regex = new Regex("\\d+");

            Match match = regex.Match(str);
            // First match is directly returned
            if (match.Success)
            {
                return ushort.Parse(match.Value);
            }
            return 0;
        }

        /// <summary>
        /// Extract rating level from the given string
        /// </summary>
        /// <param name="RatingString">Raw "rating" field in string form</param>
        /// <returns>Rating level, in integer form</returns>
        public static ushort ExtractIntRating(String RatingString)
        {
            if ((null == RatingString) || (0 == RatingString.Trim().Length)) return 0;

            // == Rating in the form of stars ==
            Regex regex = new Regex("\\*+");

            Match match = regex.Match(RatingString.Trim());
            // First match is directly returned
            if (match.Success)
            {
                return (ushort)match.Value.Length;
            }

            if (Utils.IsNumeric(RatingString)) return ExtractIntRating(Byte.Parse(RatingString));

            // If the field is only one byte long, rating is evaluated numerically
            if (1 == RatingString.Length) return ExtractIntRating((byte)RatingString[0]);

            return 0;
        }

        /// <summary>
        /// Extract rating level from the given byte
        /// </summary>
        /// <param name="Rating">Raw "rating" field in byte form</param>
        /// <returns>Rating level, in integer form</returns>
        public static ushort ExtractIntRating(byte Rating)
        {
            ushort result = Rating;

            // Popularity-meter notation
            // Compatible with Windows Explorer rating notation
            if (result > 31)
            {
                if (result < 96) return 2;
                else if (result < 160) return 3;
                else if (result < 224) return 4;
                else return 5;
            }
            else return result;
        }

        /// <summary>
        /// Finds a year (4 consecutive numeric chars) in a string
        /// </summary>
        /// <param name="str">String to search the year into</param>
        /// <returns>Found year in integer form; 0 if no year has been found</returns>
   		public static int ExtractIntYear(String str)
		{
            String resStr = ExtractStrYear(str);
            if (0 == resStr.Length) return 0; else return Int32.Parse(resStr);
        }

        /// <summary>
		/// Finds a year (4 consecutive numeric chars) in a string
		/// </summary>
		/// <param name="str">String to search the year into</param>
		/// <returns>Found year in string form; "" if no year has been found</returns>
		public static String ExtractStrYear(String str)
        {
            // == Optimizations (Regex are too damn expensive to use them lazily)

            // Invalid inputs
            if (null == str) return "";
            str = str.Trim();
            if (str.Length < 4) return "";

            // Obvious plain year
            if (str.Length > 3)
            {
                // Begins with 4 numeric chars
                if (char.IsNumber(str[0]) && char.IsNumber(str[1]) && char.IsNumber(str[2]) && char.IsNumber(str[3]))
                {
                    return str.Substring(0, 4);
                }
                // Ends with 4 numeric chars
                if (char.IsNumber(str[str.Length - 1]) && char.IsNumber(str[str.Length - 2]) && char.IsNumber(str[str.Length - 3]) && char.IsNumber(str[str.Length - 4]))
                {
                    return str.Substring(str.Length - 4, 4);
                }
            }

            // == If everything above fails...
            Regex regex = new Regex("\\d{4}");

            Match match = regex.Match(str.Trim());
            // First match is directly returned
            if (match.Success)
            {
                return match.Value;
            }
            return "";
		}


    }
}
