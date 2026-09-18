using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace murumsWiiModStudio
{
    // Windows Common Item Dialog: Explorer navigation, search and shortcut
    // resolution, including when running on the classic .NET Framework.
    internal sealed class FolderPickerDialog : IDisposable
    {
        public string Description { get; set; }
        public string SelectedPath { get; set; }

        public DialogResult ShowDialog(IWin32Window owner)
        {
            IFileDialog dialog = null;
            IShellItem initial = null;
            IShellItem result = null;
            IntPtr path = IntPtr.Zero;
            try
            {
                dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")));
                uint options;
                dialog.GetOptions(out options);
                // PICKFOLDERS | FORCEFILESYSTEM | PATHMUSTEXIST | NOCHANGEDIR.
                // Deliberately clear NODEREFERENCELINKS: .lnk opens its target.
                dialog.SetOptions((options | 0x20u | 0x40u | 0x800u | 0x8u) & ~0x100000u);
                if (!String.IsNullOrWhiteSpace(Description))
                    dialog.SetTitle(Description);
                dialog.SetOkButtonLabel(Translate("Ordner auswählen", "Select folder"));
                if (!String.IsNullOrWhiteSpace(SelectedPath) && Directory.Exists(SelectedPath))
                {
                    Guid iid = typeof(IShellItem).GUID;
                    int initialHr = SHCreateItemFromParsingName(Path.GetFullPath(SelectedPath), IntPtr.Zero, ref iid, out initial);
                    if (initialHr >= 0)
                        dialog.SetFolder(initial);
                }

                int hr = dialog.Show(owner == null ? IntPtr.Zero : owner.Handle);
                if (hr == unchecked((int)0x800704C7))
                    return DialogResult.Cancel;
                Marshal.ThrowExceptionForHR(hr);
                dialog.GetResult(out result);
                result.GetDisplayName(0x80058000u, out path); // SIGDN_FILESYSPATH
                string chosen = Marshal.PtrToStringUni(path);
                if (String.IsNullOrEmpty(chosen) || !Directory.Exists(chosen))
                    throw new IOException(Translate("Bitte einen vorhandenen Ordner auswählen.", "Please select an existing folder."));
                SelectedPath = chosen;
                return DialogResult.OK;
            }
            finally
            {
                if (path != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(path);
                if (result != null)
                    Marshal.ReleaseComObject(result);
                if (initial != null)
                    Marshal.ReleaseComObject(initial);
                if (dialog != null)
                    Marshal.ReleaseComObject(dialog);
            }
        }

        private static string Translate(string german, string english)
        {
#if SETUP_BUNDLE
            return english;
#else
            return L.T(german, english);
#endif
        }

        public void Dispose()
        {
        } // Native resources are scoped to ShowDialog.

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr value);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint kind, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        // IFileDialog vtable in native order, including unused slots before GetResult.
        [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig]
            int Show(IntPtr owner);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName(out IntPtr name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
        }
    }
}
