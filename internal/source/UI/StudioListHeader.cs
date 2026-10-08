using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    internal static class StudioListHeader
    {
        const int WmPaint = 0x000F, WmEraseBackground = 0x0014, WmPrintClient = 0x0318;
        const int LvmGetHeader = 0x101F, HdmGetItemCount = 0x1200, HdmGetItemRect = 0x1207;
        const int PrfClientAndEraseBackground = 0x0C;
        static readonly ConditionalWeakTable<ListView, HeaderWindow> headers = new ConditionalWeakTable<ListView, HeaderWindow>();

        internal static void Attach(ListView list)
        {
            HeaderWindow existing;
            if (headers.TryGetValue(list, out existing)) return;
            var header = new HeaderWindow(list);
            headers.Add(list, header);
            list.HandleCreated += delegate { header.Bind(); };
            list.HandleDestroyed += delegate { header.Unbind(); };
            list.Disposed += delegate { header.Dispose(); };
            list.StyleChanged += delegate { header.Bind(); };
            if (list.IsHandleCreated) header.Bind();
        }

        sealed class HeaderWindow : NativeWindow, IDisposable
        {
            readonly ListView list;
            readonly BufferedGraphicsContext buffer = new BufferedGraphicsContext();
            bool disposed;

            internal HeaderWindow(ListView list) { this.list = list; }

            internal void Bind()
            {
                if (disposed || list.IsDisposed || !list.IsHandleCreated) return;
                IntPtr handle = SendMessage(list.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
                if (Handle == handle) return;
                Unbind();
                if (handle != IntPtr.Zero) AssignHandle(handle);
            }

            internal void Unbind()
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
                buffer.Invalidate();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Unbind();
                buffer.Dispose();
            }

            protected override void WndProc(ref Message message)
            {
                if (SystemInformation.HighContrast || !list.OwnerDraw || list.View != View.Details)
                {
                    base.WndProc(ref message);
                    return;
                }
                if (message.Msg == WmEraseBackground)
                {
                    message.Result = new IntPtr(1);
                    return;
                }
                if (message.Msg == WmPaint)
                {
                    PaintStruct paint;
                    IntPtr dc = BeginPaint(Handle, out paint);
                    try
                    {
                        NativeRect rect;
                        if (dc != IntPtr.Zero && GetClientRect(Handle, out rect) && rect.Right > 0 && rect.Bottom > 0)
                        {
                            Rectangle bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                            Size bufferSize = new Size(bounds.Width + 1, bounds.Height + 1);
                            if (buffer.MaximumBuffer != bufferSize) buffer.MaximumBuffer = bufferSize;
                            using (BufferedGraphics frame = buffer.Allocate(dc, bounds))
                            {
                                // Spalten und freien Rest gemeinsam ausgeben, damit kein heller Zwischenstand erscheint.
                                IntPtr frameDc = frame.Graphics.GetHdc();
                                try
                                {
                                    Message nativePaint = Message.Create(Handle, WmPrintClient, frameDc, new IntPtr(PrfClientAndEraseBackground));
                                    base.WndProc(ref nativePaint);
                                }
                                finally { frame.Graphics.ReleaseHdc(frameDc); }
                                FillRemainder(frame.Graphics, bounds);
                                frame.Render(dc);
                            }
                        }
                    }
                    finally { EndPaint(Handle, ref paint); }
                    message.Result = IntPtr.Zero;
                    return;
                }
                base.WndProc(ref message);
                if (message.Msg == WmPrintClient && message.WParam != IntPtr.Zero)
                {
                    NativeRect rect;
                    if (GetClientRect(Handle, out rect))
                        using (Graphics graphics = Graphics.FromHdc(message.WParam))
                            FillRemainder(graphics, Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom));
                }
            }

            void FillRemainder(Graphics graphics, Rectangle bounds)
            {
                using (var remainder = new Region(bounds))
                {
                    int count = (int)SendMessage(Handle, HdmGetItemCount, IntPtr.Zero, IntPtr.Zero);
                    for (int index = 0; index < count; index++)
                    {
                        NativeRect column;
                        if (SendMessage(Handle, HdmGetItemRect, new IntPtr(index), out column) != IntPtr.Zero)
                            remainder.Exclude(Rectangle.FromLTRB(column.Left, column.Top, column.Right, column.Bottom));
                    }
                    using (var brush = new SolidBrush(DarkTheme.Panel2)) graphics.FillRegion(brush, remainder);
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)] struct NativeRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct PaintStruct
        {
            internal IntPtr Dc;
            internal int Erase;
            internal NativeRect Paint;
            internal int Restore, IncrementalUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] internal byte[] Reserved;
        }
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, out NativeRect lParam);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr window, out PaintStruct paint);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr window, ref PaintStruct paint);
    }
}
