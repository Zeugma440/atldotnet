using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    public static class SampleTag
    {
        public const string CHUNK_SAMPLE = "smpl";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams)
        {
            string str;
            byte[] data = new byte[256];

            // Manufacturer
            readInt32(source, meta, "sample.manufacturer", data, readTagParams.ReadAllMetaFrames);

            // Product
            readInt32(source, meta, "sample.product", data, readTagParams.ReadAllMetaFrames);

            // Period
            readInt32(source, meta, "sample.period", data, readTagParams.ReadAllMetaFrames);

            // MIDI unity note
            readInt32(source, meta, "sample.MIDIUnityNote", data, readTagParams.ReadAllMetaFrames);

            // MIDI pitch fraction
            readInt32(source, meta, "sample.MIDIPitchFraction", data, readTagParams.ReadAllMetaFrames);

            // SMPTE format
            readInt32(source, meta, "sample.SMPTEFormat", data, readTagParams.ReadAllMetaFrames);

            // SMPTE offsets
            source.Read(data, 0, 1);
            sbyte sByteData = StreamUtils.DecodeSignedByte(data);
            meta.SetMetaField("sample.SMPTEOffset.Hours", sByteData.ToString(), readTagParams.ReadAllMetaFrames);
            source.Read(data, 0, 1);
            byte byteData = StreamUtils.DecodeUByte(data);
            meta.SetMetaField("sample.SMPTEOffset.Minutes", byteData.ToString(), readTagParams.ReadAllMetaFrames);
            source.Read(data, 0, 1);
            byteData = StreamUtils.DecodeUByte(data);
            meta.SetMetaField("sample.SMPTEOffset.Seconds", byteData.ToString(), readTagParams.ReadAllMetaFrames);
            source.Read(data, 0, 1);
            byteData = StreamUtils.DecodeUByte(data);
            meta.SetMetaField("sample.SMPTEOffset.Frames", byteData.ToString(), readTagParams.ReadAllMetaFrames);

            // Num sample loops
            int numSampleLoops = readInt32(source, meta, "sample.NumSampleLoops", data, readTagParams.ReadAllMetaFrames);

            // Sample loops size (not useful here)
            source.Seek(4, SeekOrigin.Current);

            for (int i = 0; i < numSampleLoops; i++)
            {
                // Cue point ID
                readInt32(source, meta, "sample.SampleLoop[" + i + "].CuePointId", data, readTagParams.ReadAllMetaFrames);

                // Type
                readInt32(source, meta, "sample.SampleLoop[" + i + "].Type", data, readTagParams.ReadAllMetaFrames);

                // Start
                readInt32(source, meta, "sample.SampleLoop[" + i + "].Start", data, readTagParams.ReadAllMetaFrames);

                // End
                readInt32(source, meta, "sample.SampleLoop[" + i + "].End", data, readTagParams.ReadAllMetaFrames);

                // Fraction
                readInt32(source, meta, "sample.SampleLoop[" + i + "].Fraction", data, readTagParams.ReadAllMetaFrames);

                // Play count
                readInt32(source, meta, "sample.SampleLoop[" + i + "].PlayCount", data, readTagParams.ReadAllMetaFrames);
            }
        }

        private static int readInt32(Stream source, MetaDataIO meta, string fieldName, byte[] buffer, bool readAllMetaFrames)
        {
            source.Read(buffer, 0, 4);
            int value = StreamUtils.DecodeInt32(buffer);
            meta.SetMetaField(fieldName, value.ToString(), readAllMetaFrames);
            return value;
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            foreach (string key in meta.AdditionalFields.Keys)
            {
                if (key.StartsWith("sample.")) return true;
            }

            return false;
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_SAMPLE));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Int values
            writeFieldIntValue("sample.manufacturer", additionalFields, w, 0);
            writeFieldIntValue("sample.product", additionalFields, w, 0);
            writeFieldIntValue("sample.period", additionalFields, w, 1);
            writeFieldIntValue("sample.MIDIUnityNote", additionalFields, w, 0);
            writeFieldIntValue("sample.MIDIPitchFraction", additionalFields, w, 0);
            writeFieldIntValue("sample.SMPTEFormat", additionalFields, w, 0);

            // SMPTE offset
            writeFieldIntValue("sample.SMPTEOffset.Hours", additionalFields, w, (sbyte)0);
            writeFieldIntValue("sample.SMPTEOffset.Minutes", additionalFields, w, (byte)0);
            writeFieldIntValue("sample.SMPTEOffset.Seconds", additionalFields, w, (byte)0);
            writeFieldIntValue("sample.SMPTEOffset.Frames", additionalFields, w, (byte)0);

            // == Sample loops

            // How many of them do we have ? -> count distinct indexes
            IList<string> keys = new List<string>();
            foreach(string s in additionalFields.Keys)
            {
                if (s.StartsWith("sample.SampleLoop")) {
                    string key = s.Substring(0, s.IndexOf("]") + 1);
                    if (!keys.Contains(key)) keys.Add(key);
                }
            }
            w.Write(keys.Count);

            // Sample loops data size
            long sampleLoopsPos = w.BaseStream.Position;
            w.Write(0); // Placeholder for data size that will be rewritten at the end of the method

            // Sample loops data
            foreach (string key in keys)
            {
                writeFieldIntValue(key+ ".CuePointId", additionalFields, w, 0);
                writeFieldIntValue(key + ".Type", additionalFields, w, 0);
                writeFieldIntValue(key + ".Start", additionalFields, w, 0);
                writeFieldIntValue(key + ".End", additionalFields, w, 0);
                writeFieldIntValue(key + ".Fraction", additionalFields, w, 0);
                writeFieldIntValue(key + ".PlayCount", additionalFields, w, 0);
            }

            // Write actual sample loops data size
            long finalPos = w.BaseStream.Position;
            w.BaseStream.Seek(sampleLoopsPos, SeekOrigin.Begin);
            w.Write((int)(finalPos - sampleLoopsPos - 4));

            // Write actual tag size
            w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
            if (isLittleEndian)
            {
                w.Write((int)(finalPos - sizePos - 4));
            }
            else
            {
                w.Write(StreamUtils.EncodeBEInt32((int)(finalPos - sizePos - 4)));
            }

            return 10;
        }

        private static void writeFieldIntValue(string field, IDictionary<string, string> additionalFields, BinaryWriter w, object defaultValue)
        {
            if (additionalFields.Keys.Contains(field))
            {
                if (Utils.IsNumeric(additionalFields[field], true))
                {
                    if (defaultValue is int) w.Write(int.Parse(additionalFields[field]));
                    else if (defaultValue is byte) w.Write(byte.Parse(additionalFields[field]));
                    else if (defaultValue is sbyte) w.Write(sbyte.Parse(additionalFields[field]));
                    return;
                }
                else
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + field + "' : error writing field - integer required; " + additionalFields[field] + " found");
                }
            }

            if (defaultValue is int) w.Write((int)defaultValue);
            else if (defaultValue is byte) w.Write((byte)defaultValue);
            else if (defaultValue is sbyte) w.Write((sbyte)defaultValue);
        }
    }
}
