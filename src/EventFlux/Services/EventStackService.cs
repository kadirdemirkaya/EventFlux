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
        private readonly object _mutationLock = new();

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
        {
            lock (_mutationLock)
            {
                _queue.Enqueue(request);
            }
        }

        /// <summary>
        /// Dequeues and returns all currently queued event requests.
        /// </summary>
        /// <returns>A read-only list containing the dequeued event requests.</returns>
        public IReadOnlyList<IEventRequest> Drain()
        {
            lock (_mutationLock)
            {
                return DequeueAll();
            }
        }

        /// <summary>
        /// Clears all event requests from the queue.
        /// </summary>
        public void Clear()
        {
            lock (_mutationLock)
            {
                _queue.Clear();
            }
        }

        internal void RequeueAhead(IReadOnlyList<IEventRequest> requests, int startIndex)
        {
            if (startIndex >= requests.Count)
                return;

            lock (_mutationLock)
            {
                var stillQueued = DequeueAll();

                for (var i = startIndex; i < requests.Count; i++)
                    _queue.Enqueue(requests[i]);

                foreach (var request in stillQueued)
                    _queue.Enqueue(request);
            }
        }

        private List<IEventRequest> DequeueAll()
        {
            var list = new List<IEventRequest>();
            while (_queue.TryDequeue(out var item))
                list.Add(item);

            return list;
        }
    }
}
