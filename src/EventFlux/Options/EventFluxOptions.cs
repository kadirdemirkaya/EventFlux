namespace EventFlux.Options
{
    /// <summary>
    /// Configuration options for EventFlux behavior.
    /// </summary>
    public class EventFluxOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether a new dependency injection scope is created for each event dispatch.
        /// When <c>true</c> (default), each event runs in an isolated child scope.
        /// When <c>false</c>, handlers and behaviors are resolved directly from the ambient service provider.
        /// </summary>
        public bool CreateScopePerEvent { get; set; } = true;
    }
}
