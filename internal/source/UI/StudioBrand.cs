using System.Drawing;
using System.Drawing.Drawing2D;

namespace murumsWiiModStudio
{
    internal static class StudioBrand
    {
        internal static Bitmap Logo(int size)
        {
            using (var stream = typeof(StudioBrand).Assembly.GetManifestResourceStream("Studio.StudioLogo.png"))
            using (var source = Image.FromStream(stream))
            {
                var bitmap = new Bitmap(size, size);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(source, 0, 0, size, size);
                }
                return bitmap;
            }
        }
    }
}
