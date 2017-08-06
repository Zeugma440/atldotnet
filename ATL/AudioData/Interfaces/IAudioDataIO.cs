using System;
using System.IO;

namespace ATL.AudioData
{
	/// <summary>
	/// This Interface defines an object aimed at giving audio "physical" data information
	/// </summary>
	public interface IAudioDataIO
	{
        string FileName
        {
            get;
        }
        /// <summary>
        /// Bitrate of the file
        /// </summary>
        double BitRate
		{
			get;
		}
		/// <summary>
		/// Duration of the file (seconds)
		/// </summary>
		double Duration
		{
			get;
		}
        /// <summary>
		/// Sample rate of the file (Hz)
		/// </summary>
		int SampleRate
        {
            get;
        }
        /// <summary>
        /// Returns true if the bitrate is variable; false if not
        /// </summary>
        bool IsVBR
		{
			get;
		}
		/// <summary>
		/// Returns the family of the coded used for that file (see MetaDataManager for codec families)
		/// </summary>
		int CodecFamily
		{
			get;
		}
        /// <summary>
        /// Indicates if file format allows parsable metadata to be present (e.g. not MIDI files)
        /// </summary>
        bool AllowsParsableMetadata
        {
            get;
        }

        // TODO DOC
        bool IsMetaSupported(int metaDataType);

        /// <summary>
        /// Indicates if file format has a native metadata tagging system (e.g. not ID3v1, ID3v2 nor APEtag)
        /// </summary>
        bool HasNativeMeta();

        // TODO DOC
        bool Read(BinaryReader source, AudioDataManager.SizeInfo sizeInfo, MetaDataIO.ReadTagParams readTagParams);

        bool RewriteSizeMarkers(BinaryWriter w, int deltaSize);
    }
}
