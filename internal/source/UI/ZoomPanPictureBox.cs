using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal class ZoomPanPictureBox : PictureBox, IMessageFilter
    {
        float zoom = 1;
        PointF offset;
        Point lastMouse;
        bool panning;
        Image viewedImage;
        readonly ToolTip navigationTip = new ToolTip();

        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(Point point);

        public ZoomPanPictureBox()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
            navigationTip.SetToolTip(this, L.T(
                "Mausrad: zoomen • Mausrad gedrückt ziehen: verschieben • Mausrad-Doppelklick: einpassen",
                "Mouse wheel: zoom • Middle-button drag: pan • Middle-button double-click: fit"));
            Application.AddMessageFilter(this);
        }

        internal float ViewZoom { get { return zoom; } }
        internal PointF ViewOffset { get { return offset; } }

        void CheckImage()
        {
            if (ReferenceEquals(viewedImage, Image))
                return;
            viewedImage = Image;
            ResetView();
        }

        internal void ResetView()
        {
            zoom = 1;
            offset = PointF.Empty;
            panning = false;
            Capture = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        internal void ZoomAt(Point point, int delta)
        {
            CheckImage();
            if (Image == null || delta == 0 || !Enabled)
                return;
            float next = (float)Math.Max(.1, Math.Min(64, zoom * Math.Pow(1.2, delta / 120.0)));
            float ratio = next / zoom;
            offset = new PointF(point.X - (point.X - offset.X) * ratio,
                point.Y - (point.Y - offset.Y) * ratio);
            zoom = next;
            Invalidate();
        }

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != 0x020A || !Visible || !Enabled || !IsHandleCreated || Image == null)
                return false;
            long packed = message.LParam.ToInt64();
            var screen = new Point(unchecked((short)(packed & 0xffff)),
                unchecked((short)((packed >> 16) & 0xffff)));
            // Nur die sichtbare Vorschau unter dem Zeiger erhält das Mausrad.
            if (WindowFromPoint(screen) != Handle)
                return false;
            ZoomAt(PointToClient(screen), unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff)));
            return true;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ZoomAt(e.Location, e.Delta);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            CheckImage();
            if (e.Button == MouseButtons.Middle && e.Clicks == 2)
            {
                ResetView();
                base.OnMouseDown(e);
                return;
            }
            if (e.Button == MouseButtons.Middle && Image != null)
            {
                panning = true;
                lastMouse = e.Location;
                Capture = true;
                Cursor = Cursors.SizeAll;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (panning)
            {
                offset.X += e.X - lastMouse.X;
                offset.Y += e.Y - lastMouse.Y;
                lastMouse = e.Location;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                panning = false;
                Capture = false;
                Cursor = Cursors.Default;
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture)
            {
                panning = false;
                Cursor = Cursors.Default;
            }
            base.OnMouseCaptureChanged(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                ResetView();
            base.OnMouseDoubleClick(e);
        }

        protected override void OnResize(EventArgs e)
        {
            ResetView();
            base.OnResize(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            CheckImage();
            GraphicsState state = e.Graphics.Save();
            try
            {
                e.Graphics.TranslateTransform(offset.X, offset.Y);
                e.Graphics.ScaleTransform(zoom, zoom);
                base.OnPaint(e);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Application.RemoveMessageFilter(this);
                navigationTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
