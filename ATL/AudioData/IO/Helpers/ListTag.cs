using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;
using System.Linq;

namespace ATL.AudioData.IO
{
    public static class ListTag
    {
        public const string CHUNK_LIST = "LIST";

        // Purposes
        private const string PURPOSE_INFO = "INFO";
        private const string PURPOSE_ADTL = "adtl";

        // Associated Data List (adtl) sub-chunks
        private const string CHUNK_LABEL = "labl";
        private const string CHUNK_NOTE = "note";
        private const string CHUNK_LABELED_TEXT = "ltxt";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, uint chunkSize)
        {
            long position = source.Position;
            long initialPos = position;

            // Purpose
            byte[] data = new byte[4];
            source.Read(data, 0, 4);
            string typeId = Utils.Latin1Encoding.GetString(data, 0, 4);
            meta.SetMetaField("list.TypeId", typeId, readTagParams.ReadAllMetaFrames);

            long maxPos = initialPos + chunkSize - 4; // 4 being the purpose 32bits tag that belongs to the chunk

            if (typeId.Equals(PURPOSE_INFO, System.StringComparison.OrdinalIgnoreCase)) readInfoPurpose(source, meta, readTagParams, chunkSize, maxPos);
            else if (typeId.Equals(PURPOSE_ADTL, System.StringComparison.OrdinalIgnoreCase)) readDataListPurpose(source, meta, readTagParams, maxPos);
        }

        private static void readInfoPurpose(Stream source, MetaDataIO meta, ReadTagParams readTagParams, uint chunkSize, long maxPos)
        {
            byte[] data = new byte[chunkSize];
            string key, value;
            int size;

            while (source.Position < maxPos)
            {
                // Key
                source.Read(data, 0, 4);
                key = Utils.Latin1Encoding.GetString(data, 0, 4);
                // Size
                source.Read(data, 0, 4);
                size = StreamUtils.DecodeInt32(data);
                // Do _NOT_ use StreamUtils.ReadNullTerminatedString because non-textual fields may be found here (e.g. NITR)
                if (size > 0)
                {
                    source.Read(data, 0, size);
                    // Manage parasite zeroes at the end of data
                    if (source.Position < maxPos && source.ReadByte() != 0) source.Seek(-1, SeekOrigin.Current);
                    value = Utils.Latin1Encoding.GetString(data, 0, size);
                    meta.SetMetaField("info." + key, Utils.StripEndingZeroChars(value), readTagParams.ReadAllMetaFrames);
                }
            }
        }

        private static void readDataListPurpose(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long maxPos)
        {
            string id;
            int size;
            int position = 0;
            byte[] data = new byte[4];

            while (source.Position < maxPos)
            {
                // Sub-chunk ID
                source.Read(data, 0, 4);
                id = Utils.Latin1Encoding.GetString(data, 0, 4);
                // Size
                source.Read(data, 0, 4);
                size = StreamUtils.DecodeInt32(data);
                if (size > 0)
                {
                    meta.SetMetaField("info.Labels[" + position + "].Type", id, readTagParams.ReadAllMetaFrames);
                    if (id.Equals(CHUNK_LABEL, System.StringComparison.OrdinalIgnoreCase)) readLabelSubChunk(source, meta, position, size, readTagParams);
                    else if (id.Equals(CHUNK_NOTE, System.StringComparison.OrdinalIgnoreCase)) readLabelSubChunk(source, meta, position, size, readTagParams);
                    else if (id.Equals(CHUNK_LABELED_TEXT, System.StringComparison.OrdinalIgnoreCase)) readLabeledTextSubChunk(source, meta, position, size, readTagParams);

                    // Manage parasite zeroes at the end of data
                    if (source.Position < maxPos && source.ReadByte() != 0) source.Seek(-1, SeekOrigin.Current);

                    position++;
                }
            }
        }

        private static void readLabelSubChunk(Stream source, MetaDataIO meta, int position, int size, ReadTagParams readTagParams)
        {
            byte[] data = new byte[size - 4];
            WavUtils.readInt32(source, meta, "info.Labels[" + position + "].CuePointId", data, readTagParams.ReadAllMetaFrames);

            source.Read(data, 0, size - 4);
            string value = Utils.Latin1Encoding.GetString(data, 0, size - 4);
            value = Utils.StripEndingZeroChars(value); // Not ideal but effortslessly handles the ending zero and the even padding

            meta.SetMetaField("info.Labels[" + position + "].Text", value, readTagParams.ReadAllMetaFrames);
        }

        private static void readLabeledTextSubChunk(Stream source, MetaDataIO meta, int position, int size, ReadTagParams readTagParams)
        {
            byte[] data = new byte[size - 4];
            WavUtils.readInt32(source, meta, "info.Labels[" + position + "].CuePointId", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt32(source, meta, "info.Labels[" + position + "].SampleLength", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt32(source, meta, "info.Labels[" + position + "].PurposeId", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt16(source, meta, "info.Labels[" + position + "].Country", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt16(source, meta, "info.Labels[" + position + "].Language", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt16(source, meta, "info.Labels[" + position + "].Dialect", data, readTagParams.ReadAllMetaFrames);
            WavUtils.readInt16(source, meta, "info.Labels[" + position + "].CodePage", data, readTagParams.ReadAllMetaFrames);

            source.Read(data, 0, size - 20);
            string value = Utils.Latin1Encoding.GetString(data, 0, size - 20);
            value = Utils.StripEndingZeroChars(value); // Not ideal but effortslessly handles the ending zero and the even padding

            meta.SetMetaField("info.Labels[" + position + "].Text", value, readTagParams.ReadAllMetaFrames);
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            if (meta.Title.Length > 0) return true;
            if (meta.Artist.Length > 0) return true;
            if (meta.Comment.Length > 0) return true;
            if (meta.Genre.Length > 0) return true;
            if (meta.Date > System.DateTime.MinValue) return true;
            if (meta.Copyright.Length > 0) return true;

            return WavUtils.IsDataEligible(meta, "info.");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_LIST));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            string typeId = PURPOSE_INFO;
            if (additionalFields.ContainsKey("list.TypeId")) typeId = additionalFields["list.TypeId"];
            else
                foreach (var _ in additionalFields.Keys.Where(key => key.StartsWith("info.Labels")).Select(key => new { }))
                {
                    typeId = PURPOSE_ADTL;
                    break;
                }

            w.Write(Utils.Latin1Encoding.GetBytes(typeId));

            if (typeId.Equals(PURPOSE_INFO, System.StringComparison.OrdinalIgnoreCase)) writeInfoPurpose(w, meta);
            else if (typeId.Equals(PURPOSE_ADTL, System.StringComparison.OrdinalIgnoreCase)) writeDataListPurpose(w, isLittleEndian, additionalFields);

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

        private static void writeInfoPurpose(BinaryWriter w, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;

            // 'Classic' fields (NB : usually done within a loop by accessing MetaDataIO.tagData)
            IDictionary<string, string> writtenFields = new Dictionary<string, string>();
            // Title
            string value = Utils.ProtectValue(meta.Title);
            if (0 == value.Length && additionalFields.Keys.Contains("info.INAM")) value = additionalFields["info.INAM"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("INAM", value, w, writtenFields);
            // Artist
            value = Utils.ProtectValue(meta.Artist);
            if (0 == value.Length && additionalFields.Keys.Contains("info.IART")) value = additionalFields["info.IART"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("IART", value, w, writtenFields);
            // Comment
            value = Utils.ProtectValue(meta.Comment);
            if (0 == value.Length && additionalFields.Keys.Contains("info.ICMT")) value = additionalFields["info.ICMT"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICMT", value, w, writtenFields);
            // Copyright
            value = Utils.ProtectValue(meta.Copyright);
            if (0 == value.Length && additionalFields.Keys.Contains("info.ICOP")) value = additionalFields["info.ICOP"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICOP", value, w, writtenFields);
            // Year
            value = Utils.ProtectYear(meta.Date);
            if (0 == value.Length && additionalFields.Keys.Contains("info.ICRD")) value = additionalFields["info.ICRD"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICRD", value, w, writtenFields);
            // Genre
            value = Utils.ProtectValue(meta.Genre);
            if (0 == value.Length && additionalFields.Keys.Contains("info.IGNR")) value = additionalFields["info.IGNR"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("IGNR", value, w, writtenFields);

            string shortKey;
            foreach (var key in additionalFields.Keys.Where(key => key.StartsWith("info.")))
            {
                shortKey = key.Substring(5, key.Length - 5).ToUpper();
                if (!writtenFields.ContainsKey(key))
                {
                    if (additionalFields[key].Length > 0) writeSizeAndNullTerminatedString(shortKey, additionalFields[key], w, writtenFields);
                }
            }
        }

        private static void writeDataListPurpose(BinaryWriter w, bool isLittleEndian, IDictionary<string, string> additionalFields)
        {
            // Inventory of all positions
            IList<string> keys = new List<string>();
            foreach (var s in additionalFields.Keys.Where(s => s.StartsWith("info.Labels")))
            {
                string key = s.Substring(0, s.IndexOf("]") + 1);
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
                if (type.Equals(CHUNK_LABEL, System.StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabelSubChunk(w, key, additionalFields);
                else if (type.Equals(CHUNK_NOTE, System.StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabelSubChunk(w, key, additionalFields);
                else if (type.Equals(CHUNK_LABELED_TEXT, System.StringComparison.OrdinalIgnoreCase)) writtenSize = writeLabeledTextSubChunk(w, key, additionalFields);

                long finalPos = w.BaseStream.Position;
                w.BaseStream.Seek(sizePos, SeekOrigin.Begin);
                if (isLittleEndian) w.Write(writtenSize);
                else w.Write(StreamUtils.EncodeBEInt32(writtenSize));

                w.BaseStream.Seek(finalPos, SeekOrigin.Begin);
            }
        }

        private static int writeLabelSubChunk(BinaryWriter w, string key, IDictionary<string, string> additionalFields)
        {
            WavUtils.writeFieldIntValue(key + ".CuePointId", additionalFields, w, 0);

            string text = additionalFields[key + ".Text"];
            byte[] buffer = Utils.Latin1Encoding.GetBytes(text);

            // Needs one byte of padding if data size is odd
            int paddingByte = ((buffer.Length + 1) % 2 > 0) ? 1 : 0;

            int size = buffer.Length + 1 + 4; // Size shouldn't take padding byte into account, per specs

            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated
            if (paddingByte > 0) // Add padding byte if needed
                w.Write((byte)0);

            return size;
        }

        private static int writeLabeledTextSubChunk(BinaryWriter w, string key, IDictionary<string, string> additionalFields)
        {
            WavUtils.writeFieldIntValue(key + ".CuePointId", additionalFields, w, 0);
            WavUtils.writeFieldIntValue(key + ".SampleLength", additionalFields, w, 0);
            WavUtils.writeFieldIntValue(key + ".PurposeId", additionalFields, w, 0);

            WavUtils.writeFieldIntValue(key + ".Country", additionalFields, w, (short)0);
            WavUtils.writeFieldIntValue(key + ".Language", additionalFields, w, (short)0);
            WavUtils.writeFieldIntValue(key + ".Dialect", additionalFields, w, (short)0);
            WavUtils.writeFieldIntValue(key + ".CodePage", additionalFields, w, (short)0);

            string text = additionalFields[key + ".Text"];
            byte[] buffer = Utils.Latin1Encoding.GetBytes(text);

            // Needs one byte of padding if data size is odd
            int paddingByte = ((buffer.Length + 1) % 2 > 0) ? 1 : 0;

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
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + key + "' : LIST.INFO field key must be 4-characters long; cropping");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            }
            else if (key.Length < 4)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + key + "' : LIST.INFO field key must be 4-characters long; completing with whitespaces");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            }
            w.Write(Utils.Latin1Encoding.GetBytes(key));

            byte[] buffer = Utils.Latin1Encoding.GetBytes(value);
            // Needs one byte of padding if data size is odd
            int paddingByte = ((buffer.Length + 1) % 2 > 0) ? 1 : 0;
            w.Write(buffer.Length + 1 + paddingByte);
            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated
            if (paddingByte > 0) // Add padding byte if needed
                w.Write((byte)0);

            writtenFields.Add("info." + key, value);
        }
    }
}
