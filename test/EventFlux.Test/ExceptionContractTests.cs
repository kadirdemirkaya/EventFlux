using System.Collections.Concurrent;
using System.Xml.Linq;
using EventFlux.Abstractions;
using EventFlux.Attributes;
using EventFlux.Extensions;
using EventFlux.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EventFlux.Test
{
    public class ContractEvent : IEventRequest
    {
        public Guid RunId { get; set; }

        public bool Fail { get; set; }
    }

    public static class ContractLog
    {
        public static readonly ConcurrentDictionary<Guid, ConcurrentQueue<string>> Entries = new();

        public static void Record(Guid runId, string entry)
            => Entries.GetOrAdd(runId, _ => new ConcurrentQueue<string>()).Enqueue(entry);

        public static IReadOnlyCollection<string> For(Guid runId)
            => Entries.TryGetValue(runId, out var entries) ? entries.ToArray() : Array.Empty<string>();
    }

    [HandlerOrder(1)]
    public class ContractFailingHandler : IEventHandler<ContractEvent>
    {
        public Task Handle(ContractEvent @event, CancellationToken cancellationToken)
        {
            ContractLog.Record(@event.RunId, nameof(ContractFailingHandler));

            if (@event.Fail)
                throw new InvalidOperationException("contract failure " + @event.RunId);

            return Task.CompletedTask;
        }
    }

    [HandlerOrder(2)]
    public class ContractRecordingHandler : IEventHandler<ContractEvent>
    {
        public Task Handle(ContractEvent @event, CancellationToken cancellationToken)
        {
            ContractLog.Record(@event.RunId, nameof(ContractRecordingHandler));
            return Task.CompletedTask;
        }
    }

    public class ExceptionContractTests
    {
        private static ServiceProvider BuildProvider(PublishStrategy strategy)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEventBus(o => o.PublishStrategy = strategy, typeof(ExceptionContractTests).Assembly);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task StackEventDispatcherAsync_WhenAnEventFails_SwallowsAndContinuesWithTheNextEvent()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Sequential);
            var bus = provider.GetRequiredService<IEventBus>();
            var failing = new ContractEvent { RunId = Guid.NewGuid(), Fail = true };
            var succeeding = new ContractEvent { RunId = Guid.NewGuid() };

            bus.AddStackRequestEvent(failing);
            bus.AddStackRequestEvent(succeeding);

            // Act
            var exception = await Record.ExceptionAsync(() => bus.StackEventDispatcherAsync());

            // Assert
            Assert.Null(exception);
            Assert.Contains(nameof(ContractRecordingHandler), ContractLog.For(succeeding.RunId));
        }

        [Fact]
        public async Task PublishAsync_Sequential_WhenAHandlerFails_ThrowsAndSkipsTheRemainingHandlers()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Sequential);
            var bus = provider.GetRequiredService<IEventBus>();
            var evt = new ContractEvent { RunId = Guid.NewGuid(), Fail = true };

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync(evt));

            // Assert
            Assert.DoesNotContain(nameof(ContractRecordingHandler), ContractLog.For(evt.RunId));
        }

        [Fact]
        public async Task PublishAsync_Parallel_WhenAHandlerFails_RunsEveryHandlerAndThrows()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();
            var evt = new ContractEvent { RunId = Guid.NewGuid(), Fail = true };

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(() => bus.PublishAsync(evt));

            // Assert
            Assert.Contains(nameof(ContractRecordingHandler), ContractLog.For(evt.RunId));
        }

        [Fact]
        public async Task SendAsync_WhenNoHandlerIsRegistered_ThrowsInvalidOperationException()
        {
            // Arrange
            using var provider = BuildProvider(PublishStrategy.Parallel);
            var bus = provider.GetRequiredService<IEventBus>();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => bus.SendAsync(new UnregisteredContractRequest()));
        }

        [Theory]
        [InlineData("M:EventFlux.Abstractions.IEventBus.SendAsync``1(EventFlux.Abstractions.IEventRequest{``0},System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventBus.PublishAsync(EventFlux.Abstractions.IEventRequest,System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventBus.StackEventDispatcherAsync(System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.SendAsync``1(EventFlux.Abstractions.IEventRequest{``0},System.Threading.CancellationToken)")]
        [InlineData("M:EventFlux.Abstractions.IEventDispatcher.PublishAsync(EventFlux.Abstractions.IEventRequest,System.Threading.CancellationToken)")]
        public void DispatchApi_XmlDocs_DescribeTheErrorContract(string memberId)
        {
            // Arrange
            var member = LoadXmlDocs()
                .Descendants("member")
                .SingleOrDefault(m => (string?)m.Attribute("name") == memberId);

            // Assert
            Assert.NotNull(member);
            var remarks = member!.Element("remarks");
            Assert.True(remarks is not null && remarks.Value.Trim().Length > 0, $"{memberId} has no <remarks> describing its error behaviour.");
            Assert.NotEmpty(member.Elements("exception"));
        }

        [Fact]
        public void StackEventDispatcherAsync_XmlDocs_StateThatFailuresAreLoggedAndSwallowed()
        {
            // Arrange
            var member = LoadXmlDocs()
                .Descendants("member")
                .Single(m => (string?)m.Attribute("name") == "M:EventFlux.Abstractions.IEventBus.StackEventDispatcherAsync(System.Threading.CancellationToken)");

            // Assert
            var remarks = member.Element("remarks")?.Value ?? string.Empty;
            Assert.Contains("logged", remarks, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not rethrown", remarks, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Readme_DocumentsTheErrorContract()
        {
            // Arrange
            var readme = ReadRepositoryFile("README.md");

            // Assert
            Assert.Contains("Error Handling", readme);
            Assert.Contains("StackEventDispatcherAsync", readme);
            Assert.Contains("CanHandle", readme);
        }

        private static XDocument LoadXmlDocs()
        {
            var path = Path.ChangeExtension(typeof(IEventBus).Assembly.Location, ".xml");
            Assert.True(File.Exists(path), "EventFlux XML documentation file not found at " + path);
            return XDocument.Load(path);
        }

        private static string ReadRepositoryFile(string fileName)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, fileName);

                if (File.Exists(candidate) && File.Exists(Path.Combine(directory.FullName, "EventFlux.sln")))
                    return File.ReadAllText(candidate);

                directory = directory.Parent;
            }

            throw new InvalidOperationException(fileName + " could not be located from " + AppContext.BaseDirectory);
        }
    }

    public class UnregisteredContractResponse : IEventResponse
    {
    }

    public class UnregisteredContractRequest : IEventRequest<UnregisteredContractResponse>
    {
    }
}
