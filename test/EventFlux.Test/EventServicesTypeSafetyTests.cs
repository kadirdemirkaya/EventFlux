using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventFlux.Abstractions;
using EventFlux.Services;
using Xunit;

namespace EventFlux.Test
{
    public record TestRecordEvent(string Value) : IEventRequest<TestRecordResponse>;

    public record TestRecordResponse(string Result) : IEventResponse;

    public class CustomToStringEvent : IEventRequest<TestNormalResponse>
    {
        public override string ToString() => "CustomToStringEvent_OverriddenRepresentation";
    }

    public class TestNormalEvent : IEventRequest
    {
    }

    public class TestNormalResponse : IEventResponse
    {
    }

    public class DummyNormalEventHandler : IEventHandler<TestNormalEvent>
    {
        public Task Handle(TestNormalEvent @event) => Task.CompletedTask;
    }

    public class EventServicesTypeSafetyTests
    {
        [Fact]
        public void EventService_GetHandlersForEvent_WhenUnregistered_ReturnsEmptyListNotNull()
        {
            // Arrange
            var eventService = new EventService();

            // Act
            var handlers = eventService.GetHandlersForEvent(typeof(TestNormalEvent));

            // Assert
            Assert.NotNull(handlers);
            Assert.Empty(handlers);
        }

        [Fact]
        public void EventService_GetHandlersForEvent_WhenNull_ReturnsEmptyListNotNull()
        {
            // Arrange
            var eventService = new EventService();

            // Act
            var handlers = eventService.GetHandlersForEvent(null!);

            // Assert
            Assert.NotNull(handlers);
            Assert.Empty(handlers);
        }

        [Fact]
        public void EventService_GetHandlersForEventGeneric_WhenRegistered_ReturnsHandlers()
        {
            // Arrange
            var eventService = new EventService();
            eventService.Subscribe<TestNormalEvent, DummyNormalEventHandler>();

            // Act
            var handlers = eventService.GetHandlersForEvent<TestNormalEvent>();

            // Assert
            Assert.NotNull(handlers);
            Assert.Single(handlers);
            Assert.Equal(typeof(DummyNormalEventHandler), handlers[0]);
        }

        [Fact]
        public void EventService_GetHandlersForEventGeneric_WhenUnregistered_ReturnsEmptyList()
        {
            // Arrange
            var eventService = new EventService();

            // Act
            var handlers = eventService.GetHandlersForEvent<TestNormalEvent>();

            // Assert
            Assert.NotNull(handlers);
            Assert.Empty(handlers);
        }

        [Fact]
        public void EventMapService_IsMap_WithRecordInstance_ReturnsTrue()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<TestRecordEvent, TestRecordResponse>();
            var recordInstance = new TestRecordEvent("sample-payload");

            // Act
            var isMapped = mapService.IsMap(recordInstance);

            // Assert
            Assert.True(isMapped);
        }

        [Fact]
        public void EventMapService_IsMap_WithCustomToStringInstance_ReturnsTrue()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<CustomToStringEvent, TestNormalResponse>();
            var customInstance = new CustomToStringEvent();

            // Act
            var isMapped = mapService.IsMap(customInstance);

            // Assert
            Assert.True(isMapped);
        }

        [Fact]
        public void EventMapService_IsMap_ByType_ReturnsExpectedResult()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap(typeof(TestNormalEvent), typeof(TestNormalResponse));

            // Act
            var mapped = mapService.IsMap(typeof(TestNormalEvent));
            var unmapped = mapService.IsMap(typeof(CustomToStringEvent));
            var nullType = mapService.IsMap(null!);

            // Assert
            Assert.True(mapped);
            Assert.False(unmapped);
            Assert.False(nullType);
        }

        [Fact]
        public void EventMapService_IsMapGeneric_ReturnsExpectedResult()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<TestNormalEvent, TestNormalResponse>();

            // Act
            var mapped = mapService.IsMap<TestNormalEvent>();
            var unmapped = mapService.IsMap<CustomToStringEvent>();

            // Assert
            Assert.True(mapped);
            Assert.False(unmapped);
        }

        [Fact]
        public void EventMapService_TryGetValue_ByType_ReturnsExpectedResult()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap(typeof(TestNormalEvent), typeof(TestNormalResponse));

            // Act
            var success = mapService.TryGetValue(typeof(TestNormalEvent), out var resolvedType);
            var missing = mapService.TryGetValue(typeof(CustomToStringEvent), out var missingType);
            var nullLookup = mapService.TryGetValue(null!, out var nullResolvedType);

            // Assert
            Assert.True(success);
            Assert.Equal(typeof(TestNormalResponse), resolvedType);
            Assert.False(missing);
            Assert.Null(missingType);
            Assert.False(nullLookup);
            Assert.Null(nullResolvedType);
        }

        [Fact]
        public void EventMapService_TryGetValueGeneric_ReturnsExpectedResult()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<TestNormalEvent, TestNormalResponse>();

            // Act
            var success = mapService.TryGetValue<TestNormalEvent>(out var resolvedType);
            var missing = mapService.TryGetValue<CustomToStringEvent>(out var missingType);

            // Assert
            Assert.True(success);
            Assert.Equal(typeof(TestNormalResponse), resolvedType);
            Assert.False(missing);
            Assert.Null(missingType);
        }

        [Fact]
        public void EventMapService_AddMap_ByType_AddsMappingAndValidatesNull()
        {
            // Arrange
            var mapService = new EventMapService();

            // Act
            mapService.AddMap(typeof(TestNormalEvent), typeof(TestNormalResponse));

            // Assert
            Assert.True(mapService.IsMap(typeof(TestNormalEvent)));
            Assert.Throws<ArgumentNullException>(() => mapService.AddMap(null!, typeof(TestNormalResponse)));
            Assert.Throws<ArgumentNullException>(() => mapService.AddMap(typeof(TestNormalEvent), null!));
        }

        [Fact]
        public void EventMapService_RemoveMap_ByType_RemovesMapping()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap(typeof(TestNormalEvent), typeof(TestNormalResponse));

            // Act
            var removed = mapService.RemoveMap(typeof(TestNormalEvent));
            var removedAgain = mapService.RemoveMap(typeof(TestNormalEvent));
            var removedNull = mapService.RemoveMap(null!);

            // Assert
            Assert.True(removed);
            Assert.False(removedAgain);
            Assert.False(removedNull);
            Assert.False(mapService.IsMap(typeof(TestNormalEvent)));
        }

        [Fact]
        public void EventMapService_RemoveMapGeneric_RemovesMapping()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<TestNormalEvent, TestNormalResponse>();

            // Act
            var removed = mapService.RemoveMap<TestNormalEvent>();
            var removedAgain = mapService.RemoveMap<TestNormalEvent>();

            // Assert
            Assert.True(removed);
            Assert.False(removedAgain);
            Assert.False(mapService.IsMap<TestNormalEvent>());
        }

        [Fact]
        public void EventMapService_LegacyGetValue_WorksForCompatibility()
        {
            // Arrange
            var mapService = new EventMapService();
            mapService.AddMap<TestNormalEvent, TestNormalResponse>();

            // Act
#pragma warning disable CS0618
            var found = mapService.GetValue(typeof(TestNormalEvent).FullName, out var responseType);
            var notFound = mapService.GetValue("NonExistentEvent", out var missingType);
            var nullLookup = mapService.GetValue(null, out var nullType);
#pragma warning restore CS0618

            // Assert
            Assert.True(found);
            Assert.Equal(typeof(TestNormalResponse), responseType);
            Assert.False(notFound);
            Assert.Null(missingType);
            Assert.False(nullLookup);
            Assert.Null(nullType);
        }
    }
}
