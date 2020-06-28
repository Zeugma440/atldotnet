using ATL.AudioData.IO;
using Commons;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioData
{
    /// <summary>
    /// General utility class to manipulate WMA-like tags embedded in other formats (e.g. MP4)
    /// </summary>
    public static class WMAHelper
    {

        /// <summary>
        /// Read WMA-like formatted fields starting at the given reader's current position, and stopping after the given size
        /// </summary>
        /// <param name="source">Source to read the fields from</param>
        /// <param name="atomDataSize">Max size of the zone to read</param>
        /// <returns>List of the detected metadata fields</returns>
        public static IList<KeyValuePair<string, string>> ReadFields(BinaryReader source, long atomDataSize)
        {
            IList<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();

            long initialPos = source.BaseStream.Position;
            long pos = initialPos;
            while (pos < initialPos + atomDataSize)
            {
                int fieldSize = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                int stringDataSize = StreamUtils.DecodeBEInt32(source.ReadBytes(4));
                string fieldName = Utils.Latin1Encoding.GetString(source.ReadBytes(stringDataSize));
                source.BaseStream.Seek(4, SeekOrigin.Current);
                stringDataSize = StreamUtils.DecodeBEInt32(source.ReadBytes(4));

                string fieldValue;
                int fieldType = StreamUtils.DecodeBEInt16(source.ReadBytes(2));
                if (19 == fieldType) // Numeric
                    fieldValue = source.ReadInt64() + "";
                else
                    fieldValue = Utils.StripEndingZeroChars(Encoding.Unicode.GetString(source.ReadBytes(stringDataSize - 6)));

                result.Add(new KeyValuePair<string, string>(fieldName, fieldValue));
                source.BaseStream.Seek(pos + fieldSize, SeekOrigin.Begin);
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
            long frameSizePos, midSizePos, finalFramePos;
            frameSizePos = w.BaseStream.Position;

            w.Write(0); // To be rewritten at the end of the method
            w.Write(StreamUtils.EncodeBEInt32(fieldName.Length));
            w.Write(Utils.Latin1Encoding.GetBytes(fieldName));
            w.Write((ushort)0); // Frame class ?
            w.Write(StreamUtils.EncodeBEInt16(1)); // ? (always 1)
            midSizePos = w.BaseStream.Position;
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

            finalFramePos = w.BaseStream.Position;
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
        /// <returns>Matching ATL field code if found (See TagData.TAG_FIELD_XX properties); 255 if not</returns>
        public static byte getAtlCodeForFrame(string frame)
        {
            if (WMA.frameMapping.ContainsKey(frame))
                return WMA.frameMapping[frame];
            else return 255;

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
            byte atlCode = getAtlCodeForFrame(frame);
            if (atlCode < 255)
            {
                IDictionary<byte, string> dataMap = tagData.ToMap();
                if (dataMap.ContainsKey(atlCode)) return dataMap[atlCode];
            }
            return "";
        }

    }
}
