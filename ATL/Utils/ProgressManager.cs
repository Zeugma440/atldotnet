using System;

namespace ATL
{
    /// <summary>
    /// Class to handle progress report for sync and async operations
    /// at multiple levels
    /// </summary>
    public sealed class ProgressManager
    {
        private readonly IProgress<float> progress = null;
        private readonly Action<float> actionProgress = null;
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

        internal ProgressManager(IProgress<float> progress, string name = "", int maxSections = 0)
        {
            this.progress = progress;
            currentSection = 0;
            this.name = name;
            MaxSections = maxSections;
        }

        internal ProgressManager(Action<float> progress, string name = "", int maxSections = 0)
        {
            this.actionProgress = progress;
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
        /// Create an Action to report sync progress for current section
        /// </summary>
        /// <returns>Action to report sync progress for current section</returns>
        public Action<float> CreateAction()
        {
            float minBoundC = minProgressBound;
            float resolutionC = resolution;
            return new Action<float>(progress => this.actionProgress(minBoundC + resolutionC * progress));
        }

        /// <summary>
        /// Create an IProgress to report async progress for current section
        /// </summary>
        /// <returns>IProgress to report async progress for current section</returns>
        public IProgress<float> CreateIProgress()
        {
            float minBoundC = minProgressBound;
            float resolutionC = resolution;
            return new Progress<float>(progress => this.progress.Report(minBoundC + resolutionC * progress));
        }
    }
}
