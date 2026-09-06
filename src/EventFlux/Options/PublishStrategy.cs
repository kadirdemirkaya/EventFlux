namespace EventFlux.Options
{
    /// <summary>
    /// Defines the execution strategy for notification handlers during publish operations.
    /// </summary>
    public enum PublishStrategy
    {
        /// <summary>
        /// Handlers are executed concurrently using <see cref="System.Threading.Tasks.Task.WhenAll"/>.
        /// </summary>
        Parallel = 0,

        /// <summary>
        /// Handlers are executed sequentially one by one in order of their priority.
        /// </summary>
        Sequential = 1
    }
}
