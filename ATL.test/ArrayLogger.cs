using ATL.Logging;
using System.Collections.Generic;

namespace ATL.test
{
    public class ArrayLogger : ILogDevice
    {
        Log theLog = new Log();
        public IList<Log.LogItem> Items;

        public ArrayLogger()
        {
            LogDelegator.SetLog(ref theLog);
            Items = new List<Log.LogItem>();
            theLog.Register(this);
        }

        public void DoLog(Log.LogItem anItem)
        {
            Items.Add(anItem);
        }
    }
}
