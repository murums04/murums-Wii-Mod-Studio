using System.Drawing;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class ToolFileHint
    {
        internal static Control Wrap(Control header, string examples)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0)
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(header, 0, 0);
            var hint = new Label
            {
                Name = "ToolFileExamples",
                Text = (examples.Contains(".szs") ? L.T("ISO/WBFS empfohlen · ", "ISO/WBFS recommended · ") : L.T("Dateien / Beispiele: ", "Files / examples: ")) + examples,
                Font = new Font("Segoe UI", 9F),
                AutoSize = true,
                Dock = DockStyle.Fill,
                UseMnemonic = false,
                Margin = new Padding(8, 5, 8, 5)
            };
            panel.Controls.Add(hint, 0, 1);
            return panel;
        }
    }
}
