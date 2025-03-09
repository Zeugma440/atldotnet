using Commons;
using System.Collections.Generic;
using System.IO;
using ATL.Logging;
using static ATL.AudioData.IO.MetaDataIO;
using System;
using System.Linq;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Represents a CART (Radio traffic delivery data / AES Standard AES46-2002) metadata set
    ///
    /// Implementation notes
    ///     - Does not support old implementations prior to v1.00
    /// </summary>
    internal static class CartTag
    {
        /// <summary>
        /// Identifier of a cart chunk
        /// </summary>
        public const string CHUNK_CART = "cart";

        /// <summary>
        /// Read a cart chunk from the given source into the given Metadata I/O, using the given read parameters
        /// </summary>
        /// <param name="source">Stream to read data from</param>
        /// <param name="meta">Metadata I/O to copy metadata to</param>
        /// <param name="readTagParams">Read parameters to use</param>
        /// <param name="chunkSize">Size of the current chunk</param>
        public static void FromStream(Stream source, MetaDataIO meta, ReadTagParams readTagParams, long chunkSize)
        {
            byte[] data = new byte[1024];
            long initialPosition = source.Position;

            // Version
            if (source.Read(data, 0, 4) < 4) return;
            var majorVersion = Utils.Latin1Encoding.GetString(data, 0, 2);
            if (majorVersion.StartsWith('0')) majorVersion = majorVersion[1..];
            var minorVersion = Utils.Latin1Encoding.GetString(data, 2, 2);
            meta.SetMetaField("cart.version", majorVersion + "." + minorVersion, readTagParams.ReadAllMetaFrames);

            // Title
            WavHelper.Latin1FromStream(source, 64, meta, "cart.title", data, readTagParams.ReadAllMetaFrames);

            // Artist
            WavHelper.Latin1FromStream(source, 64, meta, "cart.artist", data, readTagParams.ReadAllMetaFrames);

            // Cut number
            WavHelper.Latin1FromStream(source, 64, meta, "cart.cutNumber", data, readTagParams.ReadAllMetaFrames);

            // Client ID
            WavHelper.Latin1FromStream(source, 64, meta, "cart.clientId", data, readTagParams.ReadAllMetaFrames);

            // Category
            WavHelper.Latin1FromStream(source, 64, meta, "cart.category", data, readTagParams.ReadAllMetaFrames);

            // Classification
            WavHelper.Latin1FromStream(source, 64, meta, "cart.classification", data, readTagParams.ReadAllMetaFrames);

            // Out cue text
            WavHelper.Latin1FromStream(source, 64, meta, "cart.outCue", data, readTagParams.ReadAllMetaFrames);

            // Start date (YYYY-MM-DD)
            WavHelper.Latin1FromStream(source, 10, meta, "cart.startDate", data, readTagParams.ReadAllMetaFrames);

            // Start time (hh:mm:ss)
            WavHelper.Latin1FromStream(source, 8, meta, "cart.startTime", data, readTagParams.ReadAllMetaFrames);

            // End date (YYYY-MM-DD)
            WavHelper.Latin1FromStream(source, 10, meta, "cart.endDate", data, readTagParams.ReadAllMetaFrames);

            // End time (hh:mm:ss)
            WavHelper.Latin1FromStream(source, 8, meta, "cart.endTime", data, readTagParams.ReadAllMetaFrames);

            // Producer app ID
            WavHelper.Latin1FromStream(source, 64, meta, "cart.producerAppId", data, readTagParams.ReadAllMetaFrames);

            // Producer app version
            WavHelper.Latin1FromStream(source, 64, meta, "cart.producerAppVersion", data, readTagParams.ReadAllMetaFrames);

            // User-defined
            WavHelper.Latin1FromStream(source, 64, meta, "cart.userDef", data, readTagParams.ReadAllMetaFrames);

            // Sample value for 0 dB reference
            if (source.Read(data, 0, 4) < 4) return;
            var value = StreamUtils.DecodeInt32(data);
            meta.SetMetaField("cart.dwLevelReference", value.ToString(), readTagParams.ReadAllMetaFrames);

            // Timer usage ID
            string str;
            for (int i = 0; i < 8; i++)
            {
                if (source.Read(data, 0, 4) < 4) return;
                str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 4).Trim());
                meta.SetMetaField("cart.postTimerUsageId[" + (i + 1) + "]", str, readTagParams.ReadAllMetaFrames);

                // Timer value in samples from head
                if (source.Read(data, 0, 4) < 4) return;
                var uValue = StreamUtils.DecodeUInt32(data);
                meta.SetMetaField("cart.postTimerValue[" + (i + 1) + "]", uValue.ToString(), readTagParams.ReadAllMetaFrames);
            }

            // Reserved
            source.Seek(276, SeekOrigin.Current);

            // URL
            WavHelper.Latin1FromStream(source, 1024, meta, "cart.url", data, readTagParams.ReadAllMetaFrames);

            // Free form text for scripts or tags
            int leftBytes = (int)Math.Min(int.MaxValue, chunkSize - (source.Position - initialPosition));
            if (leftBytes <= 0) return;
            if (leftBytes > 1024) data = new byte[leftBytes];

            if (source.Read(data, 0, leftBytes) < leftBytes) return;
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            // Strip ending CR LF
            foreach (var c in Utils.CR_LF.Reverse()) if (str.EndsWith((char)c)) str = str[..^2];
            meta.SetMetaField("cart.tagText", str, readTagParams.ReadAllMetaFrames);
        }

        /// <summary>
        /// Indicate whether the given Metadata I/O contains metadata relevant to the Cart format
        /// </summary>
        /// <param name="meta">Metadata I/O to test with</param>
        /// <returns>True if the given Metadata I/O contains data relevant to the Cart format; false if it doesn't</returns>
        public static bool IsDataEligible(MetaDataHolder meta)
        {
            return WavHelper.IsDataEligible(meta, "cart.");
        }

        /// <summary>
        /// Write Cart metadata from the given Metadata I/O to the given writer, using the given endianness for the size headers
        /// </summary>
        /// <param name="w">Writer to write data to</param>
        /// <param name="isLittleEndian">Endianness to write the size headers with</param>
        /// <param name="meta">Metadata to write</param>
        /// <returns>The number of written fields</returns>
        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataHolder meta)
        {
            IDictionary<string, string> additionalFields = meta.AdditionalFields;
            w.Write(Utils.Latin1Encoding.GetBytes(CHUNK_CART));

            long sizePos = w.BaseStream.Position;
            w.Write(0); // Placeholder for chunk size that will be rewritten at the end of the method

            // Version
            WavHelper.WriteFixedTextValue("0101", 4, w);

            // String properties part.1
            WavHelper.WriteFixedFieldTextValue("cart.title", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.artist", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.cutNumber", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.clientId", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.category", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.classification", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.outCue", additionalFields, 64, w);

            // Dates and times
            if (additionalFields.TryGetValue("cart.startDate", out var startDate) && !Utils.CheckDateFormat(startDate))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'cart.startDate' : error writing field - YYYY-MM-DD format required; {startDate} found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.startDate", additionalFields, 10, w);
            if (additionalFields.TryGetValue("cart.startTime", out var startTime) && !Utils.CheckTimeFormat(startTime))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'cart.startTime' : error writing field - hh:mm:ss format required; {startTime} found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.startTime", additionalFields, 8, w);

            if (additionalFields.TryGetValue("cart.endDate", out var endDate) && !Utils.CheckDateFormat(endDate))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'cart.endDate' : error writing field - YYYY-MM-DD format required; {endDate} found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.endDate", additionalFields, 10, w);
            if (additionalFields.TryGetValue("cart.endTime", out var endTime) && !Utils.CheckTimeFormat(endTime))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, $"'cart.endTime' : error writing field - hh:mm:ss format required; {endTime} found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.endTime", additionalFields, 8, w);

            // String properties part.2
            WavHelper.WriteFixedFieldTextValue("cart.producerAppId", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.producerAppVersion", additionalFields, 64, w);
            WavHelper.WriteFixedFieldTextValue("cart.userDef", additionalFields, 64, w);

            // Int32 sample value for 0 dB reference
            WavHelper.WriteFieldIntValue("cart.dwLevelReference", additionalFields, w, 0);

            // PostTimer
            for (int i = 0; i < 8; i++)
            {
                WavHelper.WriteFixedFieldTextValue("cart.postTimerUsageId[" + (i + 1) + "]", additionalFields, 4, w);
                WavHelper.WriteFieldIntValue("cart.postTimerValue[" + (i + 1) + "]", additionalFields, w, (uint)0);
            }

            // Reserved
            for (int i = 0; i < 276; i++) w.Write((byte)0);

            // URL
            WavHelper.WriteFixedFieldTextValue("cart.url", additionalFields, 1024, w);

            // Free form text for scripts or tags
            if (additionalFields.TryGetValue("cart.tagText", out var additionalField))
            {
                var textData = Utils.Latin1Encoding.GetBytes(additionalField);
                w.Write(textData);
            }
            w.Write(Utils.CR_LF);


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

            return 14;
        }
    }
}
