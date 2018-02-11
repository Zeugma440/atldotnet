using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;
using System;

namespace ATL.AudioData.IO
{
    public static class BextTag
    {
        public const string CHUNK_BEXT = "bext";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams)
        {
            string str;
            byte[] data = new byte[256];

            // Description
            source.Read(data, 0, 256);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("bext.description", str, readTagParams.ReadAllMetaFrames);

            // Originator
            source.Read(data, 0, 32);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 32).Trim());
            if (str.Length > 0) meta.SetMetaField("bext.originator", str, readTagParams.ReadAllMetaFrames);

            // OriginatorReference
            source.Read(data, 0, 32);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 32).Trim());
            if (str.Length > 0) meta.SetMetaField("bext.originatorReference", str, readTagParams.ReadAllMetaFrames);

            // OriginationDate
            source.Read(data, 0, 10);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 10).Trim());
            if (str.Length > 0) meta.SetMetaField("bext.originationDate", str, readTagParams.ReadAllMetaFrames);

            // OriginationTime
            source.Read(data, 0, 8);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 8).Trim());
            if (str.Length > 0) meta.SetMetaField("bext.originationTime", str, readTagParams.ReadAllMetaFrames);

            // TimeReference
            source.Read(data, 0, 8);
            ulong timeReference = StreamUtils.DecodeUInt64(data);
            meta.SetMetaField("bext.timeReference", timeReference.ToString(), readTagParams.ReadAllMetaFrames);

            // BEXT version
            source.Read(data, 0, 2);
            int intData = StreamUtils.DecodeUInt16(data);
            meta.SetMetaField("bext.version", intData.ToString(), readTagParams.ReadAllMetaFrames);

            // UMID
            source.Read(data, 0, 64);
            str = "";

            int usefulLength = 32; // "basic" UMID
            if (data[12] > 19) usefulLength = 64; // data[12] gives the size of remaining UMID
            for (int i = 0; i < usefulLength; i++) str = str + data[i].ToString("X2");

            meta.SetMetaField("bext.UMID", str, readTagParams.ReadAllMetaFrames);

            // LoudnessValue
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            meta.SetMetaField("bext.loudnessValue", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // LoudnessRange
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            meta.SetMetaField("bext.loudnessRange", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxTruePeakLevel
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            meta.SetMetaField("bext.maxTruePeakLevel", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxMomentaryLoudness
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            meta.SetMetaField("bext.maxMomentaryLoudness", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // MaxShortTermLoudness
            source.Read(data, 0, 2);
            intData = StreamUtils.DecodeInt16(data);
            meta.SetMetaField("bext.maxShortTermLoudness", (intData / 100.0).ToString(), readTagParams.ReadAllMetaFrames);

            // Reserved
            source.Seek(180, SeekOrigin.Current);

            // CodingHistory
            long initialPos = source.Position;
            if (StreamUtils.FindSequence(source, new byte[2] { 13, 10 } /* CR LF */ ))
            {
                long endPos = source.Position - 2;
                source.Seek(initialPos, SeekOrigin.Begin);

                if (data.Length < (int)(endPos - initialPos)) data = new byte[(int)(endPos - initialPos)];
                source.Read(data, 0, (int)(endPos - initialPos));

                str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, (int)(endPos - initialPos)).Trim());
                if (str.Length > 0) meta.SetMetaField("bext.codingHistory", str, readTagParams.ReadAllMetaFrames);
            }
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            if (meta.GeneralDescription.Length > 0) return true;

            foreach (string key in meta.AdditionalFields.Keys)
            {
                if (key.StartsWith("bext.")) return true;
            }

            return false;
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_BEXT));

            long sizePos = w.BaseStream.Position;
            w.Write((int)0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Text values
            string description = Utils.ProtectValue(meta.GeneralDescription);
            if (0 == description.Length && additionalFields.Keys.Contains("bext.description")) description = additionalFields["bext.description"];

            writeFixedTextValue(description, 256, w);
            writeFixedFieldTextValue("bext.originator", 32, additionalFields, w);
            writeFixedFieldTextValue("bext.originatorReference", 32, additionalFields, w);
            writeFixedFieldTextValue("bext.originationDate", 10, additionalFields, w);
            writeFixedFieldTextValue("bext.originationTime", 8, additionalFields, w);

            // Int values
            writeFieldIntValue("bext.timeReference", additionalFields, w, (ulong)0);
            writeFieldIntValue("bext.version", additionalFields, w, (ushort)0);

            // UMID
            if (additionalFields.Keys.Contains("bext.UMID"))
            {
                if (Utils.IsHex(additionalFields["bext.UMID"]))
                {
                    int usedValues = (int)Math.Floor(additionalFields["bext.UMID"].Length / 2.0);
                    for (int i = 0; i<usedValues; i++)
                    {
                        w.Write( Convert.ToByte(additionalFields["bext.UMID"].Substring(i*2, 2), 16) );
                    }
                    // Complete the field to 64 bytes
                    for (int i = 0; i < 64-usedValues; i++) w.Write((byte)0);
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'bext.UMID' : error writing field - hexadecimal notation required; " + additionalFields["bext.UMID"] + " found");
                    for (int i = 0; i < 64; i++) w.Write((byte)0);
                }
            } else
            {
                for (int i = 0; i < 64; i++) w.Write((byte)0);
            }


            // Float values
            writeField100DecimalValue("bext.loudnessValue", additionalFields, w, (short)0);
            writeField100DecimalValue("bext.loudnessRange", additionalFields, w, (short)0);
            writeField100DecimalValue("bext.maxTruePeakLevel", additionalFields, w, (short)0);
            writeField100DecimalValue("bext.maxMomentaryLoudness", additionalFields, w, (short)0);
            writeField100DecimalValue("bext.maxShortTermLoudness", additionalFields, w, (short)0);

            // Reserved
            for (int i = 0; i < 180; i++) w.Write((byte)0);

            // CodingHistory
            byte[] textData = new byte[0];
            if (additionalFields.Keys.Contains("bext.codingHistory"))
            {
                textData = Utils.Latin1Encoding.GetBytes(additionalFields["bext.codingHistory"]);
                w.Write( textData );
            }
            w.Write(new byte[2] { 13, 10 } /* CR LF */);

            // Emulation of the BWFMetaEdit padding behaviour (256 characters)
            for (int i = 0; i < 256 - ((textData.Length + 2) % 256); i++) w.Write((byte)0);


            long finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian)
            {
                w.Write((int)(finalPos - sizePos - 4));
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return 14;
        }

        private static void writeFixedFieldTextValue(string field, int length, IDictionary<string, string> additionalFields, BinaryWriter w, byte paddingByte = 0)
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

        private static void writeFixedTextValue(string value, int length, BinaryWriter w, byte paddingByte = 0)
        {
            w.Write(Utils.BuildStrictLengthStringBytes(value, length, paddingByte, Utils.Latin1Encoding));
        }

        private static void writeFieldIntValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.Keys.Contains(field))
            {
                if (Utils.IsNumeric(additionalFields[field], true))
                {
                    if (defaultValue is short) w.Write(short.Parse(additionalFields[field]));
                    else if (defaultValue is ulong) w.Write(ulong.Parse(additionalFields[field]));
                    else if (defaultValue is ushort) w.Write(ushort.Parse(additionalFields[field]));
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
        }

        private static void writeField100DecimalValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.Keys.Contains(field))
            {
                if (Utils.IsNumeric(additionalFields[field]))
                {
                    float f = float.Parse(additionalFields[field]) * 100;
                    if (defaultValue is short)  w.Write((short)Math.Round(f));
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
