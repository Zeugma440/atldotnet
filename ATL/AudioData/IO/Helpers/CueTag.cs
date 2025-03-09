using Commons;
using System.Collections.Generic;
using System.IO;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Represents a Cue-Points metadata set (See Cue-points Chunk of the RIFF WAV format spec)
    /// </summary>
    internal static class CueTag
    {
        /// <summary>
        /// Identifier of a cue-points chunk
        /// </summary>
        public const string CHUNK_CUE = "cue ";

        /// <summary>
        /// Read a cue-points chunk from the given source into the given Metadata I/O, using the given read parameters
        /// </summary>
        /// <param name="source">Stream to read data from</param>
        /// <param name="meta">Metadata I/O to copy metadata to</param>
        /// <param name="readTagParams">Read parameters to use</param>
        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams)
        {
            byte[] data = new byte[256];

            // Num cue points
            int numCuePoints = WavHelper.ReadInt32(source, meta, "cue.NumCuePoints", data, readTagParams.ReadAllMetaFrames);

            for (int i = 0; i < numCuePoints; i++)
            {
                // Cue point ID
                WavHelper.ReadInt32(source, meta, $"cue.CuePoints[{i}].CuePointId", data, readTagParams.ReadAllMetaFrames);

                // Play order position
                WavHelper.ReadInt32(source, meta, $"cue.CuePoints[{i}].Position", data, readTagParams.ReadAllMetaFrames);

                // RIFF ID of corresponding data chunk
                if (source.Read(data, 0, 4) < 4) return;
                meta.SetMetaField($"cue.CuePoints[{i}].DataChunkId", Utils.Latin1Encoding.GetString(data, 0, 4), readTagParams.ReadAllMetaFrames);

                // Byte Offset of Data Chunk
                WavHelper.ReadInt32(source, meta, $"cue.CuePoints[{i}].ChunkStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample of First Channel
                WavHelper.ReadInt32(source, meta, $"cue.CuePoints[{i}].BlockStart", data, readTagParams.ReadAllMetaFrames);

                // Byte Offset to sample byte of First Channel
                WavHelper.ReadInt32(source, meta, $"cue.CuePoints[{i}].SampleOffset", data, readTagParams.ReadAllMetaFrames);
            }
        }

        /// <summary>
        /// Indicate whether the given Metadata I/O contains metadata relevant to the Cue-points format
        /// </summary>
        /// <param name="meta">Metadata I/O to test with</param>
        /// <returns>True if the given Metadata I/O contains data relevant to the Cue-points format; false if it doesn't</returns>
        public static bool IsDataEligible(MetaDataHolder meta)
        {
            return WavHelper.IsDataEligible(meta, "cue.");
        }

        /// <summary>
        /// Write Cue-points metadata from the given Metadata I/O to the given writer, using the given endianness for the size headers
        /// </summary>
        /// <param name="w">Writer to write data to</param>
        /// <param name="isLittleEndian">Endianness to write the size headers with</param>
        /// <param name="meta">Metadata to write</param>
        /// <returns>The number of written fields</returns>
        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataHolder meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_CUE));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // == Cue points list

            // How many of them do we have ? -> count distinct indexes
            IList<string> keys = WavHelper.GetEligibleKeys("cue.CuePoints", additionalFields.Keys);
            w.Write(keys.Count);

            // Cue points data
            foreach (string key in keys)
            {
                WavHelper.WriteFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".Position", additionalFields, w, 0);
                w.Write(Utils.Latin1Encoding.GetBytes(additionalFields[key + ".DataChunkId"]));
                WavHelper.WriteFieldIntValue(key + ".ChunkStart", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".BlockStart", additionalFields, w, 0);
                WavHelper.WriteFieldIntValue(key + ".SampleOffset", additionalFields, w, 0);
            }

            // Add the extra padding byte if needed
            long finalPos = w.BaseStream.Position;
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
