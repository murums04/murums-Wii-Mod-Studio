using System.IO;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class ExportHelp
    {
        internal static string Message(string folder)
        {
            return L.T("Ausgabeordner erstellt oder aktualisiert:\n", "Output folder created or updated:\n")
                + Path.GetFullPath(folder) + "\n\n"
                + L.T(
                    "Nächster Schritt:\n1. Sichere die bisherigen Dateien deines Packs.\n2. Kopiere die erstellten Spieldateien aus diesem Ausgabeordner in die entsprechenden Ordner deines Packs.\n3. Wähle bei gleichnamigen Dateien „Ersetzen/Überschreiben“.\n4. Starte das Spiel neu und prüfe die Änderungen.\n\nKopiere die Dateien, nicht den gesamten Ausgabeordner. Berichte gehören nicht ins Pack.",
                    "Next steps:\n1. Back up your pack's existing files.\n2. Copy the generated game files from this output folder into the corresponding folders of your pack.\n3. Choose Replace/Overwrite for files with the same name.\n4. Restart the game and check the changes.\n\nCopy the files, not the entire output folder. Do not copy reports into the pack.");
        }

        internal static void Show(IWin32Window owner, string folder)
        {
            ToolStatus.Set(owner as Form, true);
            string message = Message(folder);
            StudioMessageBox.ShowPath(owner, Path.GetFullPath(folder), message.Substring(message.IndexOf("\n\n") + 2),
                L.T("Gespeichert – nächste Schritte", "Saved — next steps"));
        }
    }
}
