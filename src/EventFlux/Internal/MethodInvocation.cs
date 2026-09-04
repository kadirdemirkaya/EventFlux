using System.Reflection;
using System.Runtime.ExceptionServices;

namespace EventFlux.Internal
{
    internal static class MethodInvocation
    {
        public static object? InvokePreservingException(MethodInfo method, object target, object?[] arguments)
        {
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

                throw;
            }
        }
    }
}
