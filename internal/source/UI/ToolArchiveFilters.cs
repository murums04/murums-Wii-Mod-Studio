namespace murumsWiiModStudio
{
    internal static class ToolArchiveFilters
    {
        internal const string MenuPatterns = "Title.szs;Title_*.szs;MenuSingle.szs;MenuSingle_*.szs;MenuMulti.szs;MenuMulti_*.szs;Globe.szs;Globe_*.szs;Channel.szs;Channel_*.szs;Award.szs;Award_*.szs;Common.szs;Common_*.szs;UIAssets.szs;ReplacedAssets.szs";
        internal const string RacePatterns = "Race.szs;Race_*.szs;RaceAssets.szs;ReplacedAssets.szs";
        internal const string Fonts = "Font archives / BRFNT|Font.szs;Font_*.szs;homeBtn*.szs;*.brfnt";
        internal const string FontExtras = "Menu / HUD font archives|Title.szs;Title_*.szs;MenuSingle.szs;MenuSingle_*.szs;MenuMulti.szs;MenuMulti_*.szs;Globe.szs;Globe_*.szs;Channel.szs;Channel_*.szs;Award.szs;Award_*.szs;" + RacePatterns;
        internal const string Menus = "Menu archives|" + MenuPatterns;
        internal const string Race = "Race HUD archives|" + RacePatterns;
        internal const string Layouts = "Menu / HUD layout archives|" + MenuPatterns + ";" + RacePatterns;
        internal const string Messages = "Message sources (RR assets / language archives / BMG)|UIAssets.szs;RaceAssets.szs;Common_*.szs;MenuSingle_*.szs;MenuMulti_*.szs;Title_*.szs;Race_*.szs;Globe_*.szs;Channel_*.szs;Award_*.szs;*.bmg";
        internal const string Backgrounds = "Background / model archives|" + MenuPatterns + ";Earth.szs;BackModel.szs;globe.arc";

        internal static System.Windows.Forms.DialogResult Show(System.Windows.Forms.OpenFileDialog picker, System.Windows.Forms.IWin32Window owner)
        {
            string[] parts = picker.Filter.Split('|');
            string patterns = parts.Length > 1 ? parts[1] : "*.*";
            System.ComponentModel.CancelEventHandler validate = delegate(object sender, System.ComponentModel.CancelEventArgs args)
            {
                foreach (string file in picker.FileNames)
                {
                    bool allowed = false;
                    foreach (string pattern in patterns.Split(';'))
                    {
                        string expression = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                            .Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
                        if (System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(file), expression,
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { allowed = true; break; }
                    }
                    if (!allowed)
                    {
                        args.Cancel = true;
                        StudioMessageBox.Show(owner, L.T("Diese Datei passt nicht zu diesem Tool. Erlaubt: ", "This file is not supported by this tool. Allowed: ") + patterns,
                            L.T("Passende Datei auswählen", "Choose a supported file"), System.Windows.Forms.MessageBoxButtons.OK);
                        return;
                    }
                }
            };
            picker.FileOk += validate;
            try { return picker.ShowDialog(owner); }
            finally { picker.FileOk -= validate; }
        }
        internal static string Background(string name, bool language)
        {
            return language ? name + " language archive|" + name + "_*.szs"
                : name + " base archive|" + name + ".szs";
        }
    }
}