using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using System;

namespace ATL.AudioData.IO
{
    public static class WavUtils
    {
        public static bool IsDataEligible(MetaDataIO meta, string prefix)
        {
            foreach (string key in meta.AdditionalFields.Keys)
            {
                if (key.StartsWith(prefix)) return true;
            }

            return false;
        }

        // How many of them do we have ? -> count distinct indexes
        public static IList<string> getEligibleKeys(string prefix, ICollection<string> keys)
        {
            IList<string> result = new List<string>();
            foreach (string s in keys)
            {
                if (s.StartsWith(prefix + "["))
                {
                    string key = s.Substring(0, s.IndexOf("]") + 1);
                    if (!result.Contains(key)) result.Add(key);
                }
            }
            return result;
        }

        public static int readInt32(Stream source, MetaDataIO meta, string fieldName, byte[] buffer, bool readAllMetaFrames)
        {
            source.Read(buffer, 0, 4);
            int value = StreamUtils.DecodeInt32(buffer);
            meta.SetMetaField(fieldName, value.ToString(), readAllMetaFrames);
            return value;
        }

        public static void readInt16(Stream source, MetaDataIO meta, string fieldName, byte[] buffer, bool readAllMetaFrames)
        {
            source.Read(buffer, 0, 2);
            int value = StreamUtils.DecodeInt16(buffer);
            meta.SetMetaField(fieldName, value.ToString(), readAllMetaFrames);
        }


        public static void writeFixedFieldTextValue(string field, int length, IDictionary<string, string> additionalFields, BinaryWriter w, byte paddingByte = 0)
        {
            if (additionalFields.Keys.Contains(field))
            {
                writeFixedTextValue(additionalFields[field], length, w, paddingByte);
            }
            else
            {
                writeFixedTextValue("", length, w, paddingByte);
            }
        }

        public static void writeFixedTextValue(string value, int length, BinaryWriter w, byte paddingByte = 0)
        {
            w.Write(Utils.BuildStrictLengthStringBytes(value, length, paddingByte, Utils.Latin1Encoding));
        }

        public static void writeFieldIntValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.Keys.Contains(field))
            {
                if (Utils.IsNumeric(additionalFields[field], true))
                {
                    if (defaultValue is short) w.Write(short.Parse(additionalFields[field]));
                    else if (defaultValue is ulong) w.Write(ulong.Parse(additionalFields[field]));
                    else if (defaultValue is ushort) w.Write(ushort.Parse(additionalFields[field]));
                    else if (defaultValue is int) w.Write(int.Parse(additionalFields[field]));
                    else if (defaultValue is byte) w.Write(byte.Parse(additionalFields[field]));
                    else if (defaultValue is sbyte) w.Write(sbyte.Parse(additionalFields[field]));
                    return;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + field + "' : error writing field - integer required; " + additionalFields[field] + " found");
                }
            }

            if (defaultValue is short) w.Write((short)defaultValue);
            else if (defaultValue is ulong) w.Write((ulong)defaultValue);
            else if (defaultValue is ushort) w.Write((ushort)defaultValue);
            else if (defaultValue is int) w.Write((int)defaultValue);
            else if (defaultValue is byte) w.Write((byte)defaultValue);
            else if (defaultValue is sbyte) w.Write((sbyte)defaultValue);
        }

        public static void writeField100DecimalValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.Keys.Contains(field))
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
    }
}
