using System;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class LanguageChange
    {
        static bool? pendingGerman;
        internal static bool MatchesPending(bool targetGerman, bool currentGerman)
        {
            return targetGerman == (pendingGerman ?? currentGerman);
        }

        internal static bool Apply(bool unchanged, Func<bool> confirm, Action save)
        {
            if (unchanged || !confirm())
                return false;
            save();
            return true;
        }

        internal static void Request(IWin32Window owner, bool unchanged, bool german, Action save)
        {
            try
            {
                bool targetGerman = unchanged ? german : !german;
                bool saved = Apply(MatchesPending(targetGerman, german), delegate
                {
                    return StudioMessageBox.Show(owner, german ? "Die neue Sprache wird erst beim nächsten manuellen Start aktiv. Die App wird jetzt nicht neu gestartet; offene Fenster und ungespeicherte Änderungen bleiben erhalten.\n\nSprache für den nächsten Start speichern?" : "The new language takes effect the next time you start the app manually. The app will not restart now; open windows and unsaved changes stay intact.\n\nSave the language for the next start?", german ? "Sprache wechseln" : "Change language", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.OK;
                }, delegate
                {
                    save();
                    pendingGerman = targetGerman;
                });
                if (saved)
                    StudioMessageBox.Show(owner, german ? "Sprache für den nächsten Start gespeichert. Du kannst jetzt weiterarbeiten. Speichere oder exportiere deine Änderungen, bevor du die App später selbst schließt." : "Language saved for the next start. You can keep working. Save or export your changes before closing the app yourself later.", german ? "Sprache gespeichert" : "Language saved");
            }
            catch (Exception ex)
            {
                StudioMessageBox.Show(owner, ex.Message, german ? "Sprache konnte nicht gespeichert werden" : "Could not save language", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
