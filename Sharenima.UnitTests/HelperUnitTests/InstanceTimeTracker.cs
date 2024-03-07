using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sharenima.UnitTests.Mocks;
using StackExchange.Redis;

namespace Sharenima.UnitTests.HelperUnitTests;

public class InstanceTimeTracker {
    private Mock<IConnectionMultiplexer> _connectionMultiplexerMock { get; set; }
    private NullLoggerFactory _LoggerFactory { get; set; }
    private Server.Services.InstanceTimeTracker _instanceTimeTracker { get; set; }

    [SetUp]
    public void Setup() {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        mockMultiplexer.Setup(cm => cm.IsConnected).Returns(false);
        mockMultiplexer.Setup(cm => cm.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(new MockDatabase());
        _connectionMultiplexerMock = mockMultiplexer;
        _LoggerFactory = new NullLoggerFactory();
        _instanceTimeTracker = new Server.Services.InstanceTimeTracker(_LoggerFactory, _connectionMultiplexerMock.Object);
    }

    [Test]
    public void TestInsertingAnInstanceTime() {
        UpsertInstanceTime();
    }

    [Test]
    public void TestGettingAnInstancesTime() {
        Assert.IsTrue(_instanceTimeTracker.GetInstanceTime(UpsertInstanceTime()).HasValue);
    }

    [Test]
    public void TestRemovingAnInstanceTime() {
        Assert.IsTrue(_instanceTimeTracker.Remove(UpsertInstanceTime()));
    }

    private Guid UpsertInstanceTime() {
        Guid instanceId = new Guid();
        _instanceTimeTracker.Upsert(instanceId, TimeSpan.FromHours(1));
        return instanceId;
    }
}