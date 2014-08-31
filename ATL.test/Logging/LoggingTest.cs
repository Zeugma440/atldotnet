using ATL.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace ATL.test
{
    [TestClass]
    public class LoggingTest : ILogDevice
    {
        Log theLog = new Log();
        IList<Log.LogItem> messages = new List<Log.LogItem>();

        public LoggingTest()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        [TestMethod]
        public void TestSyncMessage()
        {
            messages.Clear();

            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message");

            Assert.AreEqual(Log.LV_DEBUG, messages[0].Level);
            Assert.AreEqual("test message", messages[0].Message);
        }

        [TestMethod]
        public void TestASyncMessage1()
        {
            messages.Clear();

            theLog.SwitchAsync();
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message");

            Assert.AreEqual(0, messages.Count);
            theLog.SwitchSync();

            Assert.AreEqual(Log.LV_DEBUG, messages[0].Level);
            Assert.AreEqual("test message", messages[0].Message);

            LogDelegator.GetLogDelegate()(Log.LV_INFO, "test message2");

            Assert.AreEqual(Log.LV_INFO, messages[1].Level);
            Assert.AreEqual("test message2", messages[1].Message);
        }

        [TestMethod]
        public void TestASyncMessage2()
        {
            messages.Clear();

            theLog.SwitchAsync();
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message");

            Assert.AreEqual(0, messages.Count);
            theLog.FlushQueue();

            Assert.AreEqual(Log.LV_DEBUG, messages[0].Level);
            Assert.AreEqual("test message", messages[0].Message);
        }

        public void DoLog(Log.LogItem anItem)
        {
            messages.Add(anItem);
        }
    }
}
