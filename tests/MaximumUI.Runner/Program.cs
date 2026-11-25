using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

// Run the existing net461 assertion assembly in a fresh process on Windows or
// Linux. This deliberately executes its Main; dotnet test does not discover it.
internal static class Program {
    private static int Main(string[] args) {
        if (args.Length != 1) {
            Console.Error.WriteLine("Usage: MaximumUI.Runner <MaximumUI.Tests.dll>");
            return 2;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        string directory = Path.GetDirectoryName(assemblyPath);
        AssemblyLoadContext.Default.Resolving += (context, name) => {
            string local = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(local)) return context.LoadFromAssemblyPath(local);
            string sibling = Path.GetFullPath(Path.Combine(directory, "..", "MaximumUI", name.Name + ".dll"));
            return File.Exists(sibling) ? context.LoadFromAssemblyPath(sibling) : null;
        };

        try {
            Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            MethodInfo entry = assembly.GetType("MaximumUI.Tests.Program", true)
                .GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (entry == null) throw new MissingMethodException("MaximumUI.Tests.Program.Main");
            return (int)entry.Invoke(null, null);
        } catch (Exception error) {
            Console.Error.WriteLine(error is TargetInvocationException && error.InnerException != null
                ? error.InnerException : error);
            return 1;
        }
    }
}
