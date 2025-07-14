using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;
using System.Linq;
using System;
using System.Text;

namespace ATL.AudioData.IO
{
    internal static class List
    {
        public const string CHUNK_LIST = "LIST";

        // Purposes
        public const string PURPOSE_INFO = "INFO";
        public const string PURPOSE_ADTL = "adtl";

        // Associated Data List (adtl) sub-chunks
        private const string CHUNK_LABEL = "labl";
        private const string CHUNK_NOTE = "note";
        private const string CHUNK_LABELED_TEXT = "ltxt";

        public static string FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            long position = source.Position;
            long initialPos = position;

            // Purpose
            byte[] data = new byte[4];
            if (source.Read(data, 0, 4) < 4) return "";
            string typeId = Utils.Latin1Encoding.GetString(data, 0, 4);

            long maxPos = initialPos + chunkSize - 4; // 4 being the purpose 32bits tag that belongs to the chunk

            if (typeId.Equals(PURPOSE_INFO, StringComparison.OrdinalIgnoreCase)) readInfoPurpose(source, meta, readTagParams, chunkSize, maxPos);
            else if (typeId.Equals(PURPOSE_ADTL, StringComparison.OrdinalIgnoreCase)) readDataListPurpose(source, meta, readTagParams, maxPos);

            return typeId;
        }

        private static void readInfoPurpose(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize, long maxPos)
        {
            byte[] data = new byte[chunkSize];

            while (source.Position < maxPos)
            {
                // Key
                if (source.Read(data, 0, 4) < 4) return;
                var key = Utils.Latin1Encoding.GetString(data, 0, 4);
                // Size
                if (source.Read(data, 0, 4) < 4) return;
                var size = StreamUtils.DecodeInt32(data);
                // Do _NOT_ use StreamUtils.ReadNullTerminatedString because non-textual fields may be found here (e.g. NITR)
                if (size > 0)
                {
                    if (source.Read(data, 0, size) < size) return;
                    // Manage parasite zeroes at the end of data
                    if (source.Position < maxPos && source.ReadByte() != 0) source.Seek(-1, SeekOrigin.Current);
                    var value = Encoding.UTF8.GetString(data, 0, size);
                    meta.SetMetaField($"info.{key}", Utils.StripEndingZeroChars(value), readTagParams.ReadAllMetaFrames);

                    WavHelper.SkipEndPadding(source, maxPos);
                }
            }
        }

        private static void readDataListPurpose(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long maxPos)
        {
            int position = 0;
            byte[] data = new byte[4];

            while (source.Position < maxPos)
            {
                // Sub-chunk ID
                if (source.Read(data, 0, 4) < 4) return;
                var id = Utils.Latin1Encoding.GetString(data, 0, 4);
                // Size
                if (source.Read(data, 0, 4) < 4) return;
                var size = StreamUtils.DecodeInt32(data);
                if (size <= 0) continue;

                meta.SetMetaField($"adtl.Labels[{position}].Type", id, readTagParams.ReadAllMetaFrames);
                if (id.Equals(CHUNK_LABEL, StringComparison.OrdinalIgnoreCase)) readLabelSubChunk(source, meta, position, size, readTagParams);
                else if (id.Equals(CHUNK_NOTE, StringComparison.OrdinalIgnoreCase)) readLabelSubChunk(source, meta, position, size, readTagParams);
                else if (id.Equals(CHUNK_LABELED_TEXT, StringComparison.OrdinalIgnoreCase)) readLabeledTextSubChunk(source, meta, position, size, readTagParams);

                WavHelper.SkipEndPadding(source, maxPos);
                position++;
            }
        }

        private static void readLabelSubChunk(Stream source, MetaDataIO meta, int position, int size, ReadTagParams readTagParams)
        {
            byte[] data = new byte[Math.Max(4, size - 4)];
            WavHelper.ReadInt32(source, meta, $"adtl.Labels[{position}].CuePointId", data, readTagParams.ReadAllMetaFrames);

            if (source.Read(data, 0, size - 4) < size - 4) return;
            string value = Encoding.UTF8.GetString(data, 0, size - 4);
            value = Utils.StripEndingZeroChars(value); // Not ideal but effortslessly handles the ending zero

            meta.SetMetaField($"adtl.Labels[{position}].Text", value, readTagParams.ReadAllMetaFrames);
        }

        private static void readLabeledTextSubChunk(Stream source, MetaDataIO meta, int position, int size, ReadTagParams readTagParams)
        {
            byte[] data = new byte[Math.Max(4, size - 4)];
            WavHelper.ReadInt32(source, meta, $"adtl.Labels[{position}].CuePointId", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt32(source, meta, $"adtl.Labels[{position}].SampleLength", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt32(source, meta, $"adtl.Labels[{position}].PurposeId", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt16(source, meta, $"adtl.Labels[{position}].Country", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt16(source, meta, $"adtl.Labels[{position}].Language", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt16(source, meta, $"adtl.Labels[{position}].Dialect", data, readTagParams.ReadAllMetaFrames);
            WavHelper.ReadInt16(source, meta, $"adtl.Labels[{position}].CodePage", data, readTagParams.ReadAllMetaFrames);

            if (source.Read(data, 0, size - 20) < size - 20) return;
            string value = Encoding.UTF8.GetString(data, 0, size - 20);
            value = Utils.StripEndingZeroChars(value); // Not ideal but effortslessly handles the ending zero

            meta.SetMetaField($"adtl.Labels[{position}].Text", value, readTagParams.ReadAllMetaFrames);
        }

        public static bool IsDataEligible(MetaDataHolder meta)
        {
            if (meta.Title.Length > 0) return true;
            if (meta.Album.Length > 0) return true;
            if (meta.Artist.Length > 0) return true;
            if (meta.Comment.Length > 0) return true;
            if (meta.Genre.Length > 0) return true;
            if (meta.Date > DateTime.MinValue) return true;
            if (meta.Copyright.Length > 0) return true;
            if (meta.Popularity > 0) return true;
            if (meta.EncodedBy.Length > 0) return true;
            if (meta.Encoder.Length > 0) return true;
            if (meta.Language.Length > 0) return true;

            return WavHelper.IsDataEligible(meta, "info.") || WavHelper.IsDataEligible(meta, "adtl.");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, string purpose, MetaDataHolder tag, MetaDataIO metaIO)
        {
            IDictionary<string, string> additionalFields = tag.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_LIST));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            w.Write(Utils.Latin1Encoding.GetBytes(purpose));

            if (purpose.Equals(PURPOSE_INFO, StringComparison.OrdinalIgnoreCase)) writeInfoPurpose(w, tag, metaIO);
            else if (purpose.Equals(PURPOSE_ADTL, StringComparison.OrdinalIgnoreCase)) writeDataListPurpose(w, isLittleEndian, additionalFields);

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

        private static void writeInfoPurpose(BinaryWriter w, MetaDataHolder meta, MetaDataIO metaIO)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;

            // 'Classic' fields (NB : usually done within a loop by accessing MetaDataIO.tagData)
            IDictionary<string, string> writtenFields = new Dictionary<string, string>();
            // Title
            string value = Utils.ProtectValue(meta.Title);
            if (0 == value.Length && additionalFields.TryGetValue("info.INAM", out var field)) value = field;
            if (value.Length > 0) writeSizeAndNullTerminatedString("INAM", value, w, writtenFields);
            // Artist
            value = Utils.ProtectValue(meta.Artist);
            if (0 == value.Length && additionalFields.TryGetValue("info.IART", out var additionalField)) value = additionalField;
            if (value.Length > 0) writeSizeAndNullTerminatedString("IART", value, w, writtenFields);
            // Album
            value = Utils.ProtectValue(meta.Album);
            if (0 == value.Length && additionalFields.TryGetValue("info.IPRD", out var field1)) value = field1;
            if (value.Length > 0) writeSizeAndNullTerminatedString("IPRD", value, w, writtenFields);
            // Comment
            value = Utils.ProtectValue(meta.Comment);
            if (0 == value.Length && additionalFields.TryGetValue("info.ICMT", out var additionalField1)) value = additionalField1;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICMT", value, w, writtenFields);
            // Copyright
            value = Utils.ProtectValue(meta.Copyright);
            if (0 == value.Length && additionalFields.TryGetValue("info.ICOP", out var field2)) value = field2;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICOP", value, w, writtenFields);
            // Recording date
            value = metaIO.EncodeDate(meta.Date);
            if (0 == value.Length && additionalFields.TryGetValue("info.ICRD", out var additionalField2)) value = additionalField2;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICRD", value, w, writtenFields);
            // Genre
            value = Utils.ProtectValue(meta.Genre);
            if (0 == value.Length && additionalFields.TryGetValue("info.IGNR", out var field3)) value = field3;
            if (value.Length > 0) writeSizeAndNullTerminatedString("IGNR", value, w, writtenFields);
            // Rating
            if (meta.Popularity > 0)
            {
                value = (5 * meta.Popularity).ToString();
                if (0 == value.Length && additionalFields.TryGetValue("info.IRTD", out var additionalField3)) value = additionalField3;
                if (value.Length > 0) writeSizeAndNullTerminatedString("IRTD", value, w, writtenFields);
            }
            // Track number
            if (!string.IsNullOrEmpty(meta.TrackNumber))
            {
                value = meta.TrackNumber;
                if (0 == value.Length && additionalFields.TryGetValue("info.TRCK", out var additionalField3)) value = additionalField3;
                if (0 == value.Length && additionalFields.TryGetValue("info.IPRT", out var field4)) value = field4;
                if (0 == value.Length && additionalFields.TryGetValue("info.ITRK", out var additionalField4)) value = additionalField4;
                if (value.Length > 0) writeSizeAndNullTerminatedString("TRCK", value, w, writtenFields);
                if (value.Length > 0) writeSizeAndNullTerminatedString("IPRT", value, w, writtenFields);
                if (value.Length > 0) writeSizeAndNullTerminatedString("ITRK", value, w, writtenFields);
            }
            // Encoded by
            value = Utils.ProtectValue(meta.EncodedBy);
            if (0 == value.Length && additionalFields.TryGetValue("info.ITCH", out var field5)) value = field5;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ITCH", value, w, writtenFields);
            // Encoder
            value = Utils.ProtectValue(meta.Encoder);
            if (0 == value.Length && additionalFields.TryGetValue("info.ISFT", out var field6)) value = field6;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ISFT", value, w, writtenFields);
            // Language
            value = Utils.ProtectValue(meta.Language);
            if (0 == value.Length && additionalFields.TryGetValue("info.ILNG", out var field7)) value = field7;
            if (value.Length > 0) writeSizeAndNullTerminatedString("ILNG", value, w, writtenFields);

            foreach (var key in additionalFields.Keys.Where(key => key.StartsWith("info.")))
            {
                var shortKey = key.Substring(5, key.Length - 5).ToUpper();
                if (!writtenFields.ContainsKey(key) && additionalFields[key].Length > 0)
                    writeSizeAndNullTerminatedString(shortKey, metaIO.FormatBeforeWriting(additionalFields[key]), w, writtenFields);
            }
        }

        private static void writeDataListPurpose(BinaryWriter w, bool isLittleEndian, IDictionary<string, string> additionalFields)
        {
            // Inventory of all positions
            IList<string> keys = new List<string>();
            foreach (var s in additionalFields.Keys.Where(s => s.StartsWith("adtl.Labels")))
            {
                string key = s[..(s.IndexOf(']') + 1)];
                if (!keys.Contains(key)) keys.Add(key);
            }

            foreach (string key in keys)
            {
                // Type
                string type = Utils.ProtectValue(additionalFields[key + ".Type"]);
                w.Write(Utils.Latin1Encoding.GetBytes(type));

                // Size
                long sizePos = w.BaseStream.Position;
                w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

                int writtenSize = 0;
                if (type.Equals(CHUNK_LABEL, StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabelSubChunk(w, key, additionalFields);
                else if (type.Equals(CHUNK_NOTE, StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabelSubChunk(w, key, additionalFields);
                else if (type.Equals(CHUNK_LABELED_TEXT, StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabeledTextSubChunk(w, key, additionalFields);

                long finalPos = w.BaseStream.Position;
                w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                if (isLittleEndian) w.Write(writtenSize);
                else w.Write(StreamUtils.EncodeBEInt32(writtenSize));

                w.BaseStream.Seek(finalPos, SeekOrigin.Begin);
            }
        }

        private static int writeLabelSubChunk(BinaryWriter w, string key, IDictionary<string, string> additionalFields)
        {
            WavHelper.WriteFieldIntValue(key + ".CuePointId", additionalFields, w, 0);

            string text = additionalFields[key + ".Text"];
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            // Needs one byte of padding if data size is odd
            int paddingByte = (buffer.Length + 1) % 2;

            int size = buffer.Length + 1 + 4; // Size shouldn't take padding byte into account, per specs

            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated
            if (paddingByte > 0) // Add padding byte if needed
                w.Write((byte)0);

            return size;
        }

        private static int writeLabeledTextSubChunk(BinaryWriter w, string key, IDictionary<string, string> additionalFields)
        {
            WavHelper.WriteFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue(key + ".SampleLength", additionalFields, w, 0);
            WavHelper.WriteFieldIntValue(key + ".PurposeId", additionalFields, w, 0);

            WavHelper.WriteFieldIntValue(key + ".Country", additionalFields, w, (short)0);
            WavHelper.WriteFieldIntValue(key + ".Language", additionalFields, w, (short)0);
            WavHelper.WriteFieldIntValue(key + ".Dialect", additionalFields, w, (short)0);
            WavHelper.WriteFieldIntValue(key + ".CodePage", additionalFields, w, (short)0);

            string text = additionalFields[key + ".Text"];
            byte[] buffer = Encoding.UTF8.GetBytes(text);

            // Needs one byte of padding if data size is odd
            int paddingByte = (buffer.Length + 1) % 2;

            int size = buffer.Length + 1 + 20; // Size shouldn't take padding byte into account, per specs

            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated
            if (paddingByte > 0) // Add padding byte if needed
                w.Write((byte)0);

            return size;
        }

        private static void writeSizeAndNullTerminatedString(string key, string value, BinaryWriter w, IDictionary<string, string> writtenFields)
        {
            if (key.Length > 4)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'{key}' : LIST.INFO field key must be 4-characters long; cropping");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            }
            else if (key.Length < 4)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'{key}' : LIST.INFO field key must be 4-characters long; completing with whitespaces");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            }
            w.Write(Utils.Latin1Encoding.GetBytes(key));

            byte[] buffer = Encoding.UTF8.GetBytes(value);
            // Needs one byte of padding if data size is odd
            int paddingByte = (buffer.Length + 1) % 2;
            w.Write(buffer.Length + 1 + paddingByte);
            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated
            if (paddingByte > 0) // Add padding byte if needed
                w.Write((byte)0);

            string keyFull = "info." + key;
            if (!writtenFields.TryAdd(keyFull, value))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'{key}' : already written");
            }
        }
    }
}
