using ATL.AudioData.IO;
using System;
using System.Text;
using System.Text.RegularExpressions;
using Commons;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate values extracted from tracks metadata
    /// </summary>
    internal static class TrackUtils
    {
        // A number
        private static readonly Lazy<Regex> rxNumber = new(() => new Regex("\\d+", RegexOptions.None, TimeSpan.FromMilliseconds(100)));
        // A number with 4 digits
        private static readonly Lazy<Regex> rxNumber4 = new(() => new Regex("\\d{4}", RegexOptions.None, TimeSpan.FromMilliseconds(100)));
        // Any continuous sequence of numbers after a "/"
        private static readonly Lazy<Regex> rxSlashNumbers = new(() => new Regex("/[\\s]*(\\d+)", RegexOptions.None, TimeSpan.FromMilliseconds(100)));
        // Stars
        private static readonly Lazy<Regex> rxStars = new(() => new Regex("\\*+", RegexOptions.None, TimeSpan.FromMilliseconds(100)));

        /// <summary>
        /// Extract the track number from the given string
        /// </summary>
        /// <param name="str">Raw "track" field in string form</param>
        /// <returns>Track number, in integer form; 0 if no track number has been found</returns>
        public static ushort ExtractTrackNumber(string str)
        {
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
            Match match = rxNumber.Value.Match(str);
            // First match is directly returned
            if (!match.Success) return 0;

            long number2 = long.Parse(match.Value);
            if (number2 > ushort.MaxValue) number2 = 0;
            return (ushort)number2;

        }

        /// <summary>
        /// Extract the track number from the given string
        /// </summary>
        /// <param name="str">Raw "track" field in string form</param>
        /// <returns>Track number, trimmed and isolated from its total; empty string if nothing found</returns>
        public static string ExtractTrackNumberStr(string str)
        {
            // Invalid inputs
            if (null == str) return "";
            str = str.Trim();
            if (str.Length < 1) return "";

            // Ignore "total" part if exists
            int delimiterOffset = str.IndexOf('/');
            if (delimiterOffset > -1) str = str.Substring(0, delimiterOffset).Trim();

            return str;
        }

        /// <summary>
        /// Extract the total track number from the given string
        /// </summary>
        /// <param name="str">Raw "track" field in string form</param>
        /// <returns>Total track number, in integer form; 0 if no total track number has been found</returns>
        public static ushort ExtractTrackTotal(string str)
        {
            // Invalid inputs
            if (null == str) return 0;
            str = str.Trim();
            if (str.Length < 1) return 0;
            if (!str.Contains('/')) return 0;

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
            Match match = rxSlashNumbers.Value.Match(str);
            if (!match.Success) return 0;

            long number2 = long.Parse(match.Value);
            if (number2 > ushort.MaxValue) number2 = 0;
            return (ushort)number2; // First match is directly returned
        }

        /// <summary>
        /// Extract rating level from the given string
        /// </summary>
        /// <param name="ratingString">Raw "rating" field in string form</param>
        /// <param name="convention">Tagging convention (see MetaDataIO.RC_XXX constants)</param>
        /// <returns>Rating level, in float form (0 = 0% to 1 = 100%)</returns>
        public static double? DecodePopularity(string ratingString, int convention)
        {
            if (null == ratingString || 0 == ratingString.Trim().Length) return null;

            if (Utils.IsNumeric(ratingString))
            {
                var rating = Utils.ParseDouble(ratingString);
                if (double.IsNaN(rating)) rating = 0;
                return DecodePopularity(rating, convention);
            }

            // If the field is only one byte long, rating is evaluated numerically
            if (1 == ratingString.Length) return DecodePopularity((byte)ratingString[0], convention);

            // Exceptional case : rating is stored in the form of stars
            // NB : Having just one star is embarassing, since it falls into the "one-byte-long field" case processed above
            // It won't be interpretated as a star rating, as those are very rare
            Match match = rxStars.Value.Match(ratingString.Trim());
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
            return convention switch
            {
                MetaDataIO.RC_ASF => rating switch
                {
                    < 1 => 0,
                    < 25 => 0.2,
                    < 50 => 0.4,
                    < 75 => 0.6,
                    < 99 => 0.8,
                    _ => 1
                },
                MetaDataIO.RC_APE => rating switch
                {
                    // Stored as float
                    // Stored as scale of 0..100
                    < 5.1 => rating / 5.0,
                    < 10 => 0,
                    < 20 => 0.1,
                    < 30 => 0.2,
                    < 40 => 0.3,
                    < 50 => 0.4,
                    < 60 => 0.5,
                    < 70 => 0.6,
                    < 80 => 0.7,
                    < 90 => 0.8,
                    < 100 => 0.9,
                    _ => 1
                },
                _ => rating switch
                {
                    > 10 => rating switch
                    {
                        // De facto conventions (windows explorer, mediaMonkey, musicBee)
                        // 0.2 is value "1"; handled in two blocks
                        < 54 => 0.1,
                        < 64 => 0.3,
                        < 118 => 0.4,
                        < 128 => 0.5,
                        < 186 => 0.6,
                        < 196 => 0.7,
                        < 242 => 0.8,
                        < 255 => 0.9,
                        _ => 1
                    },
                    // Between 5 and 10
                    > 5 => rating / 10.0,
                    _ => rating / 5.0
                }
            };
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
            if (double.IsNaN(rating)) rating = 0;
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
            return convention switch
            {
                MetaDataIO.RC_ASF => rating switch
                {
                    < 1 => 0,
                    < 2 => 1,
                    < 3 => 25,
                    < 4 => 50,
                    < 5 => 75,
                    _ => 99
                },
                MetaDataIO.RC_APE =>
                    // Stored as scale of 0..100
                    rating switch
                    {
                        < 0.5 => 0,
                        < 1 => 10,
                        < 1.5 => 20,
                        < 2 => 30,
                        < 2.5 => 40,
                        < 3 => 50,
                        < 3.5 => 60,
                        < 4 => 70,
                        < 4.5 => 80,
                        < 5 => 90,
                        _ => 100
                    },
                _ => rating switch
                {
                    < 0.5 => 0,
                    < 1 => 13,
                    < 1.5 => 1,
                    < 2 => 54,
                    < 2.5 => 64,
                    < 3 => 118,
                    < 3.5 => 128,
                    < 4 => 186,
                    < 4.5 => 196,
                    < 5 => 242,
                    _ => 255
                }
            };
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
            // Invalid inputs
            if (null == str) return "";
            str = str.Trim();
            switch (str.Length)
            {
                case < 4:
                    return "";
                // Obvious plain year
                // Begins with 4 numeric chars
                case > 3 when char.IsNumber(str[0]) && char.IsNumber(str[1]) && char.IsNumber(str[2]) && char.IsNumber(str[3]):
                    return str[..4];
                // Ends with 4 numeric chars
                case > 3 when char.IsNumber(str[^1]) && char.IsNumber(str[^2]) && char.IsNumber(str[^3]) && char.IsNumber(str[^4]):
                    return str.Substring(str.Length - 4, 4);
                default:
                    {
                        // == If everything above fails...
                        Match match = rxNumber4.Value.Match(str.Trim());
                        // First match is directly returned
                        return match.Success ? match.Value : "";
                    }
            }
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
            if (!value.Contains('/'))
                return formatWithLeadingZeroesInternal(value, overrideExistingFormat, existingDigits, useLeadingZeroes, total);
            string[] parts = value.Split('/');
            return formatWithLeadingZeroesInternal(parts[0], overrideExistingFormat, existingDigits, useLeadingZeroes, parts[1]) + "/" + formatWithLeadingZeroesInternal(parts[1], overrideExistingFormat, existingDigits, useLeadingZeroes, parts[1]);

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
            int totalLength = total is { Length: > 1 } ? total.Length : 2;
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
                day = dayMonth[..2];
            }


            string hour = "";
            string minutes = "";
            string seconds = "";
            if (!Utils.IsNumeric(hoursMinutesSeconds)) return FormatISOTimestamp(year, day, month, hour, minutes, seconds);
            if (hoursMinutesSeconds.Length >= 4)
            {
                hour = hoursMinutesSeconds.Substring(0, 2);
                minutes = hoursMinutesSeconds.Substring(2, 2);
            }
            if (hoursMinutesSeconds.Length >= 6)
            {
                seconds = hoursMinutesSeconds.Substring(4, 2);
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
            if (2 == month.Length && Utils.IsNumeric(month)) result.Append('-').Append(month);
            if (2 == day.Length && Utils.IsNumeric(day)) result.Append('-').Append(day);
            if (2 == hour.Length && Utils.IsNumeric(hour)) result.Append('T').Append(hour);
            if (2 == minutes.Length && Utils.IsNumeric(minutes)) result.Append(':').Append(minutes);
            if (2 == seconds.Length && Utils.IsNumeric(seconds)) result.Append(':').Append(seconds);

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
            if (initialPaddingOffset <= -1) return paddingSizeToWrite;
            if (deltaSize <= 0) paddingSizeToWrite = Math.Max(0, initialPaddingSize + deltaSize);
            else if (initialPaddingSize >= Settings.PaddingSize) paddingSizeToWrite = initialPaddingSize;
            else paddingSizeToWrite = Math.Min(initialPaddingSize + deltaSize, Settings.PaddingSize);
            return paddingSizeToWrite;
        }
    }
}
