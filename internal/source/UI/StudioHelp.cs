using System;

namespace murumsWiiModStudio
{
    internal sealed class HelpLink
    {
        public readonly string Title, Url;
        public HelpLink(string title, string url)
        {
            Title = title;
            Url = url;
        }
    }

    internal static class StudioHelp
    {
        public static void BuildNavigation(System.Windows.Forms.TabPage tab, System.Windows.Forms.RichTextBox box)
        {
            StudioHelpCards.Build(tab, box);
        }

        public static string Guide()
        {
            var text = new System.Text.StringBuilder();
            foreach (var topic in StudioHelpTopics.All())
            {
                text.AppendLine(topic.Title);
                text.AppendLine(topic.Summary);
                text.AppendLine(topic.Route);
                for (int i = 0; i < topic.Steps.Length; i++)
                    text.AppendLine((i + 1) + ". " + topic.Steps[i].Replace("|", ": "));
                text.AppendLine(topic.Note);
                text.AppendLine();
            }

            return text.ToString() + Links();
        }

        public static HelpLink[] ReferenceLinks()
        {
            return new[]
            {
                new HelpLink("murums Wii Mod Studio", "https://github.com/murums04/murums-Wii-Mod-Studio"),
                new HelpLink(L.T("Downloads & Versionen", "Downloads & releases"), "https://github.com/murums04/murums-Wii-Mod-Studio/releases"),
                new HelpLink("Wiimms SZS Tools", "https://szs.wiimm.de/"),
                new HelpLink(L.T("Wiimms: Streckenprüfung", "Wiimms: track validation"), "https://szs.wiimm.de/wszst/cmd-check.html"),
                new HelpLink("KMP — " + L.T("Startpositionen und Routen", "start positions and routes"), "https://mkwiiki.org/wiki/KMP_%28File_Format%29"),
                new HelpLink("KCL — " + L.T("Kollision", "collision surfaces"), "https://mkwiiki.org/wiki/KCL_%28File_Format%29"),
                new HelpLink("BrawlCrate", "https://github.com/soopercool101/BrawlCrate"),
                new HelpLink("Wiimms BMG Tool", "https://szs.wiimm.de/wbmgt/"),
                new HelpLink("Looping Audio Converter", "https://github.com/libertyernie/LoopingAudioConverter"),
                new HelpLink("NintyFont", "https://github.com/hadashisora/NintyFont"),
                new HelpLink("BRFNTify-Next", "https://github.com/RoadrunnerWMC/BRFNTify-Next")
            };
        }

        public static string Links()
        {
            var text = new System.Text.StringBuilder();
            foreach (var link in ReferenceLinks())
            {
                text.AppendLine(link.Title);
                text.AppendLine(link.Url);
                text.AppendLine();
            }

            return text.ToString();
        }

        public static string AnimationGuide()
        {
            return L.T("STUDIO-WORKFLOW\nFür einen Retro-Rewind-Hintergrund ist der Retro-Rewind-Editor im Hauptfenster der einfachste Einstieg. Hier bearbeitest du einzelne BRLAN-Animationen. Arbeite mit einer Kopie, ändere einen Wert, validiere und speichere mit Speichern unter. Die BRLAN und alle referenzierten TPL-Bilder müssen anschließend im passenden Menüarchiv liegen; eine BRLAN allein enthält keine Bildpixel. Teste die Animation im Spiel.\n\n", "STUDIO WORKFLOW\nFor a Retro Rewind background, start with the Retro Rewind editor in the main window. This editor is for individual BRLAN animations. Work on a copy, change one value, validate and Save as. The BRLAN and all referenced TPL pictures must then be placed in the matching menu archive; a BRLAN alone contains no picture pixels. Test the animation in game.\n\n") + Links();
        }
    }
}
