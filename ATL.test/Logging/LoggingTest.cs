using ATL.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;

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
        public void Log_Sync()
        {
            messages.Clear();

            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message");

            Assert.AreEqual(Log.LV_DEBUG, messages[0].Level);
            Assert.AreEqual("test message", messages[0].Message);
        }

        [TestMethod]
        public void Log_ASync_SwitchSync()
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
        public void Log_ASync_FlushQueue()
        {
            messages.Clear();

            theLog.SwitchAsync();
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message");

            Assert.AreEqual(0, messages.Count);
            theLog.FlushQueue();


            Assert.AreEqual(Log.LV_DEBUG, messages[0].Level);
            Assert.AreEqual("test message", messages[0].Message);
        }

        [TestMethod]
        public void Log_Location()
        {
            messages.Clear();

            LogDelegator.GetLocateDelegate()("here");
            LogDelegator.GetLogDelegate()(Log.LV_INFO, "test1");
            Assert.AreEqual("here", messages[0].Location);

            LogDelegator.GetLocateDelegate()("there");
            LogDelegator.GetLogDelegate()(Log.LV_INFO, "test2");
            Assert.AreEqual("there", messages[1].Location);
        }

        [TestMethod]
        public void Log_Location_MultiThread()
        {
            messages.Clear();

            Thread thread = new Thread(log_Location_MultiThread_sub);
            thread.Start();

            LogDelegator.GetLocateDelegate()("here");
            LogDelegator.GetLogDelegate()(Log.LV_ERROR, "testI");
            LogDelegator.GetLogDelegate()(Log.LV_ERROR, "testD");

            Thread.Sleep(200);

            Assert.AreEqual(messages.Count, 4);
            foreach (Log.LogItem logItem in messages)
            {
                if (logItem.Level.Equals(Log.LV_WARNING)) Assert.AreEqual(logItem.Location, "over there");
                if (logItem.Level.Equals(Log.LV_ERROR)) Assert.AreEqual(logItem.Location, "here");
            }
        }

        private void log_Location_MultiThread_sub()
        {
            LogDelegator.GetLocateDelegate()("over there");
            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "testE");
            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "testW");
        }

        public void DoLog(Log.LogItem anItem)
        {
            messages.Add(anItem);
        }
    }
}
