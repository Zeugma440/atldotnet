using System;

namespace ATL
{
    internal class ProgressManager
    {
        private readonly IProgress<float> progress;
        private readonly string name;
        private float minProgressBound = 0f;
        private float resolution;

        private int maxSections = 0;
        private int currentSection = 0;

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

        private void ComputeBounds()
        {
            resolution = 1f / maxSections;
            minProgressBound = resolution * currentSection;
        }

        public IProgress<float> CreateIProgress()
        {
            float minBoundC = minProgressBound;
            float resolutionC = resolution;
            return new Progress<float>(progress => this.progress.Report(minBoundC + resolutionC * progress));
        }
    }
}
