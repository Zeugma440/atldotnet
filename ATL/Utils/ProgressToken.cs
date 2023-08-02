using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATL
{
    public sealed class ProgressToken<T>
    {
        private readonly bool isAsync;
        private readonly IProgress<T> asyncProgress = null;
        private readonly Action<T> syncProgress = null;

        public ProgressToken(IProgress<T> progress)
        {
            isAsync = true;
            asyncProgress = progress;
        }

        public ProgressToken(Action<T> progress)
        {
            isAsync = false;
            syncProgress = progress;
        }

        public void Report(T value)
        {
            if (isAsync) asyncProgress.Report(value);
            else syncProgress(value);
        }
    }
}
