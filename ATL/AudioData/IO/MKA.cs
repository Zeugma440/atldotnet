using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ATL.Logging;
using Commons;
using SpawnDev.EBML;
using SpawnDev.EBML.Matroska;
using SpawnDev.EBML.WebM;
using static ATL.ChannelsArrangements;
using static ATL.TagData;

namespace ATL.AudioData.IO
{
    /// <summary>
    /// Class for Matroska Audio files manipulation (extension : .MKA)
    /// 
    /// Implementation notes
    ///
    /// Writing is not available yet as I encouter issues with the SpawnDev.EBML library
    /// 
    /// </summary>
    class MKA : MetaDataIO, IAudioDataIO
    {
        private const uint EBML_MAGIC_NUMBER = 0x1A45DFA3; // EBML header

        // Mapping between MKV format ID and ATL format IDs
        private static readonly Dictionary<string, int> codecsMapping = new Dictionary<string, int>
        {
            { "A_MPEG/L3", AudioDataIOFactory.CID_MP3 },
            { "A_MPEG/L2", AudioDataIOFactory.CID_MP3 },
            { "A_MPEG/L1", AudioDataIOFactory.CID_MP3 },
            { "A_PCM/INT/BIG", AudioDataIOFactory.CID_WAV },
            { "A_PCM/INT/LIT", AudioDataIOFactory.CID_WAV },
            { "A_PCM/FLOAT/IEEE", AudioDataIOFactory.CID_WAV },
            { "A_MPC", AudioDataIOFactory.CID_MPC },
            { "A_AC3", AudioDataIOFactory.CID_AC3 },
            { "A_AC3/BSID9", AudioDataIOFactory.CID_AC3 },
            { "A_AC3/BSID10", AudioDataIOFactory.CID_AC3 },
            // No support for ALAC
            { "A_DTS", AudioDataIOFactory.CID_DTS },
            { "A_DTS/EXPRESS", AudioDataIOFactory.CID_DTS },
            { "A_DTS/LOSSLESS", AudioDataIOFactory.CID_DTS },
            { "A_VORBIS", AudioDataIOFactory.CID_OGG },
            { "A_FLAC", AudioDataIOFactory.CID_FLAC },
            // No support for RealMedia
            // No support for MS ACM
            { "A_AAC/MPEG2/MAIN", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/LC", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/LC/SBR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG2/SSR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/MAIN", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LC", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LC/SBR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/SSR", AudioDataIOFactory.CID_AAC },
            { "A_AAC/MPEG4/LTP", AudioDataIOFactory.CID_AAC },
            // No support for QuickTime audio (though MP4 might be close)
            { "A_TTA1", AudioDataIOFactory.CID_TTA },
            { "A_WAVPACK4", AudioDataIOFactory.CID_WAVPACK }
            // No support for ATRAC1
        };

        // Mapping between MKV tag names and ATL frame codes
        private static readonly Dictionary<string, Field> frameMapping = new Dictionary<string, Field>() {
            { "track.description", Field.GENERAL_DESCRIPTION },
            { "track.title", Field.TITLE },
            { "track.artist", Field.ARTIST },
            { "album.composer", Field.COMPOSER },
            { "track.composer", Field.COMPOSER },
            { "track.comment", Field.COMMENT },
            { "track.genre", Field.GENRE},
            { "album.title", Field.ALBUM },
            { "track.part_number", Field.TRACK_NUMBER },
            { "track.total_parts", Field.TRACK_TOTAL },
            { "album.part_number", Field.DISC_NUMBER },
            { "album.total_parts", Field.DISC_TOTAL },
            { "track.rating", Field.RATING },
            { "track.copyright", Field.COPYRIGHT },
            { "album.artist", Field.ALBUM_ARTIST },
            { "track.publisher", Field.PUBLISHER },
            { "album.publisher", Field.PUBLISHER },
            { "track.conductor", Field.CONDUCTOR },
            { "album.conductor", Field.CONDUCTOR },
            { "track.lyrics", Field.LYRICS_UNSYNCH },
            { "album.date_released", Field.PUBLISHING_DATE },
            { "album.catalog_number", Field.CATALOG_NUMBER },
            { "track.bpm", Field.BPM },
            { "track.encoded_by", Field.ENCODED_BY },
            { "track.encoder", Field.ENCODER },
            { "album.isrc", Field.ISRC },
            { "album.purchase_item", Field.AUDIO_SOURCE_URL },
            { "album.lyricist", Field.LYRICIST },
            { "album.date_recorded", Field.RECORDING_DATE }
        };


        // Private declarations 
        private Format containerAudioFormat;
        private Format containeeAudioFormat;


        // ---------- INFORMATIVE INTERFACE IMPLEMENTATIONS & MANDATORY OVERRIDES

        public Format AudioFormat
        {
            get
            {
                if (!containerAudioFormat.Name.Contains('/'))
                {
                    containerAudioFormat = new Format(containerAudioFormat);
                    containerAudioFormat.Name += " / " + containeeAudioFormat.ShortName;
                    containerAudioFormat.ID += containeeAudioFormat.ID;
                }
                return containerAudioFormat;
            }
        }
        public bool IsVBR { get; private set; }
        public int CodecFamily { get; private set; }
        public string FileName { get; }
        public double BitRate { get; private set; }
        public int BitDepth { get; private set; }
        public double Duration { get; private set; }
        public int SampleRate { get; private set; }

        public ChannelsArrangement ChannelsArrangement { get; private set; }

        public List<MetaDataIOFactory.TagType> GetSupportedMetas()
        {
            return new List<MetaDataIOFactory.TagType> { MetaDataIOFactory.TagType.NATIVE };
        }

        protected override int getDefaultTagOffset()
        {
            return TO_BUILTIN;
        }

        protected override MetaDataIOFactory.TagType getImplementedTagType()
        {
            return MetaDataIOFactory.TagType.NATIVE;
        }

        protected override Field getFrameMapping(string zone, string ID, byte tagVersion)
        {
            Field supportedMetaId = Field.NO_FIELD;

            if (frameMapping.TryGetValue(ID, out var value)) supportedMetaId = value;

            return supportedMetaId;
        }

        public long AudioDataOffset { get; set; }
        public long AudioDataSize { get; set; }


        // ---------- CONSTRUCTORS & INITIALIZERS

        protected void resetData()
        {
            SampleRate = 0;
            Duration = 0;
            BitRate = 0;
            IsVBR = false;
            CodecFamily = 0;
            BitDepth = 0;
            ChannelsArrangement = null;
            AudioDataOffset = -1;
            AudioDataSize = 0;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MKA(string filePath, Format format)
        {
            this.FileName = filePath;
            containerAudioFormat = format;
            resetData();
        }

        public static bool IsValidHeader(byte[] data)
        {
            return EBML_MAGIC_NUMBER == StreamUtils.DecodeBEUInt32(data);
        }


        // ---------- SUPPORT METHODS

        private void readPhysicalData(WebMDocumentReader doc)
        {
            MasterElement audio = null;
            var tracks = doc.Tracks.Where(t => t is { TrackType: TrackType.Audio, FlagEnabled: true, FlagDefault: true }).ToList();
            if (tracks.Count > 0)
            {
                var track = tracks[0];

                var codecId = track.CodecID.ToUpper();
                if (codecsMapping.TryGetValue(codecId, out var value))
                {
                    var formats = AudioDataIOFactory.GetInstance().getFormats();
                    var format = formats.Where(f => f.ID == value).ToList();
                    if (format.Count > 0) containeeAudioFormat = format[0];
                }

                audio = track.GetContainer(MatroskaId.Audio);
            }
            else
            {
                containeeAudioFormat = Factory.UNKNOWN_FORMAT;
            }

            // Find AudioDataOffset using Clusters' timecodes
            // Try consuming less memory assuming cluster zero has timecode 0
            MasterElement startCluster;
            var clusterZero = doc.GetContainer(MatroskaId.Segment, MatroskaId.Cluster)!;
            if (0 == clusterZero.GetElement<UintElement>(MatroskaId.Timecode)!.Data) startCluster = clusterZero;
            else
            {
                // Search through all clusters
                startCluster = doc.GetContainers(MatroskaId.Segment, MatroskaId.Cluster)
                    .Find(c => 0 == c.GetElement<UintElement>(MatroskaId.Timecode)!.Data);
            }

            SegmentSource blockStream = null;
            var blocks = startCluster?.GetElements<BlockElement>(MatroskaId.BlockGroup, MatroskaId.Block)
                .Find(b => 0 == b.Timecode);
            if (null == blocks)
            {
                var simpleBlocks = startCluster?.GetElements<SimpleBlockElement>(MatroskaId.SimpleBlock)
                    .Find(sb => 0 == sb.Timecode);
                if (simpleBlocks?.Stream != null)
                {
                    // Additional offset to remove MKV metadata (e.g. SimpleBlock header & lacing information)
                    var streamOffset = 4; // 4 for "No Lacing"; does it vary according to lacing ?
                    blockStream = simpleBlocks.Stream.Slice(streamOffset, simpleBlocks.Stream.Length - streamOffset);
                }
            }
            else blockStream = blocks.Stream;

            if (blockStream != null)
            {
                AudioDataOffset = blockStream.Offset;
                // TODO AudioDataSize preferrably witout scanning all Clusters

                // Physical properties using the actual audio data header
                try
                {
                    if (containeeAudioFormat != Factory.UNKNOWN_FORMAT)
                    {
                        IAudioDataIO audioData = AudioDataIOFactory.GetInstance().GetFromStream(blockStream);
                        if (audioData.AudioFormat != Factory.UNKNOWN_FORMAT && audioData.Read(blockStream,
                                new AudioDataManager.SizeInfo(), new ReadTagParams()))
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_INFO, "Reading physical attributes from audio data");
                            CodecFamily = audioData.CodecFamily;
                            IsVBR = audioData.IsVBR;
                            BitRate = audioData.BitRate;
                            Duration = audioData.Duration;
                            SampleRate = audioData.SampleRate;
                            ChannelsArrangement = audioData.ChannelsArrangement;
                            BitDepth = audioData.BitDepth;
                        }
                    }
                }
                catch (Exception e)
                {
                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Couldn't parse inner audio data : " + e.Message);
                }
            }

            // Try getting Duration from MKA metadata
            if (Utils.ApproxEquals(Duration, 0))
            {
                var info = doc.SegmentInfo;
                if (info != null)
                {
                    var duration = info.GetElement<FloatElement>(MatroskaId.Duration)?.Data ?? 0;
                    var scale = info.GetElement<UintElement>(MatroskaId.TimecodeScale)?.Data ?? 0;
                    // Convert ns to ms
                    Duration = duration * scale / 1000000.0;
                }
            }

            if (audio != null && (0 == SampleRate || null == ChannelsArrangement || UNKNOWN == ChannelsArrangement))
            {
                if (0 == SampleRate) SampleRate = (int)(audio.GetElement<FloatElement>(MatroskaId.SamplingFrequency)?.Data ?? 0);
                ChannelsArrangement ??= GuessFromChannelNumber((int)(audio.GetElement<UintElement>(MatroskaId.Channels)?.Data ?? 0));
            }
        }

        private void readTag(MasterElement tag)
        {
            var targets = tag.GetContainer(MatroskaId.Targets)!;
            var targetTypeValue = targets.GetElement<UintElement>(MatroskaId.TargetTypeValue)?.Data ?? 0;
            var simpleTags = tag.GetContainers(MatroskaId.SimpleTag);

            switch (targetTypeValue)
            {
                case 50:
                    readSimpleTags("album", simpleTags);
                    break;
                case 30:
                    readSimpleTags("track", simpleTags);
                    break;
            }
        }

        private void readSimpleTags(string prefix, List<MasterElement> tags)
        {
            foreach (var tag in tags)
            {
                var tagName = tag.GetElement<UTF8StringElement>(MatroskaId.TagName)?.Data ?? "";
                var tagValue = tag.GetElement<UTF8StringElement>(MatroskaId.TagString)?.Data ?? "";
                SetMetaField(prefix + "." + tagName.ToLower(), tagValue, true);
            }
        }

        private void readAttachedFile(MasterElement file)
        {
            var data = file.GetElement<BinaryElement>(MatroskaId.FileData);
            if (data != null)
            {
                Stream stream = data.Stream;
                if (stream != null)
                {
                    var description = file.GetElement<UTF8StringElement>(MatroskaId.FileDescription)?.Data ?? "";
                    var name = file.GetElement<UTF8StringElement>(MatroskaId.FileName)?.Data ?? "";
                    
                    var picType = PictureInfo.PIC_TYPE.Generic;
                    if (name.Contains("cover", StringComparison.InvariantCultureIgnoreCase)) picType = PictureInfo.PIC_TYPE.Front;

                    stream.Position = 0;
                    var pic = PictureInfo.fromBinaryData(stream, (int)data.Length, picType, MetaDataIOFactory.TagType.NATIVE, 0);
                    pic.NativePicCodeStr = name;
                    pic.Description = description;
                    tagData.Pictures.Add(pic);
                }
            }
        }

        /// <inheritdoc/>
        public bool Read(Stream source, AudioDataManager.SizeInfo sizeInfo, ReadTagParams readTagParams)
        {
            return read(source, readTagParams);
        }

        /// <inheritdoc/>
        protected override bool read(Stream source, ReadTagParams readTagParams)
        {
            ResetData();
            source.Seek(0, SeekOrigin.Begin);
            var doc = new WebMDocumentReader(source);

            // Physical data
            readPhysicalData(doc);

            // Tags
            foreach (var tag in doc.GetContainers(MatroskaId.Segment, MatroskaId.Tags, MatroskaId.Tag))
                readTag(tag);

            // Embedded pictures
            foreach (var attachedFile in doc.GetContainers(MatroskaId.Segment, MatroskaId.Attachments, MatroskaId.AttachedFile))
                readAttachedFile(attachedFile);

            return true;
        }

        // WARNING : NOT IMPLEMENTED DUE TO THE LACK OF METADATA-RICH SAMPLE FILES
        /// <inheritdoc/>
        protected override int write(TagData tag, Stream s, string zone)
        {
            throw new NotImplementedException();
        }
    }
}