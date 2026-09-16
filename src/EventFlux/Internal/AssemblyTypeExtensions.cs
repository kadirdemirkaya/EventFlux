using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EventFlux.Internal
{
    [RequiresUnreferencedCode(AotMessages.UnreferencedCode)]
    internal static class AssemblyTypeExtensions
    {
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null).Select(t => t!);
            }
        }
    }
}
