using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpawnDev.EBML.Matroska;

namespace SpawnDev.EBML.WebM
{
    /// <summary>
    /// WebM and Matroska document reader
    /// </summary>
    public class WebMDocumentReader : EBMLDocumentReader
    {
        public WebMDocumentReader(Stream? stream = null) : base(stream, new List<EBMLSchema> { new WebMSchema(), new MatroskaSchema() })
        {

        }
        /// <summary>
        /// Returns the segment element if it exists
        /// </summary>
        public MasterElement? Segment => GetElement<MasterElement>(MatroskaId.Segment);
        /// <summary>
        /// Returns the segment info element if it exists
        /// </summary>
        public MasterElement? SegmentInfo => GetElement<MasterElement>(MatroskaId.Segment, MatroskaId.Info);
        /// <summary>
        /// Get and Set for TimecodeScale from the segment info element
        /// </summary>
        public virtual uint? TimecodeScale
        {
            get
            {
                var timecodeScale = GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.TimecodeScale);
                return timecodeScale != null ? (uint?)timecodeScale.Data : null;
            }
            set
            {
                var timecodeScale = GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.TimecodeScale);
                if (timecodeScale == null)
                {
                    if (value != null)
                    {
                        var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
                        info!.Add(MatroskaId.TimecodeScale, value.Value);
                    }
                }
                else
                {
                    if (value == null)
                    {
                        var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
                        info!.Remove(timecodeScale);
                    }
                    else
                    {
                        timecodeScale.Data = value.Value;
                    }
                }
            }
        }
        /// <summary>
        /// Returns the title string from the segment info element
        /// </summary>
        public virtual string? Title
        {
            get
            {
                var title = GetElement<StringElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.Title);
                return (string?)title;
            }
            set
            {
                var title = GetElement<StringElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.Title);
                if (title == null)
                {
                    if (value != null)
                    {
                        var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
                        info!.Add(MatroskaId.Title, value);
                    }
                }
                else
                {
                    if (value == null)
                    {
                        title.Remove();
                    }
                    else
                    {
                        title.Data = value;
                    }
                }
            }
        }
        /// <summary>
        /// Returns the muxing app string from the segment info element
        /// </summary>
        public virtual string? MuxingApp
        {
            get
            {
                var docType = GetElement<StringElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.MuxingApp);
                return docType != null ? docType.Data : null;
            }
        }
        /// <summary>
        /// Returns the writing app string from the segment info element
        /// </summary>
        public virtual string? WritingApp
        {
            get
            {
                var docType = GetElement<StringElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.WritingApp);
                return docType != null ? docType.Data : null;
            }
        }
        /// <summary>
        /// Returns true if audio tracks exist
        /// </summary>
        public virtual bool HasAudio => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Audio).Any();
        /// <summary>
        /// Returns the audio channels value from the first audio track
        /// </summary>
        public virtual uint? AudioChannels
        {
            get
            {
                var channels = GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry, MatroskaId.Audio, MatroskaId.Channels);
                return channels != null ? (uint?)channels : null;
            }
        }
        /// <summary>
        /// Returns the audio sampling frequency of the first audio track
        /// </summary>
        public virtual double? AudioSamplingFrequency
        {
            get
            {
                var samplingFrequency = GetElement<FloatElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry, MatroskaId.Audio, MatroskaId.SamplingFrequency);
                return samplingFrequency != null ? (double?)samplingFrequency : null;
            }
        }
        /// <summary>
        /// Returns the audio bit depth of the first audio track
        /// </summary>
        public virtual uint? AudioBitDepth
        {
            get
            {
                var bitDepth = GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry, MatroskaId.Audio, MatroskaId.BitDepth);
                return bitDepth != null ? (uint?)bitDepth : null;
            }
        }

        public List<TrackEntryElement> Tracks => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).ToList();
        /// <summary>
        /// Returns a list of video TrackEntryElements
        /// </summary>
        /// <returns></returns>
        public List<TrackEntryElement> VideoTracks => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Video).ToList();
        /// <summary>
        /// Returns a list of audio TrackEntryElements
        /// </summary>
        /// <returns></returns>
        public List<TrackEntryElement> AudioTracks => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Audio).ToList();
        /// <summary>
        /// Returns true if video tracks exist
        /// </summary>
        public virtual bool HasVideo => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Video).Any();
        /// <summary>
        /// Returns the video codec id of the first video track
        /// </summary>
        public virtual string? VideoCodecID => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Video).FirstOrDefault()?.CodecID;
        /// <summary>
        /// Returns the audio codec id of the first audio track
        /// </summary>
        public virtual string? AudioCodecID => GetElements<TrackEntryElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry).Where(o => o.TrackType == TrackType.Audio).FirstOrDefault()?.CodecID;
        /// <summary>
        /// Returns the video pixel width of the first video track
        /// </summary>
        public virtual uint? VideoPixelWidth => (uint?)GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry, MatroskaId.Video, MatroskaId.PixelWidth);
        /// <summary>
        /// Returns the video pixel height of the first video track
        /// </summary>
        public virtual uint? VideoPixelHeight => (uint?)GetElement<UintElement>(MatroskaId.Segment, MatroskaId.Tracks, MatroskaId.TrackEntry, MatroskaId.Video, MatroskaId.PixelHeight);
        /// <summary>
        /// Get and Set for the first segment block duration
        /// </summary>
        public virtual double? Duration
        {
            get
            {
                var duration = GetElement<FloatElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.Duration);
                return duration?.Data;
            }
            set
            {
                var duration = GetElement<FloatElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.Duration);
                if (duration == null)
                {
                    if (value != null)
                    {
                        var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
                        info!.Add(MatroskaId.Duration, value.Value);
                    }
                }
                else
                {
                    if (value == null)
                    {
                        var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
                        info!.Remove(duration);
                    }
                    else
                    {
                        duration.Data = value.Value;
                    }
                }
            }
        }
        /// <summary>
        /// If the Duration is not set in the first segment block, the duration will be calculated using Cluster and SimpleBlock data and written to Duration
        /// </summary>
        /// <returns></returns>
        public virtual bool FixDuration()
        {
            if (EBML == null) return false;
            if (Duration == null)
            {
                var durationEstimate = GetDurationEstimate();
                Duration = durationEstimate;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Duration calculated using Cluster and SimpleBlock data and written to Duration
        /// </summary>
        /// <returns></returns>
        public virtual double GetDurationEstimate()
        {
            if (EBML == null) return 0;
            var lastCluster = GetContainers(MatroskaId.Segment, MatroskaId.Cluster).LastOrDefault();
            if (lastCluster == null) return 0;
            var timecode = lastCluster.GetElement<UintElement>(MatroskaId.Timecode);
            if (timecode == null) return 0;
            double duration = timecode.Data;
            var simpleBlocks = lastCluster.GetElements<SimpleBlockElement>(MatroskaId.SimpleBlock);
            var simpleBlockLast = simpleBlocks.LastOrDefault();
            if (simpleBlockLast != null)
            {
                var trackId = simpleBlockLast.TrackId;
                var trackSimpleBlocks = simpleBlocks.Where(o => o.TrackId == trackId).ToList();
                duration += simpleBlockLast.Timecode;
                if (trackSimpleBlocks.Count > 1)
                {
                    var i = trackSimpleBlocks.Count - 2;
                    var blockDuration = (double)trackSimpleBlocks[i + 1].Timecode - (double)trackSimpleBlocks[i].Timecode;
                    duration += blockDuration;
                }
            }
            return duration;
        }
    }
}
