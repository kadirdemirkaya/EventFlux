using EventFlux.Abstractions;
using System.Collections.Concurrent;

namespace EventFlux.Services
{
    public sealed class EventStackService
    {
        private readonly ConcurrentQueue<IEventRequest> _queue = new();

        public void AddEventRequest(IEventRequest request)
            => _queue.Enqueue(request);

        public IReadOnlyList<IEventRequest> Drain()
        {
            var list = new List<IEventRequest>();
            while (_queue.TryDequeue(out var item))
                list.Add(item);

            return list;
        }
    }
}
