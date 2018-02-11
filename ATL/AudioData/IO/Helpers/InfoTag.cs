using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    public static class InfoTag
    {
        public const string CHUNK_LIST = "LIST";
        public const string PURPOSE_INFO = "INFO";

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, uint chunkSize)
        {
            long position = source.Position;
            long initialPos = position;
            string key, value;
            int size;
            byte[] data = new byte[256];

            while (source.Position < initialPos + chunkSize - 4) // 4 being the "INFO" purpose that belongs to the chunk
            {
                // Key
                source.Read(data, 0, 4);
                key = Utils.Latin1Encoding.GetString(data, 0, 4);
                // Size
                source.Read(data, 0, 4);
                size = StreamUtils.DecodeInt32(data);
                // Value
                value = StreamUtils.ReadNullTerminatedString(source, Utils.Latin1Encoding);

                if (value.Length > 0) meta.SetMetaField("info." + key, value, readTagParams.ReadAllMetaFrames);

                position = source.Position;
            }
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            if (meta.Title.Length > 0) return true;
            if (meta.Artist.Length > 0) return true;
            if (meta.Comment.Length > 0) return true;
            if (meta.Genre.Length > 0) return true;
            if (meta.Copyright.Length > 0) return true;

            foreach (string key in meta.AdditionalFields.Keys)
            {
                if (key.StartsWith("info.")) return true;
            }

            return false;
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_LIST));

            long sizePos = w.BaseStream.Position;
            w.Write((int)0); // Placeholder for chunk size that will be rewritten at the end of the method

            w.Write(Utils.Latin1Encoding.GetBytes(PURPOSE_INFO));

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
            // Copyright
            value = Utils.ProtectValue(meta.Copyright);
            if (0 == value.Length && additionalFields.Keys.Contains("info.ICOP")) value = additionalFields["info.ICOP"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICOP", value, w, writtenFields);
            // Genre
            value = Utils.ProtectValue(meta.Genre);
            if (0 == value.Length && additionalFields.Keys.Contains("info.IGNR")) value = additionalFields["info.IGNR"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("IGNR", value, w, writtenFields);
            // Comment
            value = Utils.ProtectValue(meta.Comment);
            if (0 == value.Length && additionalFields.Keys.Contains("info.ICMT")) value = additionalFields["info.ICMT"];
            if (value.Length > 0) writeSizeAndNullTerminatedString("ICMT", value, w, writtenFields);

            string shortKey;
            foreach(string key in additionalFields.Keys)
            {
                if (key.StartsWith("info."))
                {
                    shortKey = key.Substring(5, key.Length - 5).ToUpper();
                    if (!writtenFields.ContainsKey(key))
                    {
                        if (additionalFields[key].Length > 0) writeSizeAndNullTerminatedString(shortKey, additionalFields[key], w, writtenFields);
                    }
                }
            }

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

        private static void writeSizeAndNullTerminatedString(string key, string value, BinaryWriter w, IDictionary<string, string> writtenFields)
        {
            if (key.Length > 4)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + key + "' : LIST.INFO field key must be 4-characters long; cropping");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            } else if (key.Length < 4)
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'" + key + "' : LIST.INFO field key must be 4-characters long; completing with whitespaces");
                key = Utils.BuildStrictLengthString(key, 4, ' ');
            }
            w.Write(Utils.Latin1Encoding.GetBytes(key));

            byte[] buffer = Utils.Latin1Encoding.GetBytes(value);
            w.Write(buffer.Length);
            w.Write(buffer);
            w.Write((byte)0); // String is null-terminated

            writtenFields.Add("info."+key, value);
        }
    }
}
