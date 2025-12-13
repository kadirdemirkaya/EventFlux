using System.Reflection;

namespace EventFlux.Descriptors
{
    internal sealed record HandlerDescriptor(
        Type HandlerType,
        MethodInfo HandleMethod,
        MethodInfo? CanHandleMethod,
        int Priority
    );

}
