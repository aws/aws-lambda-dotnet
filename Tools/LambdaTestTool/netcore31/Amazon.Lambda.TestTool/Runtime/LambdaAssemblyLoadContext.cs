using System;
using System.Reflection;
using System.Runtime.Loader;

namespace Amazon.Lambda.TestTool.Runtime
{
    public class LambdaAssemblyLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public LambdaAssemblyLoadContext(string lambdaPath)
        {
            _resolver = new AssemblyDependencyResolver(lambdaPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name.StartsWith("Amazon.Lambda.Core"))
                return null;

            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
