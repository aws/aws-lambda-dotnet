using System.Reflection;

namespace Amazon.Lambda.RuntimeSupport.Bootstrap
{
    internal class Constants
    {
        internal const BindingFlags DefaultFlags = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public
                                                   | BindingFlags.Instance | BindingFlags.Static;
    }
}