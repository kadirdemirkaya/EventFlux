using EventFlux.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventFlux.Test.Events
{
    public class ExceptionThrowingEventRequest : IEventRequest
    {
    }

    public class ExceptionThrowingHandler : IEventHandler<ExceptionThrowingEventRequest>
    {
        public async Task Handle(ExceptionThrowingEventRequest request)
        {
            await Task.Delay(50);
            Console.WriteLine("ExceptionThrowingHandler: About to throw exception...");
            throw new InvalidOperationException("Simulated handler exception");
        }
    }
}
