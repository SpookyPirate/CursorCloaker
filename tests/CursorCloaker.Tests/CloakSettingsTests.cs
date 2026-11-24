/*
 * CloakSettingsTests - .ini parsing, fallbacks and round-tripping for CloakSettings.
 */
using System.IO;

using CursorCloaker.Core;
using Xunit;

namespace CursorCloaker.Tests
{
    public class CloakSettingsTests
    {
        [Fact]
        public void EmptyTextGivesDefaults()
        {
            var settings = CloakSettings.Parse("");

            Assert.True(settings.Enabled);
            Assert.Equal(CloakMode.HideAndPark, settings.Mode);
            Assert.Equal(CloakScope.FullscreenOnly, settings.Scope);
            Assert.Equal(0, settings.HideDelayMilliseconds);
            Assert.True(settings.StartWithWindows);
            Assert.Empty(settings.ExcludedProcesses);
        }

        [Fact]
        public void ParsesEveryKeyCaseInsensitively()
        {
            var settings = CloakSettings.Parse(
                "[CursorCloaker]\r\nENABLED=off\r\nmode=park\r\nScope=allprograms\r\nHideDelayMs=250\r\nStartWithWindows=no\r\nExclude=obs64\r\n");

            Assert.False(settings.Enabled);
            Assert.Equal(CloakMode.Park, settings.Mode);
            Assert.Equal(CloakScope.AllPrograms, settings.Scope);
            Assert.Equal(250, settings.HideDelayMilliseconds);
            Assert.False(settings.StartWithWindows);
            Assert.Equal(new[] { "obs64" }, settings.ExcludedProcesses);
        }

        [Theory]
        [InlineData("Mode=Sideways")]
        [InlineData("Mode=7")]
        [InlineData("Enabled=maybe")]
        [InlineData("HideDelayMs=soon")]
        [InlineData("garbage line without equals")]
        [InlineData("=valueWithoutKey")]
        public void BadValuesFallBackToDefaults(string line)
        {
            var settings = CloakSettings.Parse(line);

            Assert.True(settings.Enabled);
            Assert.Equal(CloakMode.HideAndPark, settings.Mode);
            Assert.Equal(0, settings.HideDelayMilliseconds);
        }

        [Theory]
        [InlineData("-5", 0)]
        [InlineData("999999", CloakSettings.MAX_HIDE_DELAY_MILLISECONDS)]
        public void HideDelayIsClamped(string value, int expected)
        {
            Assert.Equal(expected, CloakSettings.Parse("HideDelayMs=" + value).HideDelayMilliseconds);
        }

        [Fact]
        public void ExcludeListIsNormalised()
        {
            var settings = CloakSettings.Parse("Exclude= Steam.exe ; notepad;;STEAM ");

            Assert.Equal(new[] { "steam", "notepad" }, settings.ExcludedProcesses);
        }

        [Fact]
        public void CommentsAreIgnored()
        {
            var settings = CloakSettings.Parse("; Enabled=false\n# Mode=Park\n");

            Assert.True(settings.Enabled);
            Assert.Equal(CloakMode.HideAndPark, settings.Mode);
        }

        [Fact]
        public void SerializeRoundTrips()
        {
            var original = new CloakSettings
            {
                Enabled = false,
                Mode = CloakMode.Hide,
                Scope = CloakScope.AllPrograms,
                HideDelayMilliseconds = 1000,
                StartWithWindows = false,
            };
            original.ExcludedProcesses.Add("obs64");
            original.ExcludedProcesses.Add("notepad");

            var copy = CloakSettings.Parse(original.Serialize());

            Assert.Equal(original.Enabled, copy.Enabled);
            Assert.Equal(original.Mode, copy.Mode);
            Assert.Equal(original.Scope, copy.Scope);
            Assert.Equal(original.HideDelayMilliseconds, copy.HideDelayMilliseconds);
            Assert.Equal(original.StartWithWindows, copy.StartWithWindows);
            Assert.Equal(original.ExcludedProcesses, copy.ExcludedProcesses);
        }

        [Fact]
        public void StoreSavesAndLoadsAndToleratesMissingFile()
        {
            var directory = Path.Combine(Path.GetTempPath(), "CursorCloakerTests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new SettingsStore(directory);
                Assert.True(store.Load().Enabled);   // missing file -> defaults

                store.Save(new CloakSettings { Mode = CloakMode.Park });
                store.Save(new CloakSettings { Mode = CloakMode.Hide });   // replaces existing file

                Assert.Equal(CloakMode.Hide, store.Load().Mode);
                Assert.False(File.Exists(store.FilePath + ".tmp"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
