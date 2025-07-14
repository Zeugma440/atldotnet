using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using System;
using System.Linq;
using System.Text;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// General utility class to manipulate RIFF WAV chunks
    /// </summary>
    internal static class WavHelper
    {
        /// <summary>
        /// Indicate whether the given Metadata I/O contains metadata relevant to the given prefix
        /// </summary>
        /// <param name="meta">Metadata I/O to test with</param>
        /// <param name="prefix">Prefix to test with</param>
        /// <returns>True if the given Metadata I/O contains data relevant to the given prefix; false if it doesn't</returns>
        public static bool IsDataEligible(MetaDataHolder meta, string prefix)
        {
            return meta.AdditionalFields.Keys.Any(key => key.StartsWith(prefix));
        }

        /// <summary>
        /// Filter the given key collection with the given prefix
        /// </summary>
        /// <param name="prefix">Prefix to test with</param>
        /// <param name="keys">Collection to test with</param>
        /// <returns>List of keys from the given collection starting with the given prefix</returns>
        public static IList<string> GetEligibleKeys(string prefix, ICollection<string> keys)
        {
            IList<string> result = new List<string>();
            foreach (var s in keys.Where(s => s.StartsWith(prefix + "[")))
            {
                string key = s[..(s.IndexOf(']') + 1)];
                if (!result.Contains(key)) result.Add(key);
            }

            return result;
        }

        /// <summary>
        /// Read a 32-bit signed integer from the given source into the given Metadata I/O, using the given field name, buffer and read parameters
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="meta">Metadata I/O to copy the value to</param>
        /// <param name="fieldName">Field name to use</param>
        /// <param name="buffer">Buffer to use</param>
        /// <param name="readAllMetaFrames">Read parameters to use</param>
        /// <returns>Read value</returns>
        public static int ReadInt32(Stream source, MetaDataIO meta, string fieldName, byte[] buffer, bool readAllMetaFrames)
        {
            if (source.Read(buffer, 0, 4) < 4) return 0;
            int value = StreamUtils.DecodeInt32(buffer);
            meta.SetMetaField(fieldName, value.ToString(), readAllMetaFrames);
            return value;
        }

        /// <summary>
        /// Read a 16-bit signed integer from the given source into the given Metadata I/O, using the given field name, buffer and read parameters
        /// </summary>
        /// <param name="source">Source to read from</param>
        /// <param name="meta">Metadata I/O to copy the value to</param>
        /// <param name="fieldName">Field name to use</param>
        /// <param name="buffer">Buffer to use</param>
        /// <param name="readAllMetaFrames">Read parameters to use</param>
        /// <returns>Read value</returns>
        public static void ReadInt16(Stream source, MetaDataIO meta, string fieldName, byte[] buffer, bool readAllMetaFrames)
        {
            if (source.Read(buffer, 0, 2) < 2) return;
            int value = StreamUtils.DecodeInt16(buffer);
            meta.SetMetaField(fieldName, value.ToString(), readAllMetaFrames);
        }

        public static void Latin1FromStream(
            Stream source,
            int size,
            MetaDataIO meta,
            string field,
            byte[] buffer,
            bool readAllFrames)
        {
            StringFromStream(source, size, Utils.Latin1Encoding, meta, field, buffer, readAllFrames);
        }

        public static void Utf8FromStream(
            Stream source,
            int size,
            MetaDataIO meta,
            string field,
            byte[] buffer,
            bool readAllFrames)
        {
            StringFromStream(source, size, Encoding.UTF8, meta, field, buffer, readAllFrames);
        }

        public static void StringFromStream(
            Stream source,
            int size,
            Encoding encoding,
            MetaDataIO meta,
            string field,
            byte[] buffer,
            bool readAllFrames)
        {
            if (source.Read(buffer, 0, size) < size) return;
            var str = Utils.StripEndingZeroChars(encoding.GetString(buffer, 0, size).Trim());
            if (str.Length > 0) meta.SetMetaField(field, str, readAllFrames);
        }


        /// <summary>
        /// Write a fixed-length text value from the given Map to the given writer
        /// NB : Used encoding is Latin-1
        /// </summary>
        /// <param name="field">Key of the field to write</param>
        /// <param name="additionalFields">Map to take the value from</param>
        /// <param name="length">Fixed length of the text to write</param>
        /// <param name="w">Writer to write the value to</param>
        /// <param name="paddingByte">Padding value to use (default : 0x00)</param>
        public static void WriteFixedFieldTextValue(string field, IDictionary<string, string> additionalFields, int length, BinaryWriter w, byte paddingByte = 0)
        {
            WriteFixedTextValue(additionalFields.TryGetValue(field, out var additionalField) ? additionalField : "",
                length, w, paddingByte);
        }

        /// <summary>
        /// Write the given value to the given writer as fixed-length text
        /// NB : Used encoding is Latin-1
        /// </summary>
        /// <param name="value">Value to write</param>
        /// <param name="length">Fixed length of the text to write</param>
        /// <param name="w">Writer to write the value to</param>
        /// <param name="paddingByte">Padding value to use (default : 0x00)</param>
        public static void WriteFixedTextValue(string value, int length, BinaryWriter w, byte paddingByte = 0)
        {
            w.Write(Utils.BuildStrictLengthStringBytes(value, length, paddingByte, Encoding.UTF8));
        }

        /// <summary>
        /// Write an integer value from the given Map to the given writer using little-endian convention
        /// NB : The method auto-detects which kind of integer to write according to the type of the default value
        /// </summary>
        /// <param name="field">Key of the field to write</param>
        /// <param name="additionalFields">Map to take the value from</param>
        /// <param name="w">Writer to use</param>
        /// <param name="defaultValue">Default value to use in case no value is found in the given Map; type determines the type of the written value</param>
        public static void WriteFieldIntValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.ContainsKey(field))
            {
                if (Utils.IsNumeric(additionalFields[field], true))
                {
                    switch (defaultValue)
                    {
                        case short:
                            w.Write(short.Parse(additionalFields[field]));
                            break;
                        case ulong:
                            w.Write(ulong.Parse(additionalFields[field]));
                            break;
                        case uint:
                            w.Write(uint.Parse(additionalFields[field]));
                            break;
                        case ushort:
                            w.Write(ushort.Parse(additionalFields[field]));
                            break;
                        case int:
                            w.Write(int.Parse(additionalFields[field]));
                            break;
                        case byte:
                            w.Write(byte.Parse(additionalFields[field]));
                            break;
                        case sbyte:
                            w.Write(sbyte.Parse(additionalFields[field]));
                            break;
                    }

                    return;
                }

                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + field + "' : error writing field - integer required; " + additionalFields[field] + " found");
            }

            switch (defaultValue)
            {
                case short value:
                    w.Write(value);
                    break;
                case ulong value:
                    w.Write(value);
                    break;
                case uint value:
                    w.Write(value);
                    break;
                case ushort value:
                    w.Write(value);
                    break;
                case int value:
                    w.Write(value);
                    break;
                case byte value:
                    w.Write(value);
                    break;
                case sbyte value:
                    w.Write(value);
                    break;
            }
        }

        /// <summary>
        /// Write an 0.01-precision floating-point value from the given Map to the given writer
        /// NB : The method auto-detects which kind of floating-point to write according to the type of the default value (float or short)
        /// </summary>
        /// <param name="field">Key of the field to write</param>
        /// <param name="additionalFields">Map to take the value from</param>
        /// <param name="w">Writer to use</param>
        /// <param name="defaultValue">Default value to use in case no value is found in the given Map; type determines the type of the written value</param>
        public static void WriteField100DecimalValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.ContainsKey(field))
            {
                if (Utils.IsNumeric(additionalFields[field]))
                {
                    float f = float.Parse(additionalFields[field]) * 100;
                    if (defaultValue is short) w.Write((short)Math.Round(f));
                    return;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + field + "' : error writing field - integer or decimal required; " + additionalFields[field] + " found");
                }
            }

            w.Write((short)defaultValue);
        }

        /// <summary>
        /// Skips WAV padding byte in the given Stream, if it exists, 
        /// while not crossing the given maximum position
        /// 
        /// NB : WAV specs say padding should only be zeroes, but other values can be found in the wild
        /// => expects a _displayable_ character as part of next header ID
        /// </summary>
        /// <param name="s">Stream to use</param>
        /// <param name="maxPos">Maximum position not to cross</param>
        public static void SkipEndPadding(Stream s, long maxPos)
        {
            if (s.Position >= maxPos) return;

            int b = s.ReadByte();
            if (b is > 31 and < 255) s.Seek(-1, SeekOrigin.Current);
        }
    }
}
