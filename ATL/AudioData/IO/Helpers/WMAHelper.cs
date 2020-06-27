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

        public static void WriteField(BinaryWriter w, string fieldName, string fieldValue, bool isNumeric)
        {
            long frameSizePos, midSizePos, finalFramePos;
            frameSizePos = w.BaseStream.Position;

            w.Write(0); // To be rewritten at the end of the method
            w.Write(fieldName.Length);
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
        }

        public static byte getCodeForFrame(string frame)
        {
            if (WMA.frameMapping.ContainsKey(frame))
                return WMA.frameMapping[frame];
            else return 255;

        }
    }
}
