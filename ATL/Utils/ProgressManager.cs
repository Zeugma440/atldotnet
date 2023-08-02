using System;
using System.Runtime.CompilerServices;

namespace ATL
{
    /// <summary>
    /// Class to handle progress report for sync and async operations
    /// at multiple levels
    /// </summary>
    public sealed class ProgressManager
    {
        private readonly bool isAsync;
        private readonly IProgress<float> asyncProgress = null;
        private readonly Action<float> syncProgress = null;
#pragma warning disable S4487 // Unread "private" fields should be removed (field is used for debugging / logging purposes)
        private readonly string name;
#pragma warning restore S4487
        private float minProgressBound = 0f;
        private float resolution;

        private int maxSections = 0;
        private int currentSection = 0;

        /// <summary>
        /// Maximum number of managed sections
        /// </summary>
        public int MaxSections
        {
            get
            {
                return maxSections;
            }
            set
            {
                maxSections = value;
                ComputeBounds();
            }
        }

        /// <summary>
        /// Section whose progress report is currently reported
        /// </summary>
        public int CurrentSection
        {
            get
            {
                return currentSection;
            }
            set
            {
                if (value < maxSections) currentSection = value;
                ComputeBounds();
            }
        }

        internal ProgressManager(ProgressToken<float> progress, string name = "", int maxSections = 0)
        {
            isAsync = progress.IsAsync;
            asyncProgress = isAsync ? progress.AsyncProgress : null;
            syncProgress = isAsync ? null : progress.SyncProgress;
            currentSection = 0;
            this.name = name;
            MaxSections = maxSections;
        }

        internal ProgressManager(IProgress<float> progress, string name = "", int maxSections = 0)
        {
            isAsync = true;
            asyncProgress = progress;
            currentSection = 0;
            this.name = name;
            MaxSections = maxSections;
        }

        internal ProgressManager(Action<float> progress, string name = "", int maxSections = 0)
        {
            isAsync = false;
            syncProgress = progress;
            currentSection = 0;
            this.name = name;
            MaxSections = maxSections;
        }

        private void ComputeBounds()
        {
            resolution = 1f / maxSections;
            minProgressBound = resolution * currentSection;
        }

        /// <summary>
        /// Create a ProgressToken to report progress for current section
        /// </summary>
        /// <returns>ProgressToken to report progress for current section</returns>
        public ProgressToken<float> CreateProgressToken()
        {
            float minBoundC = minProgressBound;
            float resolutionC = resolution;
            if (isAsync && asyncProgress != null)
                return new ProgressToken<float>(progress => asyncProgress.Report(minBoundC + resolutionC * progress));
            else if (!isAsync && syncProgress != null) 
                return new ProgressToken<float>(progress => syncProgress(minBoundC + resolutionC * progress));
            else return null;
        }
    }
}
