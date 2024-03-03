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
    /// Represents a CART ("cart"; see AES Standard AES46-2002) metadata set
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
            source.Read(data, 0, 4);
            var majorVersion = Utils.Latin1Encoding.GetString(data, 0, 2);
            if (majorVersion.StartsWith('0')) majorVersion = majorVersion[1..];
            var minorVersion = Utils.Latin1Encoding.GetString(data, 2, 2);
            meta.SetMetaField("cart.version", majorVersion + "." + minorVersion, readTagParams.ReadAllMetaFrames);

            // Title
            source.Read(data, 0, 64);
            var str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.title", str, readTagParams.ReadAllMetaFrames);

            // Artist
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.artist", str, readTagParams.ReadAllMetaFrames);

            // Cut number
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.cutNumber", str, readTagParams.ReadAllMetaFrames);

            // Client ID
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.clientId", str, readTagParams.ReadAllMetaFrames);

            // Category
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.category", str, readTagParams.ReadAllMetaFrames);

            // Classification
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.classification", str, readTagParams.ReadAllMetaFrames);

            // Out cue text
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.outCue", str, readTagParams.ReadAllMetaFrames);

            // Start date (YYYY-MM-DD)
            source.Read(data, 0, 10);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 10).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.startDate", str, readTagParams.ReadAllMetaFrames);

            // Start time (hh:mm:ss)
            source.Read(data, 0, 8);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 8).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.startTime", str, readTagParams.ReadAllMetaFrames);

            // End date (YYYY-MM-DD)
            source.Read(data, 0, 10);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 10).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.endDate", str, readTagParams.ReadAllMetaFrames);

            // End time (hh:mm:ss)
            source.Read(data, 0, 8);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 8).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.endTime", str, readTagParams.ReadAllMetaFrames);

            // Producer app ID
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.producerAppId", str, readTagParams.ReadAllMetaFrames);

            // Producer app version
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.producerAppVersion", str, readTagParams.ReadAllMetaFrames);

            // User-defined
            source.Read(data, 0, 64);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data).Trim());
            if (str.Length > 0) meta.SetMetaField("cart.userDef", str, readTagParams.ReadAllMetaFrames);

            // Sample value for 0 dB reference
            source.Read(data, 0, 4);
            var value = StreamUtils.DecodeInt32(data);
            meta.SetMetaField("cart.dwLevelReference", value.ToString(), readTagParams.ReadAllMetaFrames);

            // Timer usage ID
            for (int i = 0; i < 8; i++)
            {
                source.Read(data, 0, 4);
                str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 4).Trim());
                meta.SetMetaField("cart.postTimerUsageId[" + (i + 1) + "]", str, readTagParams.ReadAllMetaFrames);

                // Timer value in samples from head
                source.Read(data, 0, 4);
                var uValue = StreamUtils.DecodeUInt32(data);
                meta.SetMetaField("cart.postTimerValue[" + (i + 1) + "]", uValue.ToString(), readTagParams.ReadAllMetaFrames);
            }

            // Reserved
            source.Seek(276, SeekOrigin.Current);

            // URL
            source.Read(data, 0, 1024);
            str = Utils.StripEndingZeroChars(Utils.Latin1Encoding.GetString(data, 0, 1024).Trim());
            meta.SetMetaField("cart.url", str, readTagParams.ReadAllMetaFrames);

            // Free form text for scripts or tags
            int leftBytes = (int)Math.Min(int.MaxValue, chunkSize - (source.Position - initialPosition));
            if (leftBytes <= 0) return;
            if (leftBytes > 1024) data = new byte[leftBytes];

            source.Read(data, 0, leftBytes);
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
        public static bool IsDataEligible(MetaDataIO meta)
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
        public static int ToStream(BinaryWriter w, bool isLittleEndian, MetaDataIO meta)
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
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'cart.startDate' : error writing field - YYYY-MM-DD format required; " + startDate + " found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.startDate", additionalFields, 10, w);
            if (additionalFields.TryGetValue("cart.startTime", out var startTime) && !Utils.CheckTimeFormat(startTime))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'cart.startTime' : error writing field - hh:mm:ss format required; " + startTime + " found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.startTime", additionalFields, 8, w);

            if (additionalFields.TryGetValue("cart.endDate", out var endDate) && !Utils.CheckDateFormat(endDate))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'cart.endDate' : error writing field - YYYY-MM-DD format required; " + endDate + " found");
            }
            WavHelper.WriteFixedFieldTextValue("cart.endDate", additionalFields, 10, w);
            if (additionalFields.TryGetValue("cart.endTime", out var endTime) && !Utils.CheckTimeFormat(endTime))
            {
                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "'cart.endTime' : error writing field - hh:mm:ss format required; " + endTime + " found");
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
