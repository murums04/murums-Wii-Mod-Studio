using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio.Brlan
{
    internal enum UiLanguage
    {
        German,
        English
    }

    internal static class L
    {
        public static UiLanguage Current = UiLanguage.English;
        public static bool IsGerman
        {
            get
            {
                return Current == UiLanguage.German;
            }
        }

        public static string T(string german, string english)
        {
            return IsGerman ? german : english;
        }

        public static string Format(string german, string english, params object[] args)
        {
            return String.Format(T(german, english), args);
        }

        // Alias used by the unified Wii Mod Studio UI layer. Keeping both names
        // avoids namespace-specific localization API mismatches after modules are merged.
        public static string F(string german, string english, params object[] args)
        {
            return Format(german, english, args);
        }
    }

    internal static class AppSettings
    {
        private static string SettingsDirectory
        {
            get
            {
                string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(basePath, "murums Wii Mod Studio");
            }
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(SettingsDirectory, "settings.ini");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;
                string[] lines = File.ReadAllLines(SettingsPath);
                int i;
                for (i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.StartsWith("language=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = line.Substring("language=".Length).Trim();
                        L.Current = value.Equals("en", StringComparison.OrdinalIgnoreCase) ? UiLanguage.English : UiLanguage.German;
                    }
                }
            }
            catch
            {
                L.Current = UiLanguage.English;
            }
        }

        public static void SaveLanguage(UiLanguage language)
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, "language=" + (language == UiLanguage.English ? "en" : "de") + Environment.NewLine);
        }

        public static void RequestLanguageChange(IWin32Window owner, UiLanguage language)
        {
            murumsWiiModStudio.LanguageChange.Request(owner, L.Current == language, L.IsGerman, delegate
            {
                SaveLanguage(language);
            });
        }
    }
}
