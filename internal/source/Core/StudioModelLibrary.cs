using System;
using System.IO;
using System.Reflection;

namespace murumsWiiModStudio
{
    internal static class StudioModelLibrary
    {
        static readonly object gate = new object();
        static Type codec;
        internal static object Call(string method, params object[] arguments)
        {
            lock (gate)
            {
                if (codec == null)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs args) {
                        string name = new AssemblyName(args.Name).Name;
                        if (name != "BrawlLib" && name != "OpenTK") return null;
                        string dependency = Path.Combine(ModelRuntime.Root, name + ".dll");
                        return File.Exists(dependency) ? Assembly.LoadFrom(dependency) : null;
                    };
                    codec = Assembly.LoadFrom(Path.Combine(ModelRuntime.Root, "StudioModelCodec.dll")).GetType("StudioModelCodec", true);
                }
                try { return codec.GetMethod(method).Invoke(null, arguments); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
        }
        internal static CharacterModelImport Reference(string source, string destination)
        {
            Call("ExportReference", source, destination);
            return CharacterModelImport.Load(destination);
        }
    }
}
