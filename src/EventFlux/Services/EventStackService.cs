using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using EventFlux.Abstractions;

namespace EventFlux.Services
{
    public class EventStackService
    {
        private readonly List<IEventRequest> _internalEventHandlers;
        private readonly object _sync = new();

        public EventStackService()
        {
            _internalEventHandlers = new List<IEventRequest>();
        }

        public List<IEventRequest> InternalEventHandlers
        {
            get
            {
                lock (_sync)
                {
                    return _internalEventHandlers;
                }
            }
        }

        public void ClearEventRequest()
        {
            lock (_sync)
            {
                _internalEventHandlers.Clear();
            }
        }

        public void AddEventRequest<TEvent>(TEvent eventRequest)
            where TEvent : IEventRequest
        {
            lock (_sync)
            {
                if (!_internalEventHandlers.Contains(eventRequest))
                {
                    _internalEventHandlers.Add(eventRequest);
                }
            }
        }

        public void RemoveEventRequest<TEvent>(TEvent eventRequest)
           where TEvent : IEventRequest
        {
            lock (_sync)
            {
                if (_internalEventHandlers.Contains(eventRequest))
                {
                    _internalEventHandlers.Remove(eventRequest);
                }
            }
        }

        public IReadOnlyList<IEventRequest> GetAllEventRequest()
        {
            lock (_sync)
            {
                return _internalEventHandlers.AsReadOnly();
            }
        }
    }
}
