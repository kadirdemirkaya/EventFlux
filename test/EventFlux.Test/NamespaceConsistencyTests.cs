using System;
using EventFlux.Services;
using Xunit;

namespace EventFlux.Test
{
    public class NamespaceConsistencyTests
    {
        [Fact]
        public void EventService_BelongsTo_EventFluxServicesNamespace()
        {
            // Assert
            Assert.Equal("EventFlux.Services", typeof(EventService).Namespace);
        }

        [Fact]
        public void EventMapService_BelongsTo_EventFluxServicesNamespace()
        {
            // Assert
            Assert.Equal("EventFlux.Services", typeof(EventMapService).Namespace);
        }

        [Fact]
        public void EventStackService_BelongsTo_EventFluxServicesNamespace()
        {
            // Assert
            Assert.Equal("EventFlux.Services", typeof(EventStackService).Namespace);
        }

        [Fact]
        public void AllServiceTypes_ShareSameNamespace()
        {
            // Assert
            var expected = "EventFlux.Services";
            Assert.Equal(expected, typeof(EventService).Namespace);
            Assert.Equal(expected, typeof(EventMapService).Namespace);
            Assert.Equal(expected, typeof(EventStackService).Namespace);
        }
    }
}
