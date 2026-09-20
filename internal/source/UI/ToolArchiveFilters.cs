namespace murumsWiiModStudio
{
    internal static class ToolArchiveFilters
    {
        internal const string MenuPatterns = "Title.szs;Title_*.szs;MenuSingle.szs;MenuSingle_*.szs;MenuMulti.szs;MenuMulti_*.szs;MenuOther.szs;MenuOther_*.szs;Globe.szs;Globe_*.szs;Channel.szs;Channel_*.szs;Award.szs;Award_*.szs;Common.szs;Common_*.szs;UIAssets.szs;ReplacedAssets.szs";
        internal const string LayoutMenuPatterns = "Title.szs;Title_*.szs;MenuSingle.szs;MenuSingle_*.szs;MenuMulti.szs;MenuMulti_*.szs;MenuOther.szs;MenuOther_*.szs;Globe.szs;Globe_*.szs;Channel.szs;Channel_*.szs;Award.szs;Award_*.szs;UIAssets.szs;ReplacedAssets.szs";
        internal const string RacePatterns = "Race.szs;Race_*.szs;RaceAssets.szs;ReplacedAssets.szs";
        internal const string Fonts = "Font archives / BRFNT|Font.szs;Font_*.szs;homeBtn*.szs;*.brfnt";
        internal const string FontExtras = "Menu / HUD font archives|Title.szs;Title_*.szs;MenuSingle.szs;MenuSingle_*.szs;MenuMulti.szs;MenuMulti_*.szs;MenuOther.szs;MenuOther_*.szs;Globe.szs;Globe_*.szs;Channel.szs;Channel_*.szs;Award.szs;Award_*.szs;" + RacePatterns;
        internal const string Menus = "Menu archives|" + MenuPatterns;
        internal const string MenuTextures = "Menu texture archives|" + LayoutMenuPatterns;
        internal const string Race = "Race HUD archives|" + RacePatterns;
        internal const string Layouts = "Menu / HUD layout archives|" + LayoutMenuPatterns + ";" + RacePatterns;
        internal const string Messages = "Message sources (RR assets / language archives / BMG)|UIAssets.szs;RaceAssets.szs;Common_*.szs;MenuSingle_*.szs;MenuMulti_*.szs;MenuOther.szs;MenuOther_*.szs;Title_*.szs;Race_*.szs;Globe_*.szs;Channel_*.szs;Award_*.szs;*.bmg";
        internal const string Backgrounds = "Background / model archives|" + MenuPatterns + ";Race.szs;Earth.szs;BackModel.szs;globe.arc";

        sealed class RecoveredSelection { internal string[] Files; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.OpenFileDialog, RecoveredSelection> recovered =
            new System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Forms.OpenFileDialog, RecoveredSelection>();
        internal static string[] SelectedFiles(System.Windows.Forms.OpenFileDialog picker)
        {
            RecoveredSelection selection;
            return recovered.TryGetValue(picker, out selection) ? selection.Files : picker.FileNames;
        }
        internal static string SelectedFile(System.Windows.Forms.OpenFileDialog picker)
        {
            string[] files = SelectedFiles(picker);
            return files.Length == 0 ? "" : files[0];
        }
        internal static System.Windows.Forms.DialogResult Show(System.Windows.Forms.OpenFileDialog picker, System.Windows.Forms.IWin32Window owner)
        {
            string[] parts = picker.Filter.Split('|');
            string patterns = parts.Length > 1 ? parts[1] : "*.*";
            recovered.Remove(picker);
            var form = owner as System.Windows.Forms.Form;
            while (form != null && System.String.IsNullOrEmpty(PackSelection.Folder(form)) && form.Owner != null) form = form.Owner;
            bool isEdited = picker.Title.StartsWith("Open edited", System.StringComparison.OrdinalIgnoreCase)
                || picker.Title.StartsWith("Bearbeit", System.StringComparison.OrdinalIgnoreCase);
            if (form != null && !isEdited && form.GetType().Name != "ArchiveMergeForm"
                && RrSourceRecovery.Patterns(form) != null && patterns.IndexOf(".szs", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string folder = PackSelection.Folder(form);
                if (!System.String.IsNullOrEmpty(folder))
                {
                    string[] roots = new string[0];
                    bool missing = false;
                    try
                    {
                        missing = !System.Array.Exists(System.IO.Directory.GetFiles(folder), file => RrMissingFiles.Matches(System.IO.Path.GetFileName(file), patterns));
                        roots = RetroRewindSource.Discover();
                        if (roots.Length == 1)
                            missing = RrMissingFiles.Plan(roots[0], folder, patterns, RrMissingFiles.InferRegion(folder)).Exists(file => !file.Exists);
                    }
                    catch (System.IO.IOException) { }
                    catch (System.UnauthorizedAccessException) { }
                    catch (System.ArgumentException) { }
                    if (missing)
                    {
                        string[] paths = RrSourceRecovery.Choose(form, patterns, null, null, 0, picker.Multiselect);
                        if (paths != null)
                        {
                            if (!picker.Multiselect && paths.Length > 1) throw new System.IO.InvalidDataException("Choose one source file.");
                            recovered.Add(picker, new RecoveredSelection { Files = paths });
                            return paths.Length == 0 ? System.Windows.Forms.DialogResult.Cancel : System.Windows.Forms.DialogResult.OK;
                        }
                    }
                }
            }
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