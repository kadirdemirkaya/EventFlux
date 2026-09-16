namespace EventFlux.Internal
{
    internal static class AotMessages
    {
        public const string DynamicCode =
            "EventFlux closes generic handler and pipeline types with MakeGenericType and compiles dispatch delegates with System.Linq.Expressions at runtime. This is not supported under NativeAOT.";

        public const string UnreferencedCode =
            "EventFlux discovers handlers by scanning assemblies and invokes them through reflection. Handler types and their members may be removed by the trimmer.";
    }
}
