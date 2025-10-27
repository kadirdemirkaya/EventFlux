namespace EventFlux.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HandlerOrderAttribute : Attribute
    {
        public int Priority { get; }

        public HandlerOrderAttribute(int priority)
        {
            Priority = priority;
        }
    }

}
