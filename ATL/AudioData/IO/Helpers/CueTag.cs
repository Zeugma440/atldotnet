using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    public static class CueTag
    {
        public const string CHUNK_CUE = "cue ";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams)
        {
            byte[] data = new byte[256];

            // Num cue points
            int numCuePoints = readInt32(source, meta, "cue.NumCuePoints", data, readTagParams.ReadAllMetaFrames);

            for (int i = 0; i < numCuePoints; i++)
            {
                // Cue point ID
                readInt32(source, meta, "cue.CuePoints[" + i + "].CuePointId", data, readTagParams.ReadAllMetaFrames);

                // Play order position
                readInt32(source, meta, "cue.CuePoints[" + i + "].Position", data, readTagParams.ReadAllMetaFrames);

                // RIFF ID of corresponding data chunk
                readInt32(source, meta, "cue.CuePoints[" + i + "].DataChunkId", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset of Data Chunk
                readInt32(source, meta, "cue.CuePoints[" + i + "].ChunkStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample of First Channel
                readInt32(source, meta, "cue.CuePoints[" + i + "].BlockStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample byte of First Channel
                readInt32(source, meta, "cue.CuePoints[" + i + "].SampleOffset", data, readTagParams.ReadAllMetaFrames);
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
                if (key.StartsWith("cue.")) return true;
            }

            return false;
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_CUE));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Int values
            writeFieldIntValue("cue.NumCuePoints", additionalFields, w, 0);

            // == Sample loops

            // How many of them do we have ? -> count distinct indexes
            IList<string> keys = new List<string>();
            foreach (string s in additionalFields.Keys)
            {
                if (s.StartsWith("cue.CuePoints"))
                {
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
                writeFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
                writeFieldIntValue(key + ".Position", additionalFields, w, 0);
                writeFieldIntValue(key + ".DataChunkId", additionalFields, w, 0);
                writeFieldIntValue(key + ".ChunkStart", additionalFields, w, 0);
                writeFieldIntValue(key + ".BlockStart", additionalFields, w, 0);
                writeFieldIntValue(key + ".SampleOffset", additionalFields, w, 0);
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
