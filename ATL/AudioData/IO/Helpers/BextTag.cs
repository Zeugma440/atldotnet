using Commons;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;

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
            long timeReference = StreamUtils.DecodeUInt64(data);
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

        public static int ToStream(BinaryWriter w, bool isLittleEndian, IDictionary<string, string> additionalFields)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_BEXT));

            long sizePos = w.BaseStream.Position;
            w.Write((int)0); // Placeholder for chunk size that will be rewritten at the end of the method

            writeFixedFieldStrValue("bext.description", 256, additionalFields, w);
            writeFixedFieldStrValue("bext.originator", 32, additionalFields, w);
            writeFixedFieldStrValue("bext.originatorReference", 32, additionalFields, w);
            writeFixedFieldStrValue("bext.originationDate", 10, additionalFields, w);
            writeFixedFieldStrValue("bext.originationTime", 8, additionalFields, w);

            // TODO - numeric fields

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

        private static void writeFixedFieldStrValue(string field, int length, IDictionary<string, string> additionalFields, BinaryWriter w, char paddingChar = '\0')
        {
            if (additionalFields.Keys.Contains(field))
            {
                w.Write(Utils.BuildStrictLengthStringBytes(additionalFields[field], length, 0, Utils.Latin1Encoding));
            }
            else
            {
                w.Write(Utils.BuildStrictLengthString("", length, paddingChar));
            }
        }
    }
}
