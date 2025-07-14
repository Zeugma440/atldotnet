using Commons;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;
using System.Linq;

namespace ATL.AudioData.IO
{
    internal static class SampleTag
    {
        public const string CHUNK_SAMPLE = "smpl";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams)
        {
            byte[] data = new byte[256];

            // Manufacturer
            WavHelper.ReadInt32(source, meta, "sample.manufacturer", data, readTagParams.ReadAllMetaFrames);

            // Product
            WavHelper.ReadInt32(source, meta, "sample.product", data, readTagParams.ReadAllMetaFrames);

            // Period
            WavHelper.ReadInt32(source, meta, "sample.period", data, readTagParams.ReadAllMetaFrames);

            // MIDI unity note
            WavHelper.ReadInt32(source, meta, "sample.MIDIUnityNote", data, readTagParams.ReadAllMetaFrames);

            // MIDI pitch fraction
            WavHelper.ReadInt32(source, meta, "sample.MIDIPitchFraction", data, readTagParams.ReadAllMetaFrames);

            // SMPTE format
            WavHelper.ReadInt32(source, meta, "sample.SMPTEFormat", data, readTagParams.ReadAllMetaFrames);

            // SMPTE offsets
            if (source.Read(data, 0, 4) < 4) return;
            meta.SetMetaField("sample.SMPTEOffset.Hours", ((sbyte)data[0]).ToString(), readTagParams.ReadAllMetaFrames);
            meta.SetMetaField("sample.SMPTEOffset.Minutes", data[1].ToString(), readTagParams.ReadAllMetaFrames);
            meta.SetMetaField("sample.SMPTEOffset.Seconds", data[2].ToString(), readTagParams.ReadAllMetaFrames);
            meta.SetMetaField("sample.SMPTEOffset.Frames", data[3].ToString(), readTagParams.ReadAllMetaFrames);

            // Num sample loops
            int numSampleLoops = WavHelper.ReadInt32(source, meta, "sample.NumSampleLoops", data, readTagParams.ReadAllMetaFrames);

            // Sample loops size (not useful here)
            source.Seek(4, SeekOrigin.Current);

            for (int i = 0; i < numSampleLoops; i++)
            {
                // Cue point ID
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].CuePointId", data, readTagParams.ReadAllMetaFrames);

                // Type
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].Type", data, readTagParams.ReadAllMetaFrames);

                // Start
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].Start", data, readTagParams.ReadAllMetaFrames);

                // End
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].End", data, readTagParams.ReadAllMetaFrames);

                // Fraction
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].Fraction", data, readTagParams.ReadAllMetaFrames);

                // Play count
                WavHelper.ReadInt32(source, meta, $"sample.SampleLoop[{i}].PlayCount", data, readTagParams.ReadAllMetaFrames);
            }
        }

        public static bool IsDataEligible(MetaDataHolder meta)
        {
            return WavHelper.IsDataEligible(meta, "sample.");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataHolder meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_SAMPLE));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Int values
            WavHelper.WriteFieldIntValue("sample.manufacturer", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue("sample.product", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue("sample.period", additionalFields, w, 1);
            WavHelper.WriteFieldIntValue("sample.MIDIUnityNote", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue("sample.MIDIPitchFraction", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue("sample.SMPTEFormat", additionalFields, w, 0);

            // SMPTE offset
            WavHelper.WriteFieldIntValue("sample.SMPTEOffset.Hours", additionalFields, w, (sbyte)0);
            WavHelper.WriteFieldIntValue("sample.SMPTEOffset.Minutes", additionalFields, w, (byte)0);
            WavHelper.WriteFieldIntValue("sample.SMPTEOffset.Seconds", additionalFields, w, (byte)0);
            WavHelper.WriteFieldIntValue("sample.SMPTEOffset.Frames", additionalFields, w, (byte)0);

            // == Sample loops

            // How many of them do we have ? -> count distinct indexes
            IList<string> keys = new List<string>();
            foreach (var s in additionalFields.Keys.Where(s => s.StartsWith("sample.SampleLoop")))
            {
                string key = s[..(s.IndexOf(']') + 1)];
                if (!keys.Contains(key)) keys.Add(key);
            }

            w.Write(keys.Count);

            // Sample loops data size
            w.Write(0); // We don't store anything else than the loop itself => value should be 0

            // Sample loops data
            foreach (string key in keys)
            {
                WavHelper.WriteFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".Type", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".Start", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".End", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".Fraction", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".PlayCount", additionalFields, w, 0);
            }

            long finalPos = w.BaseStream.Position;

            // Add the extra padding byte if needed
            long paddingSize = (finalPos - sizePos) % 2;
            if (paddingSize > 0) w.BaseStream.WriteByte(0);

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
    }
}
