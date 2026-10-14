using System;
using System.IO;

namespace Xtension.Tests
{
    internal static class CoreTests
    {
        [Test]
        public static void ClockNeverHasElapsed()
        {
            Assert.IsTrue(Clock.HasElapsed(Clock.Never, 1000000), "the Never sentinel must read as elapsed");
        }

        [Test]
        public static void ClockDoesNotElapseImmediately()
        {
            long now = Clock.NowMs;
            Assert.IsFalse(Clock.HasElapsed(now, 60000), "a just-recorded event must not be elapsed");
        }

        [Test]
        public static void VersionIsConsistent()
        {
            Assert.IsTrue(AppInfo.DisplayVersion == "v" + AppInfo.SemanticVersion, "display version must be 'v' + semantic version");
            Assert.IsTrue(AppInfo.SemanticVersion.Length > 0, "semantic version must not be empty");
        }

        [Test]
        public static void SettingsRoundTripAndDefaults()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Xtension.Tests." + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                SettingsStore store = new SettingsStore(path);
                Assert.IsTrue(store.GetBool(SettingsStore.OverlayEnabledKey, true), "missing file must use the default (true)");
                store.SetBool(SettingsStore.OverlayEnabledKey, false);
                Assert.IsFalse(new SettingsStore(path).GetBool(SettingsStore.OverlayEnabledKey, true), "saved false must be read back");
                store.SetBool(SettingsStore.OverlayEnabledKey, true);
                Assert.IsTrue(new SettingsStore(path).GetBool(SettingsStore.OverlayEnabledKey, false), "saved true must be read back");
                Assert.IsFalse(File.Exists(path + ".tmp"), "no temporary file should remain after a save");
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }

        [Test]
        public static void SettingsIgnoreMalformedLines()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Xtension.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.ini");
            try
            {
                File.WriteAllText(path, "garbage line\r\n=novalue\r\noverlayEnabled=notabool\r\n");
                Assert.IsTrue(new SettingsStore(path).GetBool(SettingsStore.OverlayEnabledKey, true), "an unparsable value must fall back to the default");
            }
            finally
            {
                try { Directory.Delete(directory, true); } catch (IOException) { }
            }
        }
    }
}
