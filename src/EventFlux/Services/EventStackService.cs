using EventFlux.Abstractions;
using System.Collections.Concurrent;

namespace EventFlux.Services
{
    /// <summary>
    /// Thread-safe queue service that stores deferred event requests for batch execution.
    /// </summary>
    public sealed class EventStackService
    {
        private readonly ConcurrentQueue<IEventRequest> _queue = new();

        /// <summary>
        /// Gets the number of event requests currently in the queue.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Gets a value indicating whether the queue is empty.
        /// </summary>
        public bool IsEmpty => _queue.IsEmpty;

        /// <summary>
        /// Enqueues an event request to the stack.
        /// </summary>
        /// <param name="request">The event request to enqueue.</param>
        public void AddEventRequest(IEventRequest request)
            => _queue.Enqueue(request);

        /// <summary>
        /// Dequeues and returns all currently queued event requests.
        /// </summary>
        /// <returns>A read-only list containing the dequeued event requests.</returns>
        public IReadOnlyList<IEventRequest> Drain()
        {
            var list = new List<IEventRequest>();
            while (_queue.TryDequeue(out var item))
                list.Add(item);

            return list;
        }

        /// <summary>
        /// Clears all event requests from the queue.
        /// </summary>
        public void Clear() => _queue.Clear();
    }
}
