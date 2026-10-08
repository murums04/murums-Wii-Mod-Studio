using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioTypography
    {
        internal static readonly Font Body = new Font("Segoe UI", 10F);
        internal static readonly Font Eyebrow = new Font("Segoe UI", 9F, FontStyle.Bold);
        internal static readonly Font EntryTitle = new Font("Segoe UI", 22F, FontStyle.Bold);
        internal static readonly Font EntryDescription = new Font("Segoe UI", 11F);
        sealed class Marker { }
        sealed class GridFonts { internal Font Header; }
        static readonly ConditionalWeakTable<Form, Marker> initialized = new ConditionalWeakTable<Form, Marker>();
        static readonly ConditionalWeakTable<DataGridView, GridFonts> grids = new ConditionalWeakTable<DataGridView, GridFonts>();

        internal static void Initialize(Control control)
        {
            var form = control as Form;
            if (form == null) return;
            Marker marker;
            if (initialized.TryGetValue(form, out marker)) return;
            initialized.Add(form, new Marker());
            Font font = form.Font;
            // Nur die alten Grundgrössen vereinheitlichen; Vorschau-Schriften und vergrösserte Schrift erhalten.
            bool legacyBody = font.Name == Body.Name && font.Style == FontStyle.Regular
                && font.SizeInPoints >= 9F && font.SizeInPoints <= 10.5F;
            if ((legacyBody || font.Equals(Control.DefaultFont)) && !font.Equals(Body)) form.Font = Body;
        }

        internal static void StyleGrid(DataGridView grid)
        {
            GridFonts fonts;
            if (grids.TryGetValue(grid, out fonts)) return;
            fonts = new GridFonts();
            grids.Add(grid, fonts);
            Action refresh = delegate
            {
                Font previous = fonts.Header;
                Font current = grid.ColumnHeadersDefaultCellStyle.Font;
                // DataGridView hinterlegt die geerbte Grundschrift bereits im Standard-Zellstil.
                bool inheritedHeader = previous == null && current != null && current.Equals(grid.Font);
                if (current != null && current != previous && !inheritedHeader) return;
                fonts.Header = new Font(grid.Font, FontStyle.Bold);
                grid.ColumnHeadersDefaultCellStyle.Font = fonts.Header;
                if (previous != null) previous.Dispose();
            };
            refresh();
            grid.FontChanged += delegate { refresh(); };
            grid.Disposed += delegate { if (fonts.Header != null) fonts.Header.Dispose(); };
        }
    }
}
