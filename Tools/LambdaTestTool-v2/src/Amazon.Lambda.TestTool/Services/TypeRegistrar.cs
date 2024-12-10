using Spectre.Console.Cli;

namespace Amazon.Lambda.TestTool.Services;

/// <summary>
/// Provides functionality to register types and instances with an <see cref="IServiceCollection"/> for dependency injection.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar
{
    /// <inheritdoc/>
    public ITypeResolver Build()
    {
        return new TypeResolver(builder.BuildServiceProvider());
    }

    /// <inheritdoc/>
    public void Register(Type service, Type implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    /// <inheritdoc/>
    public void RegisterInstance(Type service, object implementation)
    {
        builder.AddSingleton(service, implementation);
    }

    /// <inheritdoc/>
    public void RegisterLazy(Type service, Func<object> func)
    {
        if (func is null)
        {
            throw new ArgumentNullException(nameof(func));
        }

        builder.AddSingleton(service, (provider) => func());
    }
}