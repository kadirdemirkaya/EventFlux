namespace EventFlux.Attributes
{
    /// <summary>
    /// Specifies the execution start priority for a notification event handler.
    /// Lower values are started first when publishing notifications.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class HandlerOrderAttribute : Attribute
    {
        /// <summary>
        /// Gets the priority order value.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandlerOrderAttribute"/> class with the specified priority.
        /// </summary>
        /// <param name="priority">The priority value (lower values are executed first).</param>
        public HandlerOrderAttribute(int priority)
        {
            Priority = priority;
        }
    }

}
