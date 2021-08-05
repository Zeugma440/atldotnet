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
            int numCuePoints = WavUtils.readInt32(source, meta, "cue.NumCuePoints", data, readTagParams.ReadAllMetaFrames);

            for (int i = 0; i < numCuePoints; i++)
            {
                // Cue point ID
                WavUtils.readInt32(source, meta, "cue.CuePoints[" + i + "].CuePointId", data, readTagParams.ReadAllMetaFrames);

                // Play order position
                WavUtils.readInt32(source, meta, "cue.CuePoints[" + i + "].Position", data, readTagParams.ReadAllMetaFrames);

                // RIFF ID of corresponding data chunk
                source.Read(data, 0, 4);
                meta.SetMetaField("cue.CuePoints[" + i + "].DataChunkId", Utils.Latin1Encoding.GetString(data, 0, 4), readTagParams.ReadAllMetaFrames);

                // Byte Offset of Data Chunk
                WavUtils.readInt32(source, meta, "cue.CuePoints[" + i + "].ChunkStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample of First Channel
                WavUtils.readInt32(source, meta, "cue.CuePoints[" + i + "].BlockStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample byte of First Channel
                WavUtils.readInt32(source, meta, "cue.CuePoints[" + i + "].SampleOffset", data, readTagParams.ReadAllMetaFrames);
            }
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

            // == Cue points list

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

            // Cue points data
            foreach (string key in keys)
            {
                WavUtils.writeFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
                WavUtils.writeFieldIntValue(key + ".Position", additionalFields, w, 0);
                w.Write(Utils.Latin1Encoding.GetBytes(additionalFields[key + ".DataChunkId"]));
                WavUtils.writeFieldIntValue(key + ".ChunkStart", additionalFields, w, 0);
                WavUtils.writeFieldIntValue(key + ".BlockStart", additionalFields, w, 0);
                WavUtils.writeFieldIntValue(key + ".SampleOffset", additionalFields, w, 0);
            }

            // Write actual tag size
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

            return 10;
        }
    }
}
