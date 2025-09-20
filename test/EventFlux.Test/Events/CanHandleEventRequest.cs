using EventFlux.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventFlux.Test.Events
{
    public class CanHandleEventRequest : IEventRequest
    {
        public string Data { get; set; }
    }

    public class CanHandleEventHandler : IEventHandler<CanHandleEventRequest>
    {
        public bool CanHandle(CanHandleEventRequest @event)
            => @event.Data.Length > 15;

        public async Task Handle(CanHandleEventRequest @event)
        {
            Console.WriteLine("Received event", @event.Data);
        }
    }
}
