using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    // Shared assistance for all WinForms windows, including dynamically created editors.
    internal static class StudioUx
    {
        sealed class Marker
        {
        }

        static readonly ConditionalWeakTable<Control, Marker> attached = new ConditionalWeakTable<Control, Marker>();
        static readonly ConditionalWeakTable<Form, Marker> forms = new ConditionalWeakTable<Form, Marker>();
        static Timer timer;
        static HoverHintWindow hover;
        static HoverInputFilter inputFilter;
        static Control last;
        static Point lastPoint;
        static DateTime since;
        static bool shown;
        public static void Install()
        {
            if (timer != null)
                return;
            hover = new HoverHintWindow();
            inputFilter = new HoverInputFilter(Hide);
            Application.AddMessageFilter(inputFilter);
            timer = new Timer
            {
                Interval = 150
            };
            timer.Tick += delegate
            {
                foreach (Form form in Application.OpenForms)
                {
                    if (form is HoverHintWindow)
                        continue;
                    Marker m;
                    if (!forms.TryGetValue(form, out m))
                    {
                        forms.Add(form, new Marker());
                        Attach(form);
                    }
                }

                ShowHover();
            };
            timer.Start();
            Application.ApplicationExit += delegate
            {
                Application.RemoveMessageFilter(inputFilter);
                timer.Dispose();
                hover.Dispose();
            };
        }

        public static void SetHelp(Control c, string text)
        {
            c.AccessibleDescription = text;
        }

        public static void Attach(Control c)
        {
            Marker marker;
            if (attached.TryGetValue(c, out marker))
                return;
            attached.Add(c, new Marker());
            var b = c as ButtonBase;
            if (b != null && !(b is SelectionClearButton))
            {
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.ForeColor = DarkTheme.Fore;
                b.Paint += PaintDisabled;
                b.EnabledChanged += delegate
                {
                    b.Invalidate();
                };
                if (b is CheckBox || b is RadioButton)
                    b.MinimumSize = new Size(TextRenderer.MeasureText(b.Text, b.Font).Width + 30, b.MinimumSize.Height);
                if (b is Button)
                {
                    b.FlatAppearance.BorderColor = DarkTheme.Border;
                    b.FlatAppearance.MouseOverBackColor = DarkTheme.Panel3;
                    b.FlatAppearance.MouseDownBackColor = DarkTheme.AccentSoft;
                }
            }

            var combo = c as ComboBox;
            if (combo != null)
            {
                combo.DropDown += delegate
                {
                    Hide();
                };
                combo.DropDownClosed += delegate
                {
                    Hide();
                };
            }

            var tabControl = c as TabControl;
            if (tabControl != null)
            {
                tabControl.ShowToolTips = false;
                tabControl.HotTrack = false;
            }

            var strip = c as ToolStrip;
            if (strip != null)
            {
                strip.ShowItemToolTips = true;
                DescribeItems(strip.Items);
                strip.ItemAdded += delegate
                {
                    DescribeItems(strip.Items);
                };
            }

            c.ControlAdded += delegate (object sender, ControlEventArgs e)
            {
                Attach(e.Control);
            };
            foreach (Control child in c.Controls)
                Attach(child);
        }

        static void DescribeItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                if (string.IsNullOrEmpty(item.ToolTipText) || item.ToolTipText == item.Text)
                    item.ToolTipText = ActionHelp(item.Text);
                var drop = item as ToolStripDropDownItem;
                if (drop != null)
                    DescribeItems(drop.DropDownItems);
            }
        }

        static void PaintDisabled(object sender, PaintEventArgs e)
        {
            var b = (ButtonBase)sender;
            if (b.Enabled)
                return;
            Color bg = (b is CheckBox || b is RadioButton) ? b.BackColor : DarkTheme.Panel;
            using (var brush = new SolidBrush(bg))
                e.Graphics.FillRectangle(brush, b.ClientRectangle);
            Rectangle text = new Rectangle(7, 2, Math.Max(0, b.Width - 14), Math.Max(0, b.Height - 4));
            var check = b as CheckBox;
            var radio = b as RadioButton;
            if (check != null || radio != null)
            {
                int size = Math.Min(14, b.Height - 4);
                var box = new Rectangle(2, (b.Height - size) / 2, size, size);
                using (var p = new Pen(DarkTheme.Disabled))
                {
                    if (radio != null)
                        e.Graphics.DrawEllipse(p, box);
                    else
                        e.Graphics.DrawRectangle(p, box);
                }

                if ((check != null && check.Checked) || (radio != null && radio.Checked))
                    using (var brush = new SolidBrush(DarkTheme.Disabled))
                        e.Graphics.FillRectangle(brush, box.X + 4, box.Y + 4, Math.Max(1, size - 7), Math.Max(1, size - 7));
                text.X = size + 8;
                text.Width = Math.Max(0, b.Width - text.X - 2);
            }
            else
            {
                using (var p = new Pen(DarkTheme.Border))
                    e.Graphics.DrawRectangle(p, 0, 0, Math.Max(0, b.Width - 1), Math.Max(0, b.Height - 1));
            }

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (check == null && radio == null)
                flags |= TextFormatFlags.HorizontalCenter;
            else
                flags |= TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, text, DarkTheme.Disabled, flags);
        }

        internal static Form HoverHost(Form active)
        {
            var hint = active as HoverHintWindow;
            return hint == null ? active : hint.HostForm;
        }

        internal static bool HasOpenDropDown(Control control)
        {
            var combo = control as ComboBox;
            if (combo != null && combo.DroppedDown)
                return true;
            var strip = control as ToolStrip;
            if (strip != null)
            {
                foreach (ToolStripItem item in strip.Items)
                {
                    var drop = item as ToolStripDropDownItem;
                    if (drop != null && drop.HasDropDownItems && drop.DropDown.Visible)
                        return true;
                    var list = item as ToolStripComboBox;
                    if (list != null && list.ComboBox.DroppedDown)
                        return true;
                }
            }

            foreach (Control child in control.Controls)
                if (HasOpenDropDown(child))
                    return true;
            return false;
        }

        static void ShowHover()
        {
            Form form = HoverHost(Form.ActiveForm);
            if (Control.MouseButtons != MouseButtons.None || form == null || form.IsDisposed || !form.Visible || HasOpenDropDown(form))
            {
                Hide();
                return;
            }

            Point screen = Cursor.Position;
            Control c = form;
            while (true)
            {
                var next = c.GetChildAtPoint(c.PointToClient(screen), GetChildAtPointSkip.Invisible);
                if (next == null)
                    break;
                c = next;
            }

            if (!form.ClientRectangle.Contains(form.PointToClient(screen)))
            {
                Hide();
                return;
            }

            while (c.Parent is NumericUpDown || c.Parent is ComboBox)
                c = c.Parent;
            string help = HoverHelpAt(c, c.PointToClient(screen));
            if (string.IsNullOrEmpty(help))
            {
                Hide();
                return;
            }

            if (c != last || screen != lastPoint)
            {
                Hide();
                last = c;
                lastPoint = screen;
                since = DateTime.UtcNow;
                return;
            }

            if (shown || (DateTime.UtcNow - since).TotalMilliseconds < 650)
                return;
            if (!c.Enabled)
                help += "\n" + L.T("Derzeit nicht verfügbar. Prüfe die geladene Datei und Auswahl.", "Currently unavailable. Check the loaded file and current selection.");
            hover.ShowHint(help, form, new Point(screen.X + 12, screen.Y + 24));
            shown = true;
        }

        static void Hide()
        {
            if (hover != null)
                hover.Hide();
            last = null;
            shown = false;
        }

        internal static string HoverHelpAt(Control c, Point point)
        {
            // Help text may also be used for accessibility. Only concrete interactive
            // targets should create floating hints, never the empty area of a container.
            if (c == null || c.IsDisposed || !c.ClientRectangle.Contains(point))
                return null;
            if (c is TabControl)
                return null; // Labels already identify the views; no floating tab popup.
            var list = c as ListBox;
            if (list != null)
            {
                int i = list.IndexFromPoint(point);
                return i >= 0 && list.GetItemRectangle(i).Contains(point) ? Help(c) : null;
            }

            var view = c as ListView;
            if (view != null)
                return view.GetItemAt(point.X, point.Y) != null ? Help(c) : null;
            var tree = c as TreeView;
            if (tree != null)
            {
                var hit = tree.HitTest(point);
                return hit.Node != null && (hit.Location & (TreeViewHitTestLocations.Label | TreeViewHitTestLocations.Image | TreeViewHitTestLocations.PlusMinus | TreeViewHitTestLocations.StateImage)) != 0 ? Help(c) : null;
            }

            var grid = c as DataGridView;
            if (grid != null)
            {
                var hit = grid.HitTest(point.X, point.Y);
                return hit.Type == DataGridViewHitTestType.Cell || hit.Type == DataGridViewHitTestType.ColumnHeader || hit.Type == DataGridViewHitTestType.RowHeader ? Help(c) : null;
            }

            if (c is ButtonBase || c is TextBoxBase || c is ComboBox || c is NumericUpDown || c is LinkLabel)
                return Help(c);
            return null;
        }

        public static string Help(Control c)
        {
            if (!string.IsNullOrEmpty(c.AccessibleDescription))
                return c.AccessibleDescription;
            if (c is ButtonBase)
                return ActionHelp(c.Text);
            if (c is ComboBox)
                return L.T("Wähle eine der verfügbaren Optionen. Mit den Pfeiltasten kannst du die Auswahl wechseln.", "Choose an available option. Use the arrow keys to change the selection.");
            if (c is NumericUpDown)
            {
                var n = (NumericUpDown)c;
                return L.T("Wert eingeben oder mit den Pfeilen ändern. Bereich: ", "Enter a value or use the arrows. Range: ") + n.Minimum + " – " + n.Maximum + ".";
            }

            if (c is TextBox)
            {
                var t = (TextBox)c;
                return t.ReadOnly ? L.T("Nur Anzeige. Text kann markiert und mit Strg+C kopiert werden.", "Read-only. Select text and press Ctrl+C to copy it.") : L.T("Wert oder Pfad direkt eingeben. Strg+A markiert den gesamten Inhalt.", "Enter a value or path directly. Ctrl+A selects the entire field.");
            }

            if (c is TreeView)
                return L.T("Wähle einen Eintrag für Vorschau und Eigenschaften. Pfeile klappen Ordner auf und zu.", "Select an entry to inspect its preview and properties. Arrow keys expand and collapse folders.");
            if (c is ListBox || c is ListView)
                return L.T("Wähle einen Eintrag, um seine Vorschau oder Details anzuzeigen.", "Select an entry to display its preview or details.");
            if (c is PropertyGrid)
            {
                var p = (PropertyGrid)c;
                var item = p.SelectedGridItem;
                return item != null && item.PropertyDescriptor != null ? item.PropertyDescriptor.Description : "Select a property to read its description below the grid.";
            }

            if (c is TabControl)
                return "Switch between the views in this window.";
            if (c is DataGridView && ((DataGridView)c).ReadOnly)
                return "Select a row for the available actions. This table is read-only.";
            if (c is DataGridView)
                return L.T("Wähle eine Zeile. Bearbeitbare Werte per Doppelklick ändern; Enter bestätigt die Zelle.", "Select a row. Double-click editable values to change them; Enter commits the cell.");
            if (c is PictureBox)
                return L.T("Bildvorschau. Darstellung im Spiel kann durch Layouts, Animationen und Farben abweichen.", "Picture preview. Layouts, animations and colours can change its in-game appearance.");
            return null;
        }

        public static string FieldHelp(string label)
        {
            string n = label.ToLowerInvariant();
            if (n.Contains("alpha"))
                return "Opacity: 0 = invisible, 255 = fully visible. Animations may override this value.";
            if (n.Contains("scale") || n.Contains("skal"))
                return "Pane size multiplier: 1 is the original size. X and Y act independently.";
            if (n.Contains("rotat") || n.Contains("dreh"))
                return "Pane rotation around the specified axis, in degrees.";
            if (n.Contains("origin") || n.Contains("ursprung"))
                return "Reference point for the pane position and alignment.";
            if (n.Contains("material"))
                return "Link the pane to a material. Materials define textures, colours and transparency.";
            return label + ": property of the selected pane. Changes appear in the layout preview; animations may override them in game.";
        }

        public static string ActionHelp(string caption)
        {
            string t = (caption ?? "").Replace("&", "").Replace("…", "").Replace("...", "").Trim().ToLowerInvariant();
            if (t.Contains("undo this") || t.Contains("clear picture") || t.Contains("bildauswahl"))
                return L.T("Verwirft diese Bildauswahl für den nächsten Export. Bereits gespeicherte Dateien werden nicht zurückgesetzt.", "Discard this picture selection for the next export. Files already saved are not restored.");
            if (t.Contains("clear custom") || t.Contains("benutzerpfad"))
                return "Clear the manually assigned tool path. The external program is not uninstalled.";
            if (t.Contains("refresh") || t.Contains("neu erkennen"))
                return "Read the current state again and refresh the list.";
            if (t.Contains("paste") || t.Contains("einfügen"))
                return "Insert clipboard entries into the current list. Check names and ordering before saving.";
            if (t.Contains("rltp"))
                return "Create a texture-switch animation from the TPL order. Image indices must match the archive.";
            if (t.Contains("original"))
                return L.T("Setzt diese Option auf den geöffneten Quellstand zurück. Erstelle anschließend eine neue Kopie, um das Ergebnis zu übernehmen.", "Reset this option to the opened source. Create a new copy afterwards to apply the result.");
            if (t.Contains("save as") || t.Contains("speichern unter"))
                return L.T("Speichert den aktuellen Stand unter einem von dir gewählten Dateinamen.", "Save the current document under a filename you choose.");
            if (t.Contains("cop") || t.Contains("kopi") || t.Contains("edited archive"))
                return L.T("Erstellt Ausgabedateien aus deinen Einstellungen. Prüfe den Ausgabeordner und teste die Kopien vor dem Übernehmen.", "Create output files from your settings. Check the output folder and test the copies before installing them.");
            if (t == "save" || t == "speichern")
                return L.T("Speichert die Änderungen im aktuellen Dokument. Nutze Speichern unter für eine separate Kopie.", "Save changes to the current document. Use Save as for a separate copy.");
            if (t.Contains("browse") || t.Contains("suchen") || t.Contains("choose source"))
                return L.T("Öffnet die Dateiauswahl oder Ordnersuche für dieses Feld.", "Open the file or folder picker for this field.");
            if (t.Contains("import") || t.Contains("replace") || t.Contains("ersetzen"))
                return L.T("Wählt Ersatzdaten für die aktuelle Auswahl. Prüfe die Vorschau vor dem Speichern.", "Choose replacement data for the current selection. Review the preview before saving.");
            if (t.Contains("export"))
                return L.T("Exportiert die ausgewählten Daten in eine separate Datei.", "Export the selected data to a separate file.");
            if (t.Contains("delete") || t.Contains("löschen"))
                return L.T("Entfernt den ausgewählten Eintrag aus dem geöffneten Dokument. Prüfe die Auswahl vor dem Speichern.", "Remove the selected entry from the open document. Check the selection before saving.");
            if (t.Contains("keyframe") || t.Contains("schlüsselbild"))
                return L.T("Bearbeitet einen Animations-Schlüsselwert. Frame legt den Zeitpunkt fest; Wert und Interpolation bestimmen den Verlauf.", "Edit an animation key. Frame sets its time; value and interpolation determine the transition.");
            if (t.Contains("sort") || t.Contains("sortieren"))
                return L.T("Ordnet die Schlüsselwerte nach ihrem Frame-Zeitpunkt.", "Order animation keys by their frame time.");
            if (t.Contains("validate") || t.Contains("validier") || t.Contains("check") || t.Contains("prüf"))
                return L.T("Prüft Daten auf strukturelle Probleme. Eine erfolgreiche Prüfung ersetzt keinen Test im Spiel.", "Check data for structural problems. A successful check does not replace testing in game.");
            if (t.Contains("undo") || t.Contains("rückgängig"))
                return L.T("Macht den letzten bearbeitbaren Schritt rückgängig.", "Undo the last editable operation.");
            if (t.Contains("redo") || t.Contains("wiederholen"))
                return L.T("Stellt den zuletzt rückgängig gemachten Schritt wieder her.", "Restore the last undone operation.");
            if (t.Contains("install"))
                return L.T("Installiert das gewählte externe Werkzeug über die Toolchain. Dafür kann ein Download nötig sein.", "Install the selected external tool through the toolchain. A download may be required.");
            if (t.Contains("colour") || t.Contains("color") || t.Contains("farbe"))
                return L.T("Wählt die Farbe für diesen Bereich. Vorschau und Export berücksichtigen die jeweilige Quelltextur.", "Choose the colour for this area. The source texture also affects the result.");
            if (t.Contains("shadow") || t.Contains("schatten"))
                return L.T("Blendet die separate Zahlenschatten-Ebene aus. Schatten im Bild selbst bleiben erhalten.", "Hide the separate placement-shadow layer. Shadows painted into the image remain.");
            if (t.Contains("visible") || t.Contains("sichtbar"))
                return L.T("Schaltet dieses Modell für den nächsten Export ein oder aus. Gemeinsam verwendete Modelle betreffen mehrere Menüs.", "Show or hide this model in the next export. Shared models can affect several menus.");
            if (t.Contains("rename") || t.Contains("umbenenn"))
                return L.T("Ändert den Namen des ausgewählten Eintrags. Referenzen müssen zum neuen Namen passen.", "Rename the selected entry. References must match the new name.");
            if (t.Contains("move up") || t.Contains("nach oben") || t.Contains("move down") || t.Contains("nach unten"))
                return L.T("Verschiebt den ausgewählten Eintrag in der Reihenfolge. Bei Texturen können sich dadurch Indizes ändern.", "Move the selected entry in the ordered list. Texture indices may change.");
            if (t.Contains("add") || t.Contains("hinzufügen"))
                return L.T("Fügt einen Eintrag zur aktuellen Auswahl oder Liste hinzu.", "Add an entry to the current selection or list.");
            if (t.Contains("open") || t.Contains("öffnen"))
                return L.T("Öffnet die gewählte Datei, Ansicht oder verknüpfte Seite.", "Open the selected file, view or linked page.");
            if (t.Contains("cancel") || t.Contains("abbrechen"))
                return L.T("Schließt diesen Dialog ohne Übernahme der aktuellen Eingabe.", "Close this dialog without accepting the current input.");
            if (t.Contains("close") || t.Contains("schließen"))
                return L.T("Schließt dieses Fenster. Speichere gewünschte Änderungen vorher.", "Close this window. Save any changes you want to keep first.");
            if (t == "ok" || t.Contains("apply") || t.Contains("übernehmen"))
                return L.T("Bestätigt die aktuellen Angaben für diesen Dialog.", "Accept the current values in this dialog.");
            if (t == "◀" || t == "▶")
                return L.T("Wechselt das angezeigte Bild innerhalb der geöffneten Textur.", "Change the displayed image within the opened texture.");
            return string.IsNullOrEmpty(t) ? null : caption + L.T(". Bezieht sich auf die aktuelle Ansicht und Auswahl.", ". Applies to the current view and selection.");
        }
    }
}
