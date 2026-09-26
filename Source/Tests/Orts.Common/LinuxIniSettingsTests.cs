#if RIEL_UNIX
using System;
using System.IO;
using ORTS.Common;
using Xunit;

namespace Tests.OrtsCommon
{
    public class LinuxIniSettingsTests
    {
        [Fact]
        public void IniSettingsRoundTripWithoutWindowsProfileApi()
        {
            string path = Path.GetTempFileName();
            try
            {
                var main = SettingsStore.GetSettingStore(path, null, "ORTS");
                main.SetUserValue("Logging", true);
                main.SetUserValue("LoggingPath", "/home/test/Riel Logs");
                var folders = SettingsStore.GetSettingStore(path, null, "Folders");
                folders.SetUserValue("Home", "/home/test/Train Simulator");

                Assert.Equal(true, main.GetUserValue("Logging", typeof(bool)));
                Assert.Equal("/home/test/Riel Logs", main.GetUserValue("LoggingPath", typeof(string)));
                Assert.Equal("/home/test/Train Simulator", folders.GetUserValue("Home", typeof(string)));
                Assert.Contains("Logging", main.GetUserNames());
                Assert.Contains("Folders", ((SettingsStoreLocalIni)main).GetSectionNames());

                main.DeleteUserValue("Logging");
                Assert.Null(main.GetUserValue("Logging", typeof(bool)));
                Assert.Equal("/home/test/Train Simulator", folders.GetUserValue("Home", typeof(string)));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
#endif
