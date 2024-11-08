using ATL.AudioData.IO;
using Commons;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static ATL.TagData;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate WMA-like tags embedded in other formats (e.g. MP4)
    /// </summary>
    internal static class WMAHelper
    {

        /// <summary>
        /// Read WMA-like formatted fields starting at the given reader's current position, and stopping after the given size
        /// </summary>
        /// <param name="source">Source to read the fields from</param>
        /// <param name="atomDataSize">Max size of the zone to read</param>
        /// <returns>List of the detected metadata fields</returns>
        public static IList<KeyValuePair<string, string>> ReadFields(Stream source, long atomDataSize)
        {
            IList<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            byte[] buffer = new byte[8];

            long initialPos = source.Position;
            long pos = initialPos;
            while (pos < initialPos + atomDataSize)
            {
                if (source.Read(buffer, 0, 4) < 4) return result;
                int fieldSize = StreamUtils.DecodeBEInt32(buffer);
                if (source.Read(buffer, 0, 4) < 4) return result;
                int stringDataSize = StreamUtils.DecodeBEInt32(buffer);
                byte[] data = new byte[stringDataSize];
                if (source.Read(data, 0, stringDataSize) < stringDataSize) return result;
                string fieldName = Utils.Latin1Encoding.GetString(data);
                source.Seek(4, SeekOrigin.Current);
                if (source.Read(buffer, 0, 4) < 4) return result;
                stringDataSize = StreamUtils.DecodeBEInt32(buffer);

                string fieldValue;
                if (source.Read(buffer, 0, 2) < 2) return result;
                int fieldType = StreamUtils.DecodeBEInt16(buffer);
                if (19 == fieldType) // Numeric
                {
                    if (source.Read(buffer, 0, 8) < 8) return result;
                    fieldValue = StreamUtils.DecodeInt64(buffer) + "";
                }
                else
                {
                    data = new byte[stringDataSize - 6];
                    if (source.Read(data, 0, stringDataSize - 6) < stringDataSize - 6) return result;
                    fieldValue = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(data, 0, stringDataSize - 6));
                }

                result.Add(new KeyValuePair<string, string>(fieldName, fieldValue));
                source.Seek(pos + fieldSize, SeekOrigin.Begin);
                pos += fieldSize;
            }

            return result;
        }

        /// <summary>
        /// Write the given field with the given writer, using the WMA-like format
        /// </summary>
        /// <param name="w">Write to write the fields to</param>
        /// <param name="fieldName">Field name</param>
        /// <param name="fieldValue">Field value</param>
        /// <param name="isNumeric">True if the field is numeric; false if not (will be formatted as string)</param>
        public static void WriteField(BinaryWriter w, string fieldName, string fieldValue, bool isNumeric)
        {
            long frameSizePos = w.BaseStream.Position;

            w.Write(0); // To be rewritten at the end of the method
            w.Write(StreamUtils.EncodeBEInt32(fieldName.Length));
            w.Write(Utils.Latin1Encoding.GetBytes(fieldName));
            w.Write((ushort)0); // Frame class ?
            w.Write(StreamUtils.EncodeBEInt16(1)); // ? (always 1)
            long midSizePos = w.BaseStream.Position;
            w.Write(0); // To be rewritten at the end of the method

            if (isNumeric)
            {
                w.Write(StreamUtils.EncodeBEInt16(19)); // ?? (works for rating)
                w.Write(long.Parse(fieldValue)); // 64-bit little-endian integer ?
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt16(8)); // ?? (always 8)
                w.Write(Encoding.Unicode.GetBytes(fieldValue + '\0')); // String is null-terminated
            }

            var finalFramePos = w.BaseStream.Position;
            // Go back to frame size locations to write their actual size 
            w.BaseStream.Seek(midSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - midSizePos)));

            w.BaseStream.Seek(frameSizePos, SeekOrigin.Begin);
            w.Write(StreamUtils.EncodeBEInt32((int)(finalFramePos - frameSizePos)));
            w.BaseStream.Seek(finalFramePos, SeekOrigin.Begin);
        }

        /// <summary>
        /// Get the ATL field code from the given WMA field name
        /// </summary>
        /// <param name="frame">WMA field name</param>
        /// <returns>Matching ATL field code if found (See TagData.Field.XX properties); 255 if not</returns>
        public static Field getAtlCodeForFrame(string frame)
        {
            if (WMA.frameMapping.TryGetValue(frame, out var forFrame)) return forFrame;
            return Field.NO_FIELD;

        }

        /// <summary>
        /// Return the value of the ATL field equivalent to the given WMA field that has been set in the given tag values
        /// Return an empty string if no equivalent field has been found
        /// </summary>
        /// <param name="frame">WMA field name of the field to find</param>
        /// <param name="tagData">Tag values to search into</param>
        /// <returns>Matching value taken from tagData; empty string if no match found</returns>
        public static string getValueFromTagData(string frame, TagData tagData)
        {
            Field atlCode = getAtlCodeForFrame(frame);
            if (atlCode != Field.NO_FIELD)
            {
                IDictionary<Field, string> dataMap = tagData.ToMap();
                if (dataMap.TryGetValue(atlCode, out var data)) return data;
            }
            return "";
        }

    }
}
