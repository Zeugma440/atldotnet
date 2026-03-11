using System;

namespace ATL
{
    /// <summary>
    /// Class to handle progress report for sync and async operations
    /// at multiple levels
    /// </summary>
    public sealed class ProgressManager
    {
        private readonly bool isAsync;
        private readonly IProgress<float> asyncProgress;
        private readonly Action<float> syncProgress;
#pragma warning disable S4487 // Unread "private" fields should be removed (field is used for debugging / logging purposes)
        // ReSharper disable once NotAccessedField.Local
        private readonly string name;
#pragma warning restore S4487
        private float minProgressBound;
        private float resolution;

        private int maxSections;
        private int currentSection;

        /// <summary>
        /// Maximum number of managed sections
        /// </summary>
        public int MaxSections
        {
            get => maxSections;
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
            get => currentSection;
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
