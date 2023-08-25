using System;

namespace ATL
{
    /// <summary>
    /// Handle used to signal the progress of a process
    /// </summary>
    /// <typeparam name="T">Type to use to report progress</typeparam>
    public sealed class ProgressToken<T>
    {
        private readonly bool isAsync;
        private readonly IProgress<T> asyncProgress;
        private readonly Action<T> syncProgress;

        internal bool IsAsync => isAsync;
        internal IProgress<T> AsyncProgress => asyncProgress;
        internal Action<T> SyncProgress => syncProgress;


        internal ProgressToken(IProgress<T> progress)
        {
            isAsync = true;
            asyncProgress = progress;
        }

        internal ProgressToken(Action<T> progress)
        {
            isAsync = false;
            syncProgress = progress;
        }

        internal void Report(T value)
        {
            if (isAsync && asyncProgress != null) asyncProgress.Report(value);
            else if (!isAsync && syncProgress != null) syncProgress(value);
        }
    }
}
