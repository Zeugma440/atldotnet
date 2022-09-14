using System;

namespace ATL
{
    internal class ProgressManager
    {
        private readonly Action<float> progress;
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

        internal ProgressManager(Action<float> progress, string name = "", int maxSections = 0)
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

        public Action<float> CreateAction()
        {
            float minBoundC = minProgressBound;
            float resolutionC = resolution;
            return new Action<float>(progress => this.progress(minBoundC + resolutionC * progress));
        }
    }
}
