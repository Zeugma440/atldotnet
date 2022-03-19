using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;

namespace ATL.AudioData.IO
{
    public static class DispTag
    {
        public const string CHUNK_DISP = "disp";

        /// <summary>
        /// Text
        /// </summary>
        public const int CF_TEXT = 1;
        /// <summary>
        /// Bitmap (.BMP files)
        /// </summary>
        public const int CF_BITMAP = 2;
        /// <summary>
        /// Metafile (.WMF files)
        /// </summary>
        public const int CF_METAFILE = 3;
        /// <summary>
        /// Device-independent bitmap
        /// </summary>
        public const int CF_DIB = 8;
        /// <summary>
        /// Color palette
        /// </summary>
        public const int CF_PALETTE = 9;

        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, uint chunkSize)
        {
            if (chunkSize <= 4) return;
            byte[] data = new byte[chunkSize - 4];

            IList<string> keys = WavHelper.getEligibleKeys("disp", meta.AdditionalFields.Keys);
            int index = keys.Count;

            // Type
            source.Read(data, 0, 4);
            int type = StreamUtils.DecodeInt32(data);
            meta.SetMetaField("disp[" + index + "].type", getCfLabel(type), readTagParams.ReadAllMetaFrames);

            // Data
            source.Read(data, 0, (int)chunkSize - 4);
            string dataStr;
            if (CF_TEXT == type) dataStr = Utils.Latin1Encoding.GetString(data);
            else
            {
                dataStr = Utils.Latin1Encoding.GetString(Utils.EncodeTo64(data));
            }
            meta.SetMetaField("disp[" + index + "].value", dataStr, readTagParams.ReadAllMetaFrames);
        }

        private static string getCfLabel(int code)
        {
            switch (code)
            {
                case CF_TEXT: return "CF_TEXT";
                case CF_BITMAP: return "CF_BITMAP";
                case CF_METAFILE: return "CF_METAFILE";
                case CF_DIB: return "CF_DIB";
                case CF_PALETTE: return "CF_PALETTE";
                default: return "";
            }
        }

        private static int getCfCode(string label)
        {
            switch (label)
            {
                case "CF_TEXT": return CF_TEXT;
                case "CF_BITMAP": return CF_BITMAP;
                case "CF_METAFILE": return CF_METAFILE;
                case "CF_DIB": return CF_DIB;
                case "CF_PALETTE": return CF_PALETTE;
                default: return 0;
            }
        }

        public static bool IsDataEligible(MetaDataIO meta)
        {
            return WavHelper.IsDataEligible(meta, "disp[");
        }

        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;

            IList<string> keys = WavHelper.getEligibleKeys("disp", additionalFields.Keys);
            foreach (string key in keys) writeDispChunk(w, isLittleEndian, additionalFields, key);

            return keys.Count;
        }
        private static void writeDispChunk(BinaryWriter w, bool isLittleEndian, IDictionary<string, string> additionalFields, string key)
        {
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_DISP));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Type
            string field = key + ".type";
            int type = -1;
            if (additionalFields.Keys.Contains(field))
            {
                type = getCfCode(additionalFields[field]);
                w.Write(type);
            }

            // Value
            field = key + ".value";
            if (additionalFields.Keys.Contains(field))
            {
                string valueStr = additionalFields[field];
                byte[] value;
                if (CF_TEXT == type) value = Utils.Latin1Encoding.GetBytes(valueStr);
                else value = Utils.DecodeFrom64(Utils.Latin1Encoding.GetBytes(valueStr));
                w.Write(value);
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

            w.BaseStream.Seek(finalPos, SeekOrigin.Begin);
        }
    }
}
