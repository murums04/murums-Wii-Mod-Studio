using System.Reflection;
using System.Runtime.InteropServices;

#if SETUP_BUNDLE
[assembly: AssemblyTitle("murums Wii Mod Studio")]
#elif STUDIO_UNINSTALLER
[assembly: AssemblyTitle("murums Wii Mod Studio — Uninstall")]
#else
[assembly: AssemblyTitle("murums Wii Mod Studio")]
#endif
[assembly: AssemblyDescription("Integrated Nintendo Wii modding studio with archive, text, layout, animation, texture and specialist toolchain workflows")]
[assembly: AssemblyCompany("murums")]
[assembly: AssemblyProduct("murums Wii Mod Studio")]
[assembly: AssemblyCopyright("Copyright © 2026 murums04. PolyForm Noncommercial 1.0.0.")]
[assembly: ComVisible(false)]
[assembly: Guid("19d503b2-87c8-48a1-bf2f-612e7e29edab")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.138")]
[assembly: AssemblyInformationalVersion("2.1.0-beta2")]
