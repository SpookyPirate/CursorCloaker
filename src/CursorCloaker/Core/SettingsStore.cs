/*
 * SettingsStore - loads and saves CloakSettings as CursorCloaker.ini next to the exe.
 *
 * A missing or unreadable file yields defaults; the app must always start. Saving writes to a
 * temporary file first and then replaces, so a crash mid-write never leaves a truncated file.
 */
using System;
using System.IO;
using System.Text;

namespace CursorCloaker.Core
{
    internal sealed class SettingsStore
    {
        public const string FILE_NAME = "CursorCloaker.ini";

        private readonly string filePath;

        public SettingsStore(string directory)
        {
            filePath = Path.Combine(directory, FILE_NAME);
        }

        public string FilePath => filePath;

        public CloakSettings Load()
        {
            try
            {
                return File.Exists(filePath) ? CloakSettings.Parse(File.ReadAllText(filePath)) : new CloakSettings();
            }
            catch (IOException) { return new CloakSettings(); }
            catch (UnauthorizedAccessException) { return new CloakSettings(); }
        }

        public void Save(CloakSettings settings)
        {
            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, settings.Serialize(), new UTF8Encoding(false));
            if (File.Exists(filePath)) File.Replace(temporaryPath, filePath, null);
            else File.Move(temporaryPath, filePath);
        }
    }
}
