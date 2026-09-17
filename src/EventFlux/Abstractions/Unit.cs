namespace EventFlux.Abstractions
{
    /// <summary>
    /// Represents the empty response of a request that does not return a value.
    /// </summary>
    /// <remarks>
    /// Implement <see cref="IEventRequest{TResponse}"/> with <see cref="Unit"/> for a command, handle it with
    /// <see cref="IEventHandler{TRequest, TResponse}"/> returning <see cref="Value"/>, and dispatch it with the
    /// <c>SendAsync(IEventRequest&lt;Unit&gt;)</c> overload of <see cref="IEventBus"/> or <see cref="IEventDispatcher"/>.
    /// </remarks>
    public readonly struct Unit : IEventResponse, IEquatable<Unit>, IComparable<Unit>
    {
        /// <summary>
        /// Gets the single value of the <see cref="Unit"/> type.
        /// </summary>
        public static readonly Unit Value = default;

        /// <summary>
        /// Gets a completed task whose result is <see cref="Value"/>.
        /// </summary>
        public static Task<Unit> Task { get; } = System.Threading.Tasks.Task.FromResult(Value);

        /// <inheritdoc />
        public bool Equals(Unit other) => true;

        /// <inheritdoc />
        public int CompareTo(Unit other) => 0;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Unit;

        /// <inheritdoc />
        public override int GetHashCode() => 0;

        /// <inheritdoc />
        public override string ToString() => "()";

        /// <summary>
        /// Determines whether two <see cref="Unit"/> values are equal. Always <c>true</c>.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns><c>true</c>.</returns>
        public static bool operator ==(Unit left, Unit right) => true;

        /// <summary>
        /// Determines whether two <see cref="Unit"/> values differ. Always <c>false</c>.
        /// </summary>
        /// <param name="left">The first value.</param>
        /// <param name="right">The second value.</param>
        /// <returns><c>false</c>.</returns>
        public static bool operator !=(Unit left, Unit right) => false;
    }
}
