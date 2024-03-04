using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sharenima.UnitTests.Mocks;
using StackExchange.Redis;

namespace Sharenima.UnitTests.HelperUnitTests;

public class InstanceTimeTracker {
    private Mock<IConnectionMultiplexer> _connectionMultiplexerMock { get; set; }
    private NullLoggerFactory _LoggerFactory { get; set; }

    [SetUp]
    public void Setup() {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(cm => cm.IsConnected).Returns(false);
        mockMultiplexer.Setup(cm => cm.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(new MockDatabase());
        _connectionMultiplexerMock = mockMultiplexer;
        _LoggerFactory = new NullLoggerFactory();
    }

    [Test]
    public void TestInsertingAnInstanceTime() {
        UpsertInstanceTime(new Server.Services.InstanceTimeTracker(_LoggerFactory, _connectionMultiplexerMock.Object));
    }

    [Test]
    public void TestGettingAnInstancesTime() {
        Server.Services.InstanceTimeTracker instanceTimeTracker = new Server.Services.InstanceTimeTracker(_LoggerFactory, _connectionMultiplexerMock.Object);
        Assert.IsTrue(instanceTimeTracker.GetInstanceTime(UpsertInstanceTime(instanceTimeTracker)).HasValue);
    }

    private Guid UpsertInstanceTime(Server.Services.InstanceTimeTracker instanceTimeTracker) {
        instanceTimeTracker = new Server.Services.InstanceTimeTracker(_LoggerFactory, _connectionMultiplexerMock.Object);
        Guid instanceId = new Guid();
        instanceTimeTracker.Upsert(instanceId, TimeSpan.FromHours(1));
        return instanceId;
    }
}