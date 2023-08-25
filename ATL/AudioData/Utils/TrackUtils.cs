using ATL.AudioData.IO;
using Commons;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate values extracted from tracks metadata
    /// </summary>
    internal static class TrackUtils
    {
        /// <summary>
        /// Extract the track number from the given string
        /// </summary>
        /// <param name="str">Raw "track" field in string form</param>
        /// <returns>Track number, in integer form; 0 if no track number has been found</returns>
        public static ushort ExtractTrackNumber(string str)
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

            if (i > 0)
            {
                long number = long.Parse(str.Substring(0, i));
                if (number > ushort.MaxValue) number = 0;
                return (ushort)number;
            }


            // == If everything above fails...

            // This case covers both single track numbers and (trk/total) formatting
            Regex regex = new Regex("\\d+");

            Match match = regex.Match(str);
            // First match is directly returned
            if (match.Success)
            {
                long number = long.Parse(match.Value);
                if (number > ushort.MaxValue) number = 0;
                return (ushort)number;
            }
            return 0;
        }

        /// <summary>
        /// Extract the total track number from the given string
        /// </summary>
        /// <param name="str">Raw "track" field in string form</param>
        /// <returns>Total track number, in integer form; 0 if no total track number has been found</returns>
        public static ushort ExtractTrackTotal(string str)
        {
            // == Optimizations (Regex are too damn expensive to use them lazily)

            // Invalid inputs
            if (null == str) return 0;
            str = str.Trim();
            if (str.Length < 1) return 0;
            if (!str.Contains("/")) return 0;

            int delimiterOffset = str.IndexOf('/');
            if (delimiterOffset == str.Length - 1) return 0;

            // Try extracting the total manually when "/" is followed by a number
            int i = delimiterOffset;

            // Skip any other non-number character
            while (i < str.Length - 1 && !Utils.IsDigit(str[++i]))
            {
                // Keep advancing
            }
            if (!Utils.IsDigit(str[i])) return 0; // No number found

            int delimiterEnd = i;
            while (i < str.Length - 1 && Utils.IsDigit(str[++i]))
            {
                // Keep advancing
            }
            if (!Utils.IsDigit(str[i])) i--;

            if (i > delimiterOffset)
            {
                long number = long.Parse(str.Substring(delimiterEnd, i - delimiterEnd + 1));
                if (number > ushort.MaxValue) number = 0;
                return (ushort)number;
            }


            // == If everything above fails...
            string pattern = "/[\\s]*(\\d+)"; // Any continuous sequence of numbers after a "/"
            Match match = Regex.Match(str, pattern);
            if (match.Success)
            {
                long number = long.Parse(match.Value);
                if (number > ushort.MaxValue) number = 0;
                return (ushort)number; // First match is directly returned
            }
            return 0;
        }

        /// <summary>
        /// Extract rating level from the given string
        /// </summary>
        /// <param name="ratingString">Raw "rating" field in string form</param>
        /// <param name="convention">Tagging convention (see MetaDataIO.RC_XXX constants)</param>
        /// <returns>Rating level, in float form (0 = 0% to 1 = 100%)</returns>
        public static double? DecodePopularity(string ratingString, int convention)
        {
            if ((null == ratingString) || (0 == ratingString.Trim().Length)) return null;

            if (Utils.IsNumeric(ratingString))
            {
                ratingString = ratingString.Replace(',', '.');
                return DecodePopularity(Utils.ParseDouble(ratingString), convention);
            }

            // If the field is only one byte long, rating is evaluated numerically
            if (1 == ratingString.Length) return DecodePopularity((byte)ratingString[0], convention);

            // Exceptional case : rating is stored in the form of stars
            // NB : Having just one star is embarassing, since it falls into the "one-byte-long field" case processed above
            // It won't be interpretated as a star rating, as those are very rare
            Regex regex = new Regex("\\*+");

            Match match = regex.Match(ratingString.Trim());
            // First match is directly returned
            if (match.Success)
            {
                return match.Value.Length / 5.0;
            }

            return null;
        }

        /// <summary>
        /// Extract rating level from the given byte
        /// </summary>
        /// <param name="rating">Raw "rating" field in byte form</param>
        /// <param name="convention">Tagging convention (see MetaDataIO.RC_XXX constants)</param>
        /// <returns>Rating level, in float form (0 = 0% to 1 = 100%)</returns>
        public static double DecodePopularity(double rating, int convention)
        {
            switch (convention)
            {
                case MetaDataIO.RC_ASF:

                    if (rating < 1) return 0;
                    else if (rating < 25) return 0.2;
                    else if (rating < 50) return 0.4;
                    else if (rating < 75) return 0.6;
                    else if (rating < 99) return 0.8;
                    else return 1;

                case MetaDataIO.RC_APE:

                    if (rating < 5.1) return rating / 5.0; // Stored as float
                    else if (rating < 10) return 0;           // Stored as scale of 0..100
                    else if (rating < 20) return 0.1;
                    else if (rating < 30) return 0.2;
                    else if (rating < 40) return 0.3;
                    else if (rating < 50) return 0.4;
                    else if (rating < 60) return 0.5;
                    else if (rating < 70) return 0.6;
                    else if (rating < 80) return 0.7;
                    else if (rating < 90) return 0.8;
                    else if (rating < 100) return 0.9;
                    else return 1;

                default:                // ID3v2 convention
                    if (rating > 10)
                    {
                        // De facto conventions (windows explorer, mediaMonkey, musicBee)
                        if (rating < 54) return 0.1;
                        // 0.2 is value "1"; handled in two blocks
                        else if (rating < 64) return 0.3;
                        else if (rating < 118) return 0.4;
                        else if (rating < 128) return 0.5;
                        else if (rating < 186) return 0.6;
                        else if (rating < 196) return 0.7;
                        else if (rating < 242) return 0.8;
                        else if (rating < 255) return 0.9;
                        else return 1;
                    }
                    else if (rating > 5) // Between 5 and 10
                    {
                        return rating / 10.0;
                    }
                    else // Between 1 and 5
                    {
                        return rating / 5.0;
                    }
            }
        }

        /// <summary>
        /// Return the given popularity encoded with the given convention
        /// </summary>
        /// <param name="ratingStr">Popularity (note 0-5), represented in String form (e.g. "2.5")</param>
        /// <param name="convention">Convention type (See MetaDataIO.RC_XXX constants)</param>
        /// <returns>Popularity encoded with the given convention</returns>
        public static int EncodePopularity(string ratingStr, int convention)
        {
            double rating = Utils.ParseDouble(ratingStr);
            return EncodePopularity(rating, convention);
        }
        /// <summary>
        /// Return the given popularity encoded with the given convention
        /// </summary>
        /// <param name="rating">Popularity (note 0-5)</param>
        /// <param name="convention">Convention type (See MetaDataIO.RC_XXX constants)</param>
        /// <returns>Popularity encoded with the given convention</returns>
        public static int EncodePopularity(double rating, int convention)
        {
            switch (convention)
            {
                case MetaDataIO.RC_ASF:

                    if (rating < 1) return 0;
                    if (rating < 2) return 1;
                    if (rating < 3) return 25;
                    if (rating < 4) return 50;
                    if (rating < 5) return 75;
                    return 99;

                case MetaDataIO.RC_APE:

                    if (rating < 0.5) return 0;           // Stored as scale of 0..100
                    if (rating < 1) return 10;
                    if (rating < 1.5) return 20;
                    if (rating < 2) return 30;
                    if (rating < 2.5) return 40;
                    if (rating < 3) return 50;
                    if (rating < 3.5) return 60;
                    if (rating < 4) return 70;
                    if (rating < 4.5) return 80;
                    if (rating < 5) return 90;
                    return 100;

                default:                // ID3v2 convention
                    if (rating < 0.5) return 0;
                    if (rating < 1) return 13;
                    if (rating < 1.5) return 1;
                    if (rating < 2) return 54;
                    if (rating < 2.5) return 64;
                    if (rating < 3) return 118;
                    if (rating < 3.5) return 128;
                    if (rating < 4) return 186;
                    if (rating < 4.5) return 196;
                    if (rating < 5) return 242;
                    return 255;
            }
        }

        /// <summary>
        /// Finds a year (4 consecutive numeric chars) in a string
        /// </summary>
        /// <param name="str">String to search the year into</param>
        /// <returns>Found year in integer form; 0 if no year has been found</returns>
   		public static int ExtractIntYear(string str)
        {
            string resStr = ExtractStrYear(str);
            return 0 == resStr.Length ? 0 : int.Parse(resStr);
        }

        /// <summary>
		/// Find a year (4 consecutive numeric chars) in a string
		/// </summary>
		/// <param name="str">String to search the year into</param>
		/// <returns>Found year in string form; "" if no year has been found</returns>
		public static string ExtractStrYear(string str)
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
                    return str[..4];
                }
                // Ends with 4 numeric chars
                if (char.IsNumber(str[^1]) && char.IsNumber(str[^2]) && char.IsNumber(str[^3]) && char.IsNumber(str[^4]))
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

        /// <summary>
        /// Format the given track or disc number string according to the given parameters
        ///     - If a single figure is given, result is a single figure
        ///     - If two figures are given separated by "/" (e.g. "3/5"), result is both figures formatted and separated by "/"
        /// The number of digits and leading zeroes to use can be customized.
        /// </summary>
        /// <param name="value">Value to format; may contain separators. Examples of valid values : "1", "03/5", "4/005"...</param>
        /// <param name="overrideExistingFormat">If false, the given value will be formatted using <c>existingDigits</c></param>
        /// <param name="existingDigits">Target number of digits for each of the formatted values; 0 if no constraint</param>
        /// <param name="useLeadingZeroes">If true, and <c>overrideExistingFormat</c> is also true, the given value will be formatted using <c>total</c></param>
        /// <param name="total">Total number of tracks/albums to align the given value with while formatting. The length of the string will be used, not its value</param>
        /// <returns>Given track or disc number(s) formatted according to the given paramaters</returns>
        public static string FormatWithLeadingZeroes(string value, bool overrideExistingFormat, int existingDigits, bool useLeadingZeroes, string total)
        {
            if (value.Contains("/"))
            {
                string[] parts = value.Split('/');
                return formatWithLeadingZeroesInternal(parts[0], overrideExistingFormat, existingDigits, useLeadingZeroes, parts[1]) + "/" + formatWithLeadingZeroesInternal(parts[1], overrideExistingFormat, existingDigits, useLeadingZeroes, parts[1]);
            }
            else return formatWithLeadingZeroesInternal(value, overrideExistingFormat, existingDigits, useLeadingZeroes, total);
        }

        /// <summary>
        /// Format the given value according to the given parameters
        /// </summary>
        /// <param name="value">Value to format; should not contain separators</param>
        /// <param name="overrideExistingFormat">If false, the given value will be formatted using <c>existingDigits</c></param>
        /// <param name="existingDigits">Target number of digits for the formatted value; 0 if no constraint</param>
        /// <param name="useLeadingZeroes">If true, and <c>overrideExistingFormat</c> is also true, the given value will be formatted using <c>total</c></param>
        /// <param name="total">Total number of tracks/albums to align the given value with while formatting. The length of the string will be used, not its value</param>
        /// <returns>Given track or disc number(s) formatted according to the given paramaters</returns>
        private static string formatWithLeadingZeroesInternal(string value, bool overrideExistingFormat, int existingDigits, bool useLeadingZeroes, string total)
        {
            if (!overrideExistingFormat && existingDigits > 0) return Utils.BuildStrictLengthString(value, existingDigits, '0', false);
            int totalLength = (total != null && total.Length > 1) ? total.Length : 2;
            return useLeadingZeroes ? Utils.BuildStrictLengthString(value, totalLength, '0', false) : value;
        }

        /// <summary>
        /// Format the given DateTime to the most concise human-readable string
        /// Subsets of ISO 8601 will be used : yyyy, yyyy-MM, yyyy-MM-dd, yyyy-MM-ddTHH, yyyy-MM-ddTHH:mm, yyyy-MM-ddTHH:mm:ss
        /// </summary>
        /// <param name="dateTime">DateTime to format</param>
        /// <returns>Human-readable string representation of the given DateTime with relevant information only</returns>
        public static string FormatISOTimestamp(DateTime dateTime)
        {
            bool includeTime = (dateTime.Hour > 0 || dateTime.Minute > 0 || dateTime.Second > 0);
            return FormatISOTimestamp(
                Utils.BuildStrictLengthString(dateTime.Year, 4, '0', false),
                Utils.BuildStrictLengthString(dateTime.Day, 2, '0', false),
                Utils.BuildStrictLengthString(dateTime.Month, 2, '0', false),
                includeTime ? Utils.BuildStrictLengthString(dateTime.Hour, 2, '0', false) : "",
                includeTime ? Utils.BuildStrictLengthString(dateTime.Minute, 2, '0', false) : "",
                includeTime ? Utils.BuildStrictLengthString(dateTime.Second, 2, '0', false) : ""
            );
        }

        /// <summary>
        /// Format the given elemnts to the most concise human-readable string
        /// Subsets of ISO 8601 will be used : yyyy, yyyy-MM, yyyy-MM-dd, yyyy-MM-ddTHH, yyyy-MM-ddTHH:mm, yyyy-MM-ddTHH:mm:ss
        /// </summary>
        /// <param name="year">Year</param>
        /// <param name="dayMonth">Day and month (DDMM format)</param>
        /// <param name="hoursMinutesSeconds">Time (hhmm format)</param>
        /// <returns>Human-readable string representation of the given DateTime with relevant information only</returns>
        public static string FormatISOTimestamp(string year, string dayMonth, string hoursMinutesSeconds)
        {
            string day = "";
            string month = "";
            if (Utils.IsNumeric(dayMonth) && (4 == dayMonth.Length))
            {
                month = dayMonth.Substring(2, 2);
                day = dayMonth.Substring(0, 2);
            }


            string hour = "";
            string minutes = "";
            string seconds = "";
            if (Utils.IsNumeric(hoursMinutesSeconds))
            {
                if (hoursMinutesSeconds.Length >= 4)
                {
                    hour = hoursMinutesSeconds.Substring(0, 2);
                    minutes = hoursMinutesSeconds.Substring(2, 2);
                }
                if (hoursMinutesSeconds.Length >= 6)
                {
                    seconds = hoursMinutesSeconds.Substring(4, 2);
                }
            }

            return FormatISOTimestamp(year, day, month, hour, minutes, seconds);
        }

        /// <summary>
        /// Format the given elemnts to the most concise human-readable string
        /// Subsets of ISO 8601 will be used : yyyy, yyyy-MM, yyyy-MM-dd, yyyy-MM-ddTHH, yyyy-MM-ddTHH:mm, yyyy-MM-ddTHH:mm:ss
        /// </summary>
        /// <param name="year">Year</param>
        /// <param name="day">Day</param>
        /// <param name="month">Month</param>
        /// <param name="hour">Hours</param>
        /// <param name="minutes">Minutes</param>
        /// <param name="seconds">Seconds</param>
        /// <returns>Human-readable string representation of the given DateTime with relevant information only</returns>
        public static string FormatISOTimestamp(string year, string day, string month, string hour, string minutes, string seconds)
        {
            StringBuilder result = new StringBuilder();

            if (4 == year.Length && Utils.IsNumeric(year)) result.Append(year);
            if (2 == month.Length && Utils.IsNumeric(month)) result.Append("-").Append(month);
            if (2 == day.Length && Utils.IsNumeric(day)) result.Append("-").Append(day);
            if (2 == hour.Length && Utils.IsNumeric(hour)) result.Append("T").Append(hour);
            if (2 == minutes.Length && Utils.IsNumeric(minutes)) result.Append(":").Append(minutes);
            if (2 == seconds.Length && Utils.IsNumeric(seconds)) result.Append(":").Append(seconds);

            return result.ToString();
        }

        /// <summary>
        /// Compute new size of the padding area according to the given parameters
        /// </summary>
        /// <param name="initialPaddingOffset">Initial offset of the padding zone</param>
        /// <param name="initialPaddingSize">Initial size of the padding zone</param>
        /// <param name="initialTagSize">Initial size of the tag area</param>
        /// <param name="currentTagSize">Current size of the tag area</param>
        /// <returns>New size to give to the padding area</returns>
        public static long ComputePaddingSize(long initialPaddingOffset, long initialPaddingSize, long initialTagSize, long currentTagSize)
        {
            return ComputePaddingSize(initialPaddingOffset, initialPaddingSize, initialTagSize - currentTagSize);
        }

        /// <summary>
        /// Compute new size of the padding area according to the given parameters
        /// </summary>
        /// <param name="initialPaddingOffset">Initial offset of the padding zone</param>
        /// <param name="initialPaddingSize">Initial size of the padding zone</param>
        /// <param name="deltaSize">Variation of padding zone size
        ///     lower than 0 => Metadata size has increased => Padding should decrease
        ///     higher than 0 => Metadata size has decreased => Padding should increase
        /// </param>
        /// <returns>New size to give to the padding area</returns>
        public static long ComputePaddingSize(long initialPaddingOffset, long initialPaddingSize, long deltaSize)
        {
            long paddingSizeToWrite = Settings.AddNewPadding ? Settings.PaddingSize : 0;
            // Padding size is constrained by either its initial size or the max size defined in settings
            if (initialPaddingOffset > -1)
            {
                if (deltaSize <= 0) paddingSizeToWrite = Math.Max(0, initialPaddingSize + deltaSize);
                else if (initialPaddingSize >= Settings.PaddingSize) paddingSizeToWrite = initialPaddingSize;
                else paddingSizeToWrite = Math.Min(initialPaddingSize + deltaSize, Settings.PaddingSize);
            }
            return paddingSizeToWrite;
        }
    }
}
