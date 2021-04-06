using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace Amazon.Lambda.TestTool.Runtime
{
    public class LambdaAssemblyLoadContext : AssemblyLoadContext
    {
#if !NETCOREAPP2_1
        private AssemblyDependencyResolver _builtInResolver;
#endif
        private CustomAssemblyResolver _customResolver;

        private CustomAssemblyResolver _customDefaultContextResolver;


        public LambdaAssemblyLoadContext(string lambdaPath)
            :
#if !NETCOREAPP2_1
            base("LambdaContext")
        {
            _builtInResolver = new AssemblyDependencyResolver(lambdaPath);   
#else
            base()
        { 
#endif
        _customResolver = new CustomAssemblyResolver(this, lambdaPath);

            _customDefaultContextResolver = new CustomAssemblyResolver(AssemblyLoadContext.Default, lambdaPath);

            AssemblyLoadContext.Default.Resolving += OnDefaultAssemblyLoadContextResolving;
        }

        private Assembly OnDefaultAssemblyLoadContextResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            string assemblyPath = _customDefaultContextResolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name.StartsWith("Amazon.Lambda.Core"))
                return null;

            string assemblyPath = null;
#if !NETCOREAPP2_1
            assemblyPath = _builtInResolver.ResolveAssemblyToPath(assemblyName);
#endif
            if (assemblyPath == null || !File.Exists(assemblyPath))
            {
                assemblyPath = _customResolver.ResolveAssemblyToPath(assemblyName);
            }

            if (assemblyPath == null)
            {
                assemblyPath = SearchMicrosoftAspNetCoreApp(assemblyName);
            }

            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        /// <summary>
        /// See if the assembly being loaded is coming from the Microsoft.AspNetCore.App runtime. If so
        /// then load that assembly. 
        /// 
        /// This is done because if fallback to the Default AssemblyLoadContext is used then services added 
        /// to the IServiceCollection will not be resolved. They will be added from the Lambda context but be 
        /// attempted to resolved in the Default context. The Default context doesn't have access to the types in the
        /// Lambda context and so they will fail to resolve.
        /// </summary>
        /// <param name="assemblyName"></param>
        /// <returns></returns>
        private string SearchMicrosoftAspNetCoreApp(AssemblyName assemblyName)
        {
            var pathMicrosoftAspNetCoreApp = Path.GetDirectoryName(typeof(string).Assembly.Location).Replace("Microsoft.NETCore.App", "Microsoft.AspNetCore.App");
            var assemblyDllName = assemblyName.Name + ".dll";
            var fullPath = Path.Combine(pathMicrosoftAspNetCoreApp, assemblyDllName);

            return File.Exists(fullPath) ? fullPath : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = null;
#if !NETCOREAPP2_1
            libraryPath = _builtInResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
#endif
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }


        class CustomAssemblyResolver
        {
            private readonly ICompilationAssemblyResolver assemblyResolver;
            private readonly DependencyContext dependencyContext;

            public CustomAssemblyResolver(AssemblyLoadContext assemblyLoadContext, string rootAssemblyPath)
            {
                var assembly = assemblyLoadContext.LoadFromAssemblyPath(rootAssemblyPath);
                this.dependencyContext = DependencyContext.Load(assembly);

                this.assemblyResolver = new CompositeCompilationAssemblyResolver
                                        (new ICompilationAssemblyResolver[]
                {
                    new AppBaseCompilationAssemblyResolver(Path.GetDirectoryName(rootAssemblyPath)),
                    new ReferenceAssemblyPathResolver(),
                    new PackageCompilationAssemblyResolver()
                });
            }

            public string ResolveAssemblyToPath(AssemblyName name)
            {
                bool NamesMatch(RuntimeLibrary runtime)
                {
                    return string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase);
                }

                bool ResourceAssetPathMatch(RuntimeLibrary runtime)
                {
                    foreach (var group in runtime.RuntimeAssemblyGroups)
                    {
                        foreach (var path in group.AssetPaths)
                        {
                            if (path.EndsWith("/" + name.Name + ".dll"))
                            {
                                return true;
                            }
                        }
                    }
                    return false;
                }

                RuntimeLibrary library =
                    this.dependencyContext.RuntimeLibraries.FirstOrDefault(NamesMatch);

                if (library == null)
                    library = this.dependencyContext.RuntimeLibraries.FirstOrDefault(ResourceAssetPathMatch);

                if (library != null)
                {
                    var wrapper = new CompilationLibrary(
                        library.Type,
                        library.Name,
                        library.Version,
                        library.Hash,
                        library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                        library.Dependencies,
                        library.Serviceable);

                    var assemblies = new List<string>();
                    this.assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblies);
                    if (assemblies.Count > 0)
                    {
                        return assemblies[0];
                    }
                }

                return null;
            }
        }
    }
}
