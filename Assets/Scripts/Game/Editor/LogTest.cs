using NUnit.Framework;
using System;
using System.IO;
using Log = Framework.Log.Log;

namespace Game.Tests
{
    public class LogTest
    {
        [Test]
        public void LogPath_FailedChangeKeepsExistingWriterAndReopenAppends()
        {
            var original = Log.Path;
            var directory = Path.Combine(Path.GetTempPath(), "PokerLogTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "test.log");
            var lockedPath = Path.Combine(directory, "locked.log");
            try
            {
                Log.Path = path;
                Log.WriteInfo("first entry");
                using (var locked = File.Open(lockedPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    Assert.Throws<IOException>(() => Log.Path = lockedPath);
                Assert.AreEqual(path, Log.Path);
                Log.WriteInfo("second entry");
                Log.Close();
                Assert.DoesNotThrow(() => Log.WriteInfo("after shutdown"));
                Log.Path = path;
                Log.WriteInfo("third entry");
                Log.Close();
                var text = File.ReadAllText(path);
                StringAssert.Contains("first entry", text);
                StringAssert.Contains("second entry", text);
                StringAssert.Contains("third entry", text);
            }
            finally
            {
                Log.Close();
                if (!string.IsNullOrEmpty(original))
                    Log.Path = original;
                File.Delete(path);
                File.Delete(lockedPath);
                Directory.Delete(directory);
            }
        }
    }
}
