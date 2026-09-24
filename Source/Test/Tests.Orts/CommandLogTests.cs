using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Simulation.Commanding;

namespace Tests.Orts
{
    [TestClass]
    public class CommandLogTests
    {
        [TestMethod]
        public void ReplayRoundTripsCommandStateWithoutBinaryFormatter()
        {
            string replayPath = Path.Combine(Path.GetTempPath(), $"riel-replay-{Guid.NewGuid():N}.replay");
            try
            {
                var source = new CommandLog(null);

                var save = (SaveCommand)RuntimeHelpers.GetUninitializedObject(typeof(SaveCommand));
                save.Time = 12.5;
                typeof(SaveCommand).GetProperty(nameof(SaveCommand.FileStem),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .SetValue(save, "test-slot");
                source.CommandList.Add(save);

                var injector = (ContinuousInjectorCommand)RuntimeHelpers.GetUninitializedObject(typeof(ContinuousInjectorCommand));
                injector.Time = 27.25;
                typeof(BooleanCommand).GetField("targetState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(injector, true);
                typeof(ContinuousCommand).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(injector, (float?)0.75f);
                typeof(ContinuousInjectorCommand).GetField("injector", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(injector, 2);
                source.CommandList.Add(injector);

                source.SaveLog(replayPath);

                var loaded = new CommandLog(null);
                loaded.LoadLog(replayPath);

                Assert.AreEqual(2, loaded.CommandList.Count);

                Assert.IsInstanceOfType(loaded.CommandList[0], typeof(SaveCommand));
                var loadedSave = (SaveCommand)loaded.CommandList[0];
                Assert.AreEqual(12.5, loadedSave.Time);
                Assert.AreEqual("test-slot", loadedSave.FileStem);

                Assert.IsInstanceOfType(loaded.CommandList[1], typeof(ContinuousInjectorCommand));
                var loadedInjector = (ContinuousInjectorCommand)loaded.CommandList[1];
                Assert.AreEqual(27.25, loadedInjector.Time);
                Assert.AreEqual(true, typeof(BooleanCommand)
                    .GetField("targetState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedInjector));
                Assert.AreEqual((float?)0.75f, typeof(ContinuousCommand)
                    .GetField("target", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedInjector));
                Assert.AreEqual(2, typeof(ContinuousInjectorCommand)
                    .GetField("injector", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(loadedInjector));
            }
            finally
            {
                File.Delete(replayPath);
                File.Delete(replayPath + ".tmp");
            }
        }

        [TestMethod]
        public void LegacyReplayDoesNotCrashResumePath()
        {
            string replayPath = Path.Combine(Path.GetTempPath(), $"riel-legacy-replay-{Guid.NewGuid():N}.replay");
            try
            {
                File.WriteAllBytes(replayPath, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });

                var log = new CommandLog(null);
                log.LoadLog(replayPath);

                Assert.AreEqual(0, log.CommandList.Count);
            }
            finally
            {
                File.Delete(replayPath);
            }
        }
    }
}
